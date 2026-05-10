using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public interface IJournalAiAgentRuntime
{
    Task<OpenAiCompatibleRunResult> RunJsonAsync(
        OpenAiCompatibleRunRequest request,
        CancellationToken cancellationToken);
}

public sealed record OpenAiCompatibleRunRequest(
    string ProviderId,
    string BaseUrl,
    string Model,
    string ApiKey,
    string SystemPrompt,
    string UserPrompt,
    string ResponseFormat,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens,
    bool RequiresJournalAiJson = true);

public sealed record OpenAiCompatibleRunResult(
    bool IsSuccess,
    JournalAiJson? AiJson,
    string SafeResponseSnippet,
    TimeSpan Latency,
    int? HttpStatus,
    JournalAiSafeError? Error)
{
    public static OpenAiCompatibleRunResult Success(
        JournalAiJson? aiJson,
        string safeResponseSnippet,
        TimeSpan latency,
        int? httpStatus = 200) =>
        new(true, aiJson, TrimSnippet(safeResponseSnippet), latency, httpStatus, null);

    public static OpenAiCompatibleRunResult Failure(
        JournalAiSafeError error,
        TimeSpan latency,
        int? httpStatus = null,
        string safeResponseSnippet = "") =>
        new(false, null, TrimSnippet(safeResponseSnippet), latency, httpStatus, error);

    private static string TrimSnippet(string value) =>
        value.Length <= 240 ? value : value[..240];
}
