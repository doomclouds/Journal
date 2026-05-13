using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Application;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
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
builder.Services.AddSingleton<IJournalVersionStore, JournalVersionStore>();
builder.Services.AddSingleton<JournalIndexStore>();
builder.Services.AddSingleton<JournalIndexingService>();
builder.Services.AddSingleton<EntryWritePipeline>();
builder.Services.AddSingleton<JournalAiSettingsStore>();
builder.Services.AddSingleton<IJournalAiEnvironment, SystemJournalAiEnvironment>();
builder.Services.AddSingleton<JournalAiSettingsService>();
builder.Services.AddSingleton<IJournalAiSettingsReader>(sp => sp.GetRequiredService<JournalAiSettingsService>());
builder.Services.AddSingleton<MockAiProvider>();
builder.Services.AddSingleton<IJournalAiAgentRuntime, OpenAiCompatibleAgentRuntime>();
builder.Services.AddSingleton<OpenAiCompatibleJournalAiProvider>();
builder.Services.AddSingleton<JournalAiGenerationService>();
builder.Services.AddSingleton<JournalHarnessPlanner>();
builder.Services.AddSingleton<JournalHarnessAuditStore>();
builder.Services.AddSingleton<JournalHarnessService>();
builder.Services.AddSingleton<TodayJournalService>();
builder.Services.AddSingleton<JournalHistoryService>();

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

app.MapGet("/settings/ai/{providerId}/api-key", async (
    string providerId,
    JournalAiSettingsService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ReadFileApiKeyAsync(providerId, cancellationToken);
    return result is null
        ? Results.NotFound(new { error = "file-backed API key was not found" })
        : Results.Ok(result);
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
    JournalAiSettingsService settingsService,
    JournalAiGenerationService generationService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ProviderId))
    {
        return Results.BadRequest(new { error = "providerId is required" });
    }

    try
    {
        var health = request.Candidate is null
            ? await generationService.CheckAsync(request.ProviderId, cancellationToken)
            : await generationService.CheckAsync(
                request.ProviderId,
                await settingsService.BuildEffectiveCandidateAsync(request.Candidate, cancellationToken),
                cancellationToken);

        return Results.Ok(health);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/settings/ai/activate", async (
    JournalAiSettingsSaveRequest request,
    JournalAiSettingsService settingsService,
    JournalAiGenerationService generationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var candidate = await settingsService.BuildEffectiveCandidateAsync(request, cancellationToken);
        var testResult = await generationService.CheckAsync(candidate.ActiveProviderId, candidate, cancellationToken);
        if (testResult.IsSuccess)
        {
            await settingsService.SaveAsync(request, cancellationToken);
        }

        var view = await settingsService.ReadViewAsync(cancellationToken);
        return Results.Ok(new JournalAiSettingsActivationResult(testResult.IsSuccess, view, testResult));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
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

app.MapPost("/journal/today/harness/runs", async (
    HarnessRunRequest request,
    JournalHarnessService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "text is required" });
    }

    try
    {
        var result = await service.StartTodayRunAsync(request.Text, request.Source ?? "text", cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/journal/harness/runs/{runId}", async Task<IResult> (
    string runId,
    JournalHarnessAuditStore auditStore,
    CancellationToken cancellationToken) =>
{
    if (!TryParseHarnessRunDate(runId, out var date))
    {
        return Results.BadRequest(new { error = "runId is invalid" });
    }

    var run = await auditStore.ReadAsync(date, runId, cancellationToken);
    return run is null
        ? Results.NotFound(new { error = "harness run was not found" })
        : Results.Ok(run);
});

app.MapGet("/journal/harness/runs/{runId}/events", async Task<IResult> (
    string runId,
    JournalHarnessAuditStore auditStore,
    JournalHarnessService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseHarnessRunDate(runId, out var date))
    {
        return Results.BadRequest(new { error = "runId is invalid" });
    }

    var run = await auditStore.ReadAsync(date, runId, cancellationToken);
    if (run is null)
    {
        return Results.NotFound(new { error = "harness run was not found" });
    }

    return TypedResults.ServerSentEvents(ToHarnessRunSseItems(service, runId, cancellationToken));
});

app.MapGet("/journal/audit", async Task<IResult> (
    string? date,
    JournalHarnessAuditStore auditStore,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    var runs = await auditStore.ReadByDateAsync(journalDate, cancellationToken);
    return Results.Ok(runs);
});

