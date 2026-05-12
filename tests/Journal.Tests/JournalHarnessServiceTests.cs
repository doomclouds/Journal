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
    public async Task ExecuteRunAsync_WhenNoOpHasNoBaseline_DoesNotWriteDraftAndCompletesNoChange()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("当前输入不需要改写草稿。")],
                "no-op",
                TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("今天只是保留原始记录", "text", CancellationToken.None);

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);
        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(started.Run.Date, started.Run.Id, CancellationToken.None);
        var rawInputs = await new RawInputStore(paths).ReadAsync(started.Run.Date, CancellationToken.None);

        Assert.Equal("no-change", result.Run.Status);
        Assert.Equal("no-change", persisted!.Status);
        Assert.Equal(JournalStatus.Empty, result.Today.Status);
        Assert.Null(result.Today.Draft);
        Assert.Single(rawInputs);
        Assert.Equal(started.Run.CurrentRawInputId, rawInputs[0].Id);
        Assert.Equal("今天只是保留原始记录", rawInputs[0].Text);
        Assert.False(File.Exists(paths.DraftPath(result.Today.Date)));
        Assert.False(File.Exists(paths.EntryPath(result.Today.Date)));
    }

    [Fact]
    public async Task ExecuteRunAsync_WhenNoOpHasExistingDraft_DoesNotOverwriteDraft()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var date = JournalDate.From(FixedDay);
        await SeedExistingDraftAsync(paths, date);
        var before = await new DraftStore(paths).ReadAsync(date, CancellationToken.None);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("已有 draft 不需要改动。")],
                "no-op",
                TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("当前输入只做审计记录", "text", CancellationToken.None);

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);
        var after = await new DraftStore(paths).ReadAsync(date, CancellationToken.None);

        Assert.Equal("no-change", result.Run.Status);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before.Markdown, after.Markdown);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.SourceRawInputIds, after.SourceRawInputIds);
        Assert.Equal(before.Errors, after.Errors);
        Assert.Equal(before.Markdown, result.Today.Draft!.Markdown);
        Assert.False(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task ExecuteRunAsStreamAsync_WhenTwoStreamsStartQueuedRun_InvokesPlannerOnce()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("并发连接只允许首个执行。")],
                "no-op",
                TimeSpan.FromMilliseconds(17)))
        {
            PlannerEntered = CreateSignal(),
            ReleasePlanner = CreateSignal()
        };
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("并发 SSE 连接不应重复执行", "text", CancellationToken.None);

        var firstStreamTask = CollectRunEventsAsync(service.ExecuteRunAsStreamAsync(started.Run.Id, CancellationToken.None));
        await runtime.PlannerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondEvents = await CollectRunEventsAsync(service.ExecuteRunAsStreamAsync(started.Run.Id, CancellationToken.None));
        runtime.ReleasePlanner.SetResult(null);
        var firstEvents = await firstStreamTask.WaitAsync(TimeSpan.FromSeconds(5));
        var persisted = await new JournalHarnessAuditStore(paths).ReadAsync(started.Run.Date, started.Run.Id, CancellationToken.None);

        Assert.Equal(1, runtime.HarnessPlannerCallCount);
        Assert.Contains(firstEvents, runEvent => runEvent.Type == "run-completed");
        var secondEvent = Assert.Single(secondEvents);
        Assert.Equal("run-status", secondEvent.Type);
        Assert.Equal("running", secondEvent.Status);
        Assert.NotEqual("running", persisted!.Status);
    }

    [Fact]
    public async Task ExecuteRunAsStreamAsync_WhenClientCancelsAfterStart_RunContinuesToCompletion()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("断开连接不取消执行。")],
                "no-op",
                TimeSpan.FromMilliseconds(17)))
        {
            PlannerEntered = CreateSignal(),
            ReleasePlanner = CreateSignal()
        };
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("SSE 断开后 run 仍要完成", "text", CancellationToken.None);
        using var streamCancellation = new CancellationTokenSource();
        var enumerator = service
            .ExecuteRunAsStreamAsync(started.Run.Id, streamCancellation.Token)
            .GetAsyncEnumerator(streamCancellation.Token);

        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await runtime.PlannerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await streamCancellation.CancelAsync();
        try
        {
            await enumerator.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
        }

        runtime.ReleasePlanner.SetResult(null);
        var completed = await WaitForRunStatusAsync(
            paths,
            started.Run.Date,
            started.Run.Id,
            status => status != "running",
            TimeSpan.FromSeconds(5));

        Assert.Equal(1, runtime.HarnessPlannerCallCount);
        Assert.True(
            string.Equals("no-change", completed.Status, StringComparison.Ordinal),
            $"Expected no-change but was {completed.Status}: {string.Join(" | ", completed.Errors)}");
        Assert.NotNull(completed.CompletedAt);
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
    public async Task ExecuteRunAsync_AllowsReviseAiGeneratedSectionFromLegacyGeneratedDraftWithoutProvenance()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var date = JournalDate.From(FixedDay);
        await SeedLegacyGeneratedDraftWithoutProvenanceAsync(paths, date);
        var operation = JournalHarnessOperation.ReviseAiGeneratedSection(
            "today-focus",
            "- 也许翻翻《第一性原理》这本书，看缘分吧～",
            ["raw-current"],
            "将 AI 生成的原描述改得更柔和俏皮。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "revise accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync(
            "帮我把可能看《第一性原理》这本书（但不确定）改得更俏皮柔和",
            "text",
            CancellationToken.None);
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success(
            [operation with { BasedOnRawInputIds = [started.Run.CurrentRawInputId] }],
            "revise accepted",
            TimeSpan.FromMilliseconds(17));

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, result.Today.Status);
        Assert.Equal("reviewing", result.Run.Status);
        Assert.Single(result.Run.ToolCalls);
        Assert.Equal("applied", result.Run.ToolCalls[0].Status);
        var todayFocus = GetSection(result.Today.Draft!.Markdown, "today-focus");
        Assert.Equal("- 也许翻翻《第一性原理》这本书，看缘分吧～", todayFocus.Content);
        Assert.Equal("ai", todayFocus.Provenance.Origin);
        Assert.Equal("ai", todayFocus.Provenance.CreatedBy);
        Assert.Equal("ai", todayFocus.Provenance.LastTouchedBy);
        Assert.Equal("revise", todayFocus.Provenance.LastOperation);
        Assert.Equal([started.Run.CurrentRawInputId], todayFocus.Provenance.BasedOnRawInputIds);
        Assert.False(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task ExecuteRunAsync_FiltersPlannerRawInputIdsToCurrentServerRawInput()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- AI 追加当前输入整理",
            ["raw-current"],
            "整理当前输入。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success([operation], "append accepted", TimeSpan.FromMilliseconds(17)));
        var service = CreateService(paths, runtime);
        var started = await service.StartTodayRunAsync("当前 raw input 才允许进入 provenance", "text", CancellationToken.None);
        runtime.PlannerResult = JournalHarnessPlannerRuntimeResult.Success(
            [
                operation with
                {
                    BasedOnRawInputIds =
                    [
                        started.Run.CurrentRawInputId,
                        "raw-does-not-exist",
                        "raw-x\" --><script>alert(1)</script><!--"
                    ]
                }
            ],
            "append accepted",
            TimeSpan.FromMilliseconds(17));

        var result = await service.ExecuteRunAsync(started.Run.Id, CancellationToken.None);

        var todayFocus = GetSection(result.Today.Draft!.Markdown, "today-focus");
        Assert.Equal([started.Run.CurrentRawInputId], todayFocus.Provenance.BasedOnRawInputIds);
        Assert.Contains($"based_on_raw_inputs=\"{started.Run.CurrentRawInputId}\"", result.Today.Draft.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-does-not-exist", result.Today.Draft.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", result.Today.Draft.Markdown, StringComparison.Ordinal);
        Assert.False(File.Exists(paths.EntryPath(result.Today.Date)));
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

    private static async Task SeedLegacyGeneratedDraftWithoutProvenanceAsync(LocalJournalPaths paths, JournalDate date)
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
                LegacyGeneratedDraftWithoutProvenanceMarkdown(),
                [oldRawInput.Id],
                Array.Empty<string>(),
                FixedNow.AddMinutes(-20)),
            CancellationToken.None);
    }

    private static JmfSection GetSection(string markdown, string sectionId) =>
        JmfMarkdownParser.Parse(markdown).Document.Sections.Single(section =>
            string.Equals(section.Id, sectionId, StringComparison.Ordinal));

    private static TaskCompletionSource<object?> CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<IReadOnlyList<JournalHarnessRunEvent>> CollectRunEventsAsync(
        IAsyncEnumerable<JournalHarnessRunEvent> events)
    {
        var result = new List<JournalHarnessRunEvent>();
        await foreach (var runEvent in events)
        {
            result.Add(runEvent);
        }

        return result;
    }

    private static async Task<JournalHarnessAuditRun> WaitForRunStatusAsync(
        LocalJournalPaths paths,
        JournalDate date,
        string runId,
        Func<string, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        var store = new JournalHarnessAuditStore(paths);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await store.ReadAsync(date, runId, CancellationToken.None);
            if (run is not null && predicate(run.Status))
            {
                return run;
            }

            await Task.Delay(20);
        }

        var latest = await store.ReadAsync(date, runId, CancellationToken.None);
        throw new TimeoutException($"Run {runId} stayed {latest?.Status ?? "missing"}.");
    }

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

    private static string LegacyGeneratedDraftWithoutProvenanceMarkdown() =>
        """
        ---
        schema: journal-entry/v1
        date: "2026-05-12"
        month_day: "05-12"
        status: draft
        version: 1
        provider: deepseek
        model: deepseek-chat
        prompt_version: journal-entry-json-v1.1
        generated_at: "2026-05-12T08:30:00.0000000+08:00"
        ---

        <!-- journal:section raw-inputs -->
        ## 原始输入

        - 旧 raw input 已在 draft 里

        <!-- /journal:section raw-inputs -->

        <!-- journal:section yesterday-review -->
        ## 昨日回顾

        - 昨天继续推进 Journal

        <!-- /journal:section yesterday-review -->

        <!-- journal:section today-focus -->
        ## 今日重点

        - 可能看《第一性原理》这本书（但不确定）

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

        public TaskCompletionSource<object?>? PlannerEntered { get; set; }

        public TaskCompletionSource<object?>? ReleasePlanner { get; set; }

        public bool HarnessPlannerCalled { get; private set; }

        public int HarnessPlannerCallCount { get; private set; }

        public bool ThrowOnHarnessPlanner { get; set; }

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "wrong_path", "Wrong runtime path.", "RunJsonAsync was called."),
                TimeSpan.Zero));

        public async Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            HarnessPlannerCalled = true;
            HarnessPlannerCallCount++;
            PlannerEntered?.TrySetResult(null);
            if (ReleasePlanner is not null)
            {
                await ReleasePlanner.Task.WaitAsync(cancellationToken);
            }

            if (ThrowOnHarnessPlanner)
            {
                throw new InvalidOperationException("Planner exploded with secret test detail.");
            }

            return PlannerResult;
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
