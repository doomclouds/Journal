using Journal.Domain.Entries;

namespace Journal.Tests;

public sealed class JmfSectionCatalogTests
{
    [Fact]
    public void AllDefinitions_AreOrderedAndUnique()
    {
        var definitions = JmfSectionCatalog.All;

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
        Assert.Equal(JmfSectionKind.System, JmfSectionCatalog.Require("metadata-note").Kind);
    }

    [Fact]
    public void AvailableOptionalSections_ExcludesExistingSections()
    {
        var available = JmfSectionCatalog.GetAvailableOptionalSections(["mood", "today-focus"]);

        Assert.DoesNotContain(available, item => item.Id == "mood");
        Assert.Contains(available, item => item.Id == "inspiration");
        Assert.DoesNotContain(available, item => item.Kind != JmfSectionKind.OptionalSingleton);
    }
}
