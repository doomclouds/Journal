namespace Journal.Infrastructure.Ai;

public sealed record JournalAiProviderHealthResult(
    bool IsSuccess,
    string Status,
    string SafeResponseSnippet,
    int? HttpStatus,
    TimeSpan? Latency,
    JournalAiSafeError? Error)
{
    public static JournalAiProviderHealthResult Success(string safeResponseSnippet, TimeSpan latency, int? httpStatus = 200) =>
        new(true, "success", safeResponseSnippet, httpStatus, latency, null);

    public static JournalAiProviderHealthResult Failure(string status, int? httpStatus, TimeSpan? latency, JournalAiSafeError error) =>
        new(false, status, string.Empty, httpStatus, latency, error);
}
