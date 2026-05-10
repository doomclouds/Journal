using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Jmf;
using System.Text.RegularExpressions;

namespace Journal.Tests;

public sealed class MockAiAndJmfTests
{
    [Fact]
    public async Task MockAiProvider_GeneratesDeterministicJsonFromAllRawInputs()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var generatedAt = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");
        var inputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-08T08:00:00+08:00"), "text", "昨天完成了存储骨架 #工程"),
            new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-08T08:10:00+08:00"), "text", "今天准备做 JMF renderer #Journal"),
            new RawInput("raw-3", date, DateTimeOffset.Parse("2026-05-08T08:20:00+08:00"), "text", "想到一个原则：Markdown 必须稳定可读，有推进感")
        };

        var result = await new MockAiProvider().GenerateAsync(
            new JournalAiGenerationRequest(date, inputs, generatedAt, CreateDefaultMockProviderSettings()),
            CancellationToken.None);
        var aiJson = Assert.IsType<JournalAiJson>(result.AiJson);

        Assert.True(result.IsSuccess);
        Assert.Equal(JournalAiMetadata.Mock, result.Metadata);
        Assert.Equal("journal-entry/v1", aiJson.Schema);
        Assert.Equal("2026-05-08", aiJson.Date);
        Assert.Equal("05-08", aiJson.MonthDay);
        Assert.Equal("draft", aiJson.Status);
        Assert.Equal(["工程", "Journal"], aiJson.Tags);
        Assert.Contains("昨天完成了存储骨架 #工程", aiJson.RawInputs);
        Assert.Contains("今天准备做 JMF renderer #Journal", aiJson.RawInputs);
        Assert.Contains("想到一个原则：Markdown 必须稳定可读，有推进感", aiJson.RawInputs);
        Assert.Contains("昨天完成了存储骨架 #工程", aiJson.YesterdayReview);
        Assert.Contains("今天准备做 JMF renderer #Journal", aiJson.TodayFocus);
        Assert.Contains("想到一个原则：Markdown 必须稳定可读，有推进感", aiJson.Inspiration);
        Assert.Equal("有推进感", aiJson.Mood);
    }

    [Fact]
    public void JournalAiJsonValidator_ReturnsInvalidWhenRequiredSectionsAreMissing()
    {
        var aiJson = new JournalAiJson(
            "legacy-schema",
            "2026-05-08",
            "05-08",
            "draft",
            [],
            [],
            "未标注",
            [],
            [],
            [],
            []);

        var result = JournalAiJsonValidator.Validate(aiJson);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("schema must be journal-entry/v1", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("rawInputs", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("yesterdayReview", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("todayFocus", StringComparison.Ordinal));
    }

    [Fact]
    public void JournalAiJsonValidator_AcceptsJournalEntryV1Schema()
    {
        var result = JournalAiJsonValidator.Validate(CreateAiJson());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void JournalAiSafeError_CreateRedactsAuthorizationBearerAndApiKeyValues()
    {
        var error = JournalAiSafeError.Create(
            "generation",
            "provider-error",
            "Provider request failed.",
            "Authorization: Bearer secret-key status=401 api_key=abc");

        Assert.DoesNotContain("secret-key", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted-header]", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted-key-name]", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted-value]", error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JmfMarkdownRenderer_RendersJmfV1MarkdownWithFrontMatterAndSectionMarkers()
    {
        var generatedAt = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            "2026-05-08",
            "05-08",
            "draft",
            ["工程", "Journal"],
            ["JMF: renderer"],
            "有推进感",
            ["昨天完成了存储骨架 #工程"],
            ["完成了 Task 1/2 依赖。"],
            ["实现 JMF renderer。"],
            ["可以让 Markdown 稳定可读。"]);

        var markdown = JmfMarkdownRenderer.Render(aiJson, generatedAt);

        Assert.Contains("---", markdown);
        Assert.Contains("schema: journal-entry/v1", markdown);
        Assert.Contains("date: \"2026-05-08\"", markdown);
        Assert.Contains("month_day: \"05-08\"", markdown);
        Assert.Contains("status: draft", markdown);
        Assert.Contains("tags:", markdown);
        Assert.Contains("  - 工程", markdown);
        Assert.Contains("topics:", markdown);
        Assert.Contains("  - \"JMF: renderer\"", markdown);
        Assert.Contains("mood: 有推进感", markdown);
        Assert.Contains("version: 1", markdown);
        Assert.Contains("provider: mock", markdown);
        Assert.Contains("model: mock-journal", markdown);
        Assert.Contains("prompt_version: mock-journal-entry-v1", markdown);
        Assert.Contains("generated_at: \"2026-05-08T09:30:00.0000000+08:00\"", markdown);
        Assert.Contains("<!-- journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- /journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- journal:section yesterday-review -->", markdown);
        Assert.Contains("<!-- /journal:section yesterday-review -->", markdown);
        Assert.Contains("<!-- journal:section today-focus -->", markdown);
        Assert.Contains("<!-- /journal:section today-focus -->", markdown);
        Assert.Contains("<!-- journal:section mood -->", markdown);
        Assert.Contains("<!-- journal:section inspiration -->", markdown);
        Assert.Contains("## 原始输入", markdown);
        Assert.Contains("## 昨日回顾", markdown);
        Assert.Contains("## 今日重点", markdown);
        Assert.Contains("## 情绪状态", markdown);
        Assert.Contains("## 灵感", markdown);
        Assert.Contains("- 昨天完成了存储骨架 #工程", markdown);
        Assert.Contains("- 完成了 Task 1/2 依赖。", markdown);
        Assert.Contains("- 实现 JMF renderer。", markdown);
        Assert.Contains("- 可以让 Markdown 稳定可读。", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_WritesProviderMetadataFromGenerationResult()
    {
        var markdown = JmfMarkdownRenderer.Render(
            CreateAiJson(),
            DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
            new JournalAiMetadata(
                Provider: "deepseek",
                Model: "deepseek-v4-flash",
                PromptVersion: "journal-entry-json-v1"));

        Assert.Contains("provider: deepseek", markdown);
        Assert.Contains("model: deepseek-v4-flash", markdown);
        Assert.Contains("prompt_version: journal-entry-json-v1", markdown);
        Assert.Contains("generated_at: \"2026-05-10T08:30:00.0000000+08:00\"", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_DoesNotWriteProviderSecrets()
    {
        var markdown = JmfMarkdownRenderer.Render(
            CreateAiJson(),
            DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
            new JournalAiMetadata("custom", "local-model", "journal-entry-json-v1"));

        Assert.DoesNotContain("api_key", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base_url", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("request_id", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_response", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JmfMarkdownRenderer_UsesFixedDocumentSchema()
    {
        var markdown = JmfMarkdownRenderer.Render(
            CreateAiJson(schema: "untrusted-ai-schema"),
            DateTimeOffset.Parse("2026-05-08T09:30:00+08:00"));

        Assert.Contains("schema: journal-entry/v1", markdown);
        Assert.DoesNotContain("schema: untrusted-ai-schema", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_EscapesYamlSpecialCharactersStably()
    {
        var aiJson = CreateAiJson(
            tags: ["tag:with-colon"],
            topics: ["quote \"topic\"", "path C:\\journal\\today"],
            mood: "平静\n继续");

        var markdown = JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse("2026-05-08T09:30:00+08:00"));

        Assert.Contains("  - \"tag:with-colon\"", markdown);
        Assert.Contains("  - \"quote \\\"topic\\\"\"", markdown);
        Assert.Contains("  - \"path C:\\\\journal\\\\today\"", markdown);
        Assert.Contains("mood: \"平静\\n继续\"", markdown);
        Assert.Contains("provider: mock", markdown);
        Assert.Contains("model: mock-journal", markdown);
        Assert.Contains("prompt_version: mock-journal-entry-v1", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_QuotesYamlPlainScalarEdgeCases()
    {
        var aiJson = CreateAiJson(
            tags:
            [
                "tag # comment",
                "- leading dash",
                "[flow]",
                "{map}",
                "*alias",
                "&anchor",
                "true",
                "null"
            ],
            mood: " trailing ");

        var markdown = JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse("2026-05-08T09:30:00+08:00"));

        Assert.Contains("  - \"tag # comment\"", markdown);
        Assert.Contains("  - \"- leading dash\"", markdown);
        Assert.Contains("  - \"[flow]\"", markdown);
        Assert.Contains("  - \"{map}\"", markdown);
        Assert.Contains("  - \"*alias\"", markdown);
        Assert.Contains("  - \"&anchor\"", markdown);
        Assert.Contains("  - \"true\"", markdown);
        Assert.Contains("  - \"null\"", markdown);
        Assert.Contains("mood: \" trailing \"", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_QuotesNumericAndDateLikeYamlScalars()
    {
        var aiJson = CreateAiJson(
            tags: ["2026", "3.14"],
            topics: ["2026-05-08", "05-08"],
            mood: "001");

        var markdown = JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse("2026-05-08T09:30:00+08:00"));

        Assert.Contains("date: \"2026-05-08\"", markdown);
        Assert.Contains("month_day: \"05-08\"", markdown);
        Assert.Contains("  - \"2026\"", markdown);
        Assert.Contains("  - \"3.14\"", markdown);
        Assert.Contains("  - \"2026-05-08\"", markdown);
        Assert.Contains("  - \"05-08\"", markdown);
        Assert.Contains("mood: \"001\"", markdown);
    }

    [Fact]
    public void JmfMarkdownRenderer_SanitizesSectionBulletsAndKeepsMarkerPairsStable()
    {
        var aiJson = CreateAiJson(
            mood: "未标注",
            rawInputs: ["第一行\r\n<!-- journal:section fake -->\n第二行"],
            yesterdayReview: ["昨天 <!-- /journal:section raw-inputs --> 完成"],
            todayFocus: ["今天继续"],
            inspiration: ["可以保留灵感 closing marker"]);

        var markdown = JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse("2026-05-08T09:30:00+08:00"));

        Assert.Equal(8, CountSectionMarkers(markdown));
        Assert.Contains("<!-- journal:section inspiration -->", markdown);
        Assert.Contains("<!-- /journal:section inspiration -->", markdown);
        Assert.DoesNotContain("<!-- journal:section fake -->", markdown);
        Assert.DoesNotContain("<!-- /journal:section raw-inputs --> 完成", markdown);
        Assert.Contains("- 第一行 &lt;!-- journal:section fake --&gt; 第二行", markdown);
        Assert.Contains("- 昨天 &lt;!-- /journal:section raw-inputs --&gt; 完成", markdown);
    }

    private static JournalAiJson CreateAiJson(
        string schema = "journal-entry/v1",
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? topics = null,
        string mood = "有推进感",
        IReadOnlyList<string>? rawInputs = null,
        IReadOnlyList<string>? yesterdayReview = null,
        IReadOnlyList<string>? todayFocus = null,
        IReadOnlyList<string>? inspiration = null) =>
        new(
            schema,
            "2026-05-08",
            "05-08",
            "draft",
            tags ?? ["工程"],
            topics ?? ["JMF"],
            mood,
            rawInputs ?? ["原始输入"],
            yesterdayReview ?? ["昨天复盘"],
            todayFocus ?? ["今天重点"],
            inspiration ?? ["灵感"]);

    private static int CountSectionMarkers(string markdown) =>
        Regex.Matches(markdown, @"<!--\s*/?journal:section\b").Count;

    private static JournalAiProviderSettings CreateDefaultMockProviderSettings() =>
        JournalAiSettings.CreateDefault().Providers.Single(provider => provider.Id == "mock");
}
