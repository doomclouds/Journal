using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPlannerTests
{
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
