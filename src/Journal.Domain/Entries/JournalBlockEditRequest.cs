namespace Journal.Domain.Entries;

public sealed record JournalBlockEditRequest(
    IReadOnlyList<JournalBlockEditSection> Sections);
