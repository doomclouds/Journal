using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiProviderResult(
    bool IsSuccess,
    JournalAiJson? AiJson,
    JournalAiMetadata Metadata,
    JournalAiSafeError? Error)
{
    public static JournalAiProviderResult Success(JournalAiJson aiJson, JournalAiMetadata metadata) =>
        new(true, aiJson, metadata, null);

    public static JournalAiProviderResult Failure(JournalAiMetadata metadata, JournalAiSafeError error) =>
        new(false, null, metadata, error);
}
