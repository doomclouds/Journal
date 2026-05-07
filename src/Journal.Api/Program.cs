using Journal.Domain.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopDevelopment", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("DesktopDevelopment");

app.MapGet("/health", (IHostEnvironment environment) =>
{
    return Results.Ok(new HealthResponse(
        ApplicationInfo.Name,
        "ok",
        ApplicationInfo.Version,
        environment.EnvironmentName,
        DateTimeOffset.Now));
});

app.Run();

public partial class Program
{
}

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
