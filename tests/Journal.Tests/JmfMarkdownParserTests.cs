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
    public void Parse_ReturnsIssueForUnmatchedMarkers(string markdown)
    {
        var result = JmfMarkdownParser.Parse(markdown);

        Assert.Contains(result.Issues, issue => issue.Code == "unmatched-section-marker");
    }
}
