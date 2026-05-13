using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

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
    public async Task SearchAsync_UsesTrigramFtsForSectionContent()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, "- Review DeepSeek adapter notes")],
            CancellationToken.None);

        var result = await store.SearchAsync(
            new JournalHistoryQuery("DeepSeek", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
        var hit = Assert.Single(item.Hits);
        Assert.Equal("section", hit.SourceType);
        Assert.Equal("today-focus", hit.SectionId);
    }

    [Fact]
    public async Task SearchAsync_UsesLikeFallbackForShortChineseQuery()
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

    [Theory]
    [InlineData("foo-bar")]
    [InlineData("\"abc")]
    [InlineData("a:b")]
    [InlineData("AND")]
    public async Task SearchAsync_TreatsFtsQueryAsLiteralText(string queryText)
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, """Need foo-bar, "abc, a:b, and AND as literal notes.""")],
            CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => store.SearchAsync(
            new JournalHistoryQuery(queryText, null, null, null, null, 20),
            CancellationToken.None));

        Assert.Null(exception);
        var result = await store.SearchAsync(
            new JournalHistoryQuery(queryText, null, null, null, null, 20),
            CancellationToken.None);
        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
    }

    [Fact]
    public async Task SearchAsync_ReturnsBoundedFtsSnippetsWithHighlightMarkers()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var longSectionContent = string.Join(
            ' ',
            Enumerable.Range(1, 40).Select(index => $"section-prefix-{index}"))
            + " DeepSeek "
            + string.Join(' ', Enumerable.Range(1, 40).Select(index => $"section-suffix-{index}"));
        var longRawInputText = string.Join(
            ' ',
            Enumerable.Range(1, 40).Select(index => $"raw-prefix-{index}"))
            + " DeepSeek "
            + string.Join(' ', Enumerable.Range(1, 40).Select(index => $"raw-suffix-{index}"));

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, longSectionContent)],
            CancellationToken.None);
        await store.UpsertRawInputAsync(
            new JournalIndexedRawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", longRawInputText),
            CancellationToken.None);

        var result = await store.SearchAsync(
            new JournalHistoryQuery("DeepSeek", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        var sectionHit = Assert.Single(item.Hits, hit => hit.SourceType == "section");
        var rawHit = Assert.Single(item.Hits, hit => hit.SourceType == "raw-input");
        Assert.Contains("[DeepSeek]", sectionHit.Snippet, StringComparison.Ordinal);
        Assert.Contains("[DeepSeek]", rawHit.Snippet, StringComparison.Ordinal);
        Assert.True(sectionHit.Snippet.Length < longSectionContent.Length / 2);
        Assert.True(rawHit.Snippet.Length < longRawInputText.Length / 2);
    }

    [Fact]
    public async Task SearchAsync_LimitsFtsHitsPerDateToFive()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var sections = Enumerable.Range(1, 8)
            .Select(index => new JournalIndexedSection(date, $"section-{index:00}", $"Section {index}", index, $"DeepSeek hit {index}"))
            .ToArray();

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(date), sections, CancellationToken.None);

        var result = await store.SearchAsync(
            new JournalHistoryQuery("DeepSeek", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(5, item.Hits.Count);
    }

    [Fact]
    public async Task SearchAsync_ReturnsBoundedLikeFallbackSnippets()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var longContent = "整理 " + string.Join(' ', Enumerable.Range(1, 120).Select(index => $"fallback-tail-{index}"));

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, longContent)],
            CancellationToken.None);

        var result = await store.SearchAsync(
            new JournalHistoryQuery("整理", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        var hit = Assert.Single(item.Hits);
        Assert.Contains("整理", hit.Snippet, StringComparison.Ordinal);
        Assert.True(hit.Snippet.Length <= 240);
        Assert.True(hit.Snippet.Length < longContent.Length);
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
    public async Task BackupAndResetAsync_PreservesWalAndShmSidecarsWithDatabaseBackup()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalIndexStore(paths);
        await store.EnsureReadyAsync(CancellationToken.None);
        await File.WriteAllTextAsync(paths.IndexPath() + "-wal", "diagnostic wal", CancellationToken.None);
        await File.WriteAllTextAsync(paths.IndexPath() + "-shm", "diagnostic shm", CancellationToken.None);

        await store.BackupAndResetAsync(
            DateTimeOffset.Parse("2026-05-13T10:00:00+08:00"),
            "wal-test",
            CancellationToken.None);

        var backupFiles = Directory.EnumerateFiles(paths.IndexBackupDirectory())
            .Select(Path.GetFileName)
            .ToArray();
        Assert.Contains("journal-20260513100000-wal-test.db", backupFiles);
        Assert.Contains("journal-20260513100000-wal-test.db-wal", backupFiles);
        Assert.Contains("journal-20260513100000-wal-test.db-shm", backupFiles);
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
    public async Task EnsureReadyAsync_WhenDatabaseIsCorrupt_BacksUpAndRebuilds()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        Directory.CreateDirectory(paths.IndexDirectory());
        await File.WriteAllBytesAsync(paths.IndexPath(), [0x4a, 0x4d, 0x46], CancellationToken.None);
        var store = new JournalIndexStore(paths);

        await store.EnsureReadyAsync(CancellationToken.None);

        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
        Assert.NotEmpty(Directory.EnumerateFiles(paths.IndexBackupDirectory()));
    }

    [Fact]
    public async Task EnsureReadyAsync_WhenExistingSchemaMissesRequiredColumns_BacksUpAndRebuilds()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        Directory.CreateDirectory(paths.IndexDirectory());
        await using (var connection = new SqliteConnection($"Data Source={paths.IndexPath()};Pooling=False"))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE journal_meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                INSERT INTO journal_meta(key, value) VALUES('schema_version', '1');
                CREATE TABLE entries(date TEXT PRIMARY KEY, status TEXT NOT NULL);
                """;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var store = new JournalIndexStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(CreateEntry(date), [], CancellationToken.None);

        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
        Assert.NotEmpty(Directory.EnumerateFiles(paths.IndexBackupDirectory()));
    }

    [Fact]
    public async Task EnsureReadyAsync_WhenExistingFtsTablesAreOrdinaryTables_BacksUpRebuildsAndSearches()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        Directory.CreateDirectory(paths.IndexDirectory());
        await using (var connection = new SqliteConnection($"Data Source={paths.IndexPath()};Pooling=False"))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE journal_meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                INSERT INTO journal_meta(key, value) VALUES('schema_version', '1');
                CREATE TABLE entries(
                    date TEXT PRIMARY KEY,
                    month_day TEXT NOT NULL,
                    entry_path TEXT NOT NULL,
                    status TEXT NOT NULL,
                    mood TEXT NULL,
                    tags_json TEXT NOT NULL,
                    topics_json TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    last_write_time_utc TEXT NOT NULL,
                    file_size INTEGER NOT NULL,
                    indexed_at_utc TEXT NOT NULL,
                    attention_reason TEXT NULL
                );
                CREATE TABLE entry_sections(
                    date TEXT NOT NULL,
                    section_id TEXT NOT NULL,
                    title TEXT NOT NULL,
                    display_order INTEGER NOT NULL,
                    content TEXT NOT NULL,
                    PRIMARY KEY(date, section_id)
                );
                CREATE TABLE entry_versions(
                    id TEXT PRIMARY KEY,
                    date TEXT NOT NULL,
                    version_path TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    reason TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    source_entry_path TEXT NOT NULL
                );
                CREATE TABLE raw_inputs(
                    id TEXT PRIMARY KEY,
                    date TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    source TEXT NOT NULL,
                    text TEXT NOT NULL
                );
                CREATE TABLE section_fts(
                    date TEXT,
                    section_id TEXT,
                    title TEXT,
                    content TEXT,
                    metadata TEXT
                );
                CREATE TABLE raw_input_fts(
                    raw_input_id TEXT,
                    date TEXT,
                    source TEXT,
                    text TEXT
                );
                """;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var store = new JournalIndexStore(paths);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            CreateEntry(date),
            [new JournalIndexedSection(date, "today-focus", "今日重点", 10, "Need DeepSeek adapter validation")],
            CancellationToken.None);
        await store.UpsertRawInputAsync(
            new JournalIndexedRawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "DeepSeek raw input"),
            CancellationToken.None);

        var result = await store.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);

        Assert.Equal("1", await store.ReadMetaAsync("schema_version", CancellationToken.None));
        Assert.NotEmpty(Directory.EnumerateFiles(paths.IndexBackupDirectory()));
        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
        Assert.Contains(item.Hits, hit => hit.SourceType == "section" && hit.SectionId == "today-focus");
        Assert.Contains(item.Hits, hit => hit.SourceType == "raw-input" && hit.RawInputId == "raw-1");
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
