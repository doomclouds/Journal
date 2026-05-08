namespace Journal.Domain.Entries;

public readonly record struct JournalDate(DateOnly Value)
{
    public static JournalDate From(DateOnly value) => new(value);

    public string Year => Value.ToString("yyyy");

    public string Month => Value.ToString("MM");

    public string IsoDate => Value.ToString("yyyy-MM-dd");

    public string MonthDay => Value.ToString("MM-dd");

    public string MarkdownFileName => $"{IsoDate}.md";

    public override string ToString() => IsoDate;
}
