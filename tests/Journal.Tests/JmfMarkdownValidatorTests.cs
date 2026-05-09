using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class JmfMarkdownValidatorTests
{
    [Fact]
    public void Validate_ReturnsMissingFrontMatterWhenFrontMatterIsAbsent()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(frontMatterText: "", frontMatter: new Dictionary<string, string>()));

        AssertIssue(result, "missing-front-matter");
    }

    [Fact]
    public void Validate_ReturnsMissingSchemaWhenSchemaIsAbsent()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(frontMatter: new Dictionary<string, string> { ["date"] = "2026-05-09" }));

        AssertIssue(result, "missing-schema");
    }

    [Fact]
    public void Validate_ReturnsInvalidSchemaWhenSchemaIsWrong()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(frontMatter: new Dictionary<string, string> { ["schema"] = "legacy" }));

        AssertIssue(result, "invalid-schema");
    }

    [Fact]
    public void Validate_ReturnsMissingRequiredSectionWhenTodayFocusIsAbsent()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(sections: [Section("raw-inputs"), Section("yesterday-review")]));

        AssertIssue(result, "missing-required-section");
    }

    [Fact]
    public void Validate_ReturnsUnknownSectionForUnknownSectionId()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(sections: [Section("raw-inputs"), Section("yesterday-review"), Section("today-focus"), Section("custom")]));

        AssertIssue(result, "unknown-section");
    }

    [Fact]
    public void Validate_ReturnsDuplicateSectionForDuplicateInspiration()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument(sections: [Section("raw-inputs"), Section("yesterday-review"), Section("today-focus"), Section("inspiration"), Section("inspiration")]));

        AssertIssue(result, "duplicate-section");
    }

    [Fact]
    public void Validate_IncludesParseIssuesFirst()
    {
        var parseIssue = new JmfValidationIssue("unmatched-section-marker", "Marker mismatch.", "Fix section markers.");

        var result = JmfMarkdownValidator.Validate(
            CreateDocument(frontMatterText: "", frontMatter: new Dictionary<string, string>()),
            [parseIssue]);

        Assert.Equal("unmatched-section-marker", result.Issues[0].Code);
        Assert.Contains(result.Issues, issue => issue.Code == "missing-front-matter");
    }

    [Fact]
    public void Validate_ReturnsValidWhenDocumentIsValid()
    {
        var result = JmfMarkdownValidator.Validate(CreateDocument());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateBlockEditRequest_ReturnsRawInputsIsReadonlyForRawInputs()
    {
        var result = JmfMarkdownValidator.ValidateBlockEditRequest(new JournalBlockEditRequest([new("raw-inputs", "- should not edit")]));

        AssertIssue(result, "raw-inputs-is-readonly");
    }

    [Theory]
    [InlineData("keywords")]
    [InlineData("metadata-note")]
    public void ValidateBlockEditRequest_ReturnsReadonlySectionForSystemReadonlySections(string sectionId)
    {
        var result = JmfMarkdownValidator.ValidateBlockEditRequest(new JournalBlockEditRequest([new(sectionId, "- should not edit")]));

        AssertIssue(result, "readonly-section");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "raw-inputs-is-readonly");
    }

    [Fact]
    public void ValidateBlockEditRequest_ReturnsUnknownSectionForUnknownSectionId()
    {
        var result = JmfMarkdownValidator.ValidateBlockEditRequest(new JournalBlockEditRequest([new("custom", "- custom")]));

        AssertIssue(result, "unknown-section");
    }

    [Fact]
    public void ValidateBlockEditRequest_ReturnsDuplicateSectionForDuplicateSectionId()
    {
        var result = JmfMarkdownValidator.ValidateBlockEditRequest(new JournalBlockEditRequest([new("today-focus", "- one"), new("today-focus", "- two")]));

        AssertIssue(result, "duplicate-section");
    }

    private static JmfDocument CreateDocument(
        string frontMatterText = "schema: journal-entry/v1",
        IReadOnlyDictionary<string, string>? frontMatter = null,
        IReadOnlyList<JmfSection>? sections = null) =>
        new(
            frontMatterText,
            frontMatter ?? new Dictionary<string, string> { ["schema"] = "journal-entry/v1" },
            sections ?? [Section("raw-inputs"), Section("yesterday-review"), Section("today-focus")]);

    private static JmfSection Section(string id)
    {
        if (!JmfSectionCatalog.TryGet(id, out var definition))
        {
            return new JmfSection(id, id, "- content", JmfSectionKind.System, false);
        }

        return new JmfSection(definition.Id, definition.Title, "- content", definition.Kind, definition.IsEditableInBlockMode);
    }

    private static void AssertIssue(JmfValidationResult result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }
}
