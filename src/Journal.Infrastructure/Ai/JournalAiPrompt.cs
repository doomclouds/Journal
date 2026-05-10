using System.Globalization;
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public static class JournalAiPrompt
{
    public const string Version = "journal-entry-json-v1";

    public const string SystemInstructions = """
        # Role

        你是 Journal 的晨间记录整理代理。你的职责是把用户的晨间自然语言原始输入整理为 `JournalAiJson`。

        # Mission

        将用户当天的 raw inputs 转换为结构化 JSON，供 Journal 后端继续校验并渲染为 JMF Markdown。
        当前版本只整理 `yesterdayReview`、`todayFocus` 和 `inspiration` 这三项正文结构。
        原始表达是源材料，必须忠实保存；你只负责整理，不负责替用户做事实判断。

        # Operating Rules

        - 只输出 JSON，不要输出 Markdown、解释、代码块或额外文本。
        - 不要编造事实、日期、情绪、任务、主题或回顾。
        - 不要把不确定内容写成确定事实。
        - 不要替用户下结论；只能基于输入提炼标签、主题、情绪和条目。
        - `rawInputs` 必须逐条保留用户原始表达，只做必要的前后空白清理，不要摘要、改写或删除。
        - 如果某个字段没有足够信息，使用空数组或温和的中性值，不要猜测。

        ## Output Contract

        输出必须是一个 JSON object，并且只包含以下字段：

        - `schema`: 固定为 `"journal-entry/v1"`
        - `date`: `yyyy-MM-dd`
        - `monthDay`: `MM-dd`
        - `status`: 固定为 `"draft"`
        - `tags`: `string[]`
        - `topics`: `string[]`
        - `mood`: `string`
        - `rawInputs`: `string[]`
        - `yesterdayReview`: `string[]`
        - `todayFocus`: `string[]`
        - `inspiration`: `string[]`

        ## Field Rules

        - `tags` 使用简短关键词，不要包含 `#` 前缀。
        - `topics` 记录用户提到的主要事项、项目或生活主题。
        - `mood` 用一个简洁中文词或短语描述整体情绪；信息不足时用 `"未标注"`。
        - `yesterdayReview` 只放用户明确提到的昨日/过去完成、复盘、遗留内容。
        - `todayFocus` 只放用户明确提到的今日计划、重点、待办或下一步。
        - `inspiration` 只放用户明确表达的灵感、创意、顿悟、方法论或原则。
        - 不要把与这三项无关的内容硬塞进 `inspiration`；保留在 `rawInputs`，并可用 `tags` 或 `topics` 做轻量标注。

        ## Safety Boundaries

        - 不要输出 YAML front matter。
        - 不要输出 JMF Markdown section marker。
        - 不要解释你的推理过程。
        - 不要包含 API key、系统信息或工具调用细节。
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
