namespace Journal.Domain.Entries;

public sealed record JournalIndexedEntry(
    JournalDate Date,
    string EntryPath,
    string Status,
    string? Mood,
    string TagsJson,
    string TopicsJson,
    string ContentHash,
    DateTimeOffset LastWriteTimeUtc,
    long FileSize,
    DateTimeOffset IndexedAtUtc,
    string? AttentionReason);

public sealed record JournalIndexedSection(
    JournalDate Date,
    string SectionId,
    string Title,
    int DisplayOrder,
    string Content);

public sealed record JournalIndexedRawInput(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Source,
    string Text);

public sealed record JournalHistoryQuery(
    string? Query,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    string? Cursor,
    int Limit);

public sealed record JournalHistorySearchResult(IReadOnlyList<JournalHistoryEntrySummary> Items);

public sealed record JournalAnniversaryWheelResult(
    string MonthDay,
    IReadOnlyList<JournalHistoryEntrySummary> Items);

public sealed record JournalHistoryEntrySummary(
    JournalDate Date,
    string Status,
    string? Mood,
    int RawInputCount,
    int VersionCount,
    IReadOnlyList<JournalHistoryHit> Hits,
    string? AttentionReason);

public sealed record JournalHistoryHit(
    string SourceType,
    string? SectionId,
    string? RawInputId,
    string Title,
    string Snippet);
