using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class LocalJournalStorageTests
{
    [Fact]
    public void LocalJournalPaths_BuildsPhase2Paths()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(new DateOnly(2026, 5, 8));

        Assert.EndsWith(Path.Combine("entries", "2026", "05", "2026-05-08.md"), paths.EntryPath(date));
        Assert.EndsWith(Path.Combine(".journal", "raw-inputs", "2026", "05", "2026-05-08.jsonl"), paths.RawInputPath(date));
        Assert.EndsWith(Path.Combine(".journal", "drafts", "2026", "05", "2026-05-08.md"), paths.DraftPath(date));
        Assert.EndsWith(Path.Combine(".journal", "drafts", "2026", "05", "2026-05-08.meta.json"), paths.DraftMetaPath(date));
    }

    [Fact]
    public async Task RawInputStore_AppendsJsonLines()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var store = new RawInputStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));

        await store.AppendAsync(new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-08T08:00:00+08:00"), "text", "昨天完成骨架。"), CancellationToken.None);
        await store.AppendAsync(new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-08T08:03:00+08:00"), "text", "今天做 JMF。"), CancellationToken.None);

        var inputs = await store.ReadAsync(date, CancellationToken.None);

        Assert.Equal(["raw-1", "raw-2"], inputs.Select(input => input.Id).ToArray());
        Assert.Equal("今天做 JMF。", inputs[1].Text);
    }

    [Fact]
    public async Task RawInputStore_ReportsInvalidJsonLineWithPathAndLineNumber()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new RawInputStore(paths);
        var path = paths.RawInputPath(date);
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllLinesAsync(
            path,
            [
                """{"id":"raw-1","date":"2026-05-08","createdAt":"2026-05-08T00:00:00+00:00","source":"text","text":"ok"}""",
                """{"id":"raw-2","date":"""
            ],
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(date, CancellationToken.None));

        Assert.Contains(path, exception.Message);
        Assert.Contains("line 2", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task RawInputStore_ReportsInvalidDateLineWithPathAndLineNumber()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new RawInputStore(paths);
        var path = paths.RawInputPath(date);
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllLinesAsync(
            path,
            [
                """{"id":"raw-1","date":"2026-05-08","createdAt":"2026-05-08T00:00:00+00:00","source":"text","text":"ok"}""",
                """{"id":"raw-2","date":"not-a-date","createdAt":"2026-05-08T00:01:00+00:00","source":"text","text":"bad"}"""
            ],
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(date, CancellationToken.None));

        Assert.Contains(path, exception.Message);
        Assert.Contains("line 2", exception.Message);
        Assert.IsType<FormatException>(exception.InnerException);
    }

    [Fact]
    public async Task DraftAndEntryStores_WriteReadableMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var draftStore = new DraftStore(paths);
        var entryStore = new EntryStore(paths);
        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            "# 2026-05-08\n\n<!-- journal:section raw-inputs -->",
            ["raw-1"],
            [],
            DateTimeOffset.Parse("2026-05-08T08:05:00+08:00"));

        await draftStore.WriteAsync(draft, CancellationToken.None);
        var loadedDraft = await draftStore.ReadAsync(date, CancellationToken.None);
        await entryStore.WriteAsync(date, loadedDraft!.Markdown, DateTimeOffset.Parse("2026-05-08T08:06:00+08:00"), CancellationToken.None);
        var entry = await entryStore.ReadAsync(date, CancellationToken.None);

        Assert.NotNull(loadedDraft);
        Assert.Equal(JournalStatus.Reviewing, loadedDraft.Status);
        Assert.Equal(["raw-1"], loadedDraft.SourceRawInputIds);
        Assert.NotNull(entry);
        Assert.Contains("journal:section raw-inputs", entry.Markdown);
        Assert.EndsWith(Path.Combine("entries", "2026", "05", "2026-05-08.md"), entry.Path);
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
