namespace Journal.Domain.Entries;

public sealed record JournalEntry(
    JournalDate Date,
    string Markdown,
    string Path,
    DateTimeOffset UpdatedAt);