app.MapGet("/journal/history", async Task<IResult> (
    string? query,
    string? status,
    string? from,
    string? to,
    string? cursor,
    int? limit,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseOptionalJournalDate(from, out var fromDate))
    {
        return Results.BadRequest(new { error = "from must use yyyy-MM-dd" });
    }

    if (!TryParseOptionalJournalDate(to, out var toDate))
    {
        return Results.BadRequest(new { error = "to must use yyyy-MM-dd" });
    }

    var request = new JournalHistoryQuery(
        query,
        string.IsNullOrWhiteSpace(status) ? null : status,
        fromDate,
        toDate,
        string.IsNullOrWhiteSpace(cursor) ? null : cursor,
        limit.GetValueOrDefault(50));

    return Results.Ok(await service.SearchAsync(request, cancellationToken));
});

app.MapGet("/journal/history/{date}", async Task<IResult> (
    string date,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    var detail = await service.GetEntryAsync(journalDate, cancellationToken);
    return detail is null
        ? Results.NotFound(new { error = "journal entry was not found" })
        : Results.Ok(detail);
});

app.MapGet("/journal/history/{date}/versions", async Task<IResult> (
    string date,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    return Results.Ok(await service.ReadVersionsAsync(journalDate, cancellationToken));
});

app.MapGet("/journal/history/{date}/versions/{versionId}", async Task<IResult> (
    string date,
    string versionId,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    var version = await service.ReadVersionAsync(journalDate, versionId, cancellationToken);
    return version is null
        ? Results.NotFound(new { error = "version was not found" })
        : Results.Ok(new { version = version.Value.Version, markdown = version.Value.Markdown });
});

app.MapPost("/journal/history/{date}/versions/{versionId}/restore-draft", async Task<IResult> (
    string date,
    string versionId,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    try
    {
        return Results.Ok(await service.RestoreVersionAsDraftAsync(journalDate, versionId, cancellationToken));
    }
    catch (JournalHistoryRestoreConflictException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/journal/index/scan", async (
    JournalIndexingService service,
    IJournalClock clock,
    CancellationToken cancellationToken) =>
{
    await service.ScanAsync(clock.Now, cancellationToken);
    return Results.Ok(new { status = "ok" });
});

app.MapPost("/journal/index/rebuild", async (
    JournalIndexingService service,
    IJournalClock clock,
    CancellationToken cancellationToken) =>
{
    await service.RebuildAsync(clock.Now, cancellationToken);
    return Results.Ok(new { status = "ok" });
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

static async IAsyncEnumerable<SseItem<HarnessRunEventView>> ToHarnessRunSseItems(
    JournalHarnessService service,
    string runId,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var sequence = 0;
    await foreach (var runEvent in service.ExecuteRunAsStreamAsync(runId, cancellationToken))
    {
        sequence++;
        yield return new SseItem<HarnessRunEventView>(
            new HarnessRunEventView(runEvent.Type, runEvent.RunId, runEvent.Status, runEvent.Message),
            runEvent.Type)
        {
            EventId = $"{runEvent.RunId}:{sequence}"
        };
    }
}

static bool TryParseJournalDate(string? value, out JournalDate date)
{
    if (DateOnly.TryParseExact(
        value,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsed))
    {
        date = JournalDate.From(parsed);
        return true;
    }

    date = default!;
    return false;
}

static bool TryParseOptionalJournalDate(string? value, out DateOnly? date)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        date = null;
        return true;
    }

    if (DateOnly.TryParseExact(
        value,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsed))
    {
        date = parsed;
        return true;
    }

    date = null;
    return false;
}

static bool TryParseHarnessRunDate(string? runId, out JournalDate date)
{
    if (runId is not null
        && LocalJournalPaths.IsValidHarnessRunId(runId)
        && runId.Length >= 16
        && runId.StartsWith("run-", StringComparison.Ordinal)
        && runId[14] == '-'
        && DateOnly.TryParseExact(
            runId.AsSpan(4, 10),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
    {
        date = JournalDate.From(parsed);
        return true;
    }

    date = default!;
    return false;
}

app.Run();

public partial class Program
{
}

public sealed record AddTodayInputRequest(string Text, string? Source);

public sealed record HarnessRunRequest(string Text, string? Source);

public sealed record HarnessRunEventView(
    string Type,
    string RunId,
    string Status,
    string Message);

public sealed record AiProviderTestRequest(
    string ProviderId,
    JournalAiSettingsSaveRequest? Candidate = null);

public sealed record RegenerateTodayDraftRequest(string? ProviderId);

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
