using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Application;
using Journal.Domain.Entries;
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
builder.Services.AddSingleton<JournalAiSettingsStore>();
builder.Services.AddSingleton<IJournalAiEnvironment, SystemJournalAiEnvironment>();
builder.Services.AddSingleton<JournalAiSettingsService>();
builder.Services.AddSingleton<IJournalAiSettingsReader>(sp => sp.GetRequiredService<JournalAiSettingsService>());
builder.Services.AddSingleton<MockAiProvider>();
builder.Services.AddSingleton<IJournalAiAgentRuntime, OpenAiCompatibleAgentRuntime>();
builder.Services.AddSingleton<OpenAiCompatibleJournalAiProvider>();
builder.Services.AddSingleton<JournalAiGenerationService>();
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

app.MapGet("/settings/ai", async (JournalAiSettingsService service, CancellationToken cancellationToken) =>
{
    var view = await service.ReadViewAsync(cancellationToken);
    return Results.Ok(view);
});

app.MapPut("/settings/ai", async (
    JournalAiSettingsSaveRequest request,
    JournalAiSettingsService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        await service.SaveAsync(request, cancellationToken);
        var view = await service.ReadViewAsync(cancellationToken);
        return Results.Ok(view);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/settings/ai/test", async (
    AiProviderTestRequest request,
    JournalAiGenerationService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ProviderId))
    {
        return Results.BadRequest(new { error = "providerId is required" });
    }

    var health = await service.CheckAsync(request.ProviderId, cancellationToken);
    return Results.Ok(health);
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

app.MapPost("/journal/today/draft/regenerate", async (
    HttpRequest httpRequest,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    RegenerateTodayDraftRequest? request = null;
    if (httpRequest.ContentLength is > 0)
    {
        try
        {
            request = await httpRequest.ReadFromJsonAsync<RegenerateTodayDraftRequest>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid request body" });
        }
    }

    var state = await service.RegenerateDraftAsync(request?.ProviderId, cancellationToken);
    return Results.Ok(state);
});

app.MapGet("/journal/today/editor", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    var state = await service.GetTodayEditorAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPut("/journal/today/editor/blocks", async (
    JournalBlockEditRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var state = await service.SaveBlockDraftAsync(request, cancellationToken);
        return Results.Ok(state);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPut("/journal/today/editor/source", async (
    JournalSourceEditRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Markdown))
    {
        return Results.BadRequest(new { error = "markdown is required" });
    }

    try
    {
        var state = await service.SaveSourceDraftAsync(request, cancellationToken);
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

public sealed record AiProviderTestRequest(string ProviderId);

public sealed record RegenerateTodayDraftRequest(string? ProviderId);

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
