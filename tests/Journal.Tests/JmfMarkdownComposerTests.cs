using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class JmfMarkdownComposerTests
{
    [Fact]
    public void Compose_KeepsOriginalFrontMatterText()
    {
        const string frontMatterText = "schema: journal-entry/v1\r\ndate: \"2026-05-09\"\r\ncustom: keep-me";
        var document = CreateDocument(
            frontMatterText,
            Section("raw-inputs", "- 原始输入"),
            Section("yesterday-review", "- 昨日回顾"),
            Section("today-focus", "- 今日重点"));

        var markdown = JmfMarkdownComposer.Compose(document);

        Assert.DoesNotContain('\r', markdown);
        Assert.StartsWith("---\nschema: journal-entry/v1\ndate: \"2026-05-09\"\ncustom: keep-me\n---\n\n", markdown, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(markdown, "schema: journal-entry/v1\ndate: \"2026-05-09\"\ncustom: keep-me"));

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public void Compose_EmitsSectionsInCatalogOrder()
    {
        var document = CreateDocument(
            Section("today-focus", "- 今日重点"),
            Section("inspiration", "- 灵感"),
            Section("raw-inputs", "- 原始输入"),
            Section("mood", "平静"),
            Section("yesterday-review", "- 昨日回顾"));

        var markdown = JmfMarkdownComposer.Compose(document);

        Assert.True(markdown.IndexOf("<!-- journal:section raw-inputs -->", StringComparison.Ordinal) <
            markdown.IndexOf("<!-- journal:section mood -->", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("<!-- journal:section mood -->", StringComparison.Ordinal) <
            markdown.IndexOf("<!-- journal:section yesterday-review -->", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("<!-- journal:section yesterday-review -->", StringComparison.Ordinal) <
            markdown.IndexOf("<!-- journal:section today-focus -->", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("<!-- journal:section today-focus -->", StringComparison.Ordinal) <
            markdown.IndexOf("<!-- journal:section inspiration -->", StringComparison.Ordinal));

        var parseResult = JmfMarkdownParser.Parse(markdown);
        Assert.Equal(["raw-inputs", "mood", "yesterday-review", "today-focus", "inspiration"], parseResult.Document.Sections.Select(section => section.Id));
    }

    [Fact]
    public void Compose_SkipsEmptyOptionalSections()
    {
        var document = CreateDocument(
            Section("raw-inputs", "- 原始输入"),
            Section("mood", "   "),
            Section("yesterday-review", "- 昨日回顾"),
            Section("today-focus", "- 今日重点"),
            Section("inspiration", ""));

        var markdown = JmfMarkdownComposer.Compose(document);

        Assert.DoesNotContain("<!-- journal:section mood -->", markdown);
        Assert.DoesNotContain("<!-- journal:section inspiration -->", markdown);

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public void Compose_EmitsRequiredSectionsWhenContentIsEmpty()
    {
        var document = CreateDocument(
            Section("raw-inputs", ""),
            Section("yesterday-review", "   "),
            Section("today-focus", ""));

        var markdown = JmfMarkdownComposer.Compose(document);

        Assert.Contains("<!-- journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- journal:section yesterday-review -->", markdown);
        Assert.Contains("<!-- journal:section today-focus -->", markdown);

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public void Compose_EscapesMarkerLikeTextInsideContent()
    {
        const string content = """
            - 保留用户文本
            <!-- journal:section money -->
            - 不允许注入嵌套 section
            <!-- /journal:section money -->
            """;
        var document = CreateDocument(
            Section("raw-inputs", "- 原始输入"),
            Section("yesterday-review", "- 昨日回顾"),
            Section("today-focus", content));

        var markdown = JmfMarkdownComposer.Compose(document);
        var composedContent = markdown[
            markdown.IndexOf("<!-- journal:section today-focus -->", StringComparison.Ordinal)..
            markdown.IndexOf("<!-- /journal:section today-focus -->", StringComparison.Ordinal)];

        Assert.DoesNotContain("<!-- journal:section money -->", composedContent);
        Assert.Contains("&lt;!-- journal:section money --&gt;", composedContent);
        Assert.DoesNotContain("<!-- /journal:section money -->", composedContent);
        Assert.Contains("&lt;!-- /journal:section money --&gt;", composedContent);

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        Assert.True(validationResult.IsValid);
    }

    private static JmfDocument CreateDocument(params JmfSection[] sections) =>
        CreateDocument("schema: journal-entry/v1\ndate: \"2026-05-09\"", sections);

    private static JmfDocument CreateDocument(string frontMatterText, params JmfSection[] sections) =>
        new(
            frontMatterText,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema"] = "journal-entry/v1",
                ["date"] = "2026-05-09"
            },
            sections);

    private static JmfSection Section(string id, string content)
    {
        var definition = JmfSectionCatalog.Require(id);
        return new JmfSection(definition.Id, definition.Title, content, definition.Kind, definition.IsEditableInBlockMode);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
