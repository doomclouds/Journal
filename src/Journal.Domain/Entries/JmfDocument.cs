namespace Journal.Domain.Entries;

public sealed record JmfDocument(
    string FrontMatterText,
    IReadOnlyDictionary<string, string> FrontMatter,
    IReadOnlyList<JmfSection> Sections);
