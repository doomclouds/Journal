using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Application;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

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

builder.Services.AddSingleton(JournalStorageOptions.FromLocalAppData());
builder.Services.AddSingleton<LocalJournalPaths>();
builder.Services.AddSingleton<IJournalClock, SystemJournalClock>();
builder.Services.AddSingleton<RawInputStore>();
builder.Services.AddSingleton<DraftStore>();
builder.Services.AddSingleton<EntryStore>();
builder.Services.AddSingleton<IJournalAiProvider, MockAiProvider>();
builder.Services.AddSingleton<TodayJournalService>();

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

app.MapGet("/journal/today", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    var state = await service.GetTodayAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPost("/journal/today/inputs", async (
    AddTodayInputRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "text is required" });
    }

    var state = await service.AddInputAsync(request.Text, request.Source ?? "text", cancellationToken);
    return Results.Ok(state);
});

app.MapPost("/journal/today/draft/confirm", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    try
    {
        var state = await service.ConfirmDraftAsync(cancellationToken);
        return Results.Ok(state);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.Run();

public partial class Program
{
}

public sealed record AddTodayInputRequest(string Text, string? Source);

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
