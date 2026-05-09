namespace Journal.Domain.Entries;

public static class JmfSectionCatalog
{
    private static readonly IReadOnlyList<JmfSectionDefinition> Definitions =
    [
        new("raw-inputs", "原始输入", 1, JmfSectionKind.Required, false),
        new("mood", "情绪状态", 2, JmfSectionKind.OptionalSingleton, true),
        new("yesterday-review", "昨日回顾", 3, JmfSectionKind.Required, true),
        new("today-focus", "今日重点", 4, JmfSectionKind.Required, true),
        new("work", "工作推进", 5, JmfSectionKind.OptionalSingleton, true),
        new("learning", "学习与思考", 6, JmfSectionKind.OptionalSingleton, true),
        new("health", "健康与精力", 7, JmfSectionKind.OptionalSingleton, true),
        new("relationship", "关系与家庭", 8, JmfSectionKind.OptionalSingleton, true),
        new("money", "财务", 9, JmfSectionKind.OptionalSingleton, true),
        new("inspiration", "灵感", 10, JmfSectionKind.OptionalSingleton, true),
        new("future-notes", "未来提醒", 11, JmfSectionKind.OptionalSingleton, true),
        new("gratitude", "感恩", 12, JmfSectionKind.OptionalSingleton, true),
        new("keywords", "关键词", 13, JmfSectionKind.System, false),
        new("metadata-note", "生成信息", 14, JmfSectionKind.System, false)
    ];

    private static readonly IReadOnlyDictionary<string, JmfSectionDefinition> DefinitionsById =
        Definitions.ToDictionary(item => item.Id, StringComparer.Ordinal);

    public static IReadOnlyList<JmfSectionDefinition> All => Definitions;

    public static IReadOnlyList<JmfSectionDefinition> Required { get; } =
        Definitions.Where(item => item.Kind == JmfSectionKind.Required).ToArray();

    public static IReadOnlyList<JmfSectionDefinition> OptionalSingleton { get; } =
        Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton).ToArray();

    public static bool TryGet(string id, out JmfSectionDefinition definition) =>
        DefinitionsById.TryGetValue(id, out definition!);

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
