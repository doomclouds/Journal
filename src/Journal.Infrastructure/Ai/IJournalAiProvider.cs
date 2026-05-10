namespace Journal.Infrastructure.Ai;

public interface IJournalAiProvider
{
    string ProviderId { get; }

    Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken);

    Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken);
}
