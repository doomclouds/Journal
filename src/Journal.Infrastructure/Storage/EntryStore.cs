using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class EntryStore
{
    private readonly LocalJournalPaths _paths;

    public EntryStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public bool Exists(JournalDate date) => File.Exists(_paths.EntryPath(date));

    public async Task WriteAsync(JournalDate date, string markdown, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var path = _paths.EntryPath(date);
        LocalJournalPaths.EnsureParentDirectory(path);

        await File.WriteAllTextAsync(path, markdown, cancellationToken);
        File.SetLastWriteTimeUtc(path, updatedAt.UtcDateTime);
    }

    public async Task<JournalEntry?> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var path = _paths.EntryPath(date);
        if (!File.Exists(path))
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(path, cancellationToken);
        var updatedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

        return new JournalEntry(date, markdown, path, updatedAt);
    }
}
