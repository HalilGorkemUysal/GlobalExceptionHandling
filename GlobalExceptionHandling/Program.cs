using GlobalExceptionHandling.GlobalExceptionHandlers.ExceptionHandlers;
using LoggerService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ILoggerManager, LoggerManager>();

// Exception handler kayıtları
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//var logger = app.Services.GetRequiredService<ILoggerManager>();
//app.ConfigureExceptionHandler(logger);

//app.ConfigureCustomExceptionMiddleware();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
