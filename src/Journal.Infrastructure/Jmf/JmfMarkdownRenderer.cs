using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static class JmfMarkdownRenderer
{
    public static string Render(
        JournalAiJson aiJson,
        string provider,
        string model,
        string promptVersion,
        DateTimeOffset generatedAt)
    {
        var builder = new StringBuilder();

        builder.AppendLine("---");
        AppendScalar(builder, "schema", aiJson.Schema);
        AppendScalar(builder, "date", aiJson.Date);
        AppendScalar(builder, "month_day", aiJson.MonthDay);
        AppendScalar(builder, "status", aiJson.Status);
        AppendList(builder, "tags", aiJson.Tags);
        AppendList(builder, "topics", aiJson.Topics);
        AppendScalar(builder, "mood", aiJson.Mood);
        builder.AppendLine("version: 1");
        AppendScalar(builder, "provider", provider);
        AppendScalar(builder, "model", model);
        AppendScalar(builder, "prompt_version", promptVersion);
        AppendScalar(builder, "generated_at", generatedAt.ToString("O"));
        builder.AppendLine("---");
        builder.AppendLine();

        AppendSection(builder, "raw-inputs", "Raw Inputs", aiJson.RawInputs);
        AppendSection(builder, "yesterday-review", "Yesterday Review", aiJson.YesterdayReview);
        AppendSection(builder, "today-focus", "Today Focus", aiJson.TodayFocus);

        if (!string.IsNullOrWhiteSpace(aiJson.Mood) && !string.Equals(aiJson.Mood, "未标注", StringComparison.Ordinal))
        {
            AppendSection(builder, "mood", "Mood", [aiJson.Mood]);
        }

        if (aiJson.Inspiration.Count > 0 && aiJson.Inspiration.Any(item => !string.IsNullOrWhiteSpace(item)))
        {
            AppendSection(builder, "inspiration", "Inspiration", aiJson.Inspiration);
        }

        return builder.ToString();
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

    private static void AppendSection(StringBuilder builder, string marker, string title, IReadOnlyList<string> items)
    {
        builder.AppendLine($"<!-- journal:section {marker} -->");
        builder.AppendLine($"## {title}");
        builder.AppendLine();

        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            builder.Append("- ").AppendLine(item.Trim());
        }

        builder.AppendLine($"<!-- /journal:section {marker} -->");
        builder.AppendLine();
    }

    private static string EscapeYaml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(':', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
