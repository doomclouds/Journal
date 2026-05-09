using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class JmfMarkdownParserTests
{
    [Fact]
    public void Parse_ReturnsDocumentForValidJmfMarkdown()
    {
        const string markdown = """
            ---
            schema: journal-entry/v1
            date: "2026-05-09"
            mood: "steady \"flow\""
            note: "line\nnext"
            path: "C:\\Journal"
            ---

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - 今天要做 JMF parser。
            <!-- /journal:section raw-inputs -->

            <!-- journal:section yesterday-review -->
            ## 昨日回顾

            - 昨天完成 Task 1。
            <!-- /journal:section yesterday-review -->

            <!-- journal:section today-focus -->
            ## 今日重点

            - 解析 Markdown。
            - 校验 JMF。
            <!-- /journal:section today-focus -->

            <!-- journal:section inspiration -->
            ## 灵感

            - 编辑器要保护结构。
            <!-- /journal:section inspiration -->
            """;

        var result = JmfMarkdownParser.Parse(markdown);

        Assert.Empty(result.Issues);
        Assert.Equal("journal-entry/v1", result.Document.FrontMatter["schema"]);
        Assert.Equal("steady \"flow\"", result.Document.FrontMatter["mood"]);
        Assert.Equal("line\nnext", result.Document.FrontMatter["note"]);
        Assert.Equal(@"C:\Journal", result.Document.FrontMatter["path"]);

        Assert.Equal(["raw-inputs", "yesterday-review", "today-focus", "inspiration"], result.Document.Sections.Select(section => section.Id));

        var rawInputs = result.Document.Sections.Single(section => section.Id == "raw-inputs");
        Assert.Equal("原始输入", rawInputs.Title);
        Assert.Equal(JmfSectionKind.Required, rawInputs.Kind);
        Assert.False(rawInputs.IsEditableInBlockMode);
        Assert.Equal("- 今天要做 JMF parser。", rawInputs.Content);
        Assert.DoesNotContain("journal:section", rawInputs.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("## 原始输入", rawInputs.Content, StringComparison.Ordinal);

        var todayFocus = result.Document.Sections.Single(section => section.Id == "today-focus");
        Assert.Equal("今日重点", todayFocus.Title);
        Assert.Equal(JmfSectionKind.Required, todayFocus.Kind);
        Assert.True(todayFocus.IsEditableInBlockMode);
        Assert.Equal("- 解析 Markdown。\n- 校验 JMF。", todayFocus.Content);

        var inspiration = result.Document.Sections.Single(section => section.Id == "inspiration");
        Assert.Equal("灵感", inspiration.Title);
        Assert.Equal(JmfSectionKind.OptionalSingleton, inspiration.Kind);
        Assert.True(inspiration.IsEditableInBlockMode);
        Assert.Equal("- 编辑器要保护结构。", inspiration.Content);
    }

    [Theory]
    [InlineData("<!-- journal:section today-focus -->\n## 今日重点\n- item")]
    [InlineData("<!-- journal:section today-focus -->\n## 今日重点\n- item\n<!-- /journal:section raw-inputs -->")]
    [InlineData("<!-- /journal:section today-focus -->")]
    [InlineData("<!-- journal:section today-focus -->\n## 今日重点\n- item\n<!-- /journal:section today-focus -->\n<!-- /journal:section today-focus -->")]
    public void Parse_ReturnsIssueForUnmatchedMarkers(string markdown)
    {
        var result = JmfMarkdownParser.Parse(markdown);

        Assert.Contains(result.Issues, issue => issue.Code == "unmatched-section-marker");
    }

    [Fact]
    public void Parse_ReturnsIssueForNestedSectionStartMarker()
    {
        const string markdown = """
            ---
            schema: journal-entry/v1
            date: "2026-05-09"
            ---

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - 今天做编辑器。
            <!-- /journal:section raw-inputs -->

            <!-- journal:section yesterday-review -->
            ## 昨日回顾

            - 昨天完成 parser。
            <!-- /journal:section yesterday-review -->

            <!-- journal:section today-focus -->
            ## 今日重点

            - 保持 JMF marker 配对。
            <!-- journal:section money -->
            - 这个 marker 不应该被吞成普通内容。
            <!-- /journal:section today-focus -->
            """;

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);

        Assert.Contains(parseResult.Issues, issue => issue.Code == "unmatched-section-marker");
        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Issues, issue => issue.Code == "unmatched-section-marker");
    }

    [Theory]
    [InlineData("Custom")]
    [InlineData("custom_section")]
    public void Parse_CapturesInvalidSectionIdsForValidation(string invalidSectionId)
    {
        var markdown = $$"""
            ---
            schema: journal-entry/v1
            date: "2026-05-09"
            ---

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - raw
            <!-- /journal:section raw-inputs -->

            <!-- journal:section yesterday-review -->
            ## 昨日回顾

            - review
            <!-- /journal:section yesterday-review -->

            <!-- journal:section today-focus -->
            ## 今日重点

            - focus
            <!-- /journal:section today-focus -->

            <!-- journal:section {{invalidSectionId}} -->
            ## Invalid

            - should be rejected
            <!-- /journal:section {{invalidSectionId}} -->
            """;

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);

        Assert.Contains(parseResult.Document.Sections, section => section.Id == invalidSectionId);
        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Issues, issue => issue.Code == "unknown-section");
    }
}
