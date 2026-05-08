namespace Journal.Domain.Entries;

public sealed record RawInput(
    Guid Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Source,
    string Text);
