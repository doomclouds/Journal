using System.Globalization;

namespace Journal.Domain.Entries;

public readonly record struct JournalDate(DateOnly Value)
{
    public static JournalDate From(DateOnly value) => new(value);

    public static JournalDate Parse(string value) =>
        new(DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static bool TryParse(string? value, out JournalDate date)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = new JournalDate(parsed);
            return true;
        }

        date = default;
        return false;
    }

    public string Year => Value.ToString("yyyy", CultureInfo.InvariantCulture);

    public string Month => Value.ToString("MM", CultureInfo.InvariantCulture);

    public string IsoDate => Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string MonthDay => Value.ToString("MM-dd", CultureInfo.InvariantCulture);

    public string MarkdownFileName => $"{IsoDate}.md";

    public override string ToString() => IsoDate;
}
