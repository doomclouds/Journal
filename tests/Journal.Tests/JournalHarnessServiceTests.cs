using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Jmf;
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
        Assert.StartsWith("run-2026-05-12-", result.Run.Id, StringComparison.Ordinal);
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

    [Fact]
    public async Task ExecuteRunAsync_WhenRunIsNotQueued_DoesNotInvokePlannerOrRewriteAudit()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- 首次执行写入草稿",
            ["raw-current"],
            "首次执行。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("首次执行后不允许重复执行", "text", CancellationToken.None);
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success(
            [operation with { BasedOnRawInputIds = [started.Run.CurrentRawInputId] }],
            "append accepted",
            TimeSpan.FromMilliseconds(17));

        var first = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);
        var second = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);
        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(started.Run.Date, started.Run.Id, CancellationToken.None);

        Assert.Equal(1, runtime.HarnessPlannerCallCount);
        Assert.Equal(first.Run.Id, second.Run.Id);
        Assert.Equal(first.Run.Status, second.Run.Status);
        Assert.Equal(first.Run.StartedAt, second.Run.StartedAt);
        Assert.Equal(first.Run.CompletedAt, second.Run.CompletedAt);
        Assert.Equal(first.Run.Summary, second.Run.Summary);
        Assert.Equal(first.Run.Id, persisted!.Id);
        Assert.Equal(first.Run.Status, persisted.Status);
        Assert.Equal(first.Run.StartedAt, persisted.StartedAt);
        Assert.Equal(first.Run.CompletedAt, persisted.CompletedAt);
        Assert.Equal(first.Run.Summary, persisted.Summary);
        Assert.Single(second.Run.ToolCalls);
        Assert.Single(persisted.ToolCalls);
        Assert.Equal("reviewing", second.Run.Status);
    }

    [Fact]
    public async Task ExecuteRunAsync_WithExistingDraft_RebuildsRawInputsFromServerInputs()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var date = JournalDate.From(FixedDay);
        await SeedExistingDraftAsync(paths, date);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- AI 追加整理，不重复原始输入文本",
            ["raw-current"],
            "整理当前输入。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("当前 raw input 必须进入 raw-inputs 区块", "text", CancellationToken.None);
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success(
            [operation with { BasedOnRawInputIds = [started.Run.CurrentRawInputId] }],
            "append accepted",
            TimeSpan.FromMilliseconds(17));

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, result.Today.Status);
        var rawInputs = GetSection(result.Today.Draft!.Markdown, "raw-inputs");
        Assert.Contains("旧 raw input 已在 draft 里", rawInputs.Content, StringComparison.Ordinal);
        Assert.Contains("当前 raw input 必须进入 raw-inputs 区块", rawInputs.Content, StringComparison.Ordinal);
        var todayFocus = GetSection(result.Today.Draft.Markdown, "today-focus");
        Assert.Contains("用户保留的今日重点", todayFocus.Content, StringComparison.Ordinal);
        Assert.Contains("AI 追加整理，不重复原始输入文本", todayFocus.Content, StringComparison.Ordinal);
        Assert.False(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task ExecuteRunAsync_WhenPlannerFails_WritesAttentionDraftWithServerRawInputs()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var date = JournalDate.From(FixedDay);
        await SeedExistingDraftAsync(paths, date);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create("planner", "provider_error", "Planner failed.", "safe failure"),
                TimeSpan.FromMilliseconds(11),
                safeResponseSnippet: "safe failure"));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("失败路径也必须保留当前 raw input", "text", CancellationToken.None);

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.Equal(JournalStatus.Attention, result.Today.Status);
        var rawInputs = GetSection(result.Today.Draft!.Markdown, "raw-inputs");
        Assert.Contains("旧 raw input 已在 draft 里", rawInputs.Content, StringComparison.Ordinal);
        Assert.Contains("失败路径也必须保留当前 raw input", rawInputs.Content, StringComparison.Ordinal);
        Assert.False(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task ExecuteRunAsync_UsesDateEmbeddedInRunIdWhenClockMovesToNextDay()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var clock = new MutableJournalClock(FixedDay, FixedNow);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- 跨日恢复执行仍写回原日期 draft",
            ["raw-current"],
            "跨日恢复执行。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime, clock);
        var started = await service.StartTodayRunAsync("跨日执行的当前 raw input", "text", CancellationToken.None);
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success(
            [operation with { BasedOnRawInputIds = [started.Run.CurrentRawInputId] }],
            "append accepted",
            TimeSpan.FromMilliseconds(17));
        clock.Today = FixedDay.AddDays(1);
        clock.Now = FixedNow.AddDays(1);

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.Equal(JournalDate.From(FixedDay), result.Today.Date);
        Assert.Equal("reviewing", result.Run.Status);
        Assert.Contains("跨日执行的当前 raw input", result.Today.Draft!.Markdown, StringComparison.Ordinal);
        Assert.True(File.Exists(paths.DraftPath(JournalDate.From(FixedDay))));
        Assert.False(File.Exists(paths.DraftPath(JournalDate.From(FixedDay.AddDays(1)))));
    }

    [Fact]
    public async Task ExecuteRunAsync_WhenPlannerThrows_FinalizesRunAsFailedAndWritesAttentionDraft()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([], "unused", TimeSpan.Zero))
        {
            ThrowOnHarnessPlanner = true
        };
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("planner 抛异常也要保留 raw input", "text", CancellationToken.None);

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.Equal("failed", result.Run.Status);
        Assert.NotNull(result.Run.CompletedAt);
        Assert.NotEmpty(result.Run.Errors);
        Assert.DoesNotContain(result.Run.Errors, error => error.Contains("secret", StringComparison.OrdinalIgnoreCase));
        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(result.Run.Date, result.Run.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("failed", persisted.Status);
        Assert.Equal(JournalStatus.Attention, result.Today.Status);
        var rawInputs = GetSection(result.Today.Draft!.Markdown, "raw-inputs");
        Assert.Contains("planner 抛异常也要保留 raw input", rawInputs.Content, StringComparison.Ordinal);
        Assert.False(File.Exists(paths.EntryPath(result.Run.Date)));
    }

    [Fact]
    public async Task AuditStore_RejectsInvalidRunIdWithoutPathTraversal()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var store = new JournalHarnessAuditStore(paths);
        var date = JournalDate.From(FixedDay);

        Assert.Throws<ArgumentException>(() => paths.HarnessAuditRunPath(date, "..\\outside"));
        Assert.Throws<ArgumentException>(() => paths.HarnessAuditRunPath(date, "../outside"));
        Assert.Throws<ArgumentException>(() => paths.HarnessAuditRunPath(date, "run:bad"));
        Assert.Null(await store.ReadAsync(date, "..\\outside", CancellationToken.None));
    }

    [Fact]
    public async Task AuditStore_ReadByDateAsync_SortsByCreatedAtThenRunId()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var store = new JournalHarnessAuditStore(paths);
        var date = JournalDate.From(FixedDay);
        var createdAt = FixedNow;
        var later = CreateRun(date, "run-2026-05-12-cccc", createdAt.AddMinutes(1));
        var second = CreateRun(date, "run-2026-05-12-bbbb", createdAt);
        var first = CreateRun(date, "run-2026-05-12-aaaa", createdAt);

        await store.WriteAsync(second, CancellationToken.None);
        await store.WriteAsync(later, CancellationToken.None);
        await store.WriteAsync(first, CancellationToken.None);

        var runs = await store.ReadByDateAsync(date, CancellationToken.None);

        Assert.Equal(["run-2026-05-12-cccc", "run-2026-05-12-aaaa", "run-2026-05-12-bbbb"], runs.Select(run => run.Id).ToArray());
    }

    private static JournalHarnessService CreateService(
        LocalJournalPaths paths,
        CapturingPlannerRuntime runtime,
        IJournalClock? clock = null) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            new StaticSettingsReader(JournalAiSettings.CreateDefault()),
            new JournalHarnessPlanner(runtime),
            new JournalHarnessAuditStore(paths),
            clock ?? new FixedJournalClock(FixedDay, FixedNow));

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private static async Task SeedExistingDraftAsync(LocalJournalPaths paths, JournalDate date)
    {
        var oldRawInput = new RawInput(
            "raw-existing",
            date,
            FixedNow.AddMinutes(-30),
            "text",
            "旧 raw input 已在 draft 里");
        await new RawInputStore(paths).AppendAsync(oldRawInput, CancellationToken.None);
        await new DraftStore(paths).WriteAsync(
            new JournalDraft(
                date,
                JournalStatus.Reviewing,
                ExistingDraftMarkdown(),
                [oldRawInput.Id],
                Array.Empty<string>(),
                FixedNow.AddMinutes(-20)),
            CancellationToken.None);
    }

    private static JmfSection GetSection(string markdown, string sectionId) =>
        JmfMarkdownParser.Parse(markdown).Document.Sections.Single(section =>
            string.Equals(section.Id, sectionId, StringComparison.Ordinal));

    private static string ExistingDraftMarkdown() =>
        """
        ---
        schema: journal-entry/v1
        date: "2026-05-12"
        month_day: "05-12"
        status: draft
        version: 1
        ---

        <!-- journal:section raw-inputs -->
        ## 原始输入

        - 旧 raw input 已在 draft 里

        <!-- /journal:section raw-inputs -->

        <!-- journal:section yesterday-review -->
        ## 昨日回顾

        - 旧的昨日回顾

        <!-- /journal:section yesterday-review -->

        <!-- journal:section today-focus origin="user" created_by="user" last_touched_by="user" last_operation="edit" based_on_raw_inputs="raw-existing" -->
        ## 今日重点

        - 用户保留的今日重点

        <!-- /journal:section today-focus -->
        """;

    private static JournalHarnessAuditRun CreateRun(
        JournalDate date,
        string runId,
        DateTimeOffset createdAt) =>
        new(
            runId,
            date,
            createdAt,
            null,
            null,
            "queued",
            "mock",
            JournalHarnessPrompt.Version,
            "raw-test",
            Array.Empty<JournalHarnessAuditToolCall>(),
            Array.Empty<string>(),
            "queued");

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class MutableJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today { get; set; } = today;

        public DateTimeOffset Now { get; set; } = now;
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

        public int HarnessPlannerCallCount { get; private set; }

        public bool ThrowOnHarnessPlanner { get; set; }

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
            HarnessPlannerCallCount++;
            if (ThrowOnHarnessPlanner)
            {
                throw new InvalidOperationException("Planner exploded with secret test detail.");
            }

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
