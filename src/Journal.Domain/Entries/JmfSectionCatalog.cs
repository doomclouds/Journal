using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Journal.Domain.Entries;

public static class JmfSectionCatalog
{
    private static readonly JmfSectionDefinition[] DefinitionItems =
    [
        new("raw-inputs", "原始输入", 1, JmfSectionKind.Required, false, "用户原始表达的只读记录。", "任何整理、总结或改写内容。"),
        new("mood", "情绪状态", 2, JmfSectionKind.OptionalSingleton, true, "情绪、感受、心理状态和状态变化。", "具体任务、工作事项、学习计划或事实复盘。"),
        new("yesterday-review", "昨日回顾", 3, JmfSectionKind.Required, true, "昨天发生的事、完成情况、复盘和回看。", "今天要做的计划、未来提醒或泛泛情绪。"),
        new("today-focus", "今日重点", 4, JmfSectionKind.Required, true, "今天最重要的行动、优先级或日程重心。", "具体工作项目、开发排障、会议细节、读书学习、健康事项等更具体主题。"),
        new("work", "工作推进", 5, JmfSectionKind.OptionalSingleton, true, "工作项目、开发、接口、会议、交付、排障和加班。", "今日总体优先级、私人学习、健康、家庭或纯情绪表达。"),
        new("learning", "学习与思考", 6, JmfSectionKind.OptionalSingleton, true, "读书、课程、知识输入、方法论和学习计划。", "工作交付事项、单纯今日安排或无学习含义的灵感。"),
        new("health", "健康与精力", 7, JmfSectionKind.OptionalSingleton, true, "睡眠、精力、身体状态、运动、饮食和作息。", "工作任务、学习任务或单纯情绪。"),
        new("relationship", "关系与家庭", 8, JmfSectionKind.OptionalSingleton, true, "家庭、人际、朋友、沟通和关系维护。", "工作项目、学习计划或个人身体状态。"),
        new("money", "财务", 9, JmfSectionKind.OptionalSingleton, true, "消费、收入、预算、理财和财务意识。", "普通工作任务、学习内容或情绪记录。"),
        new("inspiration", "灵感", 10, JmfSectionKind.OptionalSingleton, true, "突然想到的点子、顿悟、创意火花和可探索想法。", "已经明确要今天执行的任务、复盘事实或长期提醒。"),
        new("future-notes", "未来提醒", 11, JmfSectionKind.OptionalSingleton, true, "以后再看、长期观察、未来提醒和非今日执行事项。", "今天明确要做的行动或已经发生的事实。"),
        new("gratitude", "感恩", 12, JmfSectionKind.OptionalSingleton, true, "感谢、庆幸、珍惜和值得感激的人事物。", "普通计划、工作推进或没有感谢含义的情绪。"),
        new("keywords", "关键词", 13, JmfSectionKind.System, false, "系统生成的关键词。", "用户或 AI 的正文内容。"),
        new("metadata-note", "生成信息", 14, JmfSectionKind.System, false, "系统生成信息。", "用户或 AI 的正文内容。")
    ];

    private static readonly ReadOnlyCollection<JmfSectionDefinition> Definitions =
        Array.AsReadOnly(DefinitionItems);

    private static readonly IReadOnlyDictionary<string, JmfSectionDefinition> DefinitionsById =
        Definitions.ToDictionary(item => item.Id, StringComparer.Ordinal);

    public static IReadOnlyList<JmfSectionDefinition> All => Definitions;

    public static IReadOnlyList<JmfSectionDefinition> Required { get; } = Array.AsReadOnly(
        Definitions.Where(item => item.Kind == JmfSectionKind.Required).ToArray());

    public static IReadOnlyList<JmfSectionDefinition> OptionalSingleton { get; } = Array.AsReadOnly(
        Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton).ToArray());

    public static bool TryGet(string id, [NotNullWhen(true)] out JmfSectionDefinition? definition) =>
        DefinitionsById.TryGetValue(id, out definition);

    public static JmfSectionDefinition Require(string id) =>
        TryGet(id, out var definition)
            ? definition
            : throw new InvalidOperationException($"Unknown JMF section id: {id}");

    public static IReadOnlyList<JmfSectionDefinition> GetAvailableOptionalSections(IEnumerable<string> existingIds)
    {
        var existing = existingIds.ToHashSet(StringComparer.Ordinal);

        return OptionalSingleton
            .Where(item => !existing.Contains(item.Id))
            .ToArray();
    }
}
