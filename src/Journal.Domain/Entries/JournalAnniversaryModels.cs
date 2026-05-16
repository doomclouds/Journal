namespace Journal.Domain.Entries;

public static class JournalNextYearNoteStatus
{
    public const string Pending = "pending";
    public const string Adopted = "adopted";
    public const string Dismissed = "dismissed";
}

public sealed record JournalAnniversaryDocument(
    string Schema,
    IReadOnlyList<JournalAnniversaryItem> Items)
{
    public static JournalAnniversaryDocument Empty() =>
        new("journal-anniversaries/v1", []);
}

public sealed record JournalAnniversaryItem(
    string Id,
    string MonthDay,
    string Title,
    string Type,
    string? OriginDate,
    string Description,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<JournalNextYearNote> NextYearNotes);

public sealed record JournalNextYearNote(
    string Id,
    string TargetDate,
    string Text,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AdoptedAt,
    string? RawInputId);

public sealed record JournalAnniversarySaveRequest(
    string MonthDay,
    string Title,
    string Type,
    string? OriginDate,
    string Description,
    bool Pinned);

public sealed record JournalNextYearNoteCreateRequest(string Text);

public sealed record JournalAnniversaryAdoptResult(
    JournalAnniversaryItem Anniversary,
    RawInput RawInput);
