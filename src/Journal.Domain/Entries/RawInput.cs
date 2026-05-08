namespace Journal.Domain.Entries;

public sealed record RawInput(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Source,
    string Text);
