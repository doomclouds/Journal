using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static class JmfMarkdownRenderer
{
    private const string DocumentSchema = "journal-entry/v1";
    private const string YamlFlowIndicatorCharacters = "[]{},";
    private const string YamlLeadingIndicatorCharacters = "-?:,[]{}#&*!|>'\"%@`";
    private static readonly JmfSectionProvenance AiCreateProvenance =
        new("ai", "ai", "ai", "create", Array.Empty<string>());

    public static string Render(
        JournalAiJson aiJson,
        DateTimeOffset generatedAt,
        JournalAiMetadata? metadata = null)
    {
        metadata ??= JournalAiMetadata.Mock;

        var builder = new StringBuilder();

        builder.AppendLine("---");
        AppendScalar(builder, "schema", DocumentSchema);
        AppendScalar(builder, "date", aiJson.Date);
        AppendScalar(builder, "month_day", aiJson.MonthDay);
        AppendScalar(builder, "status", aiJson.Status);
        AppendList(builder, "tags", aiJson.Tags);
        AppendList(builder, "topics", aiJson.Topics);
        AppendScalar(builder, "mood", aiJson.Mood);
        builder.AppendLine("version: 1");
        AppendScalar(builder, "provider", metadata.Provider);
        AppendScalar(builder, "model", metadata.Model);
        AppendScalar(builder, "prompt_version", metadata.PromptVersion);
        AppendScalar(builder, "generated_at", generatedAt.ToString("O"));
        builder.AppendLine("---");
        builder.AppendLine();

        AppendSection(builder, "raw-inputs", aiJson.RawInputs, JmfSectionProvenance.Unknown);

        if (!string.IsNullOrWhiteSpace(aiJson.Mood) && !string.Equals(aiJson.Mood, "未标注", StringComparison.Ordinal))
        {
            AppendSection(builder, "mood", [aiJson.Mood], AiCreateProvenance);
        }

        AppendSection(builder, "yesterday-review", aiJson.YesterdayReview, AiCreateProvenance);
        AppendSection(builder, "today-focus", aiJson.TodayFocus, AiCreateProvenance);
        AppendOptionalSection(builder, "work", aiJson.Work);
        AppendOptionalSection(builder, "relationship", aiJson.Relationship);
        AppendOptionalSection(builder, "health", aiJson.Health);
        AppendOptionalSection(builder, "money", aiJson.Money);
        AppendOptionalSection(builder, "inspiration", aiJson.Inspiration);

        return builder.ToString();
    }

    private static void AppendOptionalSection(StringBuilder builder, string marker, IReadOnlyList<string> items)
    {
        if (items.Count > 0 && items.Any(item => !string.IsNullOrWhiteSpace(item)))
        {
            AppendSection(builder, marker, items, AiCreateProvenance);
        }
    }

    private static void AppendScalar(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append(": ").AppendLine(EscapeYaml(value));

    private static void AppendList(StringBuilder builder, string key, IReadOnlyList<string> values)
    {
        builder.AppendLine($"{key}:");

        if (values.Count == 0)
        {
            builder.AppendLine("  []");
            return;
        }

        foreach (var value in values)
        {
            builder.Append("  - ").AppendLine(EscapeYaml(value));
        }
    }

    private static void AppendSection(
        StringBuilder builder,
        string marker,
        IReadOnlyList<string> items,
        JmfSectionProvenance provenance)
    {
        var title = JmfSectionCatalog.Require(marker).Title;

        builder.Append("<!-- journal:section ").Append(marker);
        AppendProvenanceAttributes(builder, provenance);
        builder.AppendLine(" -->");
        builder.AppendLine($"## {title}");
        builder.AppendLine();

        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            builder.Append("- ").AppendLine(SanitizeBullet(item));
        }

        builder.AppendLine($"<!-- /journal:section {marker} -->");
        builder.AppendLine();
    }

    private static void AppendProvenanceAttributes(StringBuilder builder, JmfSectionProvenance provenance)
    {
        if (provenance == JmfSectionProvenance.Unknown)
        {
            return;
        }

        builder.Append(" origin=\"").Append(EscapeAttributeValue(provenance.Origin)).Append('"');
        builder.Append(" created_by=\"").Append(EscapeAttributeValue(provenance.CreatedBy)).Append('"');
        builder.Append(" last_touched_by=\"").Append(EscapeAttributeValue(provenance.LastTouchedBy)).Append('"');
        builder.Append(" last_operation=\"").Append(EscapeAttributeValue(provenance.LastOperation)).Append('"');
        if (provenance.BasedOnRawInputIds.Count > 0)
        {
            builder.Append(" based_on_raw_inputs=\"")
                .Append(EscapeAttributeValue(string.Join(' ', provenance.BasedOnRawInputIds)))
                .Append('"');
        }
    }

    private static string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return NeedsYamlQuotes(value)
            ? $"\"{EscapeYamlQuotedValue(value)}\""
            : value;
    }

    private static bool NeedsYamlQuotes(string value) =>
        char.IsWhiteSpace(value[0])
        || char.IsWhiteSpace(value[^1])
        || char.IsDigit(value[0])
        || YamlLeadingIndicatorCharacters.Contains(value[0], StringComparison.Ordinal)
        || ContainsYamlFlowIndicator(value)
        || value.Contains(':', StringComparison.Ordinal)
        || value.Contains('#', StringComparison.Ordinal)
        || value.Contains('"', StringComparison.Ordinal)
        || value.Contains('\\', StringComparison.Ordinal)
        || value.Contains('\r', StringComparison.Ordinal)
        || value.Contains('\n', StringComparison.Ordinal)
        || IsYamlSpecialPlainScalar(value);

    private static bool IsYamlSpecialPlainScalar(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "~", StringComparison.Ordinal);

    private static bool ContainsYamlFlowIndicator(string value) =>
        value.Any(character => YamlFlowIndicatorCharacters.Contains(character, StringComparison.Ordinal));

    private static string EscapeYamlQuotedValue(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string SanitizeBullet(string value) =>
        value
            .Trim()
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("<!--", "&lt;!--", StringComparison.Ordinal)
            .Replace("-->", "--&gt;", StringComparison.Ordinal);

    private static string EscapeAttributeValue(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&#60;", StringComparison.Ordinal)
            .Replace(">", "&#62;", StringComparison.Ordinal);
}
