using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Journal.Domain.Entries;

public static class JmfSectionCatalog
{
    private static readonly JmfSectionDefinition[] DefinitionItems =
    [
        new("raw-inputs", "原始输入", 1, JmfSectionKind.Required, false, "用户原始表达的只读记录。", "任何整理、总结或改写内容。"),
        new("mood", "状态与情绪", 2, JmfSectionKind.OptionalSingleton, true, "情绪、感受、压力、期待、疲惫、心理状态和状态变化。", "具体任务、工作事项、学习计划、生活事件或事实复盘。"),
        new("yesterday-review", "昨日回顾", 3, JmfSectionKind.Required, true, "昨天发生的事、完成情况、复盘和回看。", "今天要做的计划、未来提醒或泛泛情绪。"),
        new("today-focus", "今日重点", 4, JmfSectionKind.Required, true, "今天最高优先级、关键行动或日程重心，最多 1-3 条。", "具体工作学习细节、健康事项、生活事件、财务记录或未来提醒。"),
        new("work", "工作与学习", 5, JmfSectionKind.OptionalSingleton, true, "工作项目、开发、接口、会议、交付、排障、读书、课程、方法论和技能成长。", "今日总体优先级、健康、家庭关系、财务或纯情绪表达。"),
        new("learning", "学习与思考", 6, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版学习与思考，新的整理应合并到工作与学习。", "新内容不应再写入此 legacy section。", false),
        new("relationship", "生活与关系", 7, JmfSectionKind.OptionalSingleton, true, "家庭、朋友、人际、生活事件、庆幸、珍惜和值得感谢的人事物。", "工作项目、学习计划、个人身体状态或纯财务记录。"),
        new("health", "健康与精力", 8, JmfSectionKind.OptionalSingleton, true, "睡眠、精力、身体状态、运动、饮食、作息和精力管理。", "工作任务、学习任务或单纯情绪。"),
        new("money", "财务", 9, JmfSectionKind.OptionalSingleton, true, "消费、收入、预算、理财和财务意识。", "普通工作任务、学习内容或情绪记录。"),
        new("inspiration", "灵感与未来提醒", 10, JmfSectionKind.OptionalSingleton, true, "突然想到的点子、顿悟、创意火花、长期观察、未来提醒和非今日执行事项。", "已经明确要今天执行的行动、已发生事实或具体复盘。"),
        new("future-notes", "未来提醒", 11, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版未来提醒，新的整理应合并到灵感与未来提醒。", "新内容不应再写入此 legacy section。", false),
        new("gratitude", "感恩", 12, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版感恩，新的整理应合并到生活与关系。", "新内容不应再写入此 legacy section。", false),
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

    public static IReadOnlyList<JmfSectionDefinition> ActiveForNewContent { get; } = Array.AsReadOnly(
        Definitions.Where(item => item.IsActiveForNewContent).ToArray());

    public static IReadOnlyList<JmfSectionDefinition> ActiveOptionalSingleton { get; } = Array.AsReadOnly(
        Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton && item.IsActiveForNewContent).ToArray());

    public static IReadOnlyList<JmfSectionDefinition> LegacyOptionalSingleton { get; } = Array.AsReadOnly(
        Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton && !item.IsActiveForNewContent).ToArray());

    public static bool TryGet(string id, [NotNullWhen(true)] out JmfSectionDefinition? definition) =>
        DefinitionsById.TryGetValue(id, out definition);

    public static JmfSectionDefinition Require(string id) =>
        TryGet(id, out var definition)
            ? definition
            : throw new InvalidOperationException($"Unknown JMF section id: {id}");

    public static IReadOnlyList<JmfSectionDefinition> GetAvailableOptionalSections(IEnumerable<string> existingIds)
    {
        var existing = existingIds.ToHashSet(StringComparer.Ordinal);

        return ActiveOptionalSingleton
            .Where(item => !existing.Contains(item.Id))
            .ToArray();
    }
}
