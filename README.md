# 🛡️ GlobalExceptionHandling

ASP.NET Core (.NET 9) Web API üzerinde **global hata yönetimini 3 farklı yöntemle** gösteren öğrenme projesi.
Her yöntem projeye kronolojik olarak eklenmiştir; aşağıdaki bölümleri açıp kapatarak inceleyebilirsiniz.

---

## 📑 İçindekiler

| # | Yöntem | Temel Fikir | Ne Zaman Eklendi |
|---|--------|-------------|------------------|
| 1 | `UseExceptionHandler` + Lambda | Framework'ün hazır middleware'ine inline kod vermek | İlk commit (Add project files) |
| 2 | Custom Middleware (`ExceptionMiddleware`) | Kendi `try/catch` middleware'imizi yazmak | `custom global exception handling middleware eklendi` |
| 3 | `IExceptionHandler` (.NET 8+) | Her hata tipi için ayrı, DI destekli handler sınıfı | `IExceptionHandler tabanlı exception handler'lar eklendi` |

---

<details>
<summary><h2>1️⃣ UseExceptionHandler + Lambda (Built-in Middleware)</h2></summary>

### Nedir?
ASP.NET Core'un hazır gelen `UseExceptionHandler` middleware'i kullanılır. Hata yakalama mantığı bir **lambda** içinde, `IExceptionHandlerFeature` üzerinden hatayı okuyarak yazılır.

### Kod
`Extensions/ExceptionMiddlewareExtensions.cs`

```csharp
public static void ConfigureExceptionHandler(this WebApplication app, ILoggerManager logger)
{
    app.UseExceptionHandler(appError =>
    {
        appError.Run(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (contextFeature != null)
            {
                logger.LogError($"Something went wrong: {contextFeature.Error}");

                await context.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = context.Response.StatusCode,
                    Message = "Internal Server Error"
                }.ToString());
            }
        });
    });
}
```

`Program.cs`

```csharp
var logger = app.Services.GetRequiredService<ILoggerManager>();
app.ConfigureExceptionHandler(logger);
```

### Davranış
- Her hata → **500 Internal Server Error**
- Yanıt: `{"StatusCode":500,"Message":"Internal Server Error"}`
- Detay sadece log'a yazılır, istemciye sızmaz.

### Önemi
- ✅ En hızlı kurulan yöntemdir; ekstra sınıf gerekmez.
- ✅ Stack trace istemciye gitmez (güvenlik).
- ⚠️ Tek bir lambda'da tüm hatalar aynı muameleyi görür; hata tipine göre ayrım yoktur.
- ⚠️ `ILogger` DI ile gelmek yerine dışarıdan parametre olarak verilmek zorundadır.
- ⚠️ Lambda içinde mantık büyüdükçe test etmek zorlaşır.

</details>

---

<details>
<summary><h2>2️⃣ Custom Middleware (ExceptionMiddleware)</h2></summary>

### Nedir?
Kendi middleware sınıfımızı yazıp pipeline'a ekleriz. `_next(httpContext)` bir `try/catch` içine alınır; yakalanan hata bir `switch` ile uygun HTTP durum koduna çevrilir.

### Kod
`CustomExceptionMiddleware/ExceptionMiddleware.cs`

```csharp
public async Task InvokeAsync(HttpContext httpContext)
{
    try
    {
        await _next(httpContext);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Something went wrong: {ex}");
        await HandleExceptionAsync(httpContext, ex);
    }
}
```

Hata → durum kodu eşlemesi:

| Exception | HTTP Kodu |
|-----------|-----------|
| `KeyNotFoundException` | 404 Not Found |
| `ArgumentException` | 400 Bad Request |
| `UnauthorizedAccessException` | 403 Forbidden |
| `NotImplementedException` | 501 Not Implemented |
| `AccessViolationException` / diğer | 500 Internal Server Error |

`Program.cs`

