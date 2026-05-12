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
            "- 用户已经调整今日计划\n\n- AI 追加 harness 验证",
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
        Assert.Equal($"{existingContent}\n\n- AI 追加内容", content);
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
