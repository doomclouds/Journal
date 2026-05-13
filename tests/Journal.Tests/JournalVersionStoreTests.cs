using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalVersionStoreTests
{
    [Fact]
    public async Task CreateSnapshotAsync_WritesMarkdownAndMetadata()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T07:11:14+08:00");

        var version = await store.CreateSnapshotAsync(
            date,
            "# Existing entry\n",
            "entries/2026/05/2026-05-13.md",
            "confirm-draft",
            now,
            CancellationToken.None);

        Assert.Equal(date, version.Date);
        Assert.Equal("confirm-draft", version.Reason);
        Assert.StartsWith("version-2026-05-13T07-11-14", version.Id, StringComparison.Ordinal);
        Assert.True(File.Exists(version.MarkdownPath));
        Assert.True(File.Exists(version.MetaPath));
        Assert.Equal("# Existing entry\n", await File.ReadAllTextAsync(version.MarkdownPath));
        Assert.Contains("\"reason\": \"confirm-draft\"", await File.ReadAllTextAsync(version.MetaPath));
        Assert.StartsWith("sha256:", version.ContentHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadByDateAsync_ReturnsVersionsNewestFirst()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await store.CreateSnapshotAsync(date, "old", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:00:00+08:00"), CancellationToken.None);
        await store.CreateSnapshotAsync(date, "new", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), CancellationToken.None);

        var versions = await store.ReadByDateAsync(date, CancellationToken.None);

        Assert.Collection(
            versions,
            first => Assert.Equal(DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), first.CreatedAt),
            second => Assert.Equal(DateTimeOffset.Parse("2026-05-13T07:00:00+08:00"), second.CreatedAt));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-version-store-tests", Guid.NewGuid().ToString("N"));
        public static TempWorkspace Create() => new();
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
