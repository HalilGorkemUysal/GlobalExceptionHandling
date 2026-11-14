using GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers
{
    public class NotFoundExceptionHandler : IExceptionHandler
    {
        private readonly ILoggerManager _logger;

        public NotFoundExceptionHandler(ILoggerManager logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not NotFoundException notFoundException)
            {
                return false; //mevcut handler'ın exception'ı işlemediğini bildirir;
            }

            _logger.LogWarn(
                $"NotFound hatası: {notFoundException.Message}");

            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.NotFound,
                Title = "Not Found",
                Detail = notFoundException.Message
            };

            problem.Extensions["traceId"] =
                httpContext.TraceIdentifier;

            httpContext.Response.StatusCode =
                StatusCodes.Status404NotFound;

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                cancellationToken);

            return true;
        }
    }
}