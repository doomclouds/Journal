# JMF Soft Section Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make new AI organization use the consolidated JMF sections while preserving old journal sections as legacy-compatible content.

**Architecture:** Add an explicit active-vs-legacy flag to `JmfSectionDefinition`. Keep `JmfSectionCatalog.All` compatible with old Markdown, but expose a new active catalog for AI/Harness/new block insertion. Harden Harness execution so legacy sections cannot be new AI write targets.

**Tech Stack:** .NET 10, xUnit, Electron + React + TypeScript, Vitest.

---

## File Structure

- Modify `src/Journal.Domain/Entries/JmfSectionDefinition.cs`: add active/legacy metadata.
- Modify `src/Journal.Domain/Entries/JmfSectionCatalog.cs`: update titles, semantic hints, active optional collection, legacy collection, and available optional behavior.
- Modify `src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs`: send only active sections to the LLM and update prompt allocation rules.
- Modify `src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs`: reject legacy section tool targets even if the model tries them.
- Modify `apps/desktop/src/JournalEditor.tsx`: align local fallback ordering with new active/legacy section order.
- Modify `apps/desktop/src/todayWorkbenchView.ts`: update display title overrides for active/legacy titles where needed.
- Modify backend tests under `tests/Journal.Tests`.
- Modify frontend tests under `apps/desktop/src`.
- Modify docs: `PROJECT_VISION.md`, `README.md`, `docs/agents/PROJECT_CONTEXT.md`, `docs/agents/PRODUCT_INVARIANTS.md`, and later archive the completed change.

---

### Task 1: Add Active And Legacy Section Catalog Semantics

**Files:**
- Modify: `src/Journal.Domain/Entries/JmfSectionDefinition.cs`
- Modify: `src/Journal.Domain/Entries/JmfSectionCatalog.cs`
- Test: `tests/Journal.Tests/JmfSectionCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

Add tests to `tests/Journal.Tests/JmfSectionCatalogTests.cs`:

```csharp
[Fact]
public void ActiveForNewContent_ExcludesLegacyMergedSections()
{
    Assert.Equal(
        [
            "raw-inputs",
            "mood",
            "yesterday-review",
            "today-focus",
            "work",
            "relationship",
            "health",
            "money",
            "inspiration",
            "keywords",
            "metadata-note"
        ],
        JmfSectionCatalog.ActiveForNewContent.Select(item => item.Id));

    Assert.Equal(
        ["learning", "future-notes", "gratitude"],
        JmfSectionCatalog.LegacyOptionalSingleton.Select(item => item.Id));
}

[Fact]
public void ActiveOptionalSections_UseConsolidatedTitlesAndExcludeLegacySections()
{
    Assert.Equal(
        ["mood", "work", "relationship", "health", "money", "inspiration"],
        JmfSectionCatalog.ActiveOptionalSingleton.Select(item => item.Id));

    Assert.Equal("状态与情绪", JmfSectionCatalog.Require("mood").Title);
    Assert.Equal("工作与学习", JmfSectionCatalog.Require("work").Title);
    Assert.Equal("生活与关系", JmfSectionCatalog.Require("relationship").Title);
    Assert.Equal("灵感与未来提醒", JmfSectionCatalog.Require("inspiration").Title);
}
```

Update the existing `AvailableOptionalSections_ExcludesExistingSections` test:

```csharp
var available = JmfSectionCatalog.GetAvailableOptionalSections(["mood", "today-focus"]);

Assert.DoesNotContain(available, item => item.Id == "mood");
Assert.Contains(available, item => item.Id == "inspiration");
Assert.DoesNotContain(available, item => item.Id is "learning" or "future-notes" or "gratitude");
Assert.DoesNotContain(available, item => item.Kind != JmfSectionKind.OptionalSingleton);
```

Update `PublishedCollections_DoNotExposeMutableArrays`:

```csharp
Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.ActiveForNewContent);
Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.ActiveOptionalSingleton);
Assert.IsNotType<JmfSectionDefinition[]>(JmfSectionCatalog.LegacyOptionalSingleton);
```

- [ ] **Step 2: Run catalog tests and verify RED**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfSectionCatalogTests
```

Expected: FAIL because `ActiveForNewContent`, `ActiveOptionalSingleton`, `LegacyOptionalSingleton`, and consolidated titles do not exist yet.

- [ ] **Step 3: Implement catalog metadata**

Change `src/Journal.Domain/Entries/JmfSectionDefinition.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JmfSectionDefinition(
    string Id,
    string Title,
    int Order,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode,
    string SemanticHint,
    string AvoidWhen,
    bool IsActiveForNewContent = true);
```

Update `DefinitionItems` in `src/Journal.Domain/Entries/JmfSectionCatalog.cs` to this structure:

```csharp
private static readonly JmfSectionDefinition[] DefinitionItems =
[
    new("raw-inputs", "原始输入", 1, JmfSectionKind.Required, false, "用户原始表达的只读记录。", "任何整理、总结或改写内容。"),
    new("mood", "状态与情绪", 2, JmfSectionKind.OptionalSingleton, true, "情绪、感受、压力、期待、疲惫、心理状态和状态变化。", "具体任务、工作事项、学习计划、生活事件或事实复盘。"),
    new("yesterday-review", "昨日回顾", 3, JmfSectionKind.Required, true, "昨天发生的事、完成情况、复盘和回看。", "今天要做的计划、未来提醒或泛泛情绪。"),
    new("today-focus", "今日重点", 4, JmfSectionKind.Required, true, "今天最高优先级、关键行动或日程重心，最多 1-3 条。", "具体工作学习细节、健康事项、生活事件、财务记录或未来提醒。"),
    new("work", "工作与学习", 5, JmfSectionKind.OptionalSingleton, true, "工作项目、开发、接口、会议、交付、排障、读书、课程、方法论和技能成长。", "今日总体优先级、健康、家庭关系、财务或纯情绪表达。"),
    new("relationship", "生活与关系", 6, JmfSectionKind.OptionalSingleton, true, "家庭、朋友、人际、生活事件、庆幸、珍惜和值得感谢的人事物。", "工作项目、学习计划、个人身体状态或纯财务记录。"),
    new("health", "健康与精力", 7, JmfSectionKind.OptionalSingleton, true, "睡眠、精力、身体状态、运动、饮食、作息和精力管理。", "工作任务、学习任务或单纯情绪。"),
    new("money", "财务", 8, JmfSectionKind.OptionalSingleton, true, "消费、收入、预算、理财和财务意识。", "普通工作任务、学习内容或情绪记录。"),
    new("inspiration", "灵感与未来提醒", 9, JmfSectionKind.OptionalSingleton, true, "突然想到的点子、顿悟、创意火花、长期观察、未来提醒和非今日执行事项。", "已经明确要今天执行的行动、已发生事实或具体复盘。"),
    new("learning", "学习与思考", 90, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版学习与思考，新的整理应合并到工作与学习。", "新内容不应再写入此 legacy section。", false),
    new("future-notes", "未来提醒", 91, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版未来提醒，新的整理应合并到灵感与未来提醒。", "新内容不应再写入此 legacy section。", false),
    new("gratitude", "感恩", 92, JmfSectionKind.OptionalSingleton, true, "Legacy: 旧版感恩，新的整理应合并到生活与关系。", "新内容不应再写入此 legacy section。", false),
    new("keywords", "关键词", 100, JmfSectionKind.System, false, "系统生成的关键词。", "用户或 AI 的正文内容。"),
    new("metadata-note", "生成信息", 101, JmfSectionKind.System, false, "系统生成信息。", "用户或 AI 的正文内容。")
];
```

Add catalog collections:

```csharp
public static IReadOnlyList<JmfSectionDefinition> ActiveForNewContent { get; } = Array.AsReadOnly(
    Definitions.Where(item => item.IsActiveForNewContent).ToArray());

public static IReadOnlyList<JmfSectionDefinition> ActiveOptionalSingleton { get; } = Array.AsReadOnly(
    Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton && item.IsActiveForNewContent).ToArray());

public static IReadOnlyList<JmfSectionDefinition> LegacyOptionalSingleton { get; } = Array.AsReadOnly(
    Definitions.Where(item => item.Kind == JmfSectionKind.OptionalSingleton && !item.IsActiveForNewContent).ToArray());
```

Change `GetAvailableOptionalSections` to return `ActiveOptionalSingleton`:

```csharp
return ActiveOptionalSingleton
    .Where(item => !existing.Contains(item.Id))
    .ToArray();
```

- [ ] **Step 4: Run catalog tests and verify GREEN**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfSectionCatalogTests
```

Expected: PASS.

---

### Task 2: Preserve Legacy Markdown Compatibility

**Files:**
- Modify: `tests/Journal.Tests/JmfMarkdownValidatorTests.cs`
- Modify: `tests/Journal.Tests/JmfMarkdownComposerTests.cs`
- No production changes expected if Task 1 kept legacy definitions in `JmfSectionCatalog.All`.

- [ ] **Step 1: Write legacy compatibility tests**

Add to `JmfMarkdownValidatorTests.cs`:

```csharp
[Theory]
[InlineData("learning")]
[InlineData("future-notes")]
[InlineData("gratitude")]
public void Validate_ReturnsValidForLegacyOptionalSections(string sectionId)
{
    var result = JmfMarkdownValidator.Validate(CreateDocument(
        sections:
        [
            Section("raw-inputs"),
            Section("yesterday-review"),
            Section("today-focus"),
            Section(sectionId)
        ]));

    Assert.True(result.IsValid);
    Assert.Empty(result.Issues);
}
```

Add to `JmfMarkdownComposerTests.cs`:

```csharp
[Fact]
public void Compose_PreservesLegacyOptionalSections()
{
    var document = CreateDocument(
        Section("raw-inputs", "- 原始输入"),
        Section("yesterday-review", "- 昨日回顾"),
        Section("today-focus", "- 今日重点"),
        Section("learning", "- 旧学习内容"),
        Section("future-notes", "- 旧未来提醒"),
        Section("gratitude", "- 旧感恩内容"));

    var markdown = JmfMarkdownComposer.Compose(document);

    Assert.Contains("<!-- journal:section learning -->", markdown, StringComparison.Ordinal);
    Assert.Contains("## 学习与思考", markdown, StringComparison.Ordinal);
    Assert.Contains("<!-- journal:section future-notes -->", markdown, StringComparison.Ordinal);
    Assert.Contains("<!-- journal:section gratitude -->", markdown, StringComparison.Ordinal);

    var parseResult = JmfMarkdownParser.Parse(markdown);
    var validationResult = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
    Assert.True(validationResult.IsValid);
}
```

- [ ] **Step 2: Run JMF parse/compose/validate tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownValidatorTests|JmfMarkdownComposerTests"
```

Expected: PASS after Task 1. If this fails, fix only catalog compatibility or ordering; do not remove legacy definitions.

---

### Task 3: Restrict Harness Prompt To Active Sections

**Files:**
- Modify: `src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs`
- Test: `tests/Journal.Tests/JournalHarnessPromptTests.cs`

- [ ] **Step 1: Write failing Harness prompt tests**

In `BuildForAppendInput_CreatesJsonJournalContextAndSeparateUserMessage`, replace the catalog assertions:

```csharp
var catalog = root.GetProperty("sectionCatalog").EnumerateArray().ToArray();
Assert.Equal(JmfSectionCatalog.ActiveForNewContent.Count, catalog.Length);
Assert.Equal(
    JmfSectionCatalog.ActiveForNewContent.Select(section => section.Id),
    catalog.Select(item => item.GetProperty("id").GetString()));
Assert.DoesNotContain(catalog, item => item.GetProperty("id").GetString() is "learning" or "future-notes" or "gratitude");
Assert.Contains(catalog, item =>
    item.GetProperty("id").GetString() == "work"
    && item.GetProperty("title").GetString() == "工作与学习"
    && item.GetProperty("semanticHint").GetString()!.Contains("读书", StringComparison.Ordinal));
Assert.Contains(catalog, item =>
    item.GetProperty("id").GetString() == "relationship"
    && item.GetProperty("title").GetString() == "生活与关系"
    && item.GetProperty("semanticHint").GetString()!.Contains("值得感谢", StringComparison.Ordinal));
Assert.Contains(catalog, item =>
    item.GetProperty("id").GetString() == "inspiration"
    && item.GetProperty("title").GetString() == "灵感与未来提醒"
    && item.GetProperty("semanticHint").GetString()!.Contains("未来提醒", StringComparison.Ordinal));
```

Add a new test:

```csharp
[Fact]
public void BuildForReorganizeExisting_ExposesOnlyActiveSectionCatalog()
{
    var date = JournalDate.From(new DateOnly(2026, 5, 15));
    var request = JournalHarnessPrompt.BuildForReorganizeExisting(
        date,
        [
            new RawInput(
                "raw-1",
                date,
                new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.FromHours(8)),
                "text",
                "今天要把分类合并得更少。")
        ],
        "# Draft",
        "# Entry");

    using var context = JsonDocument.Parse(request.ProtectedContext);
    var catalogIds = context.RootElement
        .GetProperty("sectionCatalog")
        .EnumerateArray()
        .Select(item => item.GetProperty("id").GetString())
        .ToArray();

    Assert.Equal(JmfSectionCatalog.ActiveForNewContent.Select(section => section.Id), catalogIds);
    Assert.DoesNotContain("learning", catalogIds);
    Assert.DoesNotContain("future-notes", catalogIds);
    Assert.DoesNotContain("gratitude", catalogIds);
}
```

Update `SystemInstructions_DeclarePlannerContractAndTwoLayerBoundary`:

```csharp
Assert.Contains("今日重点最多 1-3 条", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.Contains("工作与学习", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.Contains("生活与关系", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.Contains("灵感与未来提醒", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.DoesNotContain("learning 放", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.DoesNotContain("future-notes 放", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
Assert.DoesNotContain("gratitude 只放", JournalHarnessPrompt.SystemInstructions, StringComparison.Ordinal);
```

- [ ] **Step 2: Run Harness prompt tests and verify RED**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPromptTests
```

Expected: FAIL because the prompt still serializes `JmfSectionCatalog.All` and still names legacy section rules.

- [ ] **Step 3: Implement active prompt catalog**

In `JournalHarnessPrompt.SerializeJournalContext` and `SerializeReorganizeJournalContext`, replace:

```csharp
sectionCatalog = JmfSectionCatalog.All.Select(section => new
```

with:

```csharp
sectionCatalog = JmfSectionCatalog.ActiveForNewContent.Select(section => new
```

Update `SystemInstructions` text:

- Replace `重新规划整篇日记的九宫格分布。` with `重新规划整篇日记的分类分布。`
- Replace `today-focus / work / learning / health 等相近 section 边界。` with `today-focus / work / health / relationship / inspiration 等相近 section 边界。`
- Replace the section allocation bullet list with:

```text
- 同一事实只能进入一个最合适的 section。
- 如果多个 section 都能解释，选择语义更具体的 section。
- 不要为了“丰富分类”而重复填充相近主题。
- today-focus 与 work 的边界必须特别谨慎处理。
- 今日重点最多 1-3 条，只做当天导航，不承载具体分类细节。
- today-focus 只放今天总体优先级、关键行动或日程重心。
- work 放工作项目、开发、会议、交付、排障、读书、课程、方法论和技能成长。
- relationship 放家庭、朋友、人际、生活事件、庆幸、珍惜和值得感谢的人事物。
- health 放睡眠、精力、身体、运动和作息。
- money 放消费、收入、预算、理财和金钱意识。
- inspiration 放点子、长期观察、未来提醒和非今日执行事项。
- mood 只放情绪、压力、期待、疲惫和精神状态，不承载事件详情。
```

Update examples and negative examples:

- Replace “learning” examples with “work / 工作与学习”.
- Replace “future-notes” examples with “inspiration / 灵感与未来提醒”.
- Replace “gratitude” examples with “relationship / 生活与关系”.
- Keep the red line that the model must not duplicate facts across sections.

- [ ] **Step 4: Run Harness prompt tests and verify GREEN**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPromptTests
```

Expected: PASS.

---

### Task 4: Reject Legacy Harness Tool Targets Server-Side

**Files:**
- Modify: `src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs`
- Test: `tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs`

- [ ] **Step 1: Write failing executor test**

Add to `JournalHarnessOperationExecutorTests.cs`:

```csharp
[Theory]
[InlineData("learning")]
[InlineData("future-notes")]
[InlineData("gratitude")]
public void Apply_RejectsLegacySectionsAsHarnessTargets(string sectionId)
{
    var document = CreateDocument(
        Section("raw-inputs", "- 原始输入"),
        Section("yesterday-review", "- 昨日回顾"),
        Section("today-focus", "- 今日重点"));
    var operations = new[]
    {
        new JournalHarnessOperation(
            "call-1",
            "upsert",
            sectionId,
            "- AI 不应该再写 legacy section",
            "legacy section is no longer active",
            ["raw-1"])
    };

    var result = JournalHarnessOperationExecutor.Apply(document, operations, ["raw-1"]);

    Assert.False(result.Validation.IsValid);
    Assert.Contains(result.Issues, issue => issue.Code == "harness-target-inactive");
    Assert.DoesNotContain(result.Document.Sections, section => section.Id == sectionId);
}
```

- [ ] **Step 2: Run executor test and verify RED**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessOperationExecutorTests
```

Expected: FAIL because legacy sections are still editable optional sections from the executor's point of view.

- [ ] **Step 3: Implement active target guard**

In `JournalHarnessOperationExecutor.Apply`, split target validation:

```csharp
if (!JmfSectionCatalog.TryGet(operation.TargetSectionId, out var definition)
    || string.Equals(definition.Id, "raw-inputs", StringComparison.Ordinal)
    || !definition.IsEditableInBlockMode
    || definition.Kind == JmfSectionKind.System)
{
    issues.Add(CreateIssue("harness-target-readonly", $"Section '{operation.TargetSectionId}' cannot be edited by harness."));
    continue;
}

if (!definition.IsActiveForNewContent)
{
    issues.Add(CreateIssue("harness-target-inactive", $"Section '{operation.TargetSectionId}' is a legacy section and cannot be targeted by harness."));
    continue;
}
```

In `FindDuplicateOperationIndexesToSkip`, add the same inactive guard to the skip condition:

```csharp
|| !definition.IsActiveForNewContent
```

Update `GetSectionSpecificityRank` to remove legacy special ranks:

```csharp
private static int GetSectionSpecificityRank(string sectionId) =>
    sectionId switch
    {
        "work" => 100,
        "health" => 95,
        "relationship" => 95,
        "money" => 95,
        "inspiration" => 90,
        "mood" => 75,
        "yesterday-review" => 70,
        "today-focus" => 50,
        _ => 0
    };
```

- [ ] **Step 4: Run executor tests and verify GREEN**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessOperationExecutorTests
```

Expected: PASS.

---

### Task 5: Keep Editor Insertions Active While Preserving Legacy Display

**Files:**
- Modify: `apps/desktop/src/JournalEditor.tsx`
- Modify: `apps/desktop/src/todayWorkbenchView.ts`
- Test: `apps/desktop/src/App.test.tsx`
- Test: `apps/desktop/src/todayWorkbenchView.test.ts`

- [ ] **Step 1: Update frontend tests for active insert titles**

In `createEditorState` inside `apps/desktop/src/App.test.tsx`, change the default optional section to:

```typescript
availableOptionalSections: [
  {
    id: "mood",
    title: "状态与情绪",
    order: 2,
    kind: "optionalSingleton",
    isEditableInBlockMode: true
  }
],
```

Replace test assertions using `情绪感受` with `状态与情绪`.

In `keeps inserted optional blocks in catalog order after required sections`, change the available section fixture:

```typescript
{
  id: "work",
  title: "工作与学习",
  order: 5,
  kind: "optionalSingleton",
  isEditableInBlockMode: true
}
```

and the expected heading:

```typescript
[
  "今日材料",
  "昨天回顾",
  "今日重点",
  "工作与学习"
]
```

In `todayWorkbenchView.test.ts`, replace the section display title test with:

```typescript
test("maps section ids to product display titles", () => {
  expect(getSectionDisplayTitle("raw-inputs", "原始输入")).toBe("今日材料");
  expect(getSectionDisplayTitle("today-focus", "今日重点")).toBe("今日重点");
  expect(getSectionDisplayTitle("yesterday-review", "昨日回顾")).toBe("昨天回顾");
  expect(getSectionDisplayTitle("mood", "状态与情绪")).toBe("状态与情绪");
  expect(getSectionDisplayTitle("work", "工作与学习")).toBe("工作与学习");
  expect(getSectionDisplayTitle("future-notes", "未来提醒")).toBe("未来提醒");
  expect(getSectionDisplayTitle("gratitude", "感恩记录")).toBe("感恩记录");
});
```

- [ ] **Step 2: Run frontend tests and verify RED if order/display is stale**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx todayWorkbenchView.test.ts
```

Expected: tests may fail because local ordering and title overrides still reflect old categories.

- [ ] **Step 3: Update frontend section order and title overrides**

In `apps/desktop/src/JournalEditor.tsx`, update `jmfSectionCatalogOrder`:

```typescript
const jmfSectionCatalogOrder = new Map<string, number>([
  ["raw-inputs", 1],
  ["mood", 2],
  ["yesterday-review", 3],
  ["today-focus", 4],
  ["work", 5],
  ["relationship", 6],
  ["health", 7],
  ["money", 8],
  ["inspiration", 9],
  ["learning", 90],
  ["future-notes", 91],
  ["gratitude", 92],
  ["keywords", 100],
  ["metadata-note", 101]
]);
```

In `apps/desktop/src/todayWorkbenchView.ts`, update display title overrides:

```typescript
const sectionDisplayTitles: Record<string, string> = {
  "raw-inputs": "今日材料",
  "today-focus": "今日重点",
  "yesterday-review": "昨天回顾",
  "mood": "状态与情绪",
  "work": "工作与学习",
  "relationship": "生活与关系",
  "health": "健康与精力",
  "money": "财务",
  "inspiration": "灵感与未来提醒",
  "future-notes": "未来提醒"
};
```

Do not hide existing legacy sections in the frontend. The API should simply stop returning legacy sections in `availableOptionalSections`.

- [ ] **Step 4: Run frontend tests and verify GREEN**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx todayWorkbenchView.test.ts
```

Expected: PASS.

---

### Task 6: Verify Today Editor Active Optional Behavior

**Files:**
- Modify: `tests/Journal.Tests/TodayJournalEditorServiceTests.cs`
- Production changes only if Task 1 did not already make `GetAvailableOptionalSections` use active optional sections.

- [ ] **Step 1: Write service-level active optional test**

Add to `TodayJournalEditorServiceTests.cs`:

```csharp
[Fact]
public async Task GetTodayEditorAsync_DoesNotOfferLegacySectionsAsAvailableOptionalSections()
{
    using var workspace = TempWorkspace.Create();
    var paths = CreatePaths(workspace.Root);
    var service = CreateService(paths);
    await service.AddInputAsync("今天测试合并后的分类 #Journal", "text", CancellationToken.None);

    var editor = await service.GetTodayEditorAsync(CancellationToken.None);

    Assert.DoesNotContain(editor.AvailableOptionalSections, section => section.Id is "learning" or "future-notes" or "gratitude");
    Assert.Contains(editor.AvailableOptionalSections, section => section.Id == "work" && section.Title == "工作与学习");
    Assert.Contains(editor.AvailableOptionalSections, section => section.Id == "relationship" && section.Title == "生活与关系");
    Assert.Contains(editor.AvailableOptionalSections, section => section.Id == "inspiration" && section.Title == "灵感与未来提醒");
}
```

Update `SaveBlockDraftAsync_ComposesOptionalBlocksInFixedOrder` to use active sections:

```csharp
new("relationship", "- 感谢测试先行"),
new("work", "- 推进 JMF editor"),
new("mood", "平静"),
new("today-focus", "- 保存 block draft")
```

and expected ids:

```csharp
["raw-inputs", "mood", "yesterday-review", "today-focus", "work", "relationship"]
```

- [ ] **Step 2: Run editor service tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEditorServiceTests
```

Expected: PASS after Task 1. If it fails, fix `GetAvailableOptionalSections` or section order, not parser compatibility.

---

### Task 7: Update Product Docs For Active And Legacy Sections

**Files:**
- Modify: `PROJECT_VISION.md`
- Modify: `README.md`
- Modify: `docs/agents/PROJECT_CONTEXT.md`
- Modify: `docs/agents/PRODUCT_INVARIANTS.md`

- [ ] **Step 1: Update documented section list**

In `PROJECT_VISION.md`, replace the JMF v1 optional singleton list with active and legacy subsections:

```markdown
活跃可选单例块：

- `mood`：状态与情绪。
- `work`：工作与学习。
- `relationship`：生活与关系。
- `health`：健康与精力。
- `money`：财务。
- `inspiration`：灵感与未来提醒。

Legacy 可选单例块，旧日记可继续解析，新整理不再生成：

- `learning`：学习与思考，合并到 `work`。
- `future-notes`：未来提醒，合并到 `inspiration`。
- `gratitude`：感恩，合并到 `relationship`。
```

Update the fixed order list to:

```markdown
1. 原始输入
2. 状态与情绪
3. 昨日回顾
4. 今日重点
5. 工作与学习
6. 生活与关系
7. 健康与精力
8. 财务
9. 灵感与未来提醒
10. 关键词
11. 生成信息
```

In `README.md`, add a short note near the current status section:

```markdown
JMF 分类采用软合并策略：旧日记中的 `learning` / `future-notes` / `gratitude` 继续兼容解析，新 AI 整理和重新整理只使用 `mood`、`work`、`relationship`、`health`、`money`、`inspiration` 等活跃分类。用户可通过单篇重新整理自行选择是否把旧日记转换为新结构。
```

In `docs/agents/PROJECT_CONTEXT.md`, add the same behavior under delivered V1 scope or product boundaries.

In `docs/agents/PRODUCT_INVARIANTS.md`, add:

```markdown
- Legacy optional sections remain readable for old Markdown, but AI/Harness/new block insertion should target active sections only.
- Reorganize-existing is the user-controlled path for converting a day from legacy section distribution to the active section distribution.
```

- [ ] **Step 2: Run markdown checks**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

---

### Task 8: Full Verification And Asset Gate

**Files:**
- Possibly create or update `docs/superpowers/archives/2026-05/...`
- Possibly update `docs/superpowers/archives/INDEX.md`
- Possibly update `docs/superpowers/problems/2026-05/2026-05-14-harness-section-boundary-duplication-problem.md`

- [ ] **Step 1: Run focused backend test suite**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfSectionCatalogTests|JmfMarkdownValidatorTests|JmfMarkdownComposerTests|JournalHarnessPromptTests|JournalHarnessOperationExecutorTests|TodayJournalEditorServiceTests"
```

Expected: PASS.

- [ ] **Step 2: Run focused frontend tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx todayWorkbenchView.test.ts
```

Expected: PASS.

- [ ] **Step 3: Run broad regression checks**

Run:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
git diff --check
```

Expected: PASS. If a broad test fails due to existing unrelated failure, record the exact failure and run the focused tests again after confirming the failure is unrelated.

- [ ] **Step 4: Archive completed requirement**

Use the Superpowers asset-compounding gate. Recommended route is `archive` because this is an accepted product semantics change with implementation and verification evidence.

Archive title suggestion:

```text
JMF Soft Section Consolidation Delivery
```

Archive should link:

- Spec: `docs/superpowers/specs/2026-05-15-jmf-soft-section-consolidation-design.md`
- Plan: `docs/superpowers/plans/2026-05-15-jmf-soft-section-consolidation-implementation-plan.md`
- Related problem: `docs/superpowers/problems/2026-05/2026-05-14-harness-section-boundary-duplication-problem.md`

- [ ] **Step 5: Commit implementation**

Use an English Conventional Commit message:

```powershell
git status --short
git add src tests apps docs README.md PROJECT_VISION.md
git commit -m "feat: consolidate jmf active sections"
```

Expected: one implementation commit after tests and archive updates pass.

---

## Self-Review

- Spec coverage: Covers active-vs-legacy catalog, prompt rules, server-side Harness guard, editor insertions, legacy compatibility, docs, and verification.
- Placeholder scan: No TODO/TBD placeholders; every task has concrete files, tests, commands, and implementation snippets.
- Type consistency: Uses `IsActiveForNewContent`, `ActiveForNewContent`, `ActiveOptionalSingleton`, and `LegacyOptionalSingleton` consistently across tasks.
