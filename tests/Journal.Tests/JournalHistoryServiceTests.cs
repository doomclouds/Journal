using System.Text;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class JournalHistoryServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 13);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-13T08:05:00+08:00");
    private static readonly JournalDate Date = JournalDate.From(FixedDay);

    [Fact]
    public async Task SearchAsync_ScansBeforeReturningResults()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, service) = CreateSubject(workspace.Root);
        await WriteEntryAsync(paths, Date, CreateMarkdown(Date, "测试历史搜索"));

        var result = await service.SearchAsync(
            new JournalHistoryQuery("测试历史搜索", null, null, null, null, 20),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Date == Date);
    }

    [Fact]
    public async Task GetEntryAsync_ReturnsMetadataSectionsAndVersions()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, service) = CreateSubject(workspace.Root);
        var markdown = CreateMarkdown(Date, "测试历史详情");
        await new EntryStore(paths).WriteAsync(Date, markdown, FixedNow, CancellationToken.None);
        await new JournalVersionStore(paths).CreateSnapshotAsync(
            Date,
            CreateMarkdown(Date, "测试历史版本"),
            paths.EntryPath(Date),
            "test",
            FixedNow.AddMinutes(1),
            CancellationToken.None);

        var detail = await service.GetEntryAsync(Date, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("2026-05-13", detail.Date.IsoDate);
        Assert.Contains("测试历史详情", detail.Markdown, StringComparison.Ordinal);
        Assert.Contains(detail.Sections, section => section.Id == "today-focus");
        Assert.Single(detail.Versions);
    }

    [Fact]
    public async Task RestoreVersionAsDraftAsync_WritesReviewingDraftOnly()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, service) = CreateSubject(workspace.Root);
        var currentMarkdown = CreateMarkdown(Date, "current formal entry");
        await new EntryStore(paths).WriteAsync(Date, currentMarkdown, FixedNow, CancellationToken.None);
        var version = await new JournalVersionStore(paths).CreateSnapshotAsync(
            Date,
            CreateMarkdown(Date, "restored version"),
            paths.EntryPath(Date),
            "test",
            FixedNow.AddMinutes(1),
            CancellationToken.None);

        var editor = await service.RestoreVersionAsDraftAsync(Date, version.Id, CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, editor.Status);
        Assert.Contains("restored version", editor.Markdown, StringComparison.Ordinal);
        Assert.True(File.Exists(paths.DraftPath(Date)));
        Assert.False((await File.ReadAllTextAsync(paths.EntryPath(Date), Encoding.UTF8)).Contains("restored version", StringComparison.Ordinal));
    }

    private static (LocalJournalPaths Paths, JournalHistoryService Service) CreateSubject(string root)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        var indexStore = new JournalIndexStore(paths);
        var indexingService = new JournalIndexingService(paths, indexStore);
        var versionStore = new JournalVersionStore(paths);
        var entryStore = new EntryStore(paths);
        var draftStore = new DraftStore(paths);
        var clock = new FixedJournalClock(FixedDay, FixedNow);
        var todayService = new TodayJournalService(
            new RawInputStore(paths),
            draftStore,
            entryStore,
            new EntryWritePipeline(entryStore, versionStore, indexingService, paths),
            CreateGenerationService(),
            clock);

        return (paths, new JournalHistoryService(
            indexStore,
            indexingService,
            versionStore,
            entryStore,
            draftStore,
            todayService,
            clock));
    }

    private static async Task WriteEntryAsync(LocalJournalPaths paths, JournalDate date, string markdown)
    {
        LocalJournalPaths.EnsureParentDirectory(paths.EntryPath(date));
        await File.WriteAllTextAsync(paths.EntryPath(date), markdown, Encoding.UTF8);
    }

    private static string CreateMarkdown(JournalDate date, string todayFocus)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "reviewing",
            ["#Journal"],
            ["历史"],
            "平静",
            ["今天记录历史服务测试"],
            ["昨天完成索引基础"],
            [todayFocus],
            ["继续验证"]);

        return JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse($"{date.IsoDate}T09:00:00+08:00"));
    }

    private static JournalAiGenerationService CreateGenerationService() =>
        new(
            new StaticSettingsReader(JournalAiSettings.CreateDefault()),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new StaticRuntime()));

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class StaticSettingsReader(JournalAiSettings settings) : IJournalAiSettingsReader
    {
        public Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(settings);
    }

    private sealed class StaticRuntime : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "unexpected_runtime_call", "Runtime should not be called.", "Runtime should not be called."),
                TimeSpan.Zero));

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create("test", "unexpected_runtime_call", "Runtime should not be called.", "Runtime should not be called."),
                TimeSpan.Zero));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-history-service-tests", Guid.NewGuid().ToString("N"));

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
