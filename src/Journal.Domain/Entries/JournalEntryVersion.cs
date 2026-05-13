namespace Journal.Domain.Entries;

public sealed record JournalEntryVersion(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Reason,
    string SourceEntryPath,
    string MarkdownPath,
    string MetaPath,
    string ContentHash);