```csharp
app.ConfigureCustomExceptionMiddleware();   // => app.UseMiddleware<ExceptionMiddleware>();
```

### Davranış
- Hata tipine göre **farklı status code ve Türkçe mesaj** döner.
- Yanıt formatı projenin kendi `ErrorDetails` modelidir: `{"StatusCode":403,"Message":"..."}`

### Önemi
- ✅ Hata tipine göre ayrım yapılabilir (Yöntem 1'e göre büyük adım).
- ✅ Middleware sınıfı constructor üzerinden DI alır.
- ✅ Tüm .NET sürümlerinde çalışır (.NET 8 öncesi için standart yöntem).
- ⚠️ Tüm eşleme tek bir `switch` içinde toplanır; yeni hata tipi eklemek bu dosyayı büyütür (Open/Closed ilkesine uymaz).
- ⚠️ Yanıt standart `ProblemDetails` (RFC 7807) değil, özel bir modeldir.

</details>

---

<details>
<summary><h2>3️⃣ IExceptionHandler (.NET 8+ Modern Yaklaşım)</h2></summary>

### Nedir?
.NET 8 ile gelen `IExceptionHandler` arayüzü kullanılır. **Her hata tipi için ayrı bir handler sınıfı** yazılır ve DI'a kaydedilir. Framework handler'ları **kayıt sırasıyla** dener; biri `true` döndürürse zincir durur.

### Dosyalar
```
GlobalExceptionHandlers/
├── Exceptions/
│   ├── BadRequestException.cs        # Özel 400 hatası
│   └── NotFoundException.cs          # Özel 404 hatası
└── ExceptionHandlers/
    ├── BadRequestExceptionHandler.cs # 400
    ├── NotFoundExceptionHandler.cs   # 404
    └── GlobalExceptionHandler.cs     # 500 (fallback)
```

### Kod
Handler örneği:

```csharp
public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
{
    if (exception is not NotFoundException notFoundException)
        return false;   // Bu handler ilgilenmiyor, sıradakine geç

    _logger.LogWarn($"NotFound hatası: {notFoundException.Message}");

    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Not Found",
        Detail = notFoundException.Message
    };
    problem.Extensions["traceId"] = httpContext.TraceIdentifier;

    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
    await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    return true;     // Hata işlendi, zinciri durdur
}
```

`Program.cs` (**sıra önemlidir!**)

```csharp
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();   // En sonda, fallback
builder.Services.AddProblemDetails();

app.UseExceptionHandler();
```

### Davranış
| Atılan Hata | Yakalayan Handler | Yanıt |
|-------------|-------------------|-------|
| `BadRequestException` | `BadRequestExceptionHandler` | 400 + ProblemDetails |
| `NotFoundException` | `NotFoundExceptionHandler` | 404 + ProblemDetails |
| Diğer her şey | `GlobalExceptionHandler` | 500 + "Sunucu hatası oluştu." |

Yanıtlar **RFC 7807 `ProblemDetails`** formatındadır ve `traceId` içerir.

### Önemi
- ✅ **Tek sorumluluk:** her handler tek bir hata tipiyle ilgilenir.
- ✅ Yeni hata tipi = yeni sınıf; mevcut kod değişmez.
- ✅ Handler'lar DI ile gelir, kolay unit test edilir.
- ✅ Standart `ProblemDetails` + `traceId` ile istemci ve log eşleştirmesi kolaylaşır.
- ✅ `GlobalExceptionHandler` fallback olduğu için hiçbir hata sahipsiz kalmaz ve detay sızmaz.
- ⚠️ Kayıt sırası kritiktir: `GlobalExceptionHandler` her şeyi yakalar, ilk kaydedilirse diğerlerine sıra gelmez.
- ⚠️ .NET 8 ve üstü gerektirir.

</details>

---

## 🔍 Yöntemler Arasındaki Farklar

| Özellik | 1️⃣ Lambda | 2️⃣ Custom Middleware | 3️⃣ IExceptionHandler |
|---------|:---------:|:--------------------:|:--------------------:|
| Hata tipine göre ayrım | ❌ | ✅ (`switch` ile) | ✅ (ayrı sınıflarla) |
| DI desteği | ⚠️ Parametre ile | ✅ | ✅ |
| Yanıt formatı | Özel `ErrorDetails` | Özel `ErrorDetails` | Standart `ProblemDetails` |
| `traceId` | ❌ | ❌ | ✅ |
| Genişletilebilirlik | Düşük | Orta | Yüksek |
| Test edilebilirlik | Zor | Orta | Kolay |
| Minimum .NET sürümü | Eski sürümler | Eski sürümler | .NET 8+ |
| Kurulum çabası | En az | Orta | En fazla (ama en temiz) |
| Pipeline kaydı | `UseExceptionHandler(lambda)` | `UseMiddleware<T>()` | `AddExceptionHandler<T>()` + `UseExceptionHandler()` |

### Evrim Özeti
1. **Lambda** → "Hataları yakala, 500 dön." (basit ama tek tip)
2. **Custom Middleware** → "Hata tipine göre farklı cevap ver." (esnek ama tek dosyada toplanır)
3. **IExceptionHandler** → "Her hata için ayrı sorumlu, standart format." (modern ve ölçeklenebilir)

---

## 🧰 Ortak Altyapı: LoggerService

Üç yöntem de aynı log altyapısını kullanır.

- `ILoggerManager`: `LogInfo`, `LogWarn`, `LogDebug`, `LogError`
- `LoggerManager`: **NLog** üzerine kurulu implementasyon
- `nlog.config`: Konsola ve `logs/app.log` dosyasına yazar (`minlevel="Info"`)
- `Program.cs` içinde `AddSingleton<ILoggerManager, LoggerManager>()` ile kayıtlıdır.

---

## 🗂️ Proje Yapısı

```
GlobalExceptionHandling.slnx
├── GlobalExceptionHandling/            # Web API
│   ├── Controllers/StudentsController.cs
│   ├── GlobalExceptionHandlers/
│   │   ├── CustomExceptionMiddleware/  # Yöntem 2
│   │   ├── ExceptionHandlers/          # Yöntem 3
│   │   ├── Exceptions/                 # Özel exception'lar
│   │   └── Extensions/                 # Yöntem 1 ve 2 için extension'lar
│   ├── Models/ (Student, ErrorDetails)
│   ├── DataManager.cs                  # Örnek veri
│   ├── nlog.config
│   └── Program.cs
└── LoggerService/                      # NLog sarmalayıcı kütüphane
```

---

## ▶️ Çalıştırma ve Yöntem Değiştirme

```bash
dotnet run --project GlobalExceptionHandling
# GET https://localhost:<port>/api/students
```

`Program.cs` içinde **aynı anda yalnızca bir yöntemi** aktif tutun:

```csharp
// 1️⃣ Lambda
var logger = app.Services.GetRequiredService<ILoggerManager>();
app.ConfigureExceptionHandler(logger);

// 2️⃣ Custom Middleware
app.ConfigureCustomExceptionMiddleware();

// 3️⃣ IExceptionHandler (AddExceptionHandler kayıtları da gerekir)
app.UseExceptionHandler();
```

`StudentsController.Get()` test amaçlı kasıtlı olarak hata fırlatır. Hangi exception'ı fırlattığınıza göre her yöntemin farklı yanıt verdiğini görebilirsiniz.

> Örnek: `UnauthorizedAccessException` → Yöntem 2'de **403**, Yöntem 3'te (özel handler olmadığı için) **500** döner.

---

## 🛠️ Kullanılan Teknolojiler

- .NET 9 / ASP.NET Core Web API
- NLog
- ProblemDetails (RFC 7807)
- OpenAPI
