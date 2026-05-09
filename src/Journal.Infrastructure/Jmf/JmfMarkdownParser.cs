using System.Text.RegularExpressions;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static partial class JmfMarkdownParser
{
    public static JmfParseResult Parse(string markdown)
    {
        var normalized = NormalizeLineEndings(markdown);
        var (frontMatterText, frontMatter, body) = ParseFrontMatter(normalized);
        var issues = new List<JmfValidationIssue>();
        var sections = ParseSections(body, issues);

        return new JmfParseResult(
            new JmfDocument(frontMatterText, frontMatter, sections),
            issues);
    }

    private static (string Text, IReadOnlyDictionary<string, string> Values, string Body) ParseFrontMatter(string markdown)
    {
        var lines = markdown.Split('\n');
        if (lines.Length == 0 || !string.Equals(lines[0], "---", StringComparison.Ordinal))
        {
            return ("", new Dictionary<string, string>(), markdown);
        }

        var closingLineIndex = Array.FindIndex(lines, 1, line => string.Equals(line, "---", StringComparison.Ordinal));
        if (closingLineIndex < 0)
        {
            return ("", new Dictionary<string, string>(), markdown);
        }

        var frontMatterLines = lines.Skip(1).Take(closingLineIndex - 1).ToArray();
        var frontMatterText = string.Join('\n', frontMatterLines);
        var body = string.Join('\n', lines.Skip(closingLineIndex + 1));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in frontMatterLines)
        {
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            values[key] = UnescapeScalar(value);
        }

        return (frontMatterText, values, body);
    }

    private static IReadOnlyList<JmfSection> ParseSections(string body, List<JmfValidationIssue> issues)
    {
        var sections = new List<JmfSection>();
        var searchIndex = 0;

        while (searchIndex < body.Length)
        {
            var start = SectionStartRegex().Match(body, searchIndex);
            var standaloneEnd = SectionEndRegex().Match(body, searchIndex);

            if (standaloneEnd.Success && (!start.Success || standaloneEnd.Index < start.Index))
            {
                var standaloneEndSectionId = standaloneEnd.Groups["id"].Value;
                issues.Add(CreateIssue(
                    "unmatched-section-marker",
                    $"Section '{standaloneEndSectionId}' has an end marker without a matching start marker.",
                    "Remove the extra JMF section end marker or add the matching start marker."));
                searchIndex = standaloneEnd.Index + standaloneEnd.Length;
                continue;
            }

            if (!start.Success)
            {
                break;
            }

            var sectionId = start.Groups["id"].Value;
            var end = SectionEndRegex().Match(body, start.Index + start.Length);
            if (!end.Success)
            {
                issues.Add(CreateIssue(
                    "unmatched-section-marker",
                    $"Section '{sectionId}' does not have a matching end marker.",
                    "Add the matching JMF section end marker."));
                break;
            }

            var endSectionId = end.Groups["id"].Value;
            var nestedStart = SectionStartRegex().Match(body, start.Index + start.Length);
            if (nestedStart.Success && nestedStart.Index < end.Index)
            {
                var nestedSectionId = nestedStart.Groups["id"].Value;
                issues.Add(CreateIssue(
                    "unmatched-section-marker",
                    $"Section '{sectionId}' contains nested start marker '{nestedSectionId}'.",
                    "Close the current JMF section before starting another section."));
                searchIndex = nestedStart.Index;
                continue;
            }

            if (!string.Equals(sectionId, endSectionId, StringComparison.Ordinal))
            {
                issues.Add(CreateIssue(
                    "unmatched-section-marker",
                    $"Section '{sectionId}' ends with marker '{endSectionId}'.",
                    "Make the JMF section start and end marker ids match."));
                searchIndex = end.Index + end.Length;
                continue;
            }

            var content = body.Substring(start.Index + start.Length, end.Index - start.Index - start.Length);
            sections.Add(CreateSection(sectionId, RemoveLeadingHeading(content)));
            searchIndex = end.Index + end.Length;
        }

        return sections;
    }

    private static JmfSection CreateSection(string id, string content)
    {
        if (!JmfSectionCatalog.TryGet(id, out var definition))
        {
            return new JmfSection(id, id, content, JmfSectionKind.System, false);
        }

        return new JmfSection(definition.Id, definition.Title, content, definition.Kind, definition.IsEditableInBlockMode);
    }

    private static string RemoveLeadingHeading(string content)
    {
        var lines = content.Split('\n').ToList();
        var firstNonEmptyIndex = lines.FindIndex(line => !string.IsNullOrWhiteSpace(line));

        if (firstNonEmptyIndex >= 0 && LeadingHeadingRegex().IsMatch(lines[firstNonEmptyIndex]))
        {
            lines.RemoveAt(firstNonEmptyIndex);
        }

        return string.Join('\n', lines).Trim();
    }

    private static string UnescapeScalar(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        return value[1..^1]
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    private static JmfValidationIssue CreateIssue(string code, string message, string repairHint) =>
        new(code, message, repairHint);

    [GeneratedRegex(@"<!--\s*journal:section\s+(?<id>[^>\s]+)\s*-->")]
    private static partial Regex SectionStartRegex();

    [GeneratedRegex(@"<!--\s*/journal:section\s+(?<id>[^>\s]+)\s*-->")]
    private static partial Regex SectionEndRegex();

    [GeneratedRegex(@"^\s*##\s+.+$")]
    private static partial Regex LeadingHeadingRegex();
}
