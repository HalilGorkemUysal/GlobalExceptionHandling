using GlobalExceptionHandling.GlobalExceptionHandlers.Exceptions;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers
{
    public class BadRequestExceptionHandler : IExceptionHandler
    {
        private readonly ILoggerManager _logger;

        public BadRequestExceptionHandler(ILoggerManager logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not BadRequestException badRequestException)
            {
                return false;
            }

            _logger.LogWarn($"BadRequest hatası: {badRequestException.Message}");

            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Bad Request",
                Detail = badRequestException.Message
            };

            problem.Extensions["traceId"] =
                httpContext.TraceIdentifier;

            httpContext.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                cancellationToken);

            return true;
        }
    }
}