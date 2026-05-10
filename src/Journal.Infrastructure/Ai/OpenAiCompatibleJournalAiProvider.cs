using System.Text.Json;

namespace Journal.Infrastructure.Ai;

public sealed class OpenAiCompatibleJournalAiProvider : IJournalAiProvider
{
    private const string JsonObjectResponseFormat = "json_object";

    private readonly IJournalAiAgentRuntime _runtime;

    public OpenAiCompatibleJournalAiProvider(IJournalAiAgentRuntime runtime)
    {
        _runtime = runtime;
    }

    public string ProviderId => "openai-compatible";

    public async Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = request.Settings.ToMetadata();
        var validationError = ValidateSettings(request.Settings);
        if (validationError is not null)
        {
            return JournalAiProviderResult.Failure(metadata, validationError);
        }

        var runResult = await _runtime.RunJsonAsync(
            CreateRunRequest(
                request.Settings,
                JournalAiPrompt.SystemInstructions,
                JournalAiPrompt.BuildUserPrompt(request.Date, request.RawInputs)),
            cancellationToken);

        return runResult.IsSuccess && runResult.AiJson is not null
            ? JournalAiProviderResult.Success(runResult.AiJson, metadata)
            : JournalAiProviderResult.Failure(
                metadata,
                runResult.Error ?? JournalAiSafeError.Create(
                    "runtime",
                    "provider_error",
                    "AI provider returned no JSON result.",
                    runResult.SafeResponseSnippet));
    }

    public async Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateSettings(settings);
        if (validationError is not null)
        {
            return JournalAiProviderHealthResult.Failure(validationError.Code, null, null, validationError);
        }

        var runResult = await _runtime.RunJsonAsync(
            CreateRunRequest(
                settings,
                "只输出 JSON。",
                """Return { "ok": true }""",
                requiresJournalAiJson: false),
            cancellationToken);

        if (runResult.IsSuccess)
        {
            var healthValidationError = ValidateHealthCheckJson(runResult.SafeResponseSnippet);
            if (healthValidationError is not null)
            {
                return JournalAiProviderHealthResult.Failure(
                    healthValidationError.Code,
                    runResult.HttpStatus,
                    runResult.Latency,
                    healthValidationError);
            }

            return JournalAiProviderHealthResult.Success(
                runResult.SafeResponseSnippet,
                runResult.Latency,
                runResult.HttpStatus);
        }

        var status = MapStatus(runResult);
        return JournalAiProviderHealthResult.Failure(
            status,
            runResult.HttpStatus,
            runResult.Latency,
            runResult.Error ?? JournalAiSafeError.Create("runtime", status, "AI provider health check failed.", runResult.SafeResponseSnippet));
    }

    private static OpenAiCompatibleRunRequest CreateRunRequest(
        JournalAiProviderSettings settings,
        string systemPrompt,
        string userPrompt,
        bool requiresJournalAiJson = true) =>
        new(
            settings.Id,
            settings.BaseUrl,
            settings.Model,
            settings.ApiKey,
            systemPrompt,
            userPrompt,
            JsonObjectResponseFormat,
            settings.TimeoutSeconds,
            settings.Temperature,
            settings.MaxTokens,
            requiresJournalAiJson);

    private static JournalAiSafeError? ValidateHealthCheckJson(string safeResponseSnippet)
    {
        try
        {
            using var document = JsonDocument.Parse(safeResponseSnippet);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ok", out var ok)
                || ok.ValueKind != JsonValueKind.True)
            {
                return JournalAiSafeError.Create("runtime", "invalid_json", "AI provider health check returned invalid JSON.", safeResponseSnippet);
            }
        }
        catch (JsonException exception)
        {
            return JournalAiSafeError.Create("runtime", "invalid_json", "AI provider health check returned invalid JSON.", exception.Message);
        }

        return null;
    }

    private static JournalAiSafeError? ValidateSettings(JournalAiProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return JournalAiSafeError.Create("configuration", "missing_api_key", "AI provider API key is required.", "Provider key is empty.");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            return JournalAiSafeError.Create("configuration", "missing_model", "AI provider model is required.", "Provider model is empty.");
        }

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
        {
            return JournalAiSafeError.Create("configuration", "invalid_base_url", "AI provider base URL is invalid.", "Provider base URL is not absolute.");
        }

        return null;
    }

    private static string MapStatus(OpenAiCompatibleRunResult runResult)
    {
        if (runResult.Error is not null && !string.IsNullOrWhiteSpace(runResult.Error.Code))
        {
            return runResult.Error.Code;
        }

        return runResult.HttpStatus switch
        {
            401 => "unauthorized",
            403 => "forbidden",
            404 => "model_not_found",
            408 => "timeout",
            429 => "rate_limited",
            >= 500 => "provider_error",
            _ => "provider_error"
        };
    }
}
