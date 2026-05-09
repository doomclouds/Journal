using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static class JmfMarkdownComposer
{
    public static string Compose(JmfDocument document)
    {
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine(NormalizeLineEndings(document.FrontMatterText));
        builder.AppendLine("---");
        builder.AppendLine();

        foreach (var section in SortSections(document.Sections))
        {
            if (ShouldSkip(section))
            {
                continue;
            }

            AppendSection(builder, section);
        }

        return NormalizeLineEndings(builder.ToString());
    }

    private static IEnumerable<JmfSection> SortSections(IReadOnlyList<JmfSection> sections) =>
        sections
            .Select((section, index) => new
            {
                Section = section,
                Index = index,
                Definition = JmfSectionCatalog.TryGet(section.Id, out var definition) ? definition : null
            })
            .OrderBy(item => item.Definition?.Order ?? int.MaxValue)
            .ThenBy(item => item.Definition is null ? item.Index : 0)
            .Select(item => item.Section);

    private static bool ShouldSkip(JmfSection section) =>
        JmfSectionCatalog.TryGet(section.Id, out var definition)
        && definition.Kind == JmfSectionKind.OptionalSingleton
        && string.IsNullOrWhiteSpace(section.Content);

    private static void AppendSection(StringBuilder builder, JmfSection section)
    {
        var title = JmfSectionCatalog.TryGet(section.Id, out var definition)
            ? definition.Title
            : section.Title;
        var content = EscapeMarkers(NormalizeLineEndings(section.Content));

        builder.AppendLine($"<!-- journal:section {section.Id} -->");
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        builder.Append(content);

        if (content.Length > 0 && !content.EndsWith('\n'))
        {
            builder.AppendLine();
        }

        builder.AppendLine($"<!-- /journal:section {section.Id} -->");
        builder.AppendLine();
    }

    private static string EscapeMarkers(string content) =>
        content
            .Replace("<!--", "&lt;!--", StringComparison.Ordinal)
            .Replace("-->", "--&gt;", StringComparison.Ordinal);

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
}
