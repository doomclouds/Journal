using Journal.Domain.Entries;
using Microsoft.Data.Sqlite;

namespace Journal.Infrastructure.Storage;

public sealed record EntryWriteResult(
    JournalStatus Status,
    JournalEntryVersion? Snapshot,
    string? IndexWarning);

public sealed class EntryWritePipeline(
    EntryStore entryStore,
    IJournalVersionStore versionStore,
    JournalIndexingService indexingService,
    LocalJournalPaths paths)
{
    public async Task<EntryWriteResult> WriteFormalEntryAsync(
        JournalDate date,
        string markdown,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var existing = await entryStore.ReadAsync(date, cancellationToken);
        JournalEntryVersion? snapshot = null;

        if (existing is not null)
        {
            snapshot = await versionStore.CreateSnapshotAsync(
                date,
                existing.Markdown,
                existing.Path,
                reason,
                now,
                cancellationToken);
        }

        await entryStore.WriteAsync(date, markdown, now, cancellationToken);

        var status = existing is null ? JournalStatus.Processed : JournalStatus.Updated;
        string? indexWarning = null;
        try
        {
            await indexingService.IndexEntryAsync(
                date,
                markdown,
                paths.EntryPath(date),
                status.ToString().ToLowerInvariant(),
                now,
                cancellationToken);

            if (snapshot is not null)
            {
                await indexingService.SyncVersionAsync(snapshot, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            indexWarning = exception.Message;
        }

        return new EntryWriteResult(status, snapshot, indexWarning);
    }
}
