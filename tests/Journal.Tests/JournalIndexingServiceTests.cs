using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalIndexingServiceTests
{
    [Fact]
    public async Task IndexEntryAsync_IndexesValidJmfSectionsAndMetadata()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        var markdown = CreateMarkdown(date, "今天继续做接口整理");
        var entryPath = paths.EntryPath(date);
        LocalJournalPaths.EnsureParentDirectory(entryPath);
        await File.WriteAllTextAsync(entryPath, markdown, CancellationToken.None);

        await service.IndexEntryAsync(date, markdown, entryPath, "processed", now, CancellationToken.None);

        var result = await indexStore.SearchAsync(new JournalHistoryQuery("整理", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
        Assert.Equal("processed", item.Status);
        Assert.Contains(item.Hits, hit => hit.SourceType == "section" && hit.SectionId == "today-focus");
        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("平静", summary.Mood);
    }

    [Fact]
    public async Task ScanAsync_MarksMissingEntryWithoutDeletingIndexRow()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        var entryPath = paths.EntryPath(date);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "今天继续做接口整理"));
        await service.ScanAsync(now, CancellationToken.None);
        File.Delete(entryPath);

        await service.ScanAsync(now.AddMinutes(5), CancellationToken.None);

        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("missing", summary.Status);
        Assert.Equal("entry_file_missing", summary.AttentionReason);
    }

    [Fact]
    public async Task ScanAsync_ReindexesExternallyChangedValidMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "旧内容只提到接口整理"));
        await service.ScanAsync(now, CancellationToken.None);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "外部手工修改后的新内容"));

        await service.ScanAsync(now.AddMinutes(5), CancellationToken.None);

        var result = await indexStore.SearchAsync(new JournalHistoryQuery("手工修改", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
    }

    [Fact]
    public async Task ScanAsync_MarksInvalidExternallyChangedMarkdownAsAttention()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "今天继续做接口整理"));
        await service.ScanAsync(now, CancellationToken.None);
        await WriteEntryAsync(paths, date, "# broken markdown without JMF markers");

        await service.ScanAsync(now.AddMinutes(5), CancellationToken.None);

        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("attention", summary.Status);
        Assert.Equal("invalid_jmf", summary.AttentionReason);
    }

    [Fact]
    public async Task ScanAsync_InvalidMarkdownClearsStaleSectionFts()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "旧关键词仍在旧索引里"));
        await service.ScanAsync(now, CancellationToken.None);
        var before = await indexStore.SearchAsync(new JournalHistoryQuery("旧关键词", null, null, null, null, 20), CancellationToken.None);
        Assert.Contains(before.Items, item => item.Date == date && item.Hits.Any(hit => hit.SourceType == "section"));
        await WriteEntryAsync(paths, date, "# broken markdown without JMF markers");

        await service.ScanAsync(now.AddMinutes(5), CancellationToken.None);

        var after = await indexStore.SearchAsync(new JournalHistoryQuery("旧关键词", null, null, null, null, 20), CancellationToken.None);
        Assert.DoesNotContain(after.Items, item => item.Date == date && item.Hits.Any(hit => hit.SourceType == "section"));
        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("attention", summary.Status);
        Assert.Equal("invalid_jmf", summary.AttentionReason);
    }

    [Fact]
    public async Task ScanAsync_RawOnlyInvalidThenDeletedEntryMarksMissing()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        await service.SyncRawInputsAsync(
            date,
            [new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "raw-only input")],
            CancellationToken.None);
        await WriteEntryAsync(paths, date, "# broken markdown without JMF markers");
        await service.ScanAsync(now, CancellationToken.None);
        File.Delete(paths.EntryPath(date));

        await service.ScanAsync(now.AddMinutes(5), CancellationToken.None);

        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("missing", summary.Status);
        Assert.Equal("entry_file_missing", summary.AttentionReason);
    }

    [Fact]
    public async Task ScanAsync_SkipsNonCanonicalEntryPathForMatchingDateFileName()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var canonicalMarkdown = CreateMarkdown(date, "只应该索引 canonical 内容");
        await WriteEntryAsync(paths, date, canonicalMarkdown);
        var backupPath = Path.Combine(paths.EntryRootDirectory(), "backup", date.MarkdownFileName);
        LocalJournalPaths.EnsureParentDirectory(backupPath);
        await File.WriteAllTextAsync(backupPath, CreateMarkdown(date, "backup 内容不能进入索引"), CancellationToken.None);

        await service.ScanAsync(DateTimeOffset.Parse("2026-05-13T01:00:00+00:00"), CancellationToken.None);

        var canonicalResult = await indexStore.SearchAsync(new JournalHistoryQuery("canonical", null, null, null, null, 20), CancellationToken.None);
        var canonicalItem = Assert.Single(canonicalResult.Items);
        Assert.Equal(date, canonicalItem.Date);
        var backupResult = await indexStore.SearchAsync(new JournalHistoryQuery("backup", null, null, null, null, 20), CancellationToken.None);
        Assert.Empty(backupResult.Items);

        File.Delete(paths.EntryPath(date));
        await service.ScanAsync(DateTimeOffset.Parse("2026-05-13T01:05:00+00:00"), CancellationToken.None);

        backupResult = await indexStore.SearchAsync(new JournalHistoryQuery("backup", null, null, null, null, 20), CancellationToken.None);
        Assert.Empty(backupResult.Items);
    }

    [Fact]
    public async Task SyncRawInputsAsync_IndexesRawInputJsonlIntoFts()
    {
        using var workspace = TempWorkspace.Create();
        var (_, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await service.SyncRawInputsAsync(
            date,
            [new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "原始材料里提到了 DeepSeek reason content")],
            CancellationToken.None);

        var result = await indexStore.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(result.Items);
        var hit = Assert.Single(item.Hits, hit => hit.SourceType == "raw-input");
        Assert.Equal("raw-1", hit.RawInputId);
    }

    [Fact]
    public async Task RebuildAsync_RestoresRawInputsAndVersionsFromFiles()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var markdown = CreateMarkdown(date, "今天继续做接口整理");
        await WriteEntryAsync(paths, date, markdown);
        await new RawInputStore(paths).AppendAsync(
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "重建时恢复 DeepSeek 原始材料"),
            CancellationToken.None);
        await new JournalVersionStore(paths).CreateSnapshotAsync(
            date,
            markdown,
            paths.EntryPath(date),
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T09:00:00+08:00"),
            CancellationToken.None);

        await service.RebuildAsync(DateTimeOffset.Parse("2026-05-13T01:00:00+00:00"), CancellationToken.None);

        var result = await indexStore.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(result.Items);
        Assert.Contains(item.Hits, hit => hit.SourceType == "raw-input" && hit.RawInputId == "raw-1");
        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task ScanAsync_SynchronizesRawInputsAndVersionsFromFiles()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var markdown = CreateMarkdown(date, "今天继续做接口整理");
        await WriteEntryAsync(paths, date, markdown);
        await new RawInputStore(paths).AppendAsync(
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "scan 恢复 DeepSeek 原始材料"),
            CancellationToken.None);
        await new JournalVersionStore(paths).CreateSnapshotAsync(
            date,
            markdown,
            paths.EntryPath(date),
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T09:00:00+08:00"),
            CancellationToken.None);

        await service.ScanAsync(DateTimeOffset.Parse("2026-05-13T01:00:00+00:00"), CancellationToken.None);

        var result = await indexStore.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(result.Items);
        Assert.Contains(item.Hits, hit => hit.SourceType == "raw-input" && hit.RawInputId == "raw-1");
        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.RawInputCount);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task ScanAsync_RepopulatesEntriesRawInputsAndVersionsAfterSchemaReset()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var markdown = CreateMarkdown(date, "schema reset 后恢复正式正文");
        await WriteEntryAsync(paths, date, markdown);
        await new RawInputStore(paths).AppendAsync(
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), "text", "schema reset 后恢复 DeepSeek raw"),
            CancellationToken.None);
        await new JournalVersionStore(paths).CreateSnapshotAsync(
            date,
            markdown,
            paths.EntryPath(date),
            "confirm-draft",
            DateTimeOffset.Parse("2026-05-13T09:00:00+08:00"),
            CancellationToken.None);
        await indexStore.EnsureReadyAsync(CancellationToken.None);
        await indexStore.SetMetaAsync("schema_version", "999", CancellationToken.None);

        await service.ScanAsync(DateTimeOffset.Parse("2026-05-13T01:00:00+00:00"), CancellationToken.None);

        var markdownResult = await indexStore.SearchAsync(new JournalHistoryQuery("正式正文", null, null, null, null, 20), CancellationToken.None);
        Assert.Contains(markdownResult.Items, item => item.Date == date);
        var rawResult = await indexStore.SearchAsync(new JournalHistoryQuery("DeepSeek", null, null, null, null, 20), CancellationToken.None);
        var item = Assert.Single(rawResult.Items);
        Assert.Contains(item.Hits, hit => hit.SourceType == "raw-input" && hit.RawInputId == "raw-1");
        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.RawInputCount);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task SyncVersionAsync_IndexesVersionSummary()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "今天继续做接口整理"));
        await service.ScanAsync(now, CancellationToken.None);

        await service.SyncVersionAsync(
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

        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task IndexEntryAsync_InvalidJmfMarksAttention()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T01:00:00+00:00");
        var entryPath = paths.EntryPath(date);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "今天继续做接口整理"));
        await service.IndexEntryAsync(date, CreateMarkdown(date, "今天继续做接口整理"), entryPath, "processed", now, CancellationToken.None);

        await service.IndexEntryAsync(date, "# broken markdown without JMF markers", entryPath, "processed", now.AddMinutes(5), CancellationToken.None);

        var summary = await indexStore.ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("attention", summary.Status);
        Assert.Equal("invalid_jmf", summary.AttentionReason);
    }

    private static (LocalJournalPaths Paths, JournalIndexStore IndexStore, JournalIndexingService Service) CreateService(string root)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        var indexStore = new JournalIndexStore(paths);
        return (paths, indexStore, new JournalIndexingService(paths, indexStore));
    }

    private static async Task WriteEntryAsync(LocalJournalPaths paths, JournalDate date, string markdown)
    {
        var entryPath = paths.EntryPath(date);
        LocalJournalPaths.EnsureParentDirectory(entryPath);
        await File.WriteAllTextAsync(entryPath, markdown, CancellationToken.None);
    }

    private static string CreateMarkdown(JournalDate date, string todayFocus)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "reviewing",
            ["#Journal"],
            ["接口整理"],
            "平静",
            ["今天记录一条原始材料"],
            ["昨天完成接口整理"],
            [todayFocus],
            ["继续观察"]);

        return JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse($"{date.IsoDate}T09:00:00+08:00"));
    }

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
