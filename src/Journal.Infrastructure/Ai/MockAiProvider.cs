using System.Text.RegularExpressions;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed class MockAiProvider : IJournalAiProvider
{
    private const string Schema = "journal-entry/v1";

    private static readonly Regex TagRegex = new(@"(?<![\p{L}\p{N}_])#([\p{L}\p{N}_-]+)", RegexOptions.Compiled);

    private static readonly string[] YesterdayKeywords = ["昨天", "昨晚", "上次", "完成了"];
    private static readonly string[] TodayKeywords = ["今天", "接下来", "准备", "要做", "计划"];
    private static readonly string[] WorkKeywords = ["工作", "项目", "开发", "接口", "会议", "交付", "排障", "读书", "课程", "学习", "renderer", "JMF"];
    private static readonly string[] RelationshipKeywords = ["家人", "家庭", "朋友", "关系", "感谢", "感恩", "珍惜", "生活"];
    private static readonly string[] HealthKeywords = ["睡眠", "精力", "健康", "运动", "饮食", "作息", "累"];
    private static readonly string[] MoneyKeywords = ["消费", "收入", "预算", "理财", "财务", "钱"];
    private static readonly string[] InspirationKeywords = ["想到", "灵感", "应该", "可以", "原则"];
    private static readonly string[] MoodKeywords = ["有推进感", "平静", "开心", "焦虑", "累"];

    public string ProviderId => "mock";

    public Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var aiJson = GenerateJson(request.Date, request.RawInputs);
        return Task.FromResult(JournalAiProviderResult.Success(aiJson, JournalAiMetadata.Mock));
    }

    public Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken) =>
        Task.FromResult(JournalAiProviderHealthResult.Success("{\"ok\":true}", TimeSpan.Zero));

    private static JournalAiJson GenerateJson(JournalDate date, IReadOnlyList<RawInput> rawInputs)
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

        var work = ExtractSection(inputTexts, WorkKeywords);
        var relationship = ExtractSection(inputTexts, RelationshipKeywords);
        var health = ExtractSection(inputTexts, HealthKeywords);
        var money = ExtractSection(inputTexts, MoneyKeywords);
        var inspiration = ExtractSection(inputTexts, InspirationKeywords, "暂无灵感补充。");
        var specificFacts = work
            .Concat(relationship)
            .Concat(health)
            .Concat(money)
            .Concat(inspiration)
            .ToHashSet(StringComparer.Ordinal);

        return new JournalAiJson(
            Schema,
            date.IsoDate,
            date.MonthDay,
            "draft",
            tags,
            tags.Count > 0 ? tags : ["日记"],
            ExtractMood(inputTexts),
            inputTexts,
            ExtractSection(inputTexts, YesterdayKeywords, "记录了今天之前的上下文。"),
            ExtractSection(inputTexts, TodayKeywords, "整理今天的重点。", specificFacts),
            inspiration)
        {
            Work = work,
            Relationship = relationship,
            Health = health,
            Money = money
        };
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

    private static IReadOnlyList<string> ExtractSection(
        IEnumerable<string> inputTexts,
        IReadOnlyList<string> keywords,
        string fallback,
        IReadOnlySet<string> excludedFacts)
    {
        var matches = inputTexts
            .Where(text => !excludedFacts.Contains(text))
            .Where(text => keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal)))
            .ToArray();

        return matches.Length > 0 ? matches : [fallback];
    }

    private static IReadOnlyList<string> ExtractSection(IEnumerable<string> inputTexts, IReadOnlyList<string> keywords)
    {
        var matches = inputTexts
            .Where(text => keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal)))
            .ToArray();

        return matches;
    }
}
