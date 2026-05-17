using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalAnniversaryStoreTests
{
    [Fact]
    public async Task ReadAsync_WhenFileMissing_ReturnsEmptyDocument()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);

        var document = await store.ReadAsync(CancellationToken.None);

        Assert.Equal("journal-anniversaries/v1", document.Schema);
        Assert.Empty(document.Items);
    }

    [Fact]
    public async Task WriteAsync_RoundTripsAnniversaryDocument()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var item = new JournalAnniversaryItem(
            "anniv-20260516-journal-stage",
            "05-16",
            "Journal 阶段日",
            "project-milestone",
            "2024-05-16",
            "从记录习惯逐渐走向个人记忆核心。",
            true,
            DateTimeOffset.Parse("2026-05-16T23:42:00+08:00"),
            DateTimeOffset.Parse("2026-05-16T23:42:00+08:00"),
            [
                new JournalNextYearNote(
                    "note-20260516-001",
                    "2027-05-16",
                    "明年回来看 Journal 是否真正进入日常。",
                    JournalNextYearNoteStatus.Pending,
                    DateTimeOffset.Parse("2026-05-16T23:45:00+08:00"),
                    null,
                    null)
            ]);
        var document = new JournalAnniversaryDocument(
            "journal-anniversaries/v1",
            [item]);

        await store.WriteAsync(document, CancellationToken.None);
        var loaded = await store.ReadAsync(CancellationToken.None);

        var loadedItem = Assert.Single(loaded.Items);
        Assert.Equal("05-16", loadedItem.MonthDay);
        Assert.Equal("Journal 阶段日", loadedItem.Title);
        Assert.True(loadedItem.Pinned);
        Assert.Equal(JournalNextYearNoteStatus.Pending, Assert.Single(loadedItem.NextYearNotes).Status);
    }

    [Fact]
    public async Task ReadAsync_WhenJsonInvalid_ThrowsClearError()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        await File.WriteAllTextAsync(paths.AnniversaryPath(), "{ bad json", CancellationToken.None);
        var store = new JournalAnniversaryStore(paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadAsync(CancellationToken.None));

        Assert.Contains("Anniversary data is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenCancelled_DoesNotLeaveTempFile()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAnniversaryStore(paths);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => store.WriteAsync(JournalAnniversaryDocument.Empty(), cancellation.Token));

        Assert.Empty(EnumerateTempFiles(paths));
    }

    [Fact]
    public async Task WriteAsync_WhenMoveFails_DoesNotLeaveTempFile()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        Directory.CreateDirectory(paths.AnniversaryPath());
        var store = new JournalAnniversaryStore(paths);

        await Assert.ThrowsAnyAsync<Exception>(
            () => store.WriteAsync(JournalAnniversaryDocument.Empty(), CancellationToken.None));

        Assert.Empty(EnumerateTempFiles(paths));
    }

    [Fact]
    public async Task ReadAsync_WhenItemsNull_NormalizesToEmptyList()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        await File.WriteAllTextAsync(
            paths.AnniversaryPath(),
            """
            {
              "schema": "journal-anniversaries/v1",
              "items": null
            }
            """,
            CancellationToken.None);
        var store = new JournalAnniversaryStore(paths);

        var document = await store.ReadAsync(CancellationToken.None);

        Assert.Empty(document.Items);
    }

    [Fact]
    public async Task ReadAsync_WhenItemIsNull_ThrowsClearError()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        await File.WriteAllTextAsync(
            paths.AnniversaryPath(),
            """
            {
              "schema": "journal-anniversaries/v1",
              "items": [null]
            }
            """,
            CancellationToken.None);
        var store = new JournalAnniversaryStore(paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadAsync(CancellationToken.None));

        Assert.Contains("Anniversary data is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_WhenNextYearNotesNull_NormalizesToEmptyList()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        await File.WriteAllTextAsync(
            paths.AnniversaryPath(),
            """
            {
              "schema": "journal-anniversaries/v1",
              "items": [
                {
                  "id": "anniv-20260516-journal-stage",
                  "monthDay": "05-16",
                  "title": "Journal 阶段日",
                  "type": "project-milestone",
                  "originDate": "2024-05-16",
                  "description": "从记录习惯逐渐走向个人记忆核心。",
                  "pinned": true,
                  "createdAt": "2026-05-16T23:42:00+08:00",
                  "updatedAt": "2026-05-16T23:42:00+08:00",
                  "nextYearNotes": null
                }
              ]
            }
            """,
            CancellationToken.None);
        var store = new JournalAnniversaryStore(paths);

        var document = await store.ReadAsync(CancellationToken.None);

        Assert.Empty(Assert.Single(document.Items).NextYearNotes);
    }

    private static JournalAnniversaryStore CreateStore(string root)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        return new JournalAnniversaryStore(paths);
    }

    private static IReadOnlyList<string> EnumerateTempFiles(LocalJournalPaths paths) =>
        Directory.Exists(paths.AnniversaryDirectory())
            ? Directory.EnumerateFiles(paths.AnniversaryDirectory(), "*.tmp").ToArray()
            : [];

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-anniversary-store-tests", Guid.NewGuid().ToString("N"));
        public static TempWorkspace Create() => new();
        public void Dispose()
        {
            TestWorkspaceCleanup.DeleteDirectory(Root);
        }
    }
}
