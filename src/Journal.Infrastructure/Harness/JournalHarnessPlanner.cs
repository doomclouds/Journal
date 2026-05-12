using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessPlanResult(
    bool IsSuccess,
    IReadOnlyList<JournalHarnessOperation> Operations,
    string SafeResponseSnippet,
    TimeSpan Latency,
    int? HttpStatus,
    JournalAiSafeError? Error)
{
    public static JournalHarnessPlanResult Success(
        IReadOnlyList<JournalHarnessOperation> operations,
        string safeResponseSnippet,
        TimeSpan latency,
        int? httpStatus = 200) =>
        new(true, operations, safeResponseSnippet, latency, httpStatus, null);

    public static JournalHarnessPlanResult Failure(
        JournalAiSafeError error,
        string safeResponseSnippet,
        TimeSpan latency,
        int? httpStatus = null) =>
        new(false, Array.Empty<JournalHarnessOperation>(), safeResponseSnippet, latency, httpStatus, error);
}

public sealed class JournalHarnessPlanner
{
    private readonly IJournalAiAgentRuntime _runtime;

    public JournalHarnessPlanner(IJournalAiAgentRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<JournalHarnessPlanResult> PlanAsync(
        JournalAiProviderSettings settings,
        JournalHarnessPromptRequest prompt,
        CancellationToken cancellationToken)
    {
        var runResult = await _runtime.RunHarnessPlannerAsync(
            new JournalHarnessPlannerRuntimeRequest(
                settings.Id,
                settings.BaseUrl,
                settings.Model,
                settings.ApiKey,
                prompt.SystemInstructions,
                prompt.ProtectedContext,
                prompt.UserMessage,
                settings.TimeoutSeconds,
                settings.Temperature,
                settings.MaxTokens),
            cancellationToken);

        return runResult.IsSuccess
            ? JournalHarnessPlanResult.Success(
                runResult.Operations,
                runResult.SafeResponseSnippet,
                runResult.Latency,
                runResult.HttpStatus)
            : JournalHarnessPlanResult.Failure(
                runResult.Error ?? JournalAiSafeError.Create(
                    "runtime",
                    "provider_error",
                    "LLM returned no harness plan.",
                    runResult.SafeResponseSnippet),
                runResult.SafeResponseSnippet,
                runResult.Latency,
                runResult.HttpStatus);
    }
}
