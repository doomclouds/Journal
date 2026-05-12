using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPlannerTests
{
    [Fact]
    public void Collector_RecordsOperationsWithoutExecutingThemAndNormalizesNullArgs()
    {
        var collector = new JournalHarnessToolCollector();

        var appendAccepted = collector.AppendJournalSection(
            null!,
            null!,
            null!,
            null!);
        var upsertAccepted = collector.UpsertJournalSection(
            "inspiration",
            "- 新灵感",
            ["raw-1"],
            "新增内容。");
        var reviseAccepted = collector.ReviseAiGeneratedSection(
            "today-focus",
            "- 修订内容",
            ["raw-2"],
            "修订 AI 内容。");
        var noOpAccepted = collector.NoOp("无需操作。");

        Assert.Contains("accepted", appendAccepted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepted", upsertAccepted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepted", reviseAccepted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepted", noOpAccepted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, collector.Operations.Count);

        AssertOperation(collector.Operations[0], "append", string.Empty, string.Empty, [], string.Empty);
        AssertOperation(collector.Operations[1], "upsert", "inspiration", "- 新灵感", ["raw-1"], "新增内容。");
        AssertOperation(
            collector.Operations[2],
            "revise-ai-generated-section",
            "today-focus",
            "- 修订内容",
            ["raw-2"],
            "修订 AI 内容。");
        AssertOperation(collector.Operations[3], "no-op", string.Empty, string.Empty, [], "无需操作。");
    }

    [Fact]
    public async Task PlanAsync_ReturnsRuntimeOperationsWithoutUsingJsonRuntime()
    {
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- 继续推进 harness planner",
            ["raw-current"],
            "用户当前输入要求继续推进。");
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Success(
                [operation],
                "accepted",
                TimeSpan.FromMilliseconds(23)));
        var planner = new JournalHarnessPlanner(runtime);
        var settings = new JournalAiProviderSettings(
            "openai",
            "openai-compatible",
            "OpenAI",
            "custom",
            "https://api.openai.com/v1",
            "gpt-5.4",
            "secret-key",
            true,
            30,
            0.2,
            1200,
            "balanced");
        var prompt = new JournalHarnessPromptRequest(
            "system instructions",
            """{ "protected": true }""",
            """{ "id": "raw-current", "text": "继续推进 harness planner" }""");

        var result = await planner.PlanAsync(settings, prompt, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([operation], result.Operations);
        Assert.Equal("accepted", result.SafeResponseSnippet);
        Assert.False(runtime.RunJsonAsyncCalled);

        var request = Assert.IsType<JournalHarnessPlannerRuntimeRequest>(runtime.CapturedPlannerRequest);
        Assert.Equal("openai", request.ProviderId);
        Assert.Equal("https://api.openai.com/v1", request.BaseUrl);
        Assert.Equal("gpt-5.4", request.Model);
        Assert.Equal("secret-key", request.ApiKey);
        Assert.Equal("system instructions", request.SystemInstructions);
        Assert.Equal("""{ "protected": true }""", request.ProtectedContext);
        Assert.Equal("""{ "id": "raw-current", "text": "继续推进 harness planner" }""", request.UserMessage);
        Assert.Equal(30, request.TimeoutSeconds);
        Assert.Equal(0.2, request.Temperature);
        Assert.Equal(1200, request.MaxTokens);
    }

    [Fact]
    public async Task PlanAsync_MapsNoToolCallsRuntimeFailureToUnsuccessfulPlan()
    {
        var runtime = new CapturingPlannerRuntime(
            JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create("runtime", "no_tool_calls", "LLM did not call a harness tool.", "empty operation list"),
                TimeSpan.FromMilliseconds(15),
                safeResponseSnippet: "no tool calls"));
        var planner = new JournalHarnessPlanner(runtime);

        var result = await planner.PlanAsync(
            CreateSettings(),
            new JournalHarnessPromptRequest("system", "protected", "user"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Operations);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("no_tool_calls", error.Code);
        Assert.Equal("no tool calls", result.SafeResponseSnippet);
    }

    private static JournalAiProviderSettings CreateSettings() =>
        new(
            "openai",
            "openai-compatible",
            "OpenAI",
            "custom",
            "https://api.openai.com/v1",
            "gpt-5.4",
            "secret-key",
            true,
            30,
            0.2,
            1200,
            "balanced");

    private static void AssertOperation(
        JournalHarnessOperation operation,
        string kind,
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason)
    {
        Assert.Equal(kind, operation.Kind);
        Assert.Equal(targetSectionId, operation.TargetSectionId);
        Assert.Equal(content, operation.Content);
        Assert.Equal(basedOnRawInputIds, operation.BasedOnRawInputIds);
        Assert.Equal(reason, operation.Reason);
    }

    private sealed class CapturingPlannerRuntime(JournalHarnessPlannerRuntimeResult plannerResult) : IJournalAiAgentRuntime
    {
        public bool RunJsonAsyncCalled { get; private set; }

        public JournalHarnessPlannerRuntimeRequest? CapturedPlannerRequest { get; private set; }

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken)
        {
            RunJsonAsyncCalled = true;
            return Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "wrong_path", "Wrong runtime path.", "RunJsonAsync was called."),
                TimeSpan.Zero));
        }

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            CapturedPlannerRequest = request;
            return Task.FromResult(plannerResult);
        }
    }
}
