using System.Globalization;
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public static class JournalAiPrompt
{
    public const string Version = "journal-entry-json-v1.1";

    public const string SystemInstructions = """
        # Role

        你是 Journal 的晨间记录整理代理。你的职责是把用户的晨间自然语言原始输入整理为 `JournalAiJson`。

        # Mission

        将用户当天的 raw inputs 转换为结构化 JSON，供 Journal 后端继续校验并渲染为 JMF Markdown。
        当前版本只整理 `yesterdayReview`、`todayFocus` 和 `inspiration` 这三项正文结构。
        这三项不是只整理任务或待办，也用于晨间日记里的回顾、今日事件、观察和值得记录的片段。
        原始表达是源材料，必须忠实保存；你只负责整理，不负责替用户做事实判断。

        # Operating Rules

        - 只输出 JSON，不要输出 Markdown、解释、代码块或额外文本。
        - 不要编造事实、日期、情绪、任务、主题或回顾。
        - 不要把不确定内容写成确定事实。
        - 不要替用户下结论；只能基于输入提炼标签、主题、情绪和条目。
        - `rawInputs` 必须逐条保留用户原始表达，只做必要的前后空白清理，不要摘要、改写或删除。
        - 结构化条目可以做轻度整理和合并，但每一条都必须能从 raw inputs 找到依据。
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
        - `yesterdayReview` 放用户明确提到的昨天、过去、最近已完成、正在复盘、遗留或值得回顾的事情；可以包含完成事项、进展、家庭生活或情绪回看。
        - `todayFocus` 放用户明确提到的今天相关重点，包括今日计划、待办、正在推进的事情、已经发生的重要事件、家庭/生活片段、节日、值得记录、纪念或庆祝的事情。
        - `inspiration` 放用户明确表达的灵感、创意、顿悟、方法论、原则、观察、感受、感恩或值得保留的片段，尤其是无法自然归入昨日回顾或今日重点的内容。
        - 不要把所有内容都硬塞进 `inspiration`；优先按时间和语义放入 `yesterdayReview` 或 `todayFocus`，无法归类但值得保留时再使用 `inspiration`。

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
