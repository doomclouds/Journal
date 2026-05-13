using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessOperationExecutorTests
{
    [Fact]
    public void Apply_AppendsToUserTouchedSectionAndMarksMixedAiAppend()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户已经调整今日计划",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- AI 追加 harness 验证",
            ["raw-2"],
            "用户要求补充 harness 信息。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Issues);
        var section = GetSection(result.Document, "today-focus");
        Assert.Equal(
            "- 用户已经调整今日计划\n- AI 追加 harness 验证",
            section.Content);
        Assert.Equal("mixed", section.Provenance.Origin);
        Assert.Equal("user", section.Provenance.CreatedBy);
        Assert.Equal("ai", section.Provenance.LastTouchedBy);
        Assert.Equal("append", section.Provenance.LastOperation);
        Assert.Equal(["raw-2"], section.Provenance.BasedOnRawInputIds);
    }

    [Fact]
    public void Apply_AppendsWithoutTrimmingExistingMarkdownContent()
    {
        var existingContent = string.Join(
            "\n",
            "    ```csharp",
            "    var answer = 42;",
            "    ```  ");
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                existingContent,
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "  - AI 追加内容  ",
            ["raw-2"],
            "追加内容时保留已有 Markdown。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        var content = GetSection(result.Document, "today-focus").Content;
        Assert.StartsWith(existingContent, content, StringComparison.Ordinal);
        Assert.Equal($"{existingContent}\n- AI 追加内容", content);
    }

    [Fact]
    public void Apply_NormalizesBlankLinesFromAiOperationContentWhenAppending()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户已经写好的计划",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "\n\n  - 今天可能较早下班  \n\n\n  \n- 测试新整理的接口\n\n",
            ["raw-2"],
            "模型输出里带了多余空行。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Equal(
            "- 用户已经写好的计划\n- 今天可能较早下班\n- 测试新整理的接口",
            GetSection(result.Document, "today-focus").Content);
    }

    [Fact]
    public void Apply_ConvertsAiParagraphContentToBulletItemWhenAppending()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户已经写好的计划",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "今天有个更重要的目标——继续看笔记软件，并处理 Harness 提示词优化的小问题。",
            ["raw-2"],
            "模型把条目写成了整段文本。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Equal(
            "- 用户已经写好的计划\n- 今天有个更重要的目标——继续看笔记软件，并处理 Harness 提示词优化的小问题。",
            GetSection(result.Document, "today-focus").Content);
    }

    [Fact]
    public void Apply_DeduplicatesSameFactAcrossCompetingSectionsAndKeepsMoreSpecificSection()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户已经写好的计划",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"])),
            Section(
                "work",
                "- 已有工作事项",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var duplicatedFact = "今天处理 Harness 提示词优化的小问题。";
        var operations = new[]
        {
            JournalHarnessOperation.Append(
                "today-focus",
                duplicatedFact,
                ["raw-2"],
                "模型把工作事项也放进今日重点。"),
            JournalHarnessOperation.Append(
                "work",
                duplicatedFact,
                ["raw-2"],
                "这是具体开发排障事项。")
        };

        var result = JournalHarnessOperationExecutor.Apply(document, operations, ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Equal("- 用户已经写好的计划", GetSection(result.Document, "today-focus").Content);
        Assert.Equal(
            "- 已有工作事项\n- 今天处理 Harness 提示词优化的小问题。",
            GetSection(result.Document, "work").Content);
    }

    [Fact]
    public void Apply_StripsTargetSectionHeadingFromAiOperationContentWhenAppending()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "mood",
                "- 期待且略带疑惑",
                new JmfSectionProvenance("mixed", "ai", "user", "edit", ["raw-1"])),
            Section("today-focus", "- 今日重点")
        ]);
        var operation = JournalHarnessOperation.Append(
            "mood",
            "## 情绪状态\n\n- 开心且兴奋！日记架构基本完成，感觉未来可期",
            ["raw-2"],
            "模型把 section 标题一起放进了工具参数。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Equal(
            "- 期待且略带疑惑\n- 开心且兴奋！日记架构基本完成，感觉未来可期",
            GetSection(result.Document, "mood").Content);
    }

    [Fact]
    public void Apply_NormalizesBlankLinesFromAiOperationContentWhenCreatingSection()
    {
        var document = CreateDocument();
        var operation = JournalHarnessOperation.Upsert(
            "inspiration",
            "\n- 期待日积月累的观察\n\n\n- 希望 bug 已修复\n",
            ["raw-2"],
            "新增灵感时模型输出里带了多余空行。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Equal(
            "- 期待日积月累的观察\n- 希望 bug 已修复",
            GetSection(result.Document, "inspiration").Content);
    }

    [Fact]
    public void Apply_RejectsReviseAiGeneratedSectionWhenSectionWasTouchedByUser()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户修过 AI 内容",
                new JmfSectionProvenance("mixed", "ai", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.ReviseAiGeneratedSection(
            "today-focus",
            "- AI 尝试改写",
            ["raw-2"],
            "模型想要重写。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "harness-revise-user-section");
        Assert.Equal("- 用户修过 AI 内容", GetSection(result.Document, "today-focus").Content);
    }

    [Fact]
    public void Apply_NoOpChangesNothing()
    {
        var document = CreateDocument();

        var result = JournalHarnessOperationExecutor.Apply(document, [JournalHarnessOperation.NoOp("无可操作内容。")], ["raw-current"]);

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(document, result.Document);
    }

    [Theory]
    [InlineData("raw-inputs")]
    [InlineData("keywords")]
    [InlineData("metadata-note")]
    [InlineData("custom")]
    public void Apply_RejectsReadonlySystemRawInputsAndUnknownTargets(string sectionId)
    {
        var document = CreateDocument();
        var operation = JournalHarnessOperation.Append(sectionId, "- AI 追加", ["raw-2"], "目标不可写。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "harness-target-readonly");
    }

    [Fact]
    public void Apply_UpsertCreatesMissingEditableSectionWithAiCreateProvenance()
    {
        var document = CreateDocument();
        var operation = JournalHarnessOperation.Upsert("inspiration", "- 新灵感", ["raw-2"], "新增灵感。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Issues);
        var section = GetSection(result.Document, "inspiration");
        Assert.Equal("- 新灵感", section.Content);
        Assert.Equal("ai", section.Provenance.Origin);
        Assert.Equal("ai", section.Provenance.CreatedBy);
        Assert.Equal("ai", section.Provenance.LastTouchedBy);
        Assert.Equal("create", section.Provenance.LastOperation);
        Assert.Equal(["raw-2"], section.Provenance.BasedOnRawInputIds);
    }

    [Fact]
    public void Apply_RejectsMissingNonUpsertTarget()
    {
        var document = CreateDocument();
        var operation = JournalHarnessOperation.Append("inspiration", "- 新灵感", ["raw-2"], "追加缺失 section。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "harness-target-missing");
    }

    [Fact]
    public void Apply_RevisesPureAiSection()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- AI 原内容",
                new JmfSectionProvenance("ai", "ai", "ai", "create", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.ReviseAiGeneratedSection(
            "today-focus",
            "- AI 修订内容",
            ["raw-2"],
            "修订 AI 内容。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Issues);
        var section = GetSection(result.Document, "today-focus");
        Assert.Equal("- AI 修订内容", section.Content);
        Assert.Equal("ai", section.Provenance.Origin);
        Assert.Equal("ai", section.Provenance.CreatedBy);
        Assert.Equal("ai", section.Provenance.LastTouchedBy);
        Assert.Equal("revise", section.Provenance.LastOperation);
        Assert.Equal(["raw-2"], section.Provenance.BasedOnRawInputIds);
    }

    [Fact]
    public void Apply_UnknownOperationReturnsHarnessUnknownOperation()
    {
        var document = CreateDocument();
        var operation = new JournalHarnessOperation("delete", "today-focus", "- nope", ["raw-2"], "未知操作。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "harness-unknown-operation");
    }

    [Fact]
    public void Apply_FiltersProvenanceRawInputIdsToAllowedServerIds()
    {
        var document = CreateDocument([
            Section("raw-inputs", "- raw"),
            Section("yesterday-review", "- 昨天完成基础设计"),
            Section(
                "today-focus",
                "- 用户已经调整今日计划",
                new JmfSectionProvenance("user", "user", "user", "edit", ["raw-1"]))
        ]);
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- AI 追加 harness 验证",
            ["raw-2", "raw-evil", "raw-x\" --><script>alert(1)</script><!--"],
            "用户要求补充 harness 信息。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation], ["raw-2"]);

        Assert.True(result.Validation.IsValid);
        var section = GetSection(result.Document, "today-focus");
        Assert.Equal(["raw-2"], section.Provenance.BasedOnRawInputIds);
    }

    private static JmfDocument CreateDocument(IReadOnlyList<JmfSection>? sections = null) =>
        new(
            "schema: journal-entry/v1",
            new Dictionary<string, string> { ["schema"] = "journal-entry/v1" },
            sections ?? [Section("raw-inputs", "- raw"), Section("yesterday-review", "- review"), Section("today-focus", "- focus")]);

    private static JmfSection Section(
        string id,
        string content,
        JmfSectionProvenance? provenance = null)
    {
        var definition = JmfSectionCatalog.Require(id);

        return new JmfSection(
            definition.Id,
            definition.Title,
            content,
            definition.Kind,
            definition.IsEditableInBlockMode,
            provenance ?? JmfSectionProvenance.Unknown);
    }

    private static JmfSection GetSection(JmfDocument document, string sectionId) =>
        document.Sections.Single(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
}
