using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalHarnessServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 12);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-12T08:30:00+08:00");

    [Fact]
    public async Task StartTodayRunAsync_AppendsRawInputAndCreatesQueuedRun()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create("test", "unexpected", "Planner should not be called.", "Planner should not be called."),
                TimeSpan.Zero));
        var service = CreateService(paths, runtime);

        var result = await service.StartTodayRunAsync("今天先把 harness run 落盘", "", CancellationToken.None);

        Assert.Equal(JournalStatus.Empty, result.Today.Status);
        Assert.Single(result.Today.RawInputs);
        Assert.False(runtime.HarnessPlannerCalled);
        Assert.Equal("queued", result.Run.Status);
        Assert.Equal("mock", result.Run.ProviderId);
        Assert.Equal(JournalHarnessPrompt.Version, result.Run.PromptVersion);
        Assert.Equal(result.Today.RawInputs[0].Id, result.Run.CurrentRawInputId);
        Assert.Null(result.Run.StartedAt);
        Assert.Null(result.Run.CompletedAt);
        Assert.Empty(result.Run.ToolCalls);
        Assert.False(File.Exists(paths.EntryPath(result.Today.Date)));
        Assert.False(File.Exists(paths.DraftPath(result.Today.Date)));

        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(result.Today.Date, result.Run.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(result.Run.Id, persisted.Id);
        Assert.Equal("queued", persisted.Status);
    }

    [Fact]
    public async Task ExecuteRunAsync_WritesReviewingDraftAndCompletesRunWithoutWritingEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- 推进 harness service 执行链",
            ["raw-current"],
            "当前输入需要整理到今日重点。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("今天推进 harness service 执行链", "text", CancellationToken.None);
        operation = operation with { BasedOnRawInputIds = [started.Run.CurrentRawInputId] };
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17));

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.True(runtime.HarnessPlannerCalled);
        Assert.Equal(JournalStatus.Reviewing, result.Today.Status);
        Assert.NotNull(result.Today.Draft);
        Assert.Equal(JournalStatus.Reviewing, result.Today.Draft.Status);
        Assert.Equal("reviewing", result.Run.Status);
        Assert.NotNull(result.Run.StartedAt);
        Assert.NotNull(result.Run.CompletedAt);
        Assert.Contains("推进 harness service 执行链", result.Today.Draft.Markdown);
        Assert.Contains("last_operation=\"append\"", result.Today.Draft.Markdown);
        Assert.Single(result.Run.ToolCalls);
        Assert.Equal("appendJournalSection", result.Run.ToolCalls[0].Name);
        Assert.Equal("applied", result.Run.ToolCalls[0].Status);
        Assert.False(File.Exists(paths.EntryPath(result.Today.Date)));

        var draft = await new DraftStore(paths).ReadAsync(result.Today.Date, CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(JournalStatus.Reviewing, draft.Status);

        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(result.Today.Date, started.Run.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("reviewing", persisted.Status);
    }

    private static JournalHarnessService CreateService(
        LocalJournalPaths paths,
        CapturingPlannerRuntime runtime) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            new StaticSettingsReader(JournalAiSettings.CreateDefault()),
            new JournalHarnessPlanner(runtime),
            new JournalHarnessAuditStore(paths),
            new FixedJournalClock(FixedDay, FixedNow));

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

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

    private sealed class CapturingPlannerRuntime(JournalHarnessPlannerRuntimeResult plannerResult) : IJournalAiAgentRuntime
    {
        public JournalHarnessPlannerRuntimeResult PlannerResult { get; set; } = plannerResult;

        public bool HarnessPlannerCalled { get; private set; }

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "wrong_path", "Wrong runtime path.", "RunJsonAsync was called."),
                TimeSpan.Zero));

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            HarnessPlannerCalled = true;
            return Task.FromResult(PlannerResult);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-harness-service-tests", Guid.NewGuid().ToString("N"));

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
