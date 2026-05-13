using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public interface IJournalVersionStore
{
    Task<JournalEntryVersion> CreateSnapshotAsync(
        JournalDate date,
        string markdown,
        string sourceEntryPath,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
        JournalDate date,
        CancellationToken cancellationToken);

    Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken);
}
