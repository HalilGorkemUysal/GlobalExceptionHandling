using GlobalExceptionHandling.Models;
using System.Net;

namespace GlobalExceptionHandling.CustomExceptionMiddleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

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

        private async Task HandleExceptionAsync(HttpContext httpContext, Exception ex)
        {
            HttpStatusCode statusCode;
            string message;

            switch (ex)
            {
                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    message = "İstenen kaynak bulunamadı.";
                    break;

                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = "Geçersiz bir parametre gönderildi.";
                    break;

                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Forbidden;
                    message = "Bu işlem için erişim yetkiniz bulunmuyor.";
                    break;

                case NotImplementedException:
                    statusCode = HttpStatusCode.NotImplemented;
                    message = "Bu işlem henüz desteklenmiyor.";
                    break;

                case AccessViolationException:
                    statusCode = HttpStatusCode.InternalServerError;
                    message = "Bellek erişimi sırasında bir hata oluştu.";
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    message = "Internal Server Error ile karşılaşıldı.";
                    break;
            }

            httpContext.Response.StatusCode = (int)statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(new ErrorDetails
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = message
            }.ToString());
        }
    }
}
