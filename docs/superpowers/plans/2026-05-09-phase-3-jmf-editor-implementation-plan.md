# Phase 3 JMF Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Phase 3 safe JMF editing: parse today's Markdown into protected blocks, let users edit content blocks or guarded source, validate JMF structure, save only to reviewing draft, and update the formal entry only after confirmation.

**Architecture:** Keep Markdown as the source of truth. Add small JMF parser, validator, composer, and editor-state contracts around the existing Phase 2 draft/entry stores; extend `TodayJournalService` instead of adding a second workflow. The React desktop app shifts from read-only preview to a centered diary editor with block mode, source mode, insert menu, validation panel, and the existing confirmation action.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, xUnit, Microsoft.AspNetCore.Mvc.Testing, System.Text.Json, React, TypeScript, Vite, Vitest, Testing Library, react-markdown, remark-gfm.

---

## Source Documents

- Spec: `docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-design.md`
- Prototype: `docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-prototype.html`
- Current Phase 2 service: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Current JMF renderer: `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`
- Current desktop shell: `apps/desktop/src/App.tsx`

## File Structure

- Create `src/Journal.Domain/Entries/JmfSectionKind.cs`: required, optional singleton, and system section kinds.
- Create `src/Journal.Domain/Entries/JmfSectionDefinition.cs`: stable section metadata.
- Create `src/Journal.Domain/Entries/JmfSectionCatalog.cs`: known JMF v1 section ids, titles, order, and block-edit permissions.
- Create `src/Journal.Domain/Entries/JmfSection.cs`: parsed editor section content.
- Create `src/Journal.Domain/Entries/JmfDocument.cs`: front matter plus ordered sections.
- Create `src/Journal.Domain/Entries/JmfParseResult.cs`: parser output with document plus structural issues.
- Create `src/Journal.Domain/Entries/JmfValidationIssue.cs`: machine code, user message, and repair hint.
- Create `src/Journal.Domain/Entries/JmfValidationResult.cs`: validation status and issues.
- Create `src/Journal.Domain/Entries/JournalBlockEditSection.cs`: block editor request item.
- Create `src/Journal.Domain/Entries/JournalBlockEditRequest.cs`: block editor save request.
- Create `src/Journal.Domain/Entries/JournalSourceEditRequest.cs`: source editor save request.
- Create `src/Journal.Domain/Entries/TodayEditorState.cs`: API-facing editor aggregate.
- Create `src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs`: parse front matter and section marker pairs.
- Create `src/Journal.Infrastructure/Jmf/JmfMarkdownValidator.cs`: enforce JMF v1 structure.
- Create `src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs`: compose a safe Markdown draft from a parsed document.
- Modify `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`: reuse shared section catalog order and keep renderer output compatible.
- Modify `src/Journal.Infrastructure/Today/TodayJournalService.cs`: add editor read/save workflows.
- Modify `src/Journal.Api/Program.cs`: add three editor endpoints.
- Create `tests/Journal.Tests/JmfSectionCatalogTests.cs`: section catalog invariants.
- Create `tests/Journal.Tests/JmfMarkdownParserTests.cs`: parser behavior and marker failures.
- Create `tests/Journal.Tests/JmfMarkdownValidatorTests.cs`: validation behavior.
- Create `tests/Journal.Tests/JmfMarkdownComposerTests.cs`: ordering, optional skipping, and marker escaping.
- Create `tests/Journal.Tests/TodayJournalEditorServiceTests.cs`: editor workflow tests without HTTP.
- Modify `tests/Journal.Tests/TodayJournalEndpointTests.cs`: HTTP contract tests for editor endpoints.
- Modify `apps/desktop/src/api.ts`: editor request/response types and client functions.
- Create `apps/desktop/src/JournalEditor.tsx`: mode tabs, save actions, and layout composition.
- Create `apps/desktop/src/JournalBlockCard.tsx`: individual block editor card.
- Create `apps/desktop/src/InsertBlockMenu.tsx`: optional singleton insert menu.
- Create `apps/desktop/src/ValidationPanel.tsx`: validation errors and repair hints.
- Modify `apps/desktop/src/App.tsx`: load editor state and wire raw input/confirm/editor saves.
- Modify `apps/desktop/src/App.test.tsx`: update workflow tests around editor state.
- Modify `apps/desktop/src/styles.css`: adapt V3 diary-centered layout from the prototype.
- Modify `README.md`: add Phase 3 endpoints, editing boundary, and verification notes.

