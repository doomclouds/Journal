using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Tests;

public sealed class OpenAiCompatibleJournalAiProviderTests
{
    [Fact]
    public async Task GenerateAsync_SendsFaithfulPromptAndJsonObjectMode()
    {
        var runtime = new CapturingRuntime(OpenAiCompatibleRunResult.Success(CreateAiJson(), "ok", TimeSpan.FromMilliseconds(18)));
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);
        var settings = JournalAiSettings.CreateDefault().Providers.Single(item => item.Id == "deepseek") with
        {
            ApiKey = "secret-key",
            IsEnabled = true
        };
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var rawInputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-10T08:01:00+08:00"), "text", "今天想把 AI provider 接上，先保留原始表达。")
        };

        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(date, rawInputs, DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"), settings),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("deepseek", result.Metadata.Provider);
        Assert.Equal("deepseek-v4-flash", result.Metadata.Model);
        Assert.Equal("journal-entry-json-v1.1", result.Metadata.PromptVersion);

        var request = Assert.IsType<OpenAiCompatibleRunRequest>(runtime.CapturedRequest);
        Assert.Equal("deepseek-v4-flash", request.Model);
        Assert.Equal("json_object", request.ResponseFormat);
        Assert.Contains("# Role", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("## Output Contract", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("只整理 `yesterdayReview`、`todayFocus` 和 `inspiration` 这三项正文结构", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("只输出 JSON", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("不是只整理任务或待办", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("已经发生的重要事件", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("值得记录、纪念或庆祝的事情", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("观察、感受、感恩或值得保留的片段", request.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("今天想把 AI provider 接上，先保留原始表达。", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSafeFailureWhenApiKeyMissing()
    {
        var runtime = new CapturingRuntime(OpenAiCompatibleRunResult.Success(CreateAiJson(), "ok", TimeSpan.Zero));
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);
        var settings = JournalAiSettings.CreateDefault().Providers.Single(item => item.Id == "openai") with
        {
            Model = "gpt-5.4",
            ApiKey = "",
            IsEnabled = true
        };
        var date = JournalDate.From(new DateOnly(2026, 5, 10));

        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(date, [], DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"), settings),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("missing_api_key", error.Code);
        Assert.DoesNotContain("api_key", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runtime.CapturedRequest);
    }

    [Fact]
    public async Task CheckAsync_MapsRuntimeFailureWithoutLeakingSecret()
    {
        var runtime = new CapturingRuntime(OpenAiCompatibleRunResult.Failure(
            JournalAiSafeError.Create("runtime", "unauthorized", "Unauthorized.", "Authorization: Bearer secret-key status=401"),
            TimeSpan.FromMilliseconds(12),
            401));
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);
        var settings = JournalAiSettings.CreateDefault().Providers.Single(item => item.Id == "openai") with
        {
            ApiKey = "secret-key",
            IsEnabled = true
        };

        var result = await provider.CheckAsync(settings, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Status);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.DoesNotContain("secret-key", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_ReturnsFailureWhenRuntimeSuccessSnippetIsNotJson()
    {
        var runtime = new CapturingRuntime(OpenAiCompatibleRunResult.Success(null, "not json", TimeSpan.FromMilliseconds(8)));
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);
        var settings = JournalAiSettings.CreateDefault().Providers.Single(item => item.Id == "openai") with
        {
            ApiKey = "secret-key",
            IsEnabled = true
        };

        var result = await provider.CheckAsync(settings, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_json", result.Status);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("invalid_json", error.Code);
    }

    [Fact]
    public void JournalAiSafeError_RedactsBareSensitiveValues()
    {
        var details = JournalAiSafeError.Redact(
            "Incorrect API key provided: secret-key. Bearer secret-key",
            ["secret-key"]);

        Assert.DoesNotContain("secret-key", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted-value]", details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunJsonAsync_PropagatesExternalCancellation()
    {
        var runtime = new OpenAiCompatibleAgentRuntime();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Record.ExceptionAsync(() =>
            runtime.RunJsonAsync(
                new OpenAiCompatibleRunRequest(
                    "openai",
                    "https://api.openai.com/v1",
                    "gpt-5.4",
                    "secret-key",
                    "只输出 JSON。",
                    "Return JSON.",
                    "json_object",
                    45,
                    0.2,
                    1200),
                cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    private static JournalAiJson CreateAiJson() =>
        new(
            "journal-entry/v1",
            "2026-05-10",
            "05-10",
            "draft",
            ["AI"],
            ["Journal"],
            "平静",
            ["今天想把 AI provider 接上，先保留原始表达。"],
            ["昨天完成了设置页。"],
            ["接入真实 provider。"],
            ["保持 JSON 边界清晰。"]);

    private sealed class CapturingRuntime(OpenAiCompatibleRunResult result) : IJournalAiAgentRuntime
    {
        public OpenAiCompatibleRunRequest? CapturedRequest { get; private set; }

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(result);
        }

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Harness planner runtime should not be called by provider tests.");
    }
}
