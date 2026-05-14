using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed record JournalHistoryEntryDetail(
    JournalDate Date,
    string Status,
    string? AttentionReason,
    string? Markdown,
    IReadOnlyList<JmfSection> Sections,
    IReadOnlyList<JournalEntryVersion> Versions);

public sealed class JournalHistoryRestoreConflictException(string message) : Exception(message);

public sealed class JournalHistoryService(
    JournalIndexStore indexStore,
    JournalIndexingService indexingService,
    IJournalVersionStore versionStore,
    EntryStore entryStore,
    DraftStore draftStore,
    TodayJournalService todayService,
    IJournalClock clock)
{
    public async Task<JournalHistorySearchResult> SearchAsync(
        JournalHistoryQuery query,
        CancellationToken cancellationToken)
    {
        await indexingService.ScanAsync(clock.Now, cancellationToken);
        return await indexStore.SearchAsync(query, cancellationToken);
    }

    public async Task<JournalAnniversaryWheelResult> GetAnniversaryAsync(
        string monthDay,
        int limit,
        CancellationToken cancellationToken)
    {
        await indexingService.ScanAsync(clock.Now, cancellationToken);
        return await indexStore.ReadAnniversaryAsync(monthDay, limit, cancellationToken);
    }

    public async Task<JournalHistoryEntryDetail?> GetEntryAsync(
        JournalDate date,
        CancellationToken cancellationToken)
    {
        await indexingService.ScanAsync(clock.Now, cancellationToken);
        var summary = await indexStore.ReadSummaryAsync(date, cancellationToken);
        var entry = await entryStore.ReadAsync(date, cancellationToken);
        if (summary is null && entry is null)
        {
            return null;
        }

        var markdown = entry?.Markdown;
        IReadOnlyList<JmfSection> sections = [];
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            var parseResult = JmfMarkdownParser.Parse(markdown);
            sections = parseResult.Document.Sections.ToArray();
        }

        var versions = await versionStore.ReadByDateAsync(date, cancellationToken);
        return new JournalHistoryEntryDetail(
            date,
            summary?.Status ?? "missing",
            summary?.AttentionReason,
            markdown,
            sections,
            versions);
    }

    public async Task<IReadOnlyList<JournalEntryVersion>> ReadVersionsAsync(
        JournalDate date,
        CancellationToken cancellationToken) =>
        await versionStore.ReadByDateAsync(date, cancellationToken);

    public async Task<(JournalEntryVersion Version, string Markdown)?> ReadVersionAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken) =>
        await versionStore.ReadAsync(date, versionId, cancellationToken);

    public async Task<TodayEditorState> RestoreVersionAsDraftAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken)
    {
        if (date != JournalDate.From(clock.Today))
        {
            throw new JournalHistoryRestoreConflictException(
                "Only today's journal versions can be restored as a draft. Historical date-aware editing is not available yet.");
        }

        var snapshot = await versionStore.ReadAsync(date, versionId, cancellationToken)
            ?? throw new InvalidOperationException("version was not found.");

        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            snapshot.Markdown,
            [],
            [],
            clock.Now);

        await draftStore.WriteAsync(draft, cancellationToken);
        return await todayService.GetTodayEditorAsync(cancellationToken);
    }
}
