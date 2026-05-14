using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPromptTests
{
    [Fact]
    public void SystemInstructions_DeclarePlannerContractAndTwoLayerBoundary()
    {
        Assert.Equal("journal-harness-v2", JournalHarnessPrompt.Version);
        Assert.Equal("append-input", JournalHarnessPrompt.AppendInputMode);
        Assert.Equal("reorganize-existing", JournalHarnessPrompt.ReorganizeExistingMode);
        Assert.Contains("# Journal Harness Planner", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Core Principle", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Context Contract", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Intent Modes", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Section Allocation Protocol", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Tool Argument Format", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Red Lines: What You Must Not Do", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Tool Selection", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Positive Examples", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Negative Examples", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("## Writing Style", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("只能调用工具", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("不得在重新整理时新增 raw input", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("每条新增内容必须写成 Markdown bullet", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("使用 Markdown 加粗标注重点内容", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("按轻重缓急排序", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("同一事实只能进入一个最合适的 section", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("today-focus 与 work 的边界", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("调整日记结构", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("不能仅因为用户没有点名 section 就 noOp", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForAppendInput_CreatesJsonJournalContextAndSeparateUserMessage()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var historicalRawInputs = new[]
        {
            new RawInput(
                "raw-old",
                date,
                new DateTimeOffset(2026, 5, 13, 7, 10, 0, TimeSpan.FromHours(8)),
                "text",
                "历史输入：昨天完成审计设计。")
        };
        var currentInput = new RawInput(
            "raw-current",
            date,
            new DateTimeOffset(2026, 5, 13, 7, 40, 0, TimeSpan.FromHours(8)),
            "text",
            "当前输入：今天重写 planner prompt。");

        var request = JournalHarnessPrompt.BuildForAppendInput(
            date,
            historicalRawInputs,
            currentInput,
            "# Draft",
            "# Entry");

        using var context = JsonDocument.Parse(request.ProtectedContext);
        var root = context.RootElement;
        Assert.Equal("journal-harness-v2", root.GetProperty("version").GetString());
        Assert.Equal("2026-05-13", root.GetProperty("date").GetString());
        Assert.Equal("append-input", root.GetProperty("mode").GetString());
        Assert.Equal("# Draft", root.GetProperty("currentDraftMarkdown").GetString());
        Assert.Equal("# Entry", root.GetProperty("confirmedEntryMarkdown").GetString());

        var historical = root.GetProperty("historicalRawInputs").EnumerateArray().Single();
        Assert.Equal("raw-old", historical.GetProperty("id").GetString());
        Assert.Equal("text", historical.GetProperty("source").GetString());
        Assert.Equal("历史输入：昨天完成审计设计。", historical.GetProperty("text").GetString());
        Assert.DoesNotContain("当前输入：今天重写 planner prompt。", request.ProtectedContext, StringComparison.Ordinal);

        var catalog = root.GetProperty("sectionCatalog").EnumerateArray().ToArray();
        Assert.Equal(JmfSectionCatalog.All.Count, catalog.Length);
        Assert.Equal(
            JmfSectionCatalog.All.Select(section => section.Id),
            catalog.Select(item => item.GetProperty("id").GetString()));
        Assert.Contains(catalog, item => item.GetProperty("id").GetString() == "today-focus");
        Assert.Contains(catalog, item => item.GetProperty("id").GetString() == "raw-inputs");
        Assert.Contains(catalog, item => item.GetProperty("title").GetString() == "原始输入");
        Assert.Contains(catalog, item => item.TryGetProperty("isEditableInBlockMode", out _));
        Assert.Contains(catalog, item =>
            item.GetProperty("id").GetString() == "work"
            && item.GetProperty("semanticHint").GetString()!.Contains("工作项目", StringComparison.Ordinal)
            && item.GetProperty("avoidWhen").GetString()!.Contains("今日总体优先级", StringComparison.Ordinal));
        Assert.Contains(catalog, item =>
            item.GetProperty("id").GetString() == "today-focus"
            && item.GetProperty("semanticHint").GetString()!.Contains("今天最重要的行动", StringComparison.Ordinal)
            && item.GetProperty("avoidWhen").GetString()!.Contains("具体工作项目", StringComparison.Ordinal));

        var tools = root.GetProperty("availableTools").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("appendJournalSection", tools);
        Assert.Contains("upsertJournalSection", tools);
        Assert.Contains("reviseAiGeneratedSection", tools);
        Assert.Contains("noOp", tools);

        using var userMessage = JsonDocument.Parse(request.UserMessage);
        Assert.Equal("append-input", userMessage.RootElement.GetProperty("mode").GetString());
        Assert.Equal("raw-current", userMessage.RootElement.GetProperty("id").GetString());
        Assert.Equal("text", userMessage.RootElement.GetProperty("source").GetString());
        Assert.Equal("当前输入：今天重写 planner prompt。", userMessage.RootElement.GetProperty("text").GetString());
        Assert.Equal(
            "2026-05-13T07:40:00+08:00",
            userMessage.RootElement.GetProperty("createdAt").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:sszzz"));
    }

    [Fact]
    public void BuildForReorganizeExisting_UsesOnlyHistoricalRawInputsAndDoesNotExposeDraftOrEntry()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var historicalRawInputs = new[]
        {
            new RawInput(
                "raw-1",
                date,
                new DateTimeOffset(2026, 5, 13, 7, 10, 0, TimeSpan.FromHours(8)),
                "text",
                "历史输入：今天要重新整理。")
        };

        var request = JournalHarnessPrompt.BuildForReorganizeExisting(
            date,
            historicalRawInputs,
            "# Draft",
            "# Entry");

        using var context = JsonDocument.Parse(request.ProtectedContext);
        Assert.Equal("journal-harness-v2", context.RootElement.GetProperty("version").GetString());
        Assert.Equal("reorganize-existing", context.RootElement.GetProperty("mode").GetString());
        Assert.Contains("历史输入：今天要重新整理。", request.ProtectedContext, StringComparison.Ordinal);
        Assert.False(context.RootElement.TryGetProperty("currentDraftMarkdown", out _));
        Assert.False(context.RootElement.TryGetProperty("confirmedEntryMarkdown", out _));
        Assert.DoesNotContain("# Draft", request.ProtectedContext, StringComparison.Ordinal);
        Assert.DoesNotContain("# Entry", request.ProtectedContext, StringComparison.Ordinal);
        Assert.Equal(JournalHarnessPrompt.ReorganizeExistingUserMessage, request.UserMessage);
        Assert.Contains("本次请求不是新的原始输入", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("不要新增、改写或覆盖 raw inputs", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("protected context", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("全量结构重组", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("历史 raw inputs 是最高事实来源", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("不要读取、参考或继承 current draft", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("放弃现有全部日记正文", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("不得把旧日记正文当作事实来源", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("重新规划整篇日记的九宫格分布", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("合并重复、移动错分、压缩冗余表达", request.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("id", request.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-1", request.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemInstructions_DistinguishLightStructureIntentFromButtonReorganize()
    {
        Assert.Contains("轻量结构调整", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("按钮重新整理", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("强结构重组模式", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("只提供 historical raw inputs", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("不提供 currentDraftMarkdown", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("放弃现有全部日记正文", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemInstructions_RequireMarkdownEmphasisAndPriorityOrdering()
    {
        Assert.Contains("重点内容使用 Markdown 加粗", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("不要整段都加粗", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("重要且紧急的内容排在前面", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("- **今天最重要**", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemInstructions_TreatShortStructureAdjustmentAsActionableReorganizeIntent()
    {
        Assert.Contains("短结构调整命令", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("重新分配 section", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("检查重复、错分、整段内容和相近 section 边界", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("如果发现安全可执行的整理点", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SplitsHistoricalRawInputsFromCurrentUserMessage()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 12));
        var historicalRawInputs = new[]
        {
            new RawInput(
                "raw-1",
                date,
                new DateTimeOffset(2026, 5, 12, 7, 20, 0, TimeSpan.FromHours(8)),
                "manual",
                "历史输入：昨晚完成了 prompt harness 计划。")
        };
        var currentInput = new RawInput(
            "raw-3",
            date,
            new DateTimeOffset(2026, 5, 12, 7, 45, 0, TimeSpan.FromHours(8)),
            "manual",
            "当前输入：今天先把上下文拆开。");

        var request = JournalHarnessPrompt.Build(
            date,
            historicalRawInputs,
            currentInput,
            "# Current Draft\n\n已有草稿内容",
            "# Confirmed Entry\n\n已确认正文");

        using var context = JsonDocument.Parse(request.ProtectedContext);
        Assert.Equal("journal-harness-v2", context.RootElement.GetProperty("version").GetString());
        Assert.Equal("append-input", context.RootElement.GetProperty("mode").GetString());
        Assert.Equal("2026-05-12", context.RootElement.GetProperty("date").GetString());
        Assert.Equal(
            "历史输入：昨晚完成了 prompt harness 计划。",
            context.RootElement.GetProperty("historicalRawInputs").EnumerateArray().Single().GetProperty("text").GetString());
        Assert.DoesNotContain("当前输入：今天先把上下文拆开。", request.ProtectedContext, StringComparison.Ordinal);
        using var userMessage = JsonDocument.Parse(request.UserMessage);
        Assert.Equal("append-input", userMessage.RootElement.GetProperty("mode").GetString());
        Assert.Equal("raw-3", userMessage.RootElement.GetProperty("id").GetString());
        Assert.Equal("manual", userMessage.RootElement.GetProperty("source").GetString());
        Assert.Equal("当前输入：今天先把上下文拆开。", userMessage.RootElement.GetProperty("text").GetString());
        Assert.Equal(
            "2026-05-12T07:45:00+08:00",
            userMessage.RootElement.GetProperty("createdAt").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:sszzz"));
        Assert.Contains("当前输入：今天先把上下文拆开。", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("raw-3", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("# Journal Harness Planner", request.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("appendJournalSection", request.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("upsertJournalSection", request.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("reviseAiGeneratedSection", request.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("noOp", request.SystemInstructions, StringComparison.Ordinal);
    }
}
