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
    IReadOnlyList<string> Inspiration)
{
    public IReadOnlyList<string> Work { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Relationship { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Health { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Money { get; init; } = Array.Empty<string>();
}
