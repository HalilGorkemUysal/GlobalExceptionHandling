using GlobalExceptionHandling.Models;
using LoggerService;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace GlobalExceptionHandling.Extensions
{
    public static class ExceptionMiddlewareExtensions
    {//C# programlama dilinde static class (statik sınıf), kendisinden new anahtar kelimesi ile nesne üretilemeyen (örneklenemeyen) ve yalnızca static üyeler barındırabilen özel bir sınıf türüdür

        public static void ConfigureExceptionHandler(this WebApplication app, ILoggerManager logger)
        {

            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if(contextFeature != null)
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

    }
}
