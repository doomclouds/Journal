using Journal.Domain.Entries;

namespace Journal.Tests;

public sealed class JournalDateTests
{
    [Fact]
    public void JournalDate_FormatsStorageParts()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));

        Assert.Equal("2026", date.Year);
        Assert.Equal("05", date.Month);
        Assert.Equal("2026-05-08", date.IsoDate);
        Assert.Equal("05-08", date.MonthDay);
        Assert.Equal("2026-05-08.md", date.MarkdownFileName);
    }

    [Fact]
    public void Parse_ReturnsJournalDateFromIsoDate()
    {
        var date = JournalDate.Parse("2026-05-13");

        Assert.Equal(new DateOnly(2026, 5, 13), date.Value);
        Assert.Equal("2026-05-13", date.IsoDate);
    }

    [Theory]
    [InlineData("2026-05-13", true)]
    [InlineData("2026/05/13", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParse_AcceptsOnlyIsoDate(string? value, bool expected)
    {
        var parsed = JournalDate.TryParse(value, out var date);

        Assert.Equal(expected, parsed);
        if (expected)
        {
            Assert.Equal("2026-05-13", date.IsoDate);
        }
    }

    [Fact]
    public void EntryModels_AcceptStringRawInputIds()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var rawInput = new RawInput("raw-1", date, DateTimeOffset.UtcNow, "natural-language", "今天完成阶段 2 模型。");
        var draft = new JournalDraft(
            date,
            JournalStatus.Draft,
            "# 2026-05-08",
            ["raw-1"],
            [],
            DateTimeOffset.UtcNow);

        Assert.Equal("raw-1", rawInput.Id);
        Assert.Contains("raw-1", draft.SourceRawInputIds);
    }

    [Fact]
    public void JournalAiJson_AcceptsMultipleSectionItems()
    {
        var aiJson = new JournalAiJson(
            "journal.v1",
            "2026-05-08",
            "05-08",
            "draft",
            ["work"],
            ["journal"],
            "focused",
            ["今天完成阶段 2 模型。"],
            ["复盘 1", "复盘 2"],
            ["重点 1", "重点 2"],
            ["灵感 1", "灵感 2"]);

        Assert.Equal(["复盘 1", "复盘 2"], aiJson.YesterdayReview);
        Assert.Equal(["重点 1", "重点 2"], aiJson.TodayFocus);
        Assert.Equal(["灵感 1", "灵感 2"], aiJson.Inspiration);
    }
}
