using Journal.Domain.Entries;

namespace Journal.Tests;

public sealed class JmfSectionCatalogTests
{
    [Fact]
    public void AllDefinitions_AreOrderedAndUnique()
    {
        var definitions = JmfSectionCatalog.All;

        Assert.Equal(
            [
                "raw-inputs",
                "mood",
                "yesterday-review",
                "today-focus",
                "work",
                "learning",
                "relationship",
                "health",
                "money",
                "inspiration",
                "future-notes",
                "gratitude",
                "keywords",
                "metadata-note"
            ],
            definitions.Select(item => item.Id));
        Assert.Equal(definitions.Count, definitions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(definitions.OrderBy(item => item.Order).Select(item => item.Id), definitions.Select(item => item.Id));
    }

    [Fact]
    public void RequiredAndReadonlyRules_MatchPhase3Spec()
    {
        Assert.Equal(JmfSectionKind.Required, JmfSectionCatalog.Require("raw-inputs").Kind);
        Assert.False(JmfSectionCatalog.Require("raw-inputs").IsEditableInBlockMode);

        Assert.True(JmfSectionCatalog.Require("yesterday-review").IsEditableInBlockMode);
        Assert.True(JmfSectionCatalog.Require("today-focus").IsEditableInBlockMode);
        Assert.Equal(JmfSectionKind.OptionalSingleton, JmfSectionCatalog.Require("inspiration").Kind);
        Assert.Equal(JmfSectionKind.System, JmfSectionCatalog.Require("keywords").Kind);
        Assert.False(JmfSectionCatalog.Require("keywords").IsEditableInBlockMode);
        Assert.Equal(JmfSectionKind.System, JmfSectionCatalog.Require("metadata-note").Kind);
        Assert.False(JmfSectionCatalog.Require("metadata-note").IsEditableInBlockMode);

        Assert.Equal(
            ["raw-inputs", "keywords", "metadata-note"],
            JmfSectionCatalog.All.Where(item => !item.IsEditableInBlockMode).Select(item => item.Id));
    }

    [Fact]
    public void AvailableOptionalSections_ExcludesExistingSections()
    {
        var available = JmfSectionCatalog.GetAvailableOptionalSections(["mood", "today-focus"]);

        Assert.DoesNotContain(available, item => item.Id == "mood");
        Assert.Contains(available, item => item.Id == "inspiration");
        Assert.DoesNotContain(available, item => item.Id is "learning" or "future-notes" or "gratitude");
        Assert.DoesNotContain(available, item => item.Kind != JmfSectionKind.OptionalSingleton);
    }

    [Fact]
    public void ActiveForNewContent_ExcludesLegacyMergedSections()
    {
        Assert.Equal(
            [
                "raw-inputs",
                "mood",
                "yesterday-review",
                "today-focus",
                "work",
                "relationship",
                "health",
                "money",
                "inspiration",
                "keywords",
                "metadata-note"
            ],
            JmfSectionCatalog.ActiveForNewContent.Select(item => item.Id));

        Assert.Equal(
            ["learning", "future-notes", "gratitude"],
            JmfSectionCatalog.LegacyOptionalSingleton.Select(item => item.Id));
    }

    [Fact]
    public void ActiveOptionalSections_UseConsolidatedTitlesAndExcludeLegacySections()
    {
        Assert.Equal(
            ["mood", "work", "relationship", "health", "money", "inspiration"],
            JmfSectionCatalog.ActiveOptionalSingleton.Select(item => item.Id));

        Assert.Equal("状态与情绪", JmfSectionCatalog.Require("mood").Title);
        Assert.Equal("工作与学习", JmfSectionCatalog.Require("work").Title);
        Assert.Equal("生活与关系", JmfSectionCatalog.Require("relationship").Title);
        Assert.Equal("灵感与未来提醒", JmfSectionCatalog.Require("inspiration").Title);
    }

    [Fact]
    public void OptionalSingleton_IncludesLegacySectionsForMarkdownCompatibility()
    {
        Assert.Equal(
            [
                "mood",
                "work",
                "learning",
                "relationship",
                "health",
                "money",
                "inspiration",
                "future-notes",
                "gratitude"
            ],
            JmfSectionCatalog.OptionalSingleton.Select(item => item.Id));
    }

    [Fact]
    public void PublishedCollections_DoNotExposeMutableArrays()
    {
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.All);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.Required);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.OptionalSingleton);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.ActiveForNewContent);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.ActiveOptionalSingleton);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.LegacyOptionalSingleton);
    }
}
