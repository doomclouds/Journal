using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class MockAiAndJmfTests
{
    [Fact]
    public void MockAiProvider_GeneratesDeterministicJsonFromAllRawInputs()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var generatedAt = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");
        var inputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-08T08:00:00+08:00"), "text", "昨天完成了存储骨架 #工程"),
            new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-08T08:10:00+08:00"), "text", "今天准备做 JMF renderer #Journal"),
            new RawInput("raw-3", date, DateTimeOffset.Parse("2026-05-08T08:20:00+08:00"), "text", "想到一个原则：Markdown 必须稳定可读，有推进感")
        };

        var aiJson = new MockAiProvider().Generate(date, inputs, generatedAt);

        Assert.Equal("journal.v1", aiJson.Schema);
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
            "journal.v1",
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
        Assert.Contains(result.Errors, error => error.Contains("rawInputs", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("yesterdayReview", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("todayFocus", StringComparison.Ordinal));
    }

    [Fact]
    public void JmfMarkdownRenderer_RendersJmfV1MarkdownWithFrontMatterAndSectionMarkers()
    {
        var generatedAt = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");
        var aiJson = new JournalAiJson(
            "journal.v1",
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

        var markdown = JmfMarkdownRenderer.Render(
            aiJson,
            provider: "mock",
            model: "mock-deterministic",
            promptVersion: "jmf-mock-v1",
            generatedAt);

        Assert.Contains("---", markdown);
        Assert.Contains("schema: journal.v1", markdown);
        Assert.Contains("date: 2026-05-08", markdown);
        Assert.Contains("month_day: 05-08", markdown);
        Assert.Contains("status: draft", markdown);
        Assert.Contains("tags:", markdown);
        Assert.Contains("  - 工程", markdown);
        Assert.Contains("topics:", markdown);
        Assert.Contains("  - \"JMF: renderer\"", markdown);
        Assert.Contains("mood: 有推进感", markdown);
        Assert.Contains("version: 1", markdown);
        Assert.Contains("provider: mock", markdown);
        Assert.Contains("model: mock-deterministic", markdown);
        Assert.Contains("prompt_version: jmf-mock-v1", markdown);
        Assert.Contains("generated_at: \"2026-05-08T09:30:00.0000000+08:00\"", markdown);
        Assert.Contains("<!-- journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- /journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- journal:section yesterday-review -->", markdown);
        Assert.Contains("<!-- /journal:section yesterday-review -->", markdown);
        Assert.Contains("<!-- journal:section today-focus -->", markdown);
        Assert.Contains("<!-- /journal:section today-focus -->", markdown);
        Assert.Contains("<!-- journal:section mood -->", markdown);
        Assert.Contains("<!-- journal:section inspiration -->", markdown);
        Assert.Contains("- 昨天完成了存储骨架 #工程", markdown);
        Assert.Contains("- 完成了 Task 1/2 依赖。", markdown);
        Assert.Contains("- 实现 JMF renderer。", markdown);
        Assert.Contains("- 可以让 Markdown 稳定可读。", markdown);
    }
}
