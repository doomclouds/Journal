using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class EntryWritePipelineTests
{
    private static readonly JournalDate Date = JournalDate.From(new DateOnly(2026, 5, 13));
    private static readonly DateTimeOffset FirstWriteAt = DateTimeOffset.Parse("2026-05-13T08:00:00+08:00");
    private static readonly DateTimeOffset OverwriteAt = DateTimeOffset.Parse("2026-05-13T09:00:00+08:00");

    [Fact]
    public async Task WriteFormalEntryAsync_FirstWriteDoesNotCreateSnapshot()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var versionStore = new RecordingVersionStore();
        var pipeline = CreatePipeline(paths, versionStore);
        var markdown = CreateMarkdown("第一次正式写入");

        var result = await pipeline.WriteFormalEntryAsync(
            Date,
            markdown,
            FirstWriteAt,
            "confirm-draft",
            CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Null(result.IndexWarning);
        Assert.Equal(0, versionStore.CreateSnapshotCallCount);
        Assert.Equal(markdown, await File.ReadAllTextAsync(paths.EntryPath(Date), CancellationToken.None));

        var summary = await new JournalIndexStore(paths).ReadSummaryAsync(Date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("processed", summary.Status);
        Assert.Equal(0, summary.VersionCount);
    }

    [Fact]
    public async Task WriteFormalEntryAsync_ExistingEntryCreatesSnapshotBeforeOverwrite()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var versionStore = new JournalVersionStore(paths);
        var pipeline = CreatePipeline(paths, versionStore);
        var oldMarkdown = CreateMarkdown("旧正式内容");
        var newMarkdown = CreateMarkdown("新正式内容");

        await new EntryStore(paths).WriteAsync(Date, oldMarkdown, FirstWriteAt, CancellationToken.None);

        var result = await pipeline.WriteFormalEntryAsync(
            Date,
            newMarkdown,
            OverwriteAt,
            "confirm-draft",
            CancellationToken.None);

        Assert.Equal(JournalStatus.Updated, result.Status);
        Assert.NotNull(result.Snapshot);
        Assert.Null(result.IndexWarning);
        Assert.Equal(oldMarkdown, (await versionStore.ReadAsync(Date, result.Snapshot.Id, CancellationToken.None))!.Value.Markdown);
        Assert.Equal(newMarkdown, await File.ReadAllTextAsync(paths.EntryPath(Date), CancellationToken.None));

        var summary = await new JournalIndexStore(paths).ReadSummaryAsync(Date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("updated", summary.Status);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task WriteFormalEntryAsync_WhenSnapshotFails_DoesNotOverwriteExistingEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var pipeline = CreatePipeline(paths, new ThrowingVersionStore());
        var oldMarkdown = CreateMarkdown("不能被覆盖的旧内容");
        var newMarkdown = CreateMarkdown("不应该写入的新内容");
        await new EntryStore(paths).WriteAsync(Date, oldMarkdown, FirstWriteAt, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => pipeline.WriteFormalEntryAsync(
            Date,
            newMarkdown,
            OverwriteAt,
            "confirm-draft",
            CancellationToken.None));

        Assert.Equal(oldMarkdown, await File.ReadAllTextAsync(paths.EntryPath(Date), CancellationToken.None));
    }

    [Fact]
    public async Task WriteFormalEntryAsync_WhenIndexFails_KeepsFormalMarkdownAndReportsWarning()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        Directory.CreateDirectory(paths.IndexPath());
        var pipeline = CreatePipeline(paths, new JournalVersionStore(paths));
        var markdown = CreateMarkdown("索引失败时仍保留正式 Markdown");

        var result = await pipeline.WriteFormalEntryAsync(
            Date,
            markdown,
            FirstWriteAt,
            "confirm-draft",
            CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, result.Status);
        Assert.Null(result.Snapshot);
        Assert.False(string.IsNullOrWhiteSpace(result.IndexWarning));
        Assert.Equal(markdown, await File.ReadAllTextAsync(paths.EntryPath(Date), CancellationToken.None));
    }

    private static EntryWritePipeline CreatePipeline(LocalJournalPaths paths, IJournalVersionStore versionStore)
    {
        var indexStore = new JournalIndexStore(paths);
        return new EntryWritePipeline(
            new EntryStore(paths),
            versionStore,
            new JournalIndexingService(paths, indexStore),
            paths);
    }

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private static string CreateMarkdown(string todayFocus)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            Date.IsoDate,
            Date.MonthDay,
            "reviewing",
            ["#Journal"],
            ["历史索引"],
            "平静",
            ["今天记录一条原始材料"],
            ["昨天完成接口整理"],
            [todayFocus],
            ["继续观察"]);

        return JmfMarkdownRenderer.Render(aiJson, FirstWriteAt);
    }

    private sealed class RecordingVersionStore : IJournalVersionStore
    {
        public int CreateSnapshotCallCount { get; private set; }

        public Task<JournalEntryVersion> CreateSnapshotAsync(
            JournalDate date,
            string markdown,
            string sourceEntryPath,
            string reason,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken)
        {
            CreateSnapshotCallCount++;
            throw new InvalidOperationException("First write must not create a snapshot.");
        }

        public Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
            JournalDate date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JournalEntryVersion>>([]);

        public Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(
            JournalDate date,
            string versionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<(JournalEntryVersion Version, string Markdown)?>(null);
    }

    private sealed class ThrowingVersionStore : IJournalVersionStore
    {
        public Task<JournalEntryVersion> CreateSnapshotAsync(
            JournalDate date,
            string markdown,
            string sourceEntryPath,
            string reason,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken) =>
            throw new IOException("snapshot failed");

        public Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
            JournalDate date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JournalEntryVersion>>([]);

        public Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(
            JournalDate date,
            string versionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<(JournalEntryVersion Version, string Markdown)?>(null);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-entry-write-pipeline-tests", Guid.NewGuid().ToString("N"));

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
