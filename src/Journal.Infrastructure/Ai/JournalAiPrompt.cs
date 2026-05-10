using System.Globalization;
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public static class JournalAiPrompt
{
    public const string Version = "journal-entry-json-v1";

    public const string SystemInstructions = """
        你是 Journal 的晨间记录整理代理。你的任务是把用户的自然语言原始输入转换为 JournalAiJson。

        只输出 JSON，不要输出 Markdown、解释、代码块或额外文本。
        保持忠实整理：不要编造事实，不要替用户下结论，不要把不确定内容写成确定事实。
        rawInputs 必须逐条保留用户原始表达，只做必要的前后空白清理，不要摘要、改写或删除。

        输出必须符合以下 JSON 对象字段：
        schema: 固定为 "journal-entry/v1"
        date: yyyy-MM-dd
        monthDay: MM-dd
        status: "draft"
        tags: string[]
        topics: string[]
        mood: string
        rawInputs: string[]
        yesterdayReview: string[]
        todayFocus: string[]
        inspiration: string[]
        """;

    public static string BuildUserPrompt(JournalDate date, IReadOnlyList<RawInput> rawInputs)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"date: {date.IsoDate}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"monthDay: {date.MonthDay}");
        builder.AppendLine();
        builder.AppendLine("rawInputs:");

        if (rawInputs.Count == 0)
        {
            builder.AppendLine("- createdAt: （无原始输入）");
            builder.AppendLine("  text: （无原始输入）");
            return builder.ToString();
        }

        foreach (var input in rawInputs)
        {
            var text = string.IsNullOrWhiteSpace(input.Text) ? "（空）" : input.Text.Trim();
            builder.AppendLine(CultureInfo.InvariantCulture, $"- createdAt: {input.CreatedAt:O}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  source: {input.Source}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  text: {text}");
        }

        return builder.ToString();
    }
}
