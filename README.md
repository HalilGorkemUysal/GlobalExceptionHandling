<div align="center">

# 🛡️ Global Exception Handling — ASP.NET Core (.NET 9)

**ASP.NET Core Web API'de merkezi hata yönetimini üç farklı yaklaşımla öğrenme ve karşılaştırma projesi.**

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-ASP.NET_Core-239120?logo=csharp&logoColor=white)
![NLog](https://img.shields.io/badge/Logging-NLog-34495E)
![Status](https://img.shields.io/badge/Project-Educational-blue)

[📁 Kaynak Kod](https://github.com/HalilGorkemUysal/GlobalExceptionHandling) · [Karşılaştırma](#-yöntemlerin-karşılaştırması) · [Kurulum](#-kurulum-ve-ortak-altyapı) · [Test](#-test-senaryoları)

</div>

> **Kapsam:** Repository'nin `master` dalı, `4a086749` commit'i esas alınmıştır (14 Kasım 2026). Bu README, depoda **gerçekten bulunan üç yaklaşımı** açıklamaktadır. Bazı kod parçaları, kurulum ve deneme kolaylığı için açıkça **önerilen düzenleme** olarak işaretlenmiştir; bunların tümü repoda birebir aynı biçimde bulunmayabilir.

## 🎯 Amaç

Controller/Service katmanlarında yakalanmadan yükselen exception'ları tek noktadan ele almak, uygun HTTP durum kodları üretmek, hassas hata detaylarını istemciden gizlemek ve NLog aracılığıyla kayıt altına almak. Üç teknik **alternatif yaklaşımlardır**: Öğrenmek için hepsi kaynak kodda tutulabilir, fakat normal kullanımda `Program.cs` içinde **yalnızca bir yaklaşımı etkinleştirin**.

| Sıra | Yaklaşım | Exception'ı kim yakalıyor? | Yanıtı kim oluşturuyor? | Ana dosya |
|---|---|---|---|---|
| 1 | `UseExceptionHandler` + extension metodu | ASP.NET Core | Extension içindeki handler delegate'i | `ExceptionMiddlewareExtensions.cs` |
| 2 | Custom `ExceptionMiddleware` | Kendi `try/catch` kodumuz | `HandleExceptionAsync` | `ExceptionMiddleware.cs` |
| 3 | `IExceptionHandler` zinciri | ASP.NET Core | Kayıtlı `TryHandleAsync` implementasyonları | Üç ayrı handler sınıfı |

## 📁 Proje yapısı

```text
GlobalExceptionHandling.slnx
├── GlobalExceptionHandling/                 # ASP.NET Core Web API
│   ├── Controllers/
│   │   └── StudentsController.cs
│   ├── GlobalExceptionHandlers/
│   │   ├── Extensions/
│   │   │   └── ExceptionMiddlewareExtensions.cs
│   │   ├── CustomExceptionMiddleware/
│   │   │   └── ExceptionMiddleware.cs
│   │   ├── ExceptionHandlers/
│   │   │   ├── BadRequestExceptionHandler.cs
│   │   │   ├── NotFoundExceptionHandler.cs
│   │   │   └── GlobalExceptionHandler.cs
│   │   └── Exceptions/
│   │       ├── BadRequestException.cs
│   │       └── NotFoundException.cs
│   ├── Models/
│   │   ├── ErrorDetails.cs
│   │   └── Student.cs
│   ├── DataManager.cs
│   ├── nlog.config
│   └── Program.cs
└── LoggerService/                       # Class Library
    ├── ILoggerManager.cs
    └── LoggerManager.cs
```

## ⚙️ Kurulum ve ortak altyapı

**Gereksinimler:** .NET 9 SDK, Visual Studio 2022 veya uygun bir IDE, NuGet paket erişimi. Depoda `LoggerService` projesi `NLog 6.2.2` ve `NLog.Web.AspNetCore 6.3.0` referanslarını içeriyor. API projesi, `LoggerService` projesine referans veriyor.

### 0.1. Projeyi indirin ve çalıştırın

```bash
git clone https://github.com/HalilGorkemUysal/GlobalExceptionHandling.git
cd GlobalExceptionHandling
dotnet restore
dotnet run --project GlobalExceptionHandling/GlobalExceptionHandling.csproj
```

> **Not:** Repository'nin `StudentsController.Get()` metodu şu an `UnauthorizedAccessException` fırlatarak test yapıyor. Dolayısıyla GET isteğinin normal öğrenci listesini döndürmesi beklenmez. Ayrıca varsayılan `Program.cs` yalnızca 3. yöntemi etkinleştirir.

### 0.2. Ortak hata modeli: `ErrorDetails.cs`

Birinci ve ikinci yöntem bu modeli kullanır:

```csharp
using System.Text.Json;

namespace GlobalExceptionHandling.Models;

public class ErrorDetails
{
    public int StatusCode { get; set; }
    public string? Message { get; set; }

    public override string ToString() => JsonSerializer.Serialize(this);
}
```

`this`, mevcut `ErrorDetails` nesnesini ifade eder. `Serialize(this)`, bu nesneyi JSON metnine dönüştürür. Üçüncü yaklaşım ise ASP.NET Core'un `ProblemDetails` modelini kullanır.

### 0.3. Ortak NLog katmanı: `LoggerService`

**Önce interface, sonra implementasyon, ardından DI kaydı** oluşturulur:

`LoggerService/ILoggerManager.cs`

```csharp
namespace LoggerService;

public interface ILoggerManager
{
    void LogInfo(string message);
    void LogWarn(string message);
    void LogDebug(string message);
    void LogError(string message);
}
```

`LoggerService/LoggerManager.cs`

```csharp
using NLog;

namespace LoggerService;

public class LoggerManager : ILoggerManager
{
    private readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();

    public void LogInfo(string message) => _logger.Info(message);
    public void LogWarn(string message) => _logger.Warn(message);
    public void LogDebug(string message) => _logger.Debug(message);
    public void LogError(string message) => _logger.Error(message);
}
```

**Dependency Injection** kaydı (`Program.cs`, `builder.Build()` öncesinde):

```csharp
using LoggerService;
builder.Services.AddSingleton<ILoggerManager, LoggerManager>();
```

`AddSingleton` **kaydı** yapar; constructor'da `ILoggerManager` isteyen bir Controller veya Handler, DI tarafından oluşturulan aynı singleton örneğini alır. `LoggerManager` içinde `NLog.ILogger` kullanılır; ASP.NET Core'un generic `ILogger<T>` türüyle karıştırılmamalıdır.

### 0.4. `nlog.config`

Repository'de Web API klasöründe hem Console hem File target mevcuttur:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      autoReload="true">
  <targets>
    <target xsi:type="Console" name="console"
            layout="${longdate}|${level}|${logger}|${message}" />
    <target xsi:type="File" name="logfile"
            fileName="${basedir}/logs/app.log"
            layout="${longdate}|${level}|${logger}|${message}" />
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="console,logfile" />
  </rules>
</nlog>
```

`nlog.config` dosyasının çalıştırma klasörüne kopyalandığından emin olun. Gerekirse API `.csproj` içine (dosyanın build item türüne uygun şekilde) şu ayarı ekleyin:

```xml
<ItemGroup>
  <None Update="nlog.config" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**Önerilen ek düzenleme (mevcut `Program.cs` içinde yok):** ASP.NET Core'un yerleşik `ILogger<T>` loglarını da NLog'a yönlendirmek için Web API projesinin `NLog.Web.AspNetCore` paketini referans aldığından emin olup `builder.Build()` öncesinde ekleyin:

```csharp
using NLog.Web;
builder.Logging.ClearProviders();
builder.Host.UseNLog();
```

Bu entegrasyon, `LoggerManager` üzerinden **doğrudan** NLog çağırmanın aksine ASP.NET Core logging provider'ını da değiştirir. NLog konfigürasyonunun gerçekten yüklendiğini uygulama başında kontrol edin. `LogInfo` vb. mesajlar görünmüyorsa dosya konumu, build-copy ayarı ve target/rule'ları denetleyin.

---

## 🧭 Üç yöntemin kronolojik anlatımı

Aşağıdaki her bölüm GitHub tarafından desteklenen `<details>`/`<summary>` ile **açılıp kapanır**. Sıra hem öğrenme akışına hem de repodaki gelişime uygundur.

<details>
<summary><strong>1️⃣ Hazır UseExceptionHandler + Extension Method (ilk yaklaşım)</strong></summary>

### Ne işe yarar?

ASP.NET Core'un **hazır** `ExceptionHandlerMiddleware` bileşeni hatayı yakalar. Biz `ConfigureExceptionHandler` extension metodu içerisinde hata anında yapılacak işleri tanımlarız. Hata detayını `IExceptionHandlerFeature` ile alır, `ILoggerManager` üzerinden kaydeder, `ErrorDetails` ile JSON HTTP 500 döndürürüz.

### Kodları ekleme sırası

**Adım 1 — `Models/ErrorDetails.cs`:** Ortak kurulum bölümündeki `ErrorDetails` sınıfını oluşturun.

**Adım 2 — `LoggerService`:** `ILoggerManager`, `LoggerManager`, NLog konfigürasyonunu oluşturup DI kaydını yapın.

**Adım 3 — `GlobalExceptionHandlers/Extensions/ExceptionMiddlewareExtensions.cs`:**

```csharp
using GlobalExceptionHandling.Models;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace GlobalExceptionHandling.GlobalExceptionHandling.Extensions;

public static class ExceptionMiddlewareExtensions
{
    public static void ConfigureExceptionHandler(
        this WebApplication app, ILoggerManager logger)
    {
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                context.Response.StatusCode =
                    (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var feature = context.Features.Get<IExceptionHandlerFeature>();
                if (feature is not null)
                {
                    logger.LogError($"Something went wrong: {feature.Error}");
                    await context.Response.WriteAsync(new ErrorDetails
                    {
                        StatusCode = context.Response.StatusCode,
                        Message = "Internal Server Error"
                    }.ToString());
                }
            });
        });
    }
}
```

**Adım 4 — `Program.cs`:** Diğer yaklaşımları devre dışı bırakarak bu yöntemi çağırın:

```csharp
using GlobalExceptionHandling.GlobalExceptionHandling.Extensions;
using LoggerService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<ILoggerManager, LoggerManager>();
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerManager>();
app.ConfigureExceptionHandler(logger);
app.MapControllers();
app.Run();
```

### Neden önemli?

- `this WebApplication app`, **extension method** tanımlar; `app.ConfigureExceptionHandler(logger)` yazabiliriz.
- `app.UseExceptionHandler(...)`, hata yakalayan hazır middleware'i pipeline'a ekler.
- `appError.Run(...)`, hata işleme pipeline'ında terminal delegate'i tanımlar.
- `IExceptionHandlerFeature.Error`, framework'ün yakaladığı gerçek `Exception` nesnesidir.
- Logda detay tutulurken istemciye genel 500 mesajı gösterilir.

### Örnek yanıt

```json
{"StatusCode":500,"Message":"Internal Server Error"}
```

### Sınırlar

Buradaki kod **tüm exception türlerini 500** olarak döndürür; 404/400 gibi özel exception eşlemeleri içermez. Normal `return NotFound()` gibi HTTP yanıtlarını yakalamaz. HTTP response başladıktan sonra hata yanıtı güvenilir biçimde yeniden yazılamaz.

**Kaynak:** [`ExceptionMiddlewareExtensions.cs`](GlobalExceptionHandling/GlobalExceptionHandlers/Extensions/ExceptionMiddlewareExtensions.cs)

</details>

<details>
<summary><strong>2️⃣ Custom ExceptionMiddleware — RequestDelegate ve try/catch (ikinci yaklaşım)</strong></summary>

### Ne işe yarar?

Bu kez framework'ün hazır exception handler delegate'ini değil, `RequestDelegate` alan ve `try/catch` yazan **kendi middleware sınıfımızı** oluştururuz. `await _next(context)` aşağıdaki middleware/controller'ı çağırır; yakalanmamış exception dışarı yükselirse `catch` çalışır.

### Kodları ekleme sırası

**Adım 1 — Ortak bağımlılıklar:** `ErrorDetails` ve `LoggerService` hazır olmalı.

**Adım 2 — `CustomExceptionMiddleware/ExceptionMiddleware.cs`:** Repository'deki `switch` mantığının tam örneği:

```csharp
using GlobalExceptionHandling.Models;
using LoggerService;
using System.Net;

namespace GlobalExceptionHandling.GlobalExceptionHandling.CustomExceptionMiddleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoggerManager _logger;

    public ExceptionMiddleware(RequestDelegate next, ILoggerManager logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Something went wrong: {ex}");
            if (context.Response.HasStarted) throw;
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        HttpStatusCode status;
        string message;

        switch (ex)
        {
            case KeyNotFoundException:
                status = HttpStatusCode.NotFound;
                message = "İstenen kaynak bulunamadı.";
                break;
            case ArgumentException:
                status = HttpStatusCode.BadRequest;
                message = "Geçersiz bir parametre gönderildi.";
                break;
            case UnauthorizedAccessException:
                status = HttpStatusCode.Forbidden;
                message = "Bu işlem için erişim yetkiniz bulunmuyor.";
                break;
            case NotImplementedException:
                status = HttpStatusCode.NotImplemented;
                message = "Bu işlem henüz desteklenmiyor.";
                break;
            case AccessViolationException:
                status = HttpStatusCode.InternalServerError;
                message = "Bellek erişimi sırasında bir hata oluştu.";
                break;
            default:
                status = HttpStatusCode.InternalServerError;
                message = "Internal Server Error ile karşılaşıldı.";
                break;
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }
}
```

> **Önerilen güvenlik iyileştirmesi:** Yukarıdaki `if (context.Response.HasStarted) throw;` koruması repository'nin mevcut dosyasında yoktur; response başlamışsa yeniden durum kodu yazmayı denememek için eklenmiştir.

**Adım 3 — Extension method ile ekleyin:** Var olan `ExceptionMiddlewareExtensions` sınıfına:

```csharp
public static void ConfigureCustomExceptionMiddleware(this WebApplication app)
{
    app.UseMiddleware<ExceptionMiddleware>();
}
```

**Adım 4 — `Program.cs`:** Birinci ve üçüncü yöntemi devre dışı bırakıp şu çağrıyı açın:

```csharp
using GlobalExceptionHandling.GlobalExceptionHandling.Extensions;
// builder.Services.AddSingleton<ILoggerManager, LoggerManager>();  // Build öncesi
// var app = builder.Build();
app.ConfigureCustomExceptionMiddleware(); // Controller'lardan önce
```

### Neden önemli?

- `RequestDelegate _next` bir sonraki middleware'i çağırır; **bir handler seçme mekanizması değildir**.
- `try/catch` ile hata yakalama sorumluluğu sizin kodunuzdadır.
- `switch` ile exception türü → HTTP durum kodu ve kullanıcı mesajı eşlenir.
- `InvokeAsync` dönüş tipi `Task`'tır; `IExceptionHandler` gibi `true/false` dönmez.
- `catch` sonrasında exception tekrar fırlatılmazsa middleware hata yanıtını işleyip tamamlamış olur.

### Örnekler

| Exception | Custom middleware yanıtı |
|---|---|
| `KeyNotFoundException` | 404 |
| `ArgumentException` | 400 |
| `UnauthorizedAccessException` | 403 |
| `NotImplementedException` | 501 |
| `AccessViolationException` | 500 |
| Diğerleri | 500 |

> **Uygulama tasarımı uyarısı:** Her `ArgumentException` bir istemci hatası değildir; her `KeyNotFoundException` da kayıp kaynak değildir. `NotImplementedException` genellikle eksik uygulama kodu anlamına gelir (bu durumda çoğu zaman 500 daha uygundur). `AccessViolationException` her zaman güvenilir biçimde yakalanamayabilir. Bu eşlemeler projedeki eğitim amaçlı örnekleri gösterir.

**Kaynak:** [`ExceptionMiddleware.cs`](GlobalExceptionHandling/GlobalExceptionHandlers/CustomExceptionMiddleware/ExceptionMiddleware.cs)

</details>

<details>
<summary><strong>3️⃣ IExceptionHandler — BadRequest / NotFound / Global handler zinciri (üçüncü yaklaşım, mevcut aktif yöntem)</strong></summary>

### Ne işe yarar?

.NET 9'da kullanılan `IExceptionHandler` ile her exception tipi için **ayrı sorumluluk sahibi handler** yazabiliriz. Exception'ı ASP.NET Core'un `ExceptionHandlerMiddleware` bileşeni yakalar; kayıtlı `TryHandleAsync` implementasyonlarını sırayla dener.

- `false`: Bu handler exception'ı işlemedi; sıradaki handler denenebilir.
- `true`: Exception işlendi; zincir durur.
- `true` HTTP 200 anlamına **gelmez**; üretilen HTTP status 400/404/500 olabilir.

### Kodları ekleme sırası

**Adım 1 — Özel exception türleri** (`GlobalExceptionHandlers/Exceptions/`):

```csharp
namespace GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
```

> Bunlar repository'deki **gerçek constructor imzalarıdır**; `NotFoundException(string name, object key)` bu sürümde mevcut değildir. FluentValidation bağımlılığı da yoktur.

**Adım 2 — BadRequestExceptionHandler.cs:**

```csharp
using GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers;

public class BadRequestExceptionHandler : IExceptionHandler
{
    private readonly ILoggerManager _logger;
    public BadRequestExceptionHandler(ILoggerManager logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadRequestException badRequest) return false;
        _logger.LogWarn($"BadRequest hatası: {badRequest.Message}");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = badRequest.Message
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
```

**Adım 3 — NotFoundExceptionHandler.cs:**

```csharp
using GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers;

public class NotFoundExceptionHandler : IExceptionHandler
{
    private readonly ILoggerManager _logger;
    public NotFoundExceptionHandler(ILoggerManager logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFound) return false;
        _logger.LogWarn($"NotFound hatası: {notFound.Message}");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = notFound.Message
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
```

**Adım 4 — GlobalExceptionHandler.cs (fallback):**

```csharp
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILoggerManager _logger;
    public GlobalExceptionHandler(ILoggerManager logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError($"Beklenmeyen hata: {exception}");
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "Sunucu hatası oluştu."
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
```

**Adım 5 — `Program.cs`:** Bu, repository'de şu anda **aktif** olan yöntemdir:

```csharp
using GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers;
using LoggerService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ILoggerManager, LoggerManager>();

// ÖNEMLİ: özel handler'lar önce, herkesi yakalayan fallback en son.
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### Gerçek çalışma sırası

```text
Controller'da exception
        │
        ▼
ASP.NET Core ExceptionHandlerMiddleware
        │
        ▼
BadRequestExceptionHandler
  ├── BadRequestException → 400 + true → BİTER
  └── Diğerleri → false
        │
        ▼
NotFoundExceptionHandler
  ├── NotFoundException → 404 + true → BİTER
  └── Diğerleri → false
        │
        ▼
GlobalExceptionHandler
  └── Kalan exception'lar → 500 + true → BİTER
```

### Neden önemli?

Bu yaklaşımda **Single Responsibility Principle** daha açık görünür: 400, 404 ve beklenmeyen 500 ayrı sınıflardadır. `ProblemDetails` HTTP hata yanıtlarına ortak bir biçim verir; `traceId` hata kayıtlarıyla HTTP isteklerini eşleştirmeyi kolaylaştırır. Yeni bir exception tipi için yeni handler eklenebilir.

### Yanıt örnekleri

`throw new BadRequestException("Öğrenci bilgileri geçersiz.");` → HTTP **400**:

```json
{"title":"Bad Request","status":400,"detail":"Öğrenci bilgileri geçersiz.","traceId":"..."}
```

`throw new NotFoundException("Öğrenci bulunamadı.");` → HTTP **404**:

```json
{"title":"Not Found","status":404,"detail":"Öğrenci bulunamadı.","traceId":"..."}
```

`throw new Exception("Örnek hata");` → HTTP **500**:

```json
{"title":"Internal Server Error","status":500,"detail":"Sunucu hatası oluştu.","traceId":"..."}
```

> Yanıtlar anlaşılır olması için sadeleştirilmiştir; JSON serializer ayarlarına göre ek alanlar veya biçim farklılıkları olabilir. `return NotFound()` gibi doğrudan oluşturulan HTTP yanıtları, exception fırlatılmadığı için bu handler zincirini tetiklemez.

**Kaynak dosyalar:** [`BadRequestExceptionHandler.cs`](GlobalExceptionHandling/GlobalExceptionHandlers/ExceptionHandlers/BadRequestExceptionHandler.cs) · [`NotFoundExceptionHandler.cs`](GlobalExceptionHandling/GlobalExceptionHandlers/ExceptionHandlers/NotFoundExceptionHandler.cs) · [`GlobalExceptionHandler.cs`](GlobalExceptionHandling/GlobalExceptionHandlers/ExceptionHandlers/GlobalExceptionHandler.cs)

</details>

## 📊 Yöntemlerin karşılaştırması

| Kriter | 1 — Hazır middleware | 2 — Custom middleware | 3 — IExceptionHandler |
|---|---|---|---|
| Exception yakalama | ASP.NET Core | `try/catch` sizin kodunuz | ASP.NET Core |
| Hata işleme | Bir delegate | Bir middleware sınıfı | Birden çok handler |
| Devam kontrolü | Framework | `_next(context)` / `throw` | `true` / `false` |
| Repository'deki HTTP eşlemesi | 500 | 400, 403, 404, 501, 500 | 400, 404, 500 |
| Hata yanıt modeli | `ErrorDetails` | `ErrorDetails` | `ProblemDetails` |
| Loglama | `ILoggerManager` | `ILoggerManager` | `ILoggerManager` |
| Avantaj | Az kod, framework altyapısı | Middleware zincirini öğrenme ve tam denetim | Ayrı sorumluluklar, genişletilebilirlik |
| Dikkat | Basit örnekte tümü 500 | Pipeline/response edge-case sorumluluğu sizde | Kayıt sırası ve doğru `true/false` önemli |

**Öneri:** Gerçek projede yapı büyüdükçe **3. yöntem (`IExceptionHandler`)** çoğunlukla daha sürdürülebilir bir seçimdir. 1. yöntem küçük API'ler için basittir; 2. yöntem pipeline'ın çalışma şeklini öğrenmek için özellikle faydalıdır.

## 🧪 Test senaryoları

Repo içindeki `StudentsController.Get()` çağrısı şu an `ILoggerManager.LogInfo(...)` sonrasında **bilinçli olarak `UnauthorizedAccessException` fırlatır**. Etkin 3. yöntemde bu exception için özel handler **olmadığından** `GlobalExceptionHandler` üzerinden **500** beklenir (2. yöntemde ise `switch` bunu 403'e eşler).

`StudentsController` içinde geçici olarak aşağıdaki örneklerden **yalnızca birini** çalıştırarak 3. yöntemi test edebilirsiniz:

```csharp
using GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;

// HTTP 400:
throw new BadRequestException("Geçersiz öğrenci bilgisi.");

// HTTP 404:
throw new NotFoundException("Öğrenci bulunamadı.");

// HTTP 500:
throw new Exception("Beklenmeyen deneme hatası.");
```

Bunlar birbirini izleyen çalıştırılabilir satırlar değil, **alternatif test senaryolarıdır**. İstek: `GET /api/students` (uygulamanın terminalde bildirdiği portu kullanın).

### Logları inceleme

`nlog.config` dosyasındaki File target: `${basedir}/logs/app.log`. Örnek bir dosya konumu `GlobalExceptionHandling/bin/Debug/net9.0/logs/app.log` olabilir; kesin konum NLog'un `${basedir}` çözümlemesine bağlıdır. Konsol target da `Info` ve üzeri logları yazar.

## 🔎 Mevcut repository için teknik notlar

1. **Tek yaklaşım etkin:** Depodaki `Program.cs` şu an yalnızca `app.UseExceptionHandler()` ile **3. yöntemi** çalıştırıyor. İlk iki yöntemin çağrıları yorum satırında.
2. **İsimlendirme:** `ExceptionMiddlewareExtensions.cs` dosya yolu `GlobalExceptionHandlers/Extensions/` olsa da namespace'i `GlobalExceptionHandling.GlobalExceptionHandling.Extensions`. Aynı tekrar custom middleware namespace'inde de var. Derleme için gerçek namespace kullanılmalı; ileride standartlaştırılabilir.
3. **NLog provider:** `LoggerService` projesinde NLog paketleri mevcut ama API `Program.cs` içinde `builder.Host.UseNLog()` görünmüyor. Bu, `ILoggerManager` ile doğrudan NLog kullanımını engellemez; Microsoft `ILogger<T>` loglarını NLog'a yönlendirmek için entegrasyon ayrıca yapılmalıdır.
4. **`nlog.config` kopyalama:** Web API `.csproj` dosyasında açık bir `CopyToOutputDirectory` ayarı görünmüyor. NLog config bulunamıyorsa bunu kontrol edin.
5. **404 response:** `NotFoundExceptionHandler` 404 döndürür. Normal `IExceptionHandler` zincirinde `true` ile işlenmiş yanıtlarda bu geçerlidir; özel handler/fallback düzenlemelerinde 404 davranışı ayrıca test edilmelidir.
6. **Güvenlik:** Beklenmeyen exception'ın `Message`/stack trace bilgisini istemciye göndermeyin. Logda ayrıntılı hata tutun; API yanıtında genel mesaj kullanın.
7. **Test sınırı:** Bu depo eğitim odaklıdır; response başlamışken exception, cancellation, beklenmeyen background task hataları ve 401/403 authentication-authorization yanıtları için ayrı değerlendirme gerekir.

## 🕰️ Gelişim geçmişi

Repository commit geçmişiyle doğrulanabilen kronoloji:

| Aşama | Commit | İçerik |
|---|---|---|
| Başlangıç | [`e7c63e6`](https://github.com/HalilGorkemUysal/GlobalExceptionHandling/commit/e7c63e695e408bb4ab7822085fbcebb0b899d06b) | İlk proje dosyaları |
| Custom middleware | [`760f3e5`](https://github.com/HalilGorkemUysal/GlobalExceptionHandling/commit/760f3e5cff878d0f7c327067465f4b1c9db0e10e) | Custom global exception middleware |
| Ara geliştirme | [`8289913`](https://github.com/HalilGorkemUysal/GlobalExceptionHandling/commit/8289913eac64b4606a1b9b926e15fa57d6c8cd25) | Commit mesajı `.`; içeriği ayrıca incelenebilir |
| Handler zinciri | [`4a08674`](https://github.com/HalilGorkemUysal/GlobalExceptionHandling/commit/4a086749c9a37447890ccbd6f501f98a3fa2716a) | BadRequest / NotFound / Global `IExceptionHandler` sınıfları |

> **Not:** README'deki 1 → 2 → 3 yöntemi, konuyu öğrenme ve kodu kurma sırasıdır. Commit tablosu ise GitHub'daki gerçek kayıt sırasını gösterir; bu iki kavramı karıştırmamak gerekir.

## 📚 Başvuru

- [ASP.NET Core Error Handling (Microsoft Learn)](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling)
- [ASP.NET Core Middleware (Microsoft Learn)](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
- [NLog](https://nlog-project.org/)
- [NLog.Web.AspNetCore](https://www.nuget.org/packages/NLog.Web.AspNetCore)

---

<div align="center">

**ASP.NET Core · .NET 9 · C# · NLog · Middleware · IExceptionHandler · ProblemDetails**

</div>
