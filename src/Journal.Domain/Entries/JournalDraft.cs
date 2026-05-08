namespace Journal.Domain.Entries;

public sealed record JournalDraft(
    JournalDate Date,
    JournalStatus Status,
    string Markdown,
    IReadOnlyList<string> SourceRawInputIds,
    IReadOnlyList<string> Errors,
    DateTimeOffset UpdatedAt);
