using System.Text.RegularExpressions;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed class MockAiProvider : IJournalAiProvider
{
    private static readonly Regex TagRegex = new(@"(?<![\p{L}\p{N}_])#([\p{L}\p{N}_-]+)", RegexOptions.Compiled);

    private static readonly string[] YesterdayKeywords = ["昨天", "昨晚", "上次", "完成了"];
    private static readonly string[] TodayKeywords = ["今天", "接下来", "准备", "要做", "计划"];
    private static readonly string[] InspirationKeywords = ["想到", "灵感", "应该", "可以", "原则"];
    private static readonly string[] MoodKeywords = ["有推进感", "平静", "开心", "焦虑", "累"];

    public JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt)
    {
        var inputTexts = rawInputs
            .Select(input => input.Text.Trim())
            .Where(text => text.Length > 0)
            .ToArray();

        if (inputTexts.Length == 0)
        {
            inputTexts = ["（无原始输入）"];
        }

        var tags = ExtractTags(inputTexts);

        return new JournalAiJson(
            "journal.v1",
            date.IsoDate,
            date.MonthDay,
            "draft",
            tags,
            tags.Count > 0 ? tags : ["日记"],
            ExtractMood(inputTexts),
            inputTexts,
            ExtractSection(inputTexts, YesterdayKeywords, "记录了今天之前的上下文。"),
            ExtractSection(inputTexts, TodayKeywords, "整理今天的重点。"),
            ExtractSection(inputTexts, InspirationKeywords, "暂无灵感补充。"));
    }

    private static IReadOnlyList<string> ExtractTags(IEnumerable<string> inputTexts)
    {
        return inputTexts
            .SelectMany(text => TagRegex.Matches(text).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ExtractMood(IEnumerable<string> inputTexts)
    {
        return MoodKeywords.FirstOrDefault(mood => inputTexts.Any(text => text.Contains(mood, StringComparison.Ordinal)))
            ?? "未标注";
    }

    private static IReadOnlyList<string> ExtractSection(IEnumerable<string> inputTexts, IReadOnlyList<string> keywords, string fallback)
    {
        var matches = inputTexts
            .Where(text => keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal)))
            .ToArray();

        return matches.Length > 0 ? matches : [fallback];
    }
}
