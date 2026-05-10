namespace Journal.Domain.Entries;

public sealed record JournalAiMetadata(
    string Provider,
    string Model,
    string PromptVersion)
{
    public static JournalAiMetadata Mock { get; } =
        new("mock", "mock-journal", "mock-journal-entry-v1");
}
