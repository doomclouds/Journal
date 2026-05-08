namespace Journal.Domain.Entries;

public sealed record JournalAiJson(
    string Schema,
    string Date,
    string MonthDay,
    string Status,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Topics,
    string Mood,
    IReadOnlyList<string> RawInputs,
    IReadOnlyList<string> YesterdayReview,
    IReadOnlyList<string> TodayFocus,
    IReadOnlyList<string> Inspiration);