## Task 1: Add JMF Editor Domain Contracts

**Files:**
- Create: `src/Journal.Domain/Entries/JmfSectionKind.cs`
- Create: `src/Journal.Domain/Entries/JmfSectionDefinition.cs`
- Create: `src/Journal.Domain/Entries/JmfSectionCatalog.cs`
- Create: `src/Journal.Domain/Entries/JmfSection.cs`
- Create: `src/Journal.Domain/Entries/JmfDocument.cs`
- Create: `src/Journal.Domain/Entries/JmfParseResult.cs`
- Create: `src/Journal.Domain/Entries/JmfValidationIssue.cs`
- Create: `src/Journal.Domain/Entries/JmfValidationResult.cs`
- Create: `src/Journal.Domain/Entries/JournalBlockEditSection.cs`
- Create: `src/Journal.Domain/Entries/JournalBlockEditRequest.cs`
- Create: `src/Journal.Domain/Entries/JournalSourceEditRequest.cs`
- Create: `src/Journal.Domain/Entries/TodayEditorState.cs`
- Test: `tests/Journal.Tests/JmfSectionCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

Create `tests/Journal.Tests/JmfSectionCatalogTests.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Tests;

public sealed class JmfSectionCatalogTests
{
    [Fact]
    public void AllDefinitions_AreOrderedAndUnique()
    {
        var definitions = JmfSectionCatalog.All;

        Assert.Equal(definitions.Count, definitions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(definitions.OrderBy(item => item.Order).Select(item => item.Id), definitions.Select(item => item.Id));
    }

    [Fact]
    public void RequiredAndReadonlyRules_MatchPhase3Spec()
    {
        Assert.Equal(JmfSectionKind.Required, JmfSectionCatalog.Require("raw-inputs").Kind);
        Assert.False(JmfSectionCatalog.Require("raw-inputs").IsEditableInBlockMode);

        Assert.True(JmfSectionCatalog.Require("yesterday-review").IsEditableInBlockMode);
        Assert.True(JmfSectionCatalog.Require("today-focus").IsEditableInBlockMode);
        Assert.Equal(JmfSectionKind.OptionalSingleton, JmfSectionCatalog.Require("inspiration").Kind);
        Assert.Equal(JmfSectionKind.System, JmfSectionCatalog.Require("metadata-note").Kind);
    }

    [Fact]
    public void AvailableOptionalSections_ExcludesExistingSections()
    {
        var available = JmfSectionCatalog.GetAvailableOptionalSections(["mood", "today-focus"]);

        Assert.DoesNotContain(available, item => item.Id == "mood");
        Assert.Contains(available, item => item.Id == "inspiration");
        Assert.DoesNotContain(available, item => item.Kind != JmfSectionKind.OptionalSingleton);
    }
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfSectionCatalogTests
```

Expected: FAIL because `JmfSectionCatalog` and related records do not exist.

- [ ] **Step 3: Add the domain contracts**

Create `src/Journal.Domain/Entries/JmfSectionKind.cs`:

```csharp
namespace Journal.Domain.Entries;

public enum JmfSectionKind
{
    Required,
    OptionalSingleton,
    System
}
```

Create `src/Journal.Domain/Entries/JmfSectionDefinition.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JmfSectionDefinition(
    string Id,
    string Title,
    int Order,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode);
```

Create `src/Journal.Domain/Entries/JmfSectionCatalog.cs` with these ids and titles in order: `raw-inputs`/`原始输入`, `mood`/`情绪状态`, `yesterday-review`/`昨日回顾`, `today-focus`/`今日重点`, `work`/`工作推进`, `learning`/`学习与思考`, `health`/`健康与精力`, `relationship`/`关系与家庭`, `money`/`财务`, `inspiration`/`灵感`, `future-notes`/`未来提醒`, `gratitude`/`感恩`, `keywords`/`关键词`, `metadata-note`/`生成信息`. Required sections are `raw-inputs`, `yesterday-review`, `today-focus`; system sections are `keywords`, `metadata-note`; all other sections are optional singletons. Only `raw-inputs`, `keywords`, and `metadata-note` are not editable in block mode. Expose `All`, `Required`, `OptionalSingleton`, `TryGet(string id, out JmfSectionDefinition definition)`, `Require(string id)`, and `GetAvailableOptionalSections(IEnumerable<string> existingIds)`.

Create `JmfSection.cs`, `JmfDocument.cs`, `JmfParseResult.cs`, `JmfValidationIssue.cs`, `JmfValidationResult.cs`, `JournalBlockEditSection.cs`, `JournalBlockEditRequest.cs`, `JournalSourceEditRequest.cs`, and `TodayEditorState.cs` as small immutable records:

```csharp
public sealed record JmfSection(string Id, string Title, string Content, JmfSectionKind Kind, bool IsEditableInBlockMode);
public sealed record JmfDocument(string FrontMatterText, IReadOnlyDictionary<string, string> FrontMatter, IReadOnlyList<JmfSection> Sections);
public sealed record JmfParseResult(JmfDocument Document, IReadOnlyList<JmfValidationIssue> Issues);
public sealed record JmfValidationIssue(string Code, string Message, string RepairHint);
public sealed record JmfValidationResult(bool IsValid, IReadOnlyList<JmfValidationIssue> Issues)
{
    public static JmfValidationResult Valid { get; } = new(true, Array.Empty<JmfValidationIssue>());
}
public sealed record JournalBlockEditSection(string Id, string Content);
public sealed record JournalBlockEditRequest(IReadOnlyList<JournalBlockEditSection> Sections);
public sealed record JournalSourceEditRequest(string Markdown);
public sealed record TodayEditorState(
    JournalDate Date,
    JournalStatus Status,
    string Markdown,
    IReadOnlyList<JmfSection> Sections,
    IReadOnlyList<JmfSectionDefinition> AvailableOptionalSections,
    JmfValidationResult Validation,
    bool CanConfirm,
    TodayJournalState Today);
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfSectionCatalogTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Journal.Domain/Entries tests/Journal.Tests/JmfSectionCatalogTests.cs
git commit -m "feat: add jmf editor domain contracts"
```

## Task 2: Parse and Validate JMF Markdown

**Files:**
- Create: `src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs`
- Create: `src/Journal.Infrastructure/Jmf/JmfMarkdownValidator.cs`
- Test: `tests/Journal.Tests/JmfMarkdownParserTests.cs`
- Test: `tests/Journal.Tests/JmfMarkdownValidatorTests.cs`

- [ ] **Step 1: Write failing parser tests**

Create `tests/Journal.Tests/JmfMarkdownParserTests.cs` with one valid sample containing front matter plus `raw-inputs`, `yesterday-review`, `today-focus`, and `inspiration`. Assert that parser keeps section content without marker comments, maps titles from the catalog, and extracts `schema` as `journal-entry/v1`.

Also add tests for:

```csharp
[Theory]
[InlineData("<!-- journal:section today-focus -->\n## 今日重点\n- item")]
[InlineData("<!-- journal:section today-focus -->\n## 今日重点\n- item\n<!-- /journal:section raw-inputs -->")]
public void Parse_ReturnsIssueForUnmatchedMarkers(string markdown)
{
    var result = JmfMarkdownParser.Parse(markdown);

    Assert.Contains(result.Issues, issue => issue.Code == "unmatched-section-marker");
}
```

- [ ] **Step 2: Write failing validator tests**

Create `tests/Journal.Tests/JmfMarkdownValidatorTests.cs` covering:

- missing front matter -> `missing-front-matter`
- missing schema -> `missing-schema`
- wrong schema -> `invalid-schema`
- missing `today-focus` -> `missing-required-section`
- unknown section id -> `unknown-section`
- duplicate `inspiration` -> `duplicate-section`
- block edit request containing `raw-inputs` -> `raw-inputs-is-readonly`

- [ ] **Step 3: Run parser and validator tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownParserTests|JmfMarkdownValidatorTests"
```

Expected: FAIL because parser and validator do not exist.

- [ ] **Step 4: Implement parser**

Implement `JmfMarkdownParser.Parse(string markdown)` with:

- front matter detection using `---` on the first line and the next standalone `---`
- simple key extraction for scalar lines like `schema: journal-entry/v1`
- section start marker regex `<!--\s*journal:section\s+(?<id>[a-z0-9-]+)\s*-->`
- section end marker regex `<!--\s*/journal:section\s+(?<id>[a-z0-9-]+)\s*-->`
- content between start and end markers trimmed of one leading section heading when present
- issue creation for missing end marker or mismatched marker id

Use `JmfSectionCatalog.TryGet(id, out definition)` when possible; unknown sections should still be represented as `JmfSection` with title equal to id and kind `System` so the validator can report the exact unknown id.

- [ ] **Step 5: Implement validator**

Implement `JmfMarkdownValidator.Validate(JmfDocument document, IReadOnlyList<JmfValidationIssue>? parseIssues = null)` and `ValidateBlockEditRequest(JournalBlockEditRequest request)`.

Validation rules:

- include parser issues first
- front matter must be present
- front matter must include `schema`
- schema must equal `journal-entry/v1`
- required ids `raw-inputs`, `yesterday-review`, `today-focus` must exist
- every section id must be known
- no section id may appear more than once
- optional singleton ids may appear at most once
- block edit request may not contain `raw-inputs`, `keywords`, or `metadata-note`

Use issue codes from the spec exactly: `missing-front-matter`, `missing-schema`, `invalid-schema`, `missing-required-section`, `unknown-section`, `duplicate-section`, `unmatched-section-marker`, `raw-inputs-is-readonly`.

- [ ] **Step 6: Run focused tests and verify they pass**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownParserTests|JmfMarkdownValidatorTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs src/Journal.Infrastructure/Jmf/JmfMarkdownValidator.cs tests/Journal.Tests/JmfMarkdownParserTests.cs tests/Journal.Tests/JmfMarkdownValidatorTests.cs
git commit -m "feat: parse and validate jmf markdown"
```

## Task 3: Compose Safe JMF Draft Markdown

**Files:**
- Create: `src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs`
- Modify: `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`
- Test: `tests/Journal.Tests/JmfMarkdownComposerTests.cs`
- Modify: `tests/Journal.Tests/MockAiAndJmfTests.cs`

- [ ] **Step 1: Write failing composer tests**

Create `tests/Journal.Tests/JmfMarkdownComposerTests.cs` covering:

- composed output keeps original front matter text
- sections are emitted in `JmfSectionCatalog` order even when input order differs
- empty optional sections are skipped
- required sections are emitted even when content is empty
- marker-like text inside content is escaped so users cannot inject nested JMF markers

The marker injection test should assert:

```csharp
Assert.DoesNotContain("<!-- journal:section money -->", composedContent);
Assert.Contains("&lt;!-- journal:section money --&gt;", composedContent);
```

- [ ] **Step 2: Run composer tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfMarkdownComposerTests
```

Expected: FAIL because `JmfMarkdownComposer` does not exist.

- [ ] **Step 3: Implement composer**

Implement `JmfMarkdownComposer.Compose(JmfDocument document)`:

- append `document.FrontMatterText` exactly once
- normalize line endings to `\n` inside composed Markdown
- sort sections by `JmfSectionCatalog.Require(section.Id).Order`
- skip optional sections with blank content
- never skip required sections
- render each section as:

```markdown
<!-- journal:section today-focus -->
## 今日重点

- content
<!-- /journal:section today-focus -->
```

- escape `<!--` to `&lt;!--` and `-->` to `--&gt;` inside user content before rendering

Keep list/paragraph Markdown text as-is; do not parse Markdown AST.

- [ ] **Step 4: Align Phase 2 renderer with catalog**

Modify `JmfMarkdownRenderer` so its section titles match `JmfSectionCatalog` for `raw-inputs`, `mood`, `yesterday-review`, `today-focus`, and `inspiration`. Keep existing YAML quoting tests passing.

- [ ] **Step 5: Run JMF tests and verify they pass**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownComposerTests|MockAiAndJmfTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs tests/Journal.Tests/JmfMarkdownComposerTests.cs tests/Journal.Tests/MockAiAndJmfTests.cs
git commit -m "feat: compose safe jmf drafts"
```

## Task 4: Add Today Editor Service Workflow

**Files:**
- Modify: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Test: `tests/Journal.Tests/TodayJournalEditorServiceTests.cs`

- [ ] **Step 1: Write failing service workflow tests**

Create `tests/Journal.Tests/TodayJournalEditorServiceTests.cs` with these cases:

- `GetTodayEditorAsync` returns draft sections when a reviewing draft exists.
- `GetTodayEditorAsync` falls back to entry when no draft exists.
- `SaveBlockDraftAsync` preserves `raw-inputs` from the baseline and updates `today-focus`.
- `SaveBlockDraftAsync` returns attention when request contains `raw-inputs`.
- `SaveBlockDraftAsync` composes optional blocks in fixed order.
- `SaveSourceDraftAsync` writes reviewing draft for valid Markdown.
- `SaveSourceDraftAsync` writes attention draft for invalid Markdown and does not overwrite entry.

The raw-input preservation assertion should compare the baseline raw section content with the saved draft raw section content after parsing the saved draft.

- [ ] **Step 2: Run service tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEditorServiceTests
```

Expected: FAIL because the new service methods do not exist.

- [ ] **Step 3: Implement editor baseline helpers**

Inside `TodayJournalService`, add a private helper:

```csharp
private async Task<(JournalDate Date, string Markdown, JournalDraft? Draft, JournalEntry? Entry)> ReadEditorBaselineAsync(CancellationToken cancellationToken)
```

Priority:

1. today's draft markdown
2. today's entry markdown
3. empty markdown

For empty markdown, `GetTodayEditorAsync` returns an empty editor state with no sections and validation invalid with `missing-front-matter`; `SaveBlockDraftAsync` should throw `InvalidOperationException("editor baseline does not exist.")` because block editing must preserve existing `raw-inputs`.

- [ ] **Step 4: Implement `GetTodayEditorAsync`**

Use parser and validator against the baseline. Return:

- `Markdown`: baseline markdown
- `Sections`: parsed sections
- `AvailableOptionalSections`: catalog optional sections not already present
- `Validation`: validator result
- `CanConfirm`: `status == JournalStatus.Reviewing && validation.IsValid`
- `Today`: current `TodayJournalState`

- [ ] **Step 5: Implement `SaveBlockDraftAsync`**

Flow:

1. read baseline draft/entry
2. reject empty baseline with `InvalidOperationException`
3. parse and validate baseline
4. validate request using `ValidateBlockEditRequest`
5. merge editable request sections into baseline sections by id
6. ignore absent optional sections; include newly requested optional sections if known and non-system
7. preserve `raw-inputs`, `keywords`, and `metadata-note` from baseline
8. compose markdown
9. validate composed markdown
10. on success write `JournalDraft` with `JournalStatus.Reviewing`
11. on failure write `JournalDraft` with `JournalStatus.Attention` and issue messages
12. return `GetTodayEditorAsync`

- [ ] **Step 6: Implement `SaveSourceDraftAsync`**

Flow:

1. reject blank markdown with `ArgumentException("markdown is required", nameof(request.Markdown))`
2. parse and validate source markdown
3. if valid, write reviewing draft with the source markdown
4. if invalid, write attention draft with the source markdown and issue messages
5. never call `_entryStore.WriteAsync`
6. return `GetTodayEditorAsync`

- [ ] **Step 7: Run focused service tests and verify they pass**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEditorServiceTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add src/Journal.Infrastructure/Today/TodayJournalService.cs tests/Journal.Tests/TodayJournalEditorServiceTests.cs
git commit -m "feat: add today jmf editor workflow"
```

## Task 5: Expose Editor API Endpoints

**Files:**
- Modify: `src/Journal.Api/Program.cs`
- Modify: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Add endpoint tests for:

- `GET /journal/today/editor` returns `sections`, `availableOptionalSections`, `validation`, `canConfirm`, and nested `today`
- `PUT /journal/today/editor/blocks` updates `today-focus` and returns reviewing editor state
- `PUT /journal/today/editor/blocks` with `raw-inputs` returns attention editor state, not a formal entry overwrite
- `PUT /journal/today/editor/source` with invalid marker returns attention editor state with `unmatched-section-marker`
- `PUT /journal/today/editor/source` with blank markdown returns `400`

- [ ] **Step 2: Run endpoint tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEndpointTests
```

Expected: FAIL because endpoints do not exist.

- [ ] **Step 3: Add endpoints in `Program.cs`**

Add:

```csharp
app.MapGet("/journal/today/editor", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    var state = await service.GetTodayEditorAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPut("/journal/today/editor/blocks", async (
    JournalBlockEditRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var state = await service.SaveBlockDraftAsync(request, cancellationToken);
        return Results.Ok(state);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPut("/journal/today/editor/source", async (
    JournalSourceEditRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Markdown))
    {
        return Results.BadRequest(new { error = "markdown is required" });
    }

    var state = await service.SaveSourceDraftAsync(request, cancellationToken);
    return Results.Ok(state);
});
```

Add `using Journal.Domain.Entries;` to `Program.cs` if needed.

- [ ] **Step 4: Run endpoint tests and full backend tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj
dotnet test Journal.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Journal.Api/Program.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose jmf editor endpoints"
```

## Task 6: Add Frontend Editor API Client and Components

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Create: `apps/desktop/src/JournalEditor.tsx`
- Create: `apps/desktop/src/JournalBlockCard.tsx`
- Create: `apps/desktop/src/InsertBlockMenu.tsx`
- Create: `apps/desktop/src/ValidationPanel.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Write failing frontend API/component tests**

Update `App.test.tsx` fixture shape to include `TodayEditorState`. Add tests that verify:

- initial load calls `/health` and `/journal/today/editor`
- block mode shows editable `今日重点`
- `raw-inputs` text is visible but has no textarea
- clicking an available optional block inserts it into the page
- saving blocks calls `PUT /journal/today/editor/blocks`
- source mode save calls `PUT /journal/today/editor/source`
- attention state shows validation issue and hides confirm button

- [ ] **Step 2: Run frontend tests and verify they fail**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: FAIL because editor types and components do not exist.

- [ ] **Step 3: Extend `api.ts`**

Add TypeScript types matching the backend records:

```ts
export type JmfSectionKind = "required" | "optionalSingleton" | "system";
export type JmfSectionDefinition = { id: string; title: string; order: number; kind: JmfSectionKind; isEditableInBlockMode: boolean; };
export type JmfSection = { id: string; title: string; content: string; kind: JmfSectionKind; isEditableInBlockMode: boolean; };
export type JmfValidationIssue = { code: string; message: string; repairHint: string; };
export type JmfValidationResult = { isValid: boolean; issues: JmfValidationIssue[]; };
export type TodayEditorState = {
  date: JournalDate;
  status: JournalStatus;
  markdown: string;
  sections: JmfSection[];
  availableOptionalSections: JmfSectionDefinition[];
  validation: JmfValidationResult;
  canConfirm: boolean;
  today: TodayJournalState;
};
export type JournalBlockEditSection = { id: string; content: string; };
```

Add client functions:

```ts
export function getTodayEditor(): Promise<TodayEditorState>;
export function saveBlockDraft(sections: JournalBlockEditSection[]): Promise<TodayEditorState>;
export function saveSourceDraft(markdown: string): Promise<TodayEditorState>;
```

- [ ] **Step 4: Add block/source editor components**

Implement:

- `JournalBlockCard`: renders readonly text for non-editable sections and textarea for editable sections.
- `InsertBlockMenu`: renders buttons for `availableOptionalSections`; clicking emits an empty local section.
- `ValidationPanel`: renders validation issues as message plus repair hint.
- `JournalEditor`: owns local block/source editing state, mode tabs, save buttons, and calls `onSaveBlocks` or `onSaveSource`.

Component behavior:

- default mode is `blocks`
- source mode textarea starts from `editor.markdown`
- block mode local edits reset when `editor.date.isoDate` or `editor.markdown` changes
- inserted optional section appears in catalog order after the next save response; before save it may be sorted client-side by `order`
- confirm button remains in `App.tsx` because it is shared with Phase 2 status flow

- [ ] **Step 5: Run component tests and verify they pass**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/JournalEditor.tsx apps/desktop/src/JournalBlockCard.tsx apps/desktop/src/InsertBlockMenu.tsx apps/desktop/src/ValidationPanel.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: add jmf editor frontend components"
```

## Task 7: Integrate Editor UI Into the Today Workbench

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/styles.css`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Write failing integration tests**

Update existing App tests so:

- raw input submission refreshes editor state after `POST /journal/today/inputs`
- confirmation refreshes editor state after `POST /journal/today/draft/confirm`
- processed entry path is still visible after confirmation
- stale request guard still prevents older responses from replacing newer editor state

- [ ] **Step 2: Run frontend tests and verify they fail**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: FAIL until `App.tsx` is rewired to `TodayEditorState`.

- [ ] **Step 3: Rewire `App.tsx`**

Replace `today` state with `editor` state:

- initial load: `Promise.all([getHealth(), getTodayEditor()])`
- derived `today`: `editor?.today`
- raw input submit: call `addTodayInput`, then call `getTodayEditor` and store editor state
- confirm: call `confirmTodayDraft`, then call `getTodayEditor` and store editor state
- block save: call `saveBlockDraft`
- source save: call `saveSourceDraft`

Keep existing `requestIdRef` stale-response guard for every async workflow.

- [ ] **Step 4: Apply prototype-inspired layout**

Update `styles.css` to match the approved Phase 3 prototype:

- center diary/editor as the primary column
- keep raw input/history as context rail
- keep insert/save/validation tools as right dock
- make the layout fit a desktop window around 1280x760 without important controls falling below the fold
- use softer neutral colors, strong text contrast, and stable textarea/card dimensions
- avoid nested cards and decorative gradient blobs

- [ ] **Step 5: Run frontend tests and build**

Run:

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/styles.css apps/desktop/src/App.test.tsx
git commit -m "feat: integrate jmf editor workbench"
```

## Task 8: Documentation and End-to-End Verification

**Files:**
- Modify: `README.md`
- Optional modify: `docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-design.md` only if implementation reveals a spec correction.

- [ ] **Step 1: Update README**

Add Phase 3 section:

- endpoints `GET /journal/today/editor`, `PUT /journal/today/editor/blocks`, `PUT /journal/today/editor/source`
- block mode protects `raw-inputs`
- source mode is guarded by validation
- successful edits save reviewing draft only
- confirmation is still required before formal entry update

- [ ] **Step 2: Run full verification**

Run:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: all PASS.

- [ ] **Step 3: Manual smoke test**

Run API and desktop in separate terminals:

```powershell
dotnet run --project src/Journal.Api
npm run desktop --prefix apps/desktop
```

Verify:

1. create a draft from raw input
2. edit `今日重点` in block mode
3. save as draft
4. confirm formal entry is not updated until clicking confirm
5. click confirm and inspect `%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md`
6. switch source mode and break a marker
7. save source and confirm `attention` appears
8. confirm formal entry did not change after invalid source save

- [ ] **Step 4: Commit**

```powershell
git add README.md docs/superpowers/specs/2026-05-09-phase-3-jmf-editor-design.md
git commit -m "docs: document phase 3 editor workflow"
```

## Final Review Checklist

- [ ] Every Phase 3 spec goal maps to a task above.
- [ ] `raw-inputs` is readonly in block mode and preserved by the service.
- [ ] Optional section insertion is limited to known JMF v1 optional singleton sections.
- [ ] Source mode never writes directly to `entries/`.
- [ ] Validation failure writes `attention` draft and preserves formal entry.
- [ ] Existing Phase 2 confirm flow still decides `processed` vs `updated`.
- [ ] `dotnet test Journal.slnx` passes.
- [ ] `npm test --prefix apps/desktop` passes.
- [ ] `npm run build --prefix apps/desktop` passes.

## Execution Options

After this plan is accepted:

1. Subagent-Driven: use `superpowers:subagent-driven-development`, dispatch one fresh worker per task or backend/frontend slice, and review after each task.
2. Inline Execution: use `superpowers:executing-plans`, execute tasks in this session with checkpoints.

Recommended: Subagent-Driven, because backend parser/validator/composer and frontend editor integration can be reviewed as separate, low-conflict slices.
