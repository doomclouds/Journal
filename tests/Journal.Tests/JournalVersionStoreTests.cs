using System.Text.Json.Nodes;
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
            "  confirm-draft  ",
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
    public async Task CreateSnapshotAsync_SameTimestampCreatesDistinctSnapshotsAndPreservesContent()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var createdAt = DateTimeOffset.Parse("2026-05-13T07:11:14+08:00");

        var first = await store.CreateSnapshotAsync(date, "first", "entry.md", "confirm-draft", createdAt, CancellationToken.None);
        var second = await store.CreateSnapshotAsync(date, "second", "entry.md", "confirm-draft", createdAt, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("first", (await store.ReadAsync(date, first.Id, CancellationToken.None))!.Value.Markdown);
        Assert.Equal("second", (await store.ReadAsync(date, second.Id, CancellationToken.None))!.Value.Markdown);
    }

    [Fact]
    public async Task CreateSnapshotAsync_BlockedVersionDirectoryRethrowsDirectoryIOException()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalVersionStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var blockedYearPath = Path.Combine(workspace.Root, ".journal", "versions", date.Year);
        LocalJournalPaths.EnsureParentDirectory(blockedYearPath);
        await File.WriteAllTextAsync(blockedYearPath, "blocks version directory");

        var exception = await Assert.ThrowsAsync<IOException>(() => store.CreateSnapshotAsync(
            date,
            "markdown",
            "entry.md",
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"),
            CancellationToken.None));

        Assert.DoesNotContain("Could not create a unique journal version snapshot", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(paths.VersionDirectory(date)));
    }

    [Fact]
    public async Task CreateSnapshotAsync_MetadataPathCollisionDeletesMarkdownAndRetries()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalVersionStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var createdAt = DateTimeOffset.Parse("2026-05-13T07:11:14+08:00");
        var collidedId = "version-2026-05-13T07-11-14-0000000+08-00";
        var collidedMetaPath = paths.VersionMetaPath(date, collidedId);
        LocalJournalPaths.EnsureParentDirectory(collidedMetaPath);
        await File.WriteAllTextAsync(collidedMetaPath, "existing meta");

        var version = await store.CreateSnapshotAsync(date, "markdown", "entry.md", "confirm-draft", createdAt, CancellationToken.None);

        Assert.Equal($"{collidedId}-001", version.Id);
        Assert.False(File.Exists(paths.VersionMarkdownPath(date, collidedId)));
        Assert.Equal("existing meta", await File.ReadAllTextAsync(collidedMetaPath));
        Assert.Equal("markdown", (await store.ReadAsync(date, version.Id, CancellationToken.None))!.Value.Markdown);
    }

    [Fact]
    public async Task CreateSnapshotAsync_NonCollisionMetadataWriteFailureRethrowsOriginalException()
    {
        using var workspace = TempWorkspace.Create();
        var expected = new IOException("metadata stream failed");
        var store = new JournalVersionStore(
            new LocalJournalPaths(new JournalStorageOptions(workspace.Root)),
            async (path, contents, cancellationToken) =>
            {
                if (path.EndsWith(".meta.json", StringComparison.Ordinal))
                {
                    throw expected;
                }

                await WriteNewTextFileForTestAsync(path, contents, cancellationToken);
            });
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        var actual = await Assert.ThrowsAsync<IOException>(() => store.CreateSnapshotAsync(
            date,
            "markdown",
            "entry.md",
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"),
            CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CreateSnapshotAsync_NonCollisionMetadataWriteFailureCleansPartialFiles()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var expected = new IOException("metadata stream failed");
        var store = new JournalVersionStore(
            paths,
            async (path, contents, cancellationToken) =>
            {
                if (path.EndsWith(".meta.json", StringComparison.Ordinal))
                {
                    await WriteNewTextFileForTestAsync(path, "partial", cancellationToken);
                    throw expected;
                }

                await WriteNewTextFileForTestAsync(path, contents, cancellationToken);
            });
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await Assert.ThrowsAsync<IOException>(() => store.CreateSnapshotAsync(
            date,
            "markdown",
            "entry.md",
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"),
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(paths.VersionDirectory(date), "*.md"));
        Assert.Empty(Directory.EnumerateFiles(paths.VersionDirectory(date), "*.meta.json"));
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

    [Fact]
    public async Task ReadAsync_ReturnsVersionAndMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var version = await store.CreateSnapshotAsync(
            date,
            "# Existing entry\n",
            "entry.md",
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"),
            CancellationToken.None);

        var read = await store.ReadAsync(date, version.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(version, read.Value.Version);
        Assert.Equal("# Existing entry\n", read.Value.Markdown);
    }

    [Fact]
    public async Task ReadAsync_InvalidIdReturnsNull()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        var read = await store.ReadAsync(date, "../escape", CancellationToken.None);

        Assert.Null(read);
    }

    [Fact]
    public async Task ReadAsync_MissingMarkdownOrMetadataReturnsNull()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalVersionStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var missingMarkdown = await store.CreateSnapshotAsync(date, "markdown", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"), CancellationToken.None);
        File.Delete(missingMarkdown.MarkdownPath);
        var missingMeta = await store.CreateSnapshotAsync(date, "metadata", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:11:15+08:00"), CancellationToken.None);
        File.Delete(missingMeta.MetaPath);

        var missingMarkdownRead = await store.ReadAsync(date, missingMarkdown.Id, CancellationToken.None);
        var missingMetaRead = await store.ReadAsync(date, missingMeta.Id, CancellationToken.None);

        Assert.Null(missingMarkdownRead);
        Assert.Null(missingMetaRead);
    }

    [Fact]
    public async Task ReadAsync_HashMismatchReturnsNull()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var version = await store.CreateSnapshotAsync(date, "original", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"), CancellationToken.None);
        await File.WriteAllTextAsync(version.MarkdownPath, "tampered");

        var read = await store.ReadAsync(date, version.Id, CancellationToken.None);

        Assert.Null(read);
    }

    [Fact]
    public async Task ReadAsync_MetadataIdOrDateMismatchReturnsNull()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var otherDate = JournalDate.From(new DateOnly(2026, 5, 14));
        var idMismatch = await store.CreateSnapshotAsync(date, "id mismatch", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:11:14+08:00"), CancellationToken.None);
        await ReplaceMetaPropertyAsync(idMismatch.MetaPath, "id", $"{idMismatch.Id}-other");
        var dateMismatch = await store.CreateSnapshotAsync(date, "date mismatch", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:11:15+08:00"), CancellationToken.None);
        await ReplaceMetaPropertyAsync(dateMismatch.MetaPath, "date", otherDate.IsoDate);

        var idMismatchRead = await store.ReadAsync(date, idMismatch.Id, CancellationToken.None);
        var dateMismatchRead = await store.ReadAsync(date, dateMismatch.Id, CancellationToken.None);

        Assert.Null(idMismatchRead);
        Assert.Null(dateMismatchRead);
    }

    private static async Task ReplaceMetaPropertyAsync(string path, string propertyName, string value)
    {
        var node = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        node[propertyName] = value;
        await File.WriteAllTextAsync(path, node.ToJsonString());
    }

    private static async Task WriteNewTextFileForTestAsync(
        string path,
        string contents,
        CancellationToken cancellationToken)
    {
        LocalJournalPaths.EnsureParentDirectory(path);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(contents.AsMemory(), cancellationToken);
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
