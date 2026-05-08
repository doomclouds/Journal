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
}
