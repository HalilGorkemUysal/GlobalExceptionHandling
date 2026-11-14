using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILoggerManager _logger;

        public GlobalExceptionHandler(ILoggerManager logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError($"Beklenmeyen hata: {exception}");

            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Internal Server Error",
                Detail = "Sunucu hatası oluştu."
            };

            problem.Extensions["traceId"] =
                httpContext.TraceIdentifier;

            httpContext.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                cancellationToken);

            return true;
        }
    }
}