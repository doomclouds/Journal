namespace Journal.Domain.Entries;

public sealed record JournalAiJson(
    string Schema,
    JournalDate Date,
    string MonthDay,
    JournalStatus Status,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Topics,
    string Mood,
    IReadOnlyList<string> RawInputs,
    string YesterdayReview,
    string TodayFocus,
    string Inspiration);
