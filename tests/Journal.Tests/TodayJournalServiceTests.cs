using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class TodayJournalServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 8);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");

    [Fact]
    public async Task AddInputAsync_AppendsRawInputAndCreatesReviewingDraft()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var state = await service.AddInputAsync("昨天完成了存储骨架 #工程", "", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, state.Status);
        Assert.Single(state.RawInputs);
        Assert.StartsWith("raw-", state.RawInputs[0].Id);
        Assert.Equal(36, state.RawInputs[0].Id.Length);
        Assert.Equal("text", state.RawInputs[0].Source);
        Assert.Equal("昨天完成了存储骨架 #工程", state.RawInputs[0].Text);

        var rawLines = await File.ReadAllLinesAsync(paths.RawInputPath(state.Date), CancellationToken.None);
        Assert.Single(rawLines);
        using var rawJson = JsonDocument.Parse(rawLines[0]);
        Assert.Equal("昨天完成了存储骨架 #工程", rawJson.RootElement.GetProperty("text").GetString());

        var draft = await new DraftStore(paths).ReadAsync(state.Date, CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(JournalStatus.Reviewing, draft.Status);
        Assert.Equal([state.RawInputs[0].Id], draft.SourceRawInputIds);
        Assert.Empty(draft.Errors);
        Assert.Contains("<!-- journal:section raw-inputs -->", draft.Markdown);
        Assert.Contains("- 昨天完成了存储骨架 #工程", draft.Markdown);
    }

    [Fact]
    public async Task AddInputAsync_RegeneratesDraftFromAllRawInputsOnSameDay()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var first = await service.AddInputAsync("昨天完成了存储骨架 #工程", "text", CancellationToken.None);
        var second = await service.AddInputAsync("今天准备实现 TodayJournalService #Journal", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, second.Status);
        Assert.Equal(2, second.RawInputs.Count);
        Assert.Equal([first.RawInputs[0].Id, second.RawInputs[1].Id], second.Draft!.SourceRawInputIds);
        Assert.Contains("- 昨天完成了存储骨架 #工程", second.Draft.Markdown);
        Assert.Contains("- 今天准备实现 TodayJournalService #Journal", second.Draft.Markdown);

        var rawLines = await File.ReadAllLinesAsync(paths.RawInputPath(second.Date), CancellationToken.None);
        Assert.Equal(2, rawLines.Length);
    }

    [Fact]
    public async Task ConfirmDraftAsync_WritesEntryAndReturnsProcessedForFirstEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("今天准备实现确认流程 #Journal", "text", CancellationToken.None);

        var state = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, state.Status);
        Assert.NotNull(state.Entry);
        Assert.Contains("今天准备实现确认流程 #Journal", state.Entry.Markdown);
        Assert.True(File.Exists(paths.EntryPath(state.Date)));

        var draft = await new DraftStore(paths).ReadAsync(state.Date, CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(JournalStatus.Processed, draft.Status);
    }

    [Fact]
    public async Task ConfirmDraftAsync_ReturnsUpdatedAndOverwritesEntryWhenEntryAlreadyExists()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了第一版 #Journal", "text", CancellationToken.None);
        var processed = await service.ConfirmDraftAsync(CancellationToken.None);
        var firstMarkdown = processed.Entry!.Markdown;

        await service.AddInputAsync("今天补上覆盖正式 entry 的路径 #Journal", "text", CancellationToken.None);
        var updated = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Updated, updated.Status);
        Assert.NotNull(updated.Entry);
        Assert.NotEqual(firstMarkdown, updated.Entry.Markdown);
        Assert.Contains("昨天完成了第一版 #Journal", updated.Entry.Markdown);
        Assert.Contains("今天补上覆盖正式 entry 的路径 #Journal", updated.Entry.Markdown);

        var entryText = await File.ReadAllTextAsync(paths.EntryPath(updated.Date), CancellationToken.None);
        Assert.Equal(updated.Entry.Markdown, entryText);
    }

    [Fact]
    public async Task AddInputAsync_CreatesAttentionDraftAndDoesNotWriteEntryWhenAiJsonIsInvalid()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths, new InvalidAiProvider());

        var state = await service.AddInputAsync("今天输入会触发 invalid AI JSON", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Attention, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Equal(JournalStatus.Attention, state.Draft.Status);
        Assert.Equal([state.RawInputs[0].Id], state.Draft.SourceRawInputIds);
        Assert.NotEmpty(state.Errors);
        Assert.Contains(state.Errors, error => error.Contains("schema must be journal-entry/v1", StringComparison.Ordinal));
        Assert.Contains("AI JSON validation failed", state.Draft.Markdown);
        Assert.Contains("schema must be journal-entry/v1", state.Draft.Markdown);
        Assert.False(File.Exists(paths.EntryPath(state.Date)));
    }

    private static TodayJournalService CreateService(LocalJournalPaths paths, IJournalAiProvider? aiProvider = null) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            aiProvider ?? new MockAiProvider(),
            new FixedJournalClock(FixedDay, FixedNow));

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class InvalidAiProvider : IJournalAiProvider
    {
        public JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt) =>
            new(
                "invalid-schema",
                date.IsoDate,
                date.MonthDay,
                "draft",
                [],
                [],
                "未标注",
                [],
                [],
                [],
                []);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-today-tests", Guid.NewGuid().ToString("N"));

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
