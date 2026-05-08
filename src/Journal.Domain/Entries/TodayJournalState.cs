namespace Journal.Domain.Entries;

public sealed record TodayJournalState(
    JournalDate Date,
    JournalStatus Status,
    IReadOnlyList<RawInput> RawInputs,
    JournalDraft? Draft,
    JournalEntry? Entry,
    IReadOnlyList<string> Errors);
