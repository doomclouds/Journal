using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalIndexStoreTests
{
    [Fact]
    public async Task EnsureReadyAsync_CreatesSchemaAndMetaVersion()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);

        await store.EnsureReadyAsync(CancellationToken.None);

        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_UsesTrigramFtsForChineseSubstring()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date, status: "processed", mood: "平静", topicsJson: """["接口整理"]"""),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, "- 测试新整理的接口")],
            CancellationToken.None);

        var result = await store.SearchAsync(
            new JournalHistoryQuery("整理", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
        var hit = Assert.Single(item.Hits);
        Assert.Equal("section", hit.SourceType);
        Assert.Equal("today-focus", hit.SectionId);
    }

    [Fact]
    public async Task UpsertRawInputAsync_AddsRawInputFtsHit()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(date), [], CancellationToken.None);

        await store.UpsertRawInputAsync(
            new JournalIndexedRawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "DeepSeek reason content"),
            CancellationToken.None);

        var result = await store.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);

        var item = Assert.Single(result.Items);
        var hit = Assert.Single(item.Hits);
        Assert.Equal("raw-input", hit.SourceType);
        Assert.Equal("raw-1", hit.RawInputId);
    }

    [Fact]
    public async Task ReadSummaryAsync_ReturnsCounts()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalIndexStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(date), [], CancellationToken.None);
        await store.UpsertRawInputAsync(
            new JournalIndexedRawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "hello"),
            CancellationToken.None);
        await store.UpsertVersionAsync(
            new JournalEntryVersion(
                "version-1",
                date,
                DateTimeOffset.Parse("2026-05-13T09:00:00+08:00"),
                "confirm-draft",
                paths.EntryPath(date),
                paths.VersionMarkdownPath(date, "version-1"),
                paths.VersionMetaPath(date, "version-1"),
                "sha256:version"),
            CancellationToken.None);

        var summary = await store.ReadSummaryAsync(date, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(1, summary.RawInputCount);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task BackupAndResetAsync_MovesExistingDatabaseToBackupDirectory()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalIndexStore(paths);
        await store.EnsureReadyAsync(CancellationToken.None);
        Assert.True(File.Exists(paths.IndexPath()));

        await store.BackupAndResetAsync(
            DateTimeOffset.Parse("2026-05-13T10:00:00+08:00"),
            "schema",
            CancellationToken.None);

        Assert.True(File.Exists(paths.IndexPath()));
        Assert.Contains(Directory.EnumerateFiles(paths.IndexBackupDirectory()), path => Path.GetFileName(path).Contains("schema", StringComparison.Ordinal));
        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
    }

    [Fact]
    public async Task EnsureReadyAsync_WhenSchemaVersionIsIncompatible_BacksUpAndRebuilds()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalIndexStore(paths);
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.SetMetaAsync("schema_version", "999", CancellationToken.None);

        await store.EnsureReadyAsync(CancellationToken.None);

        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
        Assert.NotEmpty(Directory.EnumerateFiles(paths.IndexBackupDirectory()));
    }

    [Fact]
    public async Task SearchAsync_AppliesDateFiltersAndCursor()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var first = JournalDate.From(new DateOnly(2026, 5, 12));
        var second = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(first), [], CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(second), [], CancellationToken.None);

        var fromResult = await store.SearchAsync(new JournalHistoryQuery(null, null, new DateOnly(2026, 5, 13), null, null, 20), CancellationToken.None);
        var toResult = await store.SearchAsync(new JournalHistoryQuery(null, null, null, new DateOnly(2026, 5, 12), null, 20), CancellationToken.None);
        var cursorResult = await store.SearchAsync(new JournalHistoryQuery(null, null, null, null, "2026-05-13", 20), CancellationToken.None);

        Assert.Equal([second], fromResult.Items.Select(item => item.Date).ToArray());
        Assert.Equal([first], toResult.Items.Select(item => item.Date).ToArray());
        Assert.Equal([first], cursorResult.Items.Select(item => item.Date).ToArray());
    }

    private static JournalIndexStore CreateStore(string root) =>
        new(new LocalJournalPaths(new JournalStorageOptions(root)));

    private static JournalIndexedEntry CreateEntry(
        JournalDate date,
        string status = "processed",
        string? mood = "平静",
        string tagsJson = "[]",
        string topicsJson = "[]") =>
        new(
            date,
            $"entries/{date.Year}/{date.Month}/{date.MarkdownFileName}",
            status,
            mood,
            tagsJson,
            topicsJson,
            $"sha256:{date.IsoDate}",
            DateTimeOffset.Parse($"{date.IsoDate}T00:00:00+00:00"),
            128,
            DateTimeOffset.Parse($"{date.IsoDate}T01:00:00+00:00"),
            null);

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-tests", Guid.NewGuid().ToString("N"));

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
