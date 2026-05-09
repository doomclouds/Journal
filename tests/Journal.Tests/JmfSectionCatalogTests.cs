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
                "health",
                "relationship",
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
        Assert.DoesNotContain(available, item => item.Kind != JmfSectionKind.OptionalSingleton);
    }

    [Fact]
    public void PublishedCollections_DoNotExposeMutableArrays()
    {
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.All);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.Required);
        Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.OptionalSingleton);
    }
}
