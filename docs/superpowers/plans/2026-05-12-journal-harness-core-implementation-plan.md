# Journal Harness Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Journal LLM Harness Core slice so the LLM can use controlled Agent Framework tools to append/create/revise draft sections, preserve user content, record provenance, and expose daily audit records.

**Architecture:** The LLM planner is built on the existing OpenAI-compatible Microsoft Agent Framework runtime, but all exposed tools are side-effect-free collector tools. Tool arguments are treated as untrusted input, then a server-side JMF operation executor validates provenance, applies changes to a draft, and writes audit records. Formal entries remain writable only through the existing user confirmation path. Harness execution uses a run record plus SSE stream: the submit endpoint quickly persists the raw input and returns a run id, while the SSE execution path emits progress and completes draft/audit updates.

**Tech Stack:** .NET 10, ASP.NET Core minimal API, xUnit, Microsoft Agent Framework `Microsoft.Agents.AI` / `Microsoft.Agents.AI.OpenAI` 1.5.0, `Microsoft.Extensions.AI`, OpenAI .NET SDK 2.10.0, React + TypeScript + Vite + Vitest.

---

## Evidence Baseline

Use these facts while implementing. Re-check if packages change.

- Local project references `Microsoft.Agents.AI` 1.5.0, `Microsoft.Agents.AI.OpenAI` 1.5.0, and `OpenAI` 2.10.0 in `src/Journal.Infrastructure/Journal.Infrastructure.csproj`.
- Existing runtime already uses `OpenAI.Chat.ChatClient`, `chatClient.AsAIAgent(...)`, and `ChatClientAgentRunOptions(new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = ..., MaxOutputTokens = ... })`.
- Official Microsoft docs say `ChatClientAgent` supports structured outputs through `AgentRunOptions.ResponseFormat`; `ChatResponseFormat.Json` requests a JSON object, while `ChatResponseFormat.ForJsonSchema<T>()` can request schema-shaped output.
- Official OpenAI Agents docs for Microsoft Agent Framework show C# `ChatClient.AsAIAgent(...)` as the simple Chat Completion agent path, and list function tools as supported.
- Microsoft Agent Framework package XML explicitly warns that provided tools are invoked without user approval by default, and function arguments must be treated as untrusted input. This plan therefore exposes only side-effect-free collector tools to the model; real draft writes happen after deterministic server validation.
- ASP.NET Core 10 provides `TypedResults.ServerSentEvents(...)` and `System.Net.ServerSentEvents.SseItem<T>` for Server-Sent Events. This plan uses that built-in result API for live harness progress and keeps run records as the durable recovery/audit source.

Primary references used while writing this plan:

- `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`
- `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`
- `docs/superpowers/specs/2026-05-12-journal-harness-core-design.md`
- Microsoft Learn: `https://learn.microsoft.com/agent-framework/agents/structured-outputs`
- Microsoft Learn: `https://learn.microsoft.com/agent-framework/agents/providers/openai`
- Microsoft Learn: `https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#iresult-return-values`
- Microsoft Learn: `https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.typedresults.serversentevents?view=aspnetcore-10.0`
- GitHub sample family: `https://github.com/microsoft/agent-framework/tree/main/dotnet/samples`

---

## File Structure

### Domain

- Create `src/Journal.Domain/Entries/JmfSectionProvenance.cs`
  - Defines section provenance values and compatibility defaults.
- Create `src/Journal.Domain/Entries/JmfSectionWithProvenance.cs`
  - Not required as a separate type; prefer adding provenance to existing `JmfSection` if local edits stay simple.
- Modify `src/Journal.Domain/Entries/JmfSection.cs`
  - Add `JmfSectionProvenance Provenance`.
- Create `src/Journal.Domain/Entries/JournalHarnessOperation.cs`
  - Defines deterministic operation records: append, upsert, revise AI-generated section, no-op.
- Create `src/Journal.Domain/Entries/JournalHarnessAudit.cs`
  - Defines run and tool-call audit records for API responses and storage.

### Infrastructure: JMF

- Modify `src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs`
  - Parse section marker attributes into provenance.
- Modify `src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs`
  - Compose provenance attributes back into section markers.
- Modify `src/Journal.Infrastructure/Jmf/JmfMarkdownValidator.cs`
  - Add provenance diagnostics without breaking old documents.

### Infrastructure: Harness

- Create `src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs`
  - Builds system instructions, protected context, and current user message.
- Create `src/Journal.Infrastructure/Harness/JournalHarnessPlanner.cs`
  - Calls Agent Framework runtime with collector tools.
- Create `src/Journal.Infrastructure/Harness/JournalHarnessToolCollector.cs`
  - Side-effect-free functions exposed as AI tools.
- Create `src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs`
  - Validates and applies operations to JMF draft only.
- Create `src/Journal.Infrastructure/Harness/JournalHarnessAuditStore.cs`
  - Writes and reads per-run audit JSON files by date.
- Create `src/Journal.Infrastructure/Harness/JournalHarnessService.cs`
  - Creates run records, appends raw input, and provides execution methods for the SSE path.

### Existing Services and API

- Modify `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`
  - Add a generic harness run method, leaving existing JSON generation path stable.
- Modify `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`
  - Add Agent Framework tool execution support using `ChatClientAgentOptions.ChatOptions.Tools`.
- Modify `src/Journal.Infrastructure/Today/TodayJournalService.cs`
  - Keep current generation path; add harness-oriented entry point or delegate to `JournalHarnessService`.
- Modify `src/Journal.Api/Program.cs`
  - Add `POST /journal/today/harness/runs`, `GET /journal/harness/runs/{runId}`, `GET /journal/harness/runs/{runId}/events`, and `GET /journal/audit`.

### Frontend

- Modify `apps/desktop/src/api.ts`
  - Add harness/audit contracts, run creation, run status query, and SSE event handling.
- Modify `apps/desktop/src/App.tsx`
  - Add workbench mode `today | audit`; add Today Assistant `查看审计` entry; add `返回今日`.
- Create `apps/desktop/src/AuditWorkbench.tsx`
  - Renders three-zone audit workbench inside current shell.
- Modify `apps/desktop/src/styles.css`
  - Add audit workbench styles using existing command workspace visual language.
- Modify `apps/desktop/src/App.test.tsx`
  - Add UI workflow coverage.

### Tests

- Modify `tests/Journal.Tests/JmfMarkdownParserTests.cs`
- Modify `tests/Journal.Tests/JmfMarkdownComposerTests.cs`
- Create `tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs`
- Create `tests/Journal.Tests/JournalHarnessPromptTests.cs`
- Create `tests/Journal.Tests/JournalHarnessServiceTests.cs`
- Modify `tests/Journal.Tests/TodayJournalEndpointTests.cs`

---

## Task 1: Add Section Provenance to Domain and JMF Parser

**Files:**
- Create: `src/Journal.Domain/Entries/JmfSectionProvenance.cs`
- Modify: `src/Journal.Domain/Entries/JmfSection.cs`
- Modify: `src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs`
- Test: `tests/Journal.Tests/JmfMarkdownParserTests.cs`

- [ ] **Step 1: Write failing parser tests for provenance attributes**

Add tests to `tests/Journal.Tests/JmfMarkdownParserTests.cs`:

```csharp
[Fact]
public void Parse_ReadsSectionProvenanceAttributes()
{
    const string markdown = """
        ---
        schema: journal-entry/v1
        date: "2026-05-12"
        ---

        <!-- journal:section today-focus origin="mixed" created_by="ai" last_touched_by="ai" last_operation="append" based_on_raw_inputs="raw-1 raw-2" -->
        ## 今日重点

        - 推进 harness 设计

        <!-- /journal:section today-focus -->

        <!-- journal:section raw-inputs -->
        ## 原始输入

        - 用户原话

        <!-- /journal:section raw-inputs -->

        <!-- journal:section yesterday-review -->
        ## 昨日回顾

        <!-- /journal:section yesterday-review -->
        """;

    var result = JmfMarkdownParser.Parse(markdown);
    var todayFocus = result.Document.Sections.Single(section => section.Id == "today-focus");

    Assert.Equal("mixed", todayFocus.Provenance.Origin);
    Assert.Equal("ai", todayFocus.Provenance.CreatedBy);
    Assert.Equal("ai", todayFocus.Provenance.LastTouchedBy);
    Assert.Equal("append", todayFocus.Provenance.LastOperation);
    Assert.Equal(["raw-1", "raw-2"], todayFocus.Provenance.BasedOnRawInputIds);
}

[Fact]
public void Parse_DefaultsMissingProvenanceToUnknown()
{
    const string markdown = """
        ---
        schema: journal-entry/v1
        ---

        <!-- journal:section raw-inputs -->
        ## 原始输入
        <!-- /journal:section raw-inputs -->

        <!-- journal:section yesterday-review -->
        ## 昨日回顾
        <!-- /journal:section yesterday-review -->

        <!-- journal:section today-focus -->
        ## 今日重点
        <!-- /journal:section today-focus -->
        """;

    var result = JmfMarkdownParser.Parse(markdown);

    Assert.All(result.Document.Sections, section =>
    {
        Assert.Equal("unknown", section.Provenance.Origin);
        Assert.Equal("unknown", section.Provenance.CreatedBy);
        Assert.Equal("unknown", section.Provenance.LastTouchedBy);
        Assert.Equal("unknown", section.Provenance.LastOperation);
        Assert.Empty(section.Provenance.BasedOnRawInputIds);
    });
}
```

- [ ] **Step 2: Run parser tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfMarkdownParserTests
```

Expected: compile fails because `JmfSection.Provenance` and `JmfSectionProvenance` do not exist.

- [ ] **Step 3: Add provenance record**

Create `src/Journal.Domain/Entries/JmfSectionProvenance.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JmfSectionProvenance(
    string Origin,
    string CreatedBy,
    string LastTouchedBy,
    string LastOperation,
    IReadOnlyList<string> BasedOnRawInputIds)
{
    public static JmfSectionProvenance Unknown { get; } =
        new("unknown", "unknown", "unknown", "unknown", Array.Empty<string>());

    public JmfSectionProvenance WithAiAppend(IReadOnlyList<string> rawInputIds) =>
        this with
        {
            Origin = string.Equals(Origin, "user", StringComparison.Ordinal)
                || string.Equals(Origin, "mixed", StringComparison.Ordinal)
                ? "mixed"
                : "ai",
            LastTouchedBy = "ai",
            LastOperation = "append",
            BasedOnRawInputIds = rawInputIds
        };

    public JmfSectionProvenance WithUserEdit() =>
        this with
        {
            Origin = string.Equals(Origin, "ai", StringComparison.Ordinal) ? "mixed" : Origin,
            LastTouchedBy = "user",
            LastOperation = "edit"
        };
}
```

Modify `src/Journal.Domain/Entries/JmfSection.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JmfSection(
    string Id,
    string Title,
    string Content,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode,
    JmfSectionProvenance Provenance)
{
    public JmfSection(
        string id,
        string title,
        string content,
        JmfSectionKind kind,
        bool isEditableInBlockMode)
        : this(id, title, content, kind, isEditableInBlockMode, JmfSectionProvenance.Unknown)
    {
    }
}
```

- [ ] **Step 4: Parse section marker attributes**

In `JmfMarkdownParser.CreateSection`, construct `JmfSection` with parsed provenance. Add a helper near the section parsing code:

```csharp
private static JmfSectionProvenance ParseProvenance(string markerLine)
{
    static string ReadAttribute(string marker, string name)
    {
        var pattern = $"{name}=\"";
        var start = marker.IndexOf(pattern, StringComparison.Ordinal);
        if (start < 0)
        {
            return "unknown";
        }

        start += pattern.Length;
        var end = marker.IndexOf('"', start);
        return end > start ? marker[start..end] : "unknown";
    }

    var rawInputIds = ReadAttribute(markerLine, "based_on_raw_inputs")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return new JmfSectionProvenance(
        ReadAttribute(markerLine, "origin"),
        ReadAttribute(markerLine, "created_by"),
        ReadAttribute(markerLine, "last_touched_by"),
        ReadAttribute(markerLine, "last_operation"),
        rawInputIds);
}
```

Adjust `ParseSections` so each parsed section carries the opening marker line into `CreateSection(id, content, markerLine)`.

- [ ] **Step 5: Run parser tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JmfMarkdownParserTests
```

Expected: parser tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Domain/Entries/JmfSectionProvenance.cs src/Journal.Domain/Entries/JmfSection.cs src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs tests/Journal.Tests/JmfMarkdownParserTests.cs
git commit -m "feat: parse jmf section provenance"
```

---

## Task 2: Compose Provenance and Preserve User Edits

**Files:**
- Modify: `src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs`
- Modify: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Test: `tests/Journal.Tests/JmfMarkdownComposerTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEditorServiceTests.cs`

- [ ] **Step 1: Write failing composer test**

Add to `tests/Journal.Tests/JmfMarkdownComposerTests.cs`:

```csharp
[Fact]
public void Compose_WritesSectionProvenanceAttributes()
{
    var document = new JmfDocument(
        """
        schema: journal-entry/v1
        date: "2026-05-12"
        """,
        new Dictionary<string, string> { ["schema"] = "journal-entry/v1" },
        [
            new JmfSection(
                "raw-inputs",
                "原始输入",
                "- 用户原话",
                JmfSectionKind.Required,
                false,
                JmfSectionProvenance.Unknown),
            new JmfSection(
                "today-focus",
                "今日重点",
                "- 推进 harness",
                JmfSectionKind.Required,
                true,
                new JmfSectionProvenance("mixed", "ai", "ai", "append", ["raw-1"]))
        ]);

    var markdown = JmfMarkdownComposer.Compose(document);

    Assert.Contains("<!-- journal:section today-focus origin=\"mixed\" created_by=\"ai\" last_touched_by=\"ai\" last_operation=\"append\" based_on_raw_inputs=\"raw-1\" -->", markdown);
}
```

- [ ] **Step 2: Write failing user edit provenance test**

Add to `tests/Journal.Tests/TodayJournalEditorServiceTests.cs`:

```csharp
[Fact]
public async Task SaveBlockDraftAsync_MarksEditedSectionAsUserTouched()
{
    using var workspace = TempWorkspace.Create();
    var paths = CreatePaths(workspace.Root);
    var service = CreateService(paths);
    await service.AddInputAsync("今天验证 provenance #Journal", "text", CancellationToken.None);

    var editor = await service.SaveBlockDraftAsync(
        new JournalBlockEditRequest([new("today-focus", "- 用户手动编辑")]),
        CancellationToken.None);

    var section = GetSection(editor.Markdown, "today-focus");
    Assert.Equal("user", section.Provenance.LastTouchedBy);
    Assert.Equal("edit", section.Provenance.LastOperation);
}
```

- [ ] **Step 3: Run focused tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownComposerTests|TodayJournalEditorServiceTests"
```

Expected: composer does not output provenance and block saves do not mark user edits yet.

- [ ] **Step 4: Implement composer attributes**

In `JmfMarkdownComposer.AppendSection`, write:

```csharp
private static void AppendSection(StringBuilder builder, JmfSection section)
{
    var title = JmfSectionCatalog.TryGet(section.Id, out var definition)
        ? definition.Title
        : section.Title;
    builder.Append("<!-- journal:section ").Append(section.Id);
    AppendProvenanceAttributes(builder, section.Provenance);
    builder.AppendLine(" -->");
    builder.Append("## ").AppendLine(title);
    builder.AppendLine();
    builder.AppendLine(section.Content.Trim());
    builder.Append("<!-- /journal:section ").Append(section.Id).AppendLine(" -->");
    builder.AppendLine();
}

private static void AppendProvenanceAttributes(StringBuilder builder, JmfSectionProvenance provenance)
{
    if (provenance == JmfSectionProvenance.Unknown)
    {
        return;
    }

    builder.Append(" origin=\"").Append(provenance.Origin).Append('"');
    builder.Append(" created_by=\"").Append(provenance.CreatedBy).Append('"');
    builder.Append(" last_touched_by=\"").Append(provenance.LastTouchedBy).Append('"');
    builder.Append(" last_operation=\"").Append(provenance.LastOperation).Append('"');
    if (provenance.BasedOnRawInputIds.Count > 0)
    {
        builder.Append(" based_on_raw_inputs=\"")
            .Append(string.Join(' ', provenance.BasedOnRawInputIds))
            .Append('"');
    }
}
```

- [ ] **Step 5: Mark block edits as user touched**

In `TodayJournalService.MergeBlockSections`, when constructing replacement section for a user block edit:

```csharp
var existing = indexes.TryGetValue(definition.Id, out var existingIndex)
    ? merged[existingIndex]
    : null;

var section = new JmfSection(
    definition.Id,
    definition.Title,
    requestSection.Content,
    definition.Kind,
    definition.IsEditableInBlockMode,
    (existing?.Provenance ?? JmfSectionProvenance.Unknown).WithUserEdit());
```

Then use `existingIndex` instead of a second dictionary lookup.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownComposerTests|TodayJournalEditorServiceTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs src/Journal.Infrastructure/Today/TodayJournalService.cs tests/Journal.Tests/JmfMarkdownComposerTests.cs tests/Journal.Tests/TodayJournalEditorServiceTests.cs
git commit -m "feat: compose jmf provenance"
```

---

## Task 3: Add Harness Operation Executor

**Files:**
- Create: `src/Journal.Domain/Entries/JournalHarnessOperation.cs`
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs`
- Test: `tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs`

- [ ] **Step 1: Write failing operation executor tests**

Create `tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class JournalHarnessOperationExecutorTests
{
    [Fact]
    public void Apply_AppendsToUserTouchedSectionWithoutReplacingContent()
    {
        var document = CreateDocument(new JmfSectionProvenance("user", "user", "user", "edit", []));
        var operation = JournalHarnessOperation.Append(
            "today-focus",
            "- AI 追加内容",
            ["raw-2"],
            "用户要求补充 harness 信息。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation]);

        Assert.True(result.Validation.IsValid);
        var section = result.Document.Sections.Single(item => item.Id == "today-focus");
        Assert.Contains("- 用户原内容", section.Content, StringComparison.Ordinal);
        Assert.Contains("- AI 追加内容", section.Content, StringComparison.Ordinal);
        Assert.Equal("mixed", section.Provenance.Origin);
        Assert.Equal("ai", section.Provenance.LastTouchedBy);
        Assert.Equal("append", section.Provenance.LastOperation);
    }

    [Fact]
    public void Apply_RejectsReviseWhenSectionWasTouchedByUser()
    {
        var document = CreateDocument(new JmfSectionProvenance("mixed", "ai", "user", "edit", []));
        var operation = JournalHarnessOperation.ReviseAiGeneratedSection(
            "today-focus",
            "- AI 替换内容",
            ["raw-2"],
            "尝试替换。");

        var result = JournalHarnessOperationExecutor.Apply(document, [operation]);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "harness-revise-user-section");
        Assert.Contains("- 用户原内容", result.Document.Sections.Single(item => item.Id == "today-focus").Content, StringComparison.Ordinal);
    }

    private static JmfDocument CreateDocument(JmfSectionProvenance provenance) =>
        new(
            """
            schema: journal-entry/v1
            date: "2026-05-12"
            """,
            new Dictionary<string, string> { ["schema"] = "journal-entry/v1" },
            [
                new JmfSection("raw-inputs", "原始输入", "- 用户原话", JmfSectionKind.Required, false),
                new JmfSection("yesterday-review", "昨日回顾", "", JmfSectionKind.Required, true),
                new JmfSection("today-focus", "今日重点", "- 用户原内容", JmfSectionKind.Required, true, provenance)
            ]);
}
```

- [ ] **Step 2: Run executor tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessOperationExecutorTests
```

Expected: compile fails because harness operation types do not exist.

- [ ] **Step 3: Add operation records**

Create `src/Journal.Domain/Entries/JournalHarnessOperation.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalHarnessOperation(
    string Kind,
    string TargetSectionId,
    string Content,
    IReadOnlyList<string> BasedOnRawInputIds,
    string Reason)
{
    public static JournalHarnessOperation Append(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("append", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation Upsert(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("upsert", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation ReviseAiGeneratedSection(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("revise-ai-generated-section", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation NoOp(string reason) =>
        new("no-op", "", "", Array.Empty<string>(), reason);
}
```

- [ ] **Step 4: Add executor**

Create `src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessExecutionResult(
    JmfDocument Document,
    JmfValidationResult Validation,
    IReadOnlyList<JmfValidationIssue> Issues);

public static class JournalHarnessOperationExecutor
{
    public static JournalHarnessExecutionResult Apply(
        JmfDocument document,
        IReadOnlyList<JournalHarnessOperation> operations)
    {
        var sections = document.Sections.ToList();
        var issues = new List<JmfValidationIssue>();

        foreach (var operation in operations)
        {
            if (operation.Kind == "no-op")
            {
                continue;
            }

            if (!JmfSectionCatalog.TryGet(operation.TargetSectionId, out var definition)
                || !definition.IsEditableInBlockMode
                || definition.Kind == JmfSectionKind.System
                || string.Equals(definition.Id, "raw-inputs", StringComparison.Ordinal))
            {
                issues.Add(CreateIssue("harness-target-readonly", $"Section '{operation.TargetSectionId}' cannot be edited by harness."));
                continue;
            }

            var index = sections.FindIndex(section => string.Equals(section.Id, operation.TargetSectionId, StringComparison.Ordinal));
            if (operation.Kind == "upsert" && index < 0)
            {
                sections.Add(new JmfSection(
                    definition.Id,
                    definition.Title,
                    operation.Content.Trim(),
                    definition.Kind,
                    definition.IsEditableInBlockMode,
                    new JmfSectionProvenance("ai", "ai", "ai", "create", operation.BasedOnRawInputIds)));
                continue;
            }

            if (index < 0)
            {
                issues.Add(CreateIssue("harness-target-missing", $"Section '{operation.TargetSectionId}' does not exist."));
                continue;
            }

            var existing = sections[index];
            if (operation.Kind == "revise-ai-generated-section")
            {
                if (!CanReviseAiSection(existing.Provenance))
                {
                    issues.Add(CreateIssue("harness-revise-user-section", $"Section '{operation.TargetSectionId}' is not a pure AI section."));
                    continue;
                }

                sections[index] = existing with
                {
                    Content = operation.Content.Trim(),
                    Provenance = existing.Provenance with
                    {
                        LastTouchedBy = "ai",
                        LastOperation = "revise",
                        BasedOnRawInputIds = operation.BasedOnRawInputIds
                    }
                };
                continue;
            }

            if (operation.Kind is "append" or "upsert")
            {
                sections[index] = existing with
                {
                    Content = AppendContent(existing.Content, operation.Content),
                    Provenance = existing.Provenance.WithAiAppend(operation.BasedOnRawInputIds)
                };
                continue;
            }

            issues.Add(CreateIssue("harness-unknown-operation", $"Operation '{operation.Kind}' is not supported."));
        }

        var nextDocument = document with { Sections = sections };
        var validation = JmfMarkdownValidator.Validate(nextDocument, issues);
        return new JournalHarnessExecutionResult(nextDocument, validation, validation.Issues);
    }

    private static bool CanReviseAiSection(JmfSectionProvenance provenance) =>
        string.Equals(provenance.Origin, "ai", StringComparison.Ordinal)
        && string.Equals(provenance.CreatedBy, "ai", StringComparison.Ordinal)
        && !string.Equals(provenance.LastTouchedBy, "user", StringComparison.Ordinal);

    private static string AppendContent(string existing, string append)
    {
        var trimmedExisting = existing.TrimEnd();
        var trimmedAppend = append.Trim();
        return string.IsNullOrWhiteSpace(trimmedExisting)
            ? trimmedAppend
            : $"{trimmedExisting}{Environment.NewLine}{trimmedAppend}";
    }

    private static JmfValidationIssue CreateIssue(string code, string message) =>
        new(code, message, "Review the harness tool call and retry with an allowed operation.");
}
```

- [ ] **Step 5: Run executor tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessOperationExecutorTests
```

Expected: tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalHarnessOperation.cs src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs
git commit -m "feat: add harness operation executor"
```

---

## Task 4: Add Prompt Context Split

**Files:**
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs`
- Test: `tests/Journal.Tests/JournalHarnessPromptTests.cs`

- [ ] **Step 1: Write failing prompt tests**

Create `tests/Journal.Tests/JournalHarnessPromptTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPromptTests
{
    [Fact]
    public void Build_SplitsHistoricalRawInputsFromCurrentUserMessage()
    {
        var date = new JournalDate("2026-05-12", "05-12", "2026", "05", "2026-05-12.md");
        var historicalInputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-12T08:00:00+08:00"), "text", "历史材料"),
            new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-12T08:10:00+08:00"), "text", "上一条材料")
        };
        var currentInput = new RawInput("raw-3", date, DateTimeOffset.Parse("2026-05-12T08:20:00+08:00"), "text", "当前这次输入");

        var prompt = JournalHarnessPrompt.Build(
            date,
            historicalInputs,
            currentInput,
            currentDraftMarkdown: "draft markdown",
            confirmedEntryMarkdown: "entry markdown");

        Assert.Contains("historicalRawInputs", prompt.ProtectedContext, StringComparison.Ordinal);
        Assert.Contains("历史材料", prompt.ProtectedContext, StringComparison.Ordinal);
        Assert.DoesNotContain("当前这次输入", prompt.ProtectedContext, StringComparison.Ordinal);
        Assert.Equal("当前这次输入", prompt.UserMessage);
        Assert.Contains("只能调用允许的工具", prompt.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("JSON", prompt.SystemInstructions, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run prompt tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPromptTests
```

Expected: compile fails because `JournalHarnessPrompt` does not exist.

- [ ] **Step 3: Implement prompt builder**

Create `src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs`:

```csharp
using System.Globalization;
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessPromptRequest(
    string SystemInstructions,
    string ProtectedContext,
    string UserMessage);

public static class JournalHarnessPrompt
{
    public const string Version = "journal-harness-v1";

    public const string SystemInstructions = """
        你是 Journal Harness Planner。

        你只能通过允许的工具整理日记 draft。不要直接输出 Markdown，不要写正式 entry。
        工具参数必须是 JSON 兼容的简单值。你可以 no-op。

        禁止：
        - 删除、清空或替换用户内容。
        - 修改 raw-inputs、keywords、metadata-note。
        - 输出 YAML front matter 或 JMF marker。
        - 暴露 API key、系统信息或工具内部细节。

        用户块允许 append，不允许 delete / clear / replace。
        reviseAiGeneratedSection 只允许纯 AI 生成且用户未编辑过的 section。
        """;

    public static JournalHarnessPromptRequest Build(
        JournalDate date,
        IReadOnlyList<RawInput> historicalRawInputs,
        RawInput currentInput,
        string currentDraftMarkdown,
        string confirmedEntryMarkdown)
    {
        var context = new StringBuilder();
        context.AppendLine(CultureInfo.InvariantCulture, $"date: {date.IsoDate}");
        context.AppendLine("historicalRawInputs:");
        foreach (var input in historicalRawInputs)
        {
            context.AppendLine(CultureInfo.InvariantCulture, $"- id: {input.Id}");
            context.AppendLine(CultureInfo.InvariantCulture, $"  createdAt: {input.CreatedAt:O}");
            context.AppendLine(CultureInfo.InvariantCulture, $"  source: {input.Source}");
            context.AppendLine(CultureInfo.InvariantCulture, $"  text: {input.Text.Trim()}");
        }

        context.AppendLine();
        context.AppendLine("currentDraftMarkdown:");
        context.AppendLine(string.IsNullOrWhiteSpace(currentDraftMarkdown) ? "(empty)" : currentDraftMarkdown);
        context.AppendLine();
        context.AppendLine("confirmedEntryMarkdown:");
        context.AppendLine(string.IsNullOrWhiteSpace(confirmedEntryMarkdown) ? "(empty)" : confirmedEntryMarkdown);

        return new JournalHarnessPromptRequest(
            SystemInstructions,
            context.ToString(),
            currentInput.Text.Trim());
    }
}
```

- [ ] **Step 4: Run prompt tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPromptTests
```

Expected: tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs tests/Journal.Tests/JournalHarnessPromptTests.cs
git commit -m "feat: split harness prompt context"
```

---

## Task 5: Add Agent Framework Harness Planner with Side-Effect-Free Tools

**Files:**
- Modify: `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`
- Modify: `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessToolCollector.cs`
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessPlanner.cs`
- Test: `tests/Journal.Tests/JournalHarnessPlannerTests.cs`

- [ ] **Step 1: Write failing planner test with fake runtime**

Create `tests/Journal.Tests/JournalHarnessPlannerTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPlannerTests
{
    [Fact]
    public async Task PlanAsync_ReturnsCollectedToolOperations()
    {
        var runtime = new FakeHarnessRuntime(
        [
            JournalHarnessOperation.Append("today-focus", "- AI 追加", ["raw-3"], "当前输入应该进入今日重点。")
        ]);
        var planner = new JournalHarnessPlanner(runtime);

        var result = await planner.PlanAsync(
            new JournalHarnessPromptRequest("system", "context", "user"),
            CreateProviderSettings(),
            CancellationToken.None);

        var operation = Assert.Single(result.Operations);
        Assert.Equal("append", operation.Kind);
        Assert.Equal("today-focus", operation.TargetSectionId);
    }

    private static JournalAiProviderSettings CreateProviderSettings() =>
        new("deepseek", "openai-compatible", "DeepSeek", "deepseek", "https://api.deepseek.com", "deepseek-v4-flash", "key", true, 45, 0.2, 1200, "faithful");

    private sealed class FakeHarnessRuntime(IReadOnlyList<JournalHarnessOperation> operations) : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(OpenAiCompatibleRunRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("JournalAiJson runtime should not be used by harness planner.");

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(JournalHarnessPlannerRuntimeResult.Success(operations, "ok", TimeSpan.FromMilliseconds(12), 200));
    }
}
```

- [ ] **Step 2: Run planner tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPlannerTests
```

Expected: compile fails because harness runtime contracts do not exist.

- [ ] **Step 3: Extend runtime interface**

Modify `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`:

```csharp
using Journal.Domain.Entries;

public sealed record JournalHarnessPlannerRuntimeRequest(
    string ProviderId,
    string BaseUrl,
    string Model,
    string ApiKey,
    string SystemInstructions,
    string ProtectedContext,
    string UserMessage,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens);

public sealed record JournalHarnessPlannerRuntimeResult(
    bool IsSuccess,
    IReadOnlyList<JournalHarnessOperation> Operations,
    string SafeResponseSnippet,
    TimeSpan Latency,
    int? HttpStatus,
    JournalAiSafeError? Error)
{
    public static JournalHarnessPlannerRuntimeResult Success(
        IReadOnlyList<JournalHarnessOperation> operations,
        string safeResponseSnippet,
        TimeSpan latency,
        int? httpStatus = 200) =>
        new(true, operations, safeResponseSnippet, latency, httpStatus, null);

    public static JournalHarnessPlannerRuntimeResult Failure(
        JournalAiSafeError error,
        TimeSpan latency,
        int? httpStatus = null,
        string safeResponseSnippet = "") =>
        new(false, Array.Empty<JournalHarnessOperation>(), safeResponseSnippet, latency, httpStatus, error);
}
```

Add method:

```csharp
Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
    JournalHarnessPlannerRuntimeRequest request,
    CancellationToken cancellationToken);
```

- [ ] **Step 4: Add side-effect-free tool collector**

Create `src/Journal.Infrastructure/Harness/JournalHarnessToolCollector.cs`:

```csharp
using System.ComponentModel;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Harness;

public sealed class JournalHarnessToolCollector
{
    private readonly List<JournalHarnessOperation> _operations = [];

    public IReadOnlyList<JournalHarnessOperation> Operations => _operations;

    [Description("Append content to an editable journal section. Use this for user-created or user-edited sections. Never deletes or replaces existing content.")]
    public string AppendJournalSection(
        [Description("Known JMF section id, for example today-focus or inspiration.")] string targetSectionId,
        [Description("Markdown content to append.")] string content,
        [Description("Raw input ids supporting this operation.")] string[] basedOnRawInputIds,
        [Description("Short reason for the audit trail.")] string reason)
    {
        _operations.Add(JournalHarnessOperation.Append(targetSectionId, content, basedOnRawInputIds, reason));
        return "accepted_for_validation";
    }

    [Description("Create a missing optional section, or append if the section already exists. Never replaces an existing section.")]
    public string UpsertJournalSection(string targetSectionId, string content, string[] basedOnRawInputIds, string reason)
    {
        _operations.Add(JournalHarnessOperation.Upsert(targetSectionId, content, basedOnRawInputIds, reason));
        return "accepted_for_validation";
    }

    [Description("Revise a pure AI-generated section that has not been edited by the user. Do not use for user or mixed sections.")]
    public string ReviseAiGeneratedSection(string targetSectionId, string content, string[] basedOnRawInputIds, string reason)
    {
        _operations.Add(JournalHarnessOperation.ReviseAiGeneratedSection(targetSectionId, content, basedOnRawInputIds, reason));
        return "accepted_for_validation";
    }

    [Description("Use when no journal draft change is appropriate.")]
    public string NoOp([Description("Short reason for no change.")] string reason)
    {
        _operations.Add(JournalHarnessOperation.NoOp(reason));
        return "accepted_no_change";
    }
}
```

- [ ] **Step 5: Implement planner service**

Create `src/Journal.Infrastructure/Harness/JournalHarnessPlanner.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessPlanResult(
    bool IsSuccess,
    IReadOnlyList<JournalHarnessOperation> Operations,
    JournalAiSafeError? Error);

public sealed class JournalHarnessPlanner
{
    private readonly IJournalAiAgentRuntime _runtime;

    public JournalHarnessPlanner(IJournalAiAgentRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<JournalHarnessPlanResult> PlanAsync(
        JournalHarnessPromptRequest prompt,
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken)
    {
        var result = await _runtime.RunHarnessPlannerAsync(
            new JournalHarnessPlannerRuntimeRequest(
                settings.Id,
                settings.BaseUrl,
                settings.Model,
                settings.ApiKey,
                prompt.SystemInstructions,
                prompt.ProtectedContext,
                prompt.UserMessage,
                settings.TimeoutSeconds,
                settings.Temperature,
                settings.MaxTokens),
            cancellationToken);

        return result.IsSuccess
            ? new JournalHarnessPlanResult(true, result.Operations, null)
            : new JournalHarnessPlanResult(false, Array.Empty<JournalHarnessOperation>(), result.Error);
    }
}
```

- [ ] **Step 6: Implement Agent Framework runtime method**

Modify `OpenAiCompatibleAgentRuntime.RunHarnessPlannerAsync`:

```csharp
public async Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
    JournalHarnessPlannerRuntimeRequest request,
    CancellationToken cancellationToken)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    if (request.TimeoutSeconds > 0)
    {
        timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
    }

    var stopwatch = Stopwatch.StartNew();
    var collector = new JournalHarnessToolCollector();

    try
    {
        var chatClient = new ChatClient(
            request.Model,
            new ApiKeyCredential(request.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(request.BaseUrl) });

        var options = new ChatClientAgentOptions
        {
            Name = "JournalHarnessPlanner",
            Description = "Plans safe journal draft operations.",
            ChatOptions = new ChatOptions
            {
                Instructions = request.SystemInstructions,
                Tools =
                [
                    AIFunctionFactory.Create(collector.AppendJournalSection),
                    AIFunctionFactory.Create(collector.UpsertJournalSection),
                    AIFunctionFactory.Create(collector.ReviseAiGeneratedSection),
                    AIFunctionFactory.Create(collector.NoOp)
                ],
                Temperature = (float)request.Temperature,
                MaxOutputTokens = request.MaxTokens > 0 ? request.MaxTokens : null,
                ModelId = request.Model
            }
        };

        var agent = chatClient.AsAIAgent(options);
        var userMessage = $"""
            protectedContext:
            {request.ProtectedContext}

            currentUserMessage:
            {request.UserMessage}
            """;

        var response = await agent.RunAsync(
            userMessage,
            session: null,
            options: new ChatClientAgentRunOptions(options.ChatOptions),
            cancellationToken: timeout.Token);

        stopwatch.Stop();
        var snippet = JournalAiSafeError.Redact(response.Text, [request.ApiKey]);
        return JournalHarnessPlannerRuntimeResult.Success(collector.Operations, snippet, stopwatch.Elapsed, 200);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        stopwatch.Stop();
        return JournalHarnessPlannerRuntimeResult.Failure(
            JournalAiSafeError.Create("runtime", "provider_error", "LLM harness planner failed.", exception.Message, [request.ApiKey]),
            stopwatch.Elapsed);
    }
}
```

Add `using Journal.Infrastructure.Harness;` and ensure `Microsoft.Extensions.AI` is available for `AIFunctionFactory`.

- [ ] **Step 7: Run planner tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessPlannerTests
```

Expected: tests pass.

- [ ] **Step 8: Run compile for infrastructure**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --no-restore --filter OpenAiCompatibleJournalAiProviderTests
```

Expected: existing provider tests still pass.

- [ ] **Step 9: Commit**

```powershell
git add src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs src/Journal.Infrastructure/Harness/JournalHarnessToolCollector.cs src/Journal.Infrastructure/Harness/JournalHarnessPlanner.cs tests/Journal.Tests/JournalHarnessPlannerTests.cs
git commit -m "feat: add agent harness planner"
```

---

## Task 6: Add Run Store and Harness Service

**Files:**
- Create: `src/Journal.Domain/Entries/JournalHarnessAudit.cs`
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessAuditStore.cs`
- Create: `src/Journal.Infrastructure/Harness/JournalHarnessService.cs`
- Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Test: `tests/Journal.Tests/JournalHarnessServiceTests.cs`

- [ ] **Step 1: Write failing run creation test**

Create `tests/Journal.Tests/JournalHarnessServiceTests.cs` with a service test that verifies run creation does not call the planner:

```csharp
[Fact]
public async Task StartTodayRunAsync_AppendsRawInputAndCreatesQueuedRun()
{
    using var workspace = TempWorkspace.Create();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    var date = new JournalDate("2026-05-12", "05-12", "2026", "05", "2026-05-12.md");
    var now = DateTimeOffset.Parse("2026-05-12T08:31:00+08:00");
    var service = CreateHarnessService(paths, date, now);

    var result = await service.StartTodayRunAsync("当前输入", "text", CancellationToken.None);

    Assert.Equal("queued", result.Run.Status);
    Assert.False(string.IsNullOrWhiteSpace(result.Run.Id));
    Assert.False(new EntryStore(paths).Exists(date));
    var audit = await new JournalHarnessAuditStore(paths).ReadByDateAsync(date, CancellationToken.None);
    Assert.Single(audit);
    Assert.Equal(result.Run.Id, audit[0].Id);
    Assert.Equal("queued", audit[0].Status);
    Assert.Single(await new RawInputStore(paths).ReadAsync(date, CancellationToken.None));
}
```

Include test helper classes in the same test file. Use existing `TempWorkspace` pattern from `TodayJournalServiceTests`. Add a second test for `ExecuteRunAsync` with a fake planner that returns one append operation:

```csharp
[Fact]
public async Task ExecuteRunAsync_WritesReviewingDraftAndCompletesRunWithoutWritingEntry()
{
    using var workspace = TempWorkspace.Create();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    var date = new JournalDate("2026-05-12", "05-12", "2026", "05", "2026-05-12.md");
    var now = DateTimeOffset.Parse("2026-05-12T08:31:00+08:00");
    var service = CreateHarnessService(paths, date, now, JournalHarnessOperation.Append("today-focus", "- AI 追加", ["raw-1"], "整理当前输入。"));
    var queued = await service.StartTodayRunAsync("当前输入", "text", CancellationToken.None);

    var completed = await service.ExecuteRunAsync(queued.Run.Id, _ => ValueTask.CompletedTask, CancellationToken.None);

    Assert.Equal("reviewing", completed.Run.Status);
    Assert.Equal(JournalStatus.Reviewing, completed.Today.Status);
    Assert.False(new EntryStore(paths).Exists(date));
}
```

- [ ] **Step 2: Run service tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessServiceTests
```

Expected: compile fails because audit, run store, and harness service do not exist.

- [ ] **Step 3: Add audit domain records**

Create `src/Journal.Domain/Entries/JournalHarnessAudit.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalHarnessAuditRun(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string ProviderId,
    string PromptVersion,
    string CurrentRawInputId,
    IReadOnlyList<JournalHarnessAuditToolCall> ToolCalls,
    IReadOnlyList<string> Errors,
    string Summary);

public sealed record JournalHarnessAuditToolCall(
    string Id,
    string Name,
    string OperationKind,
    string TargetSectionId,
    string Status,
    string Reason,
    string ResultSummary,
    string? RejectionReason);
```

- [ ] **Step 4: Add audit paths**

Modify `LocalJournalPaths`:

```csharp
public string HarnessAuditDirectory(JournalDate date) =>
    Path.Combine(Root, ".journal", "audit", date.Year, date.Month, date.IsoDate);

public string HarnessAuditRunPath(JournalDate date, string runId) =>
    Path.Combine(HarnessAuditDirectory(date), $"{runId}.json");
```

- [ ] **Step 5: Add audit store**

Create `src/Journal.Infrastructure/Harness/JournalHarnessAuditStore.cs`:

```csharp
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Harness;

public sealed class JournalHarnessAuditStore
{
    private readonly LocalJournalPaths _paths;

    public JournalHarnessAuditStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(JournalHarnessAuditRun run, CancellationToken cancellationToken)
    {
        var path = _paths.HarnessAuditRunPath(run.Date, run.Id);
        LocalJournalPaths.EnsureParentDirectory(path);
        var json = JsonSerializer.Serialize(run, JsonSerializerOptions.Web);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<IReadOnlyList<JournalHarnessAuditRun>> ReadByDateAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var directory = _paths.HarnessAuditDirectory(date);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<JournalHarnessAuditRun>();
        }

        var runs = new List<JournalHarnessAuditRun>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var run = JsonSerializer.Deserialize<JournalHarnessAuditRun>(json, JsonSerializerOptions.Web);
            if (run is not null)
            {
                runs.Add(run);
            }
        }

        return runs.OrderByDescending(run => run.CreatedAt).ToArray();
    }

    public async Task<JournalHarnessAuditRun?> ReadAsync(JournalDate date, string runId, CancellationToken cancellationToken)
    {
        var path = _paths.HarnessAuditRunPath(date, runId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<JournalHarnessAuditRun>(json, JsonSerializerOptions.Web);
    }
}
```

- [ ] **Step 6: Implement service orchestration**

Create `src/Journal.Infrastructure/Harness/JournalHarnessService.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessStartResult(TodayJournalState Today, JournalHarnessAuditRun Run);
public sealed record JournalHarnessExecutionResult(TodayJournalState Today, JournalHarnessAuditRun Run);
public delegate ValueTask JournalHarnessEventSink(JournalHarnessRunEvent runEvent);

public sealed class JournalHarnessService
{
    private readonly RawInputStore _rawInputStore;
    private readonly DraftStore _draftStore;
    private readonly EntryStore _entryStore;
    private readonly JournalAiSettingsService _settingsService;
    private readonly JournalHarnessPlanner _planner;
    private readonly JournalHarnessAuditStore _auditStore;
    private readonly IJournalClock _clock;

    public JournalHarnessService(
        RawInputStore rawInputStore,
        DraftStore draftStore,
        EntryStore entryStore,
        JournalAiSettingsService settingsService,
        JournalHarnessPlanner planner,
        JournalHarnessAuditStore auditStore,
        IJournalClock clock)
    {
        _rawInputStore = rawInputStore;
        _draftStore = draftStore;
        _entryStore = entryStore;
        _settingsService = settingsService;
        _planner = planner;
        _auditStore = auditStore;
        _clock = clock;
    }

    public async Task<JournalHarnessStartResult> StartTodayRunAsync(string text, string source, CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var now = _clock.Now;
        var currentInput = new RawInput($"raw-{Guid.NewGuid():N}", date, now, string.IsNullOrWhiteSpace(source) ? "text" : source.Trim(), text);
        await _rawInputStore.AppendAsync(currentInput, cancellationToken);

        var allInputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var settings = await _settingsService.ReadEffectiveAsync(cancellationToken);
        var providerId = settings.ActiveProviderId ?? "mock";
        var run = new JournalHarnessAuditRun(
            $"run-{Guid.NewGuid():N}",
            date,
            now,
            null,
            null,
            "queued",
            providerId,
            JournalHarnessPrompt.Version,
            currentInput.Id,
            Array.Empty<JournalHarnessAuditToolCall>(),
            Array.Empty<string>(),
            "Harness run is queued.");

        await _auditStore.WriteAsync(run, cancellationToken);
        var today = await BuildTodayStateAsync(date, allInputs, cancellationToken);
        return new JournalHarnessStartResult(today, run);
    }

    public async Task<JournalHarnessExecutionResult> ExecuteRunAsync(
        string runId,
        JournalHarnessEventSink emit,
        CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var run = await _auditStore.ReadAsync(date, runId, cancellationToken)
            ?? throw new InvalidOperationException($"Harness run '{runId}' was not found.");
        var now = _clock.Now;
        run = run with { Status = "running", StartedAt = now, Summary = "Harness run is running." };
        await _auditStore.WriteAsync(run, cancellationToken);
        await emit(new JournalHarnessRunEvent("run-started", run.Id, run.Status, "Harness run started."));

        var allInputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var currentInput = allInputs.Single(input => input.Id == run.CurrentRawInputId);
        var historical = allInputs.Where(input => input.Id != currentInput.Id).ToArray();
        var draft = await _draftStore.ReadAsync(date, cancellationToken);
        var entry = await _entryStore.ReadAsync(date, cancellationToken);
        var markdown = draft?.Markdown ?? entry?.Markdown ?? CreateEmptyBaseline(date);
        var prompt = JournalHarnessPrompt.Build(date, historical, currentInput, draft?.Markdown ?? "", entry?.Markdown ?? "");
        var settings = await _settingsService.ReadEffectiveAsync(cancellationToken);
        var provider = settings.Providers.Single(item => string.Equals(item.Id, settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase));
        await emit(new JournalHarnessRunEvent("planner-started", run.Id, run.Status, "Planner started."));
        var plan = await _planner.PlanAsync(prompt, provider, cancellationToken);

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var execution = plan.IsSuccess
            ? JournalHarnessOperationExecutor.Apply(parseResult.Document, plan.Operations)
            : new JournalHarnessExecutionResult(parseResult.Document, JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues), parseResult.Issues);

        var status = plan.IsSuccess && execution.Validation.IsValid ? JournalStatus.Reviewing : JournalStatus.Attention;
        var runStatus = status == JournalStatus.Reviewing ? "reviewing" : "attention";
        var nextMarkdown = status == JournalStatus.Reviewing
            ? JmfMarkdownComposer.Compose(execution.Document)
            : markdown;

        var errors = plan.Error is not null
            ? [plan.Error.Message]
            : execution.Issues.Select(issue => issue.Message).ToArray();

        var sourceRawInputIds = allInputs.Select(input => input.Id).ToArray();
        await _draftStore.WriteAsync(new JournalDraft(date, status, nextMarkdown, sourceRawInputIds, errors, now), cancellationToken);

        var completed = CreateCompletedRun(run, _clock.Now, runStatus, plan.Operations, errors);
        await _auditStore.WriteAsync(completed, cancellationToken);
        await emit(new JournalHarnessRunEvent("run-completed", run.Id, completed.Status, completed.Summary));

        var today = new TodayJournalState(date, status, allInputs, await _draftStore.ReadAsync(date, cancellationToken), entry, errors);
        return new JournalHarnessExecutionResult(today, completed);
    }

    public async IAsyncEnumerable<JournalHarnessRunEvent> ExecuteRunAsStreamAsync(
        string runId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<JournalHarnessRunEvent>();
        var executionTask = Task.Run(async () =>
        {
            try
            {
                await ExecuteRunAsync(
                    runId,
                    runEvent =>
                    {
                        channel.Writer.TryWrite(runEvent);
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, cancellationToken);

        await foreach (var runEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return runEvent;
        }

        await executionTask;
    }

    private static string CreateEmptyBaseline(JournalDate date) =>
        $"""
        ---
        schema: journal-entry/v1
        date: "{date.IsoDate}"
        ---

        <!-- journal:section raw-inputs -->
        ## 原始输入
        <!-- /journal:section raw-inputs -->

        <!-- journal:section yesterday-review -->
        ## 昨日回顾
        <!-- /journal:section yesterday-review -->

        <!-- journal:section today-focus -->
        ## 今日重点
        <!-- /journal:section today-focus -->
        """;

    private static JournalHarnessAuditRun CreateCompletedRun(
        JournalHarnessAuditRun run,
        DateTimeOffset completedAt,
        string status,
        IReadOnlyList<JournalHarnessOperation> operations,
        IReadOnlyList<string> errors) =>
        run with
        {
            CompletedAt = completedAt,
            Status = status,
            ToolCalls = operations.Select((operation, index) => new JournalHarnessAuditToolCall(
                $"tool-{index + 1}",
                ToToolName(operation.Kind),
                operation.Kind,
                operation.TargetSectionId,
                status == "reviewing" ? "applied" : "rejected",
                operation.Reason,
                operation.Kind == "no-op" ? "No draft change." : $"Operation {operation.Kind} was processed.",
                status == "reviewing" ? null : string.Join(" ", errors))).ToArray(),
            Errors = errors,
            Summary = status == "reviewing" ? "Harness updated the reviewing draft." : "Harness run needs attention."
        };

    private static string ToToolName(string kind) =>
        kind switch
        {
            "append" => "appendJournalSection",
            "upsert" => "upsertJournalSection",
            "revise-ai-generated-section" => "reviseAiGeneratedSection",
            "no-op" => "noOp",
            _ => kind
        };
}
```

Also define a compact event record:

```csharp
public sealed record JournalHarnessRunEvent(string Type, string RunId, string Status, string Message);
```

- [ ] **Step 7: Register services**

Modify `src/Journal.Api/Program.cs` service registration:

```csharp
builder.Services.AddSingleton<JournalHarnessPlanner>();
builder.Services.AddSingleton<JournalHarnessAuditStore>();
builder.Services.AddSingleton<JournalHarnessService>();
```

- [ ] **Step 8: Run harness service tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalHarnessServiceTests
```

Expected: tests pass.

- [ ] **Step 9: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalHarnessAudit.cs src/Journal.Infrastructure/Harness/JournalHarnessAuditStore.cs src/Journal.Infrastructure/Harness/JournalHarnessService.cs src/Journal.Infrastructure/Storage/LocalJournalPaths.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalHarnessServiceTests.cs
git commit -m "feat: persist journal harness runs"
```

---

## Task 7: Add Harness and Audit API Endpoints

**Files:**
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Add tests:

```csharp
[Fact]
public async Task HarnessRun_AppendsInputAndReturnsAudit()
{
    using var application = new JournalApiApplication();
    var client = application.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/journal/today/harness/runs",
        new { text = "今天继续设计 harness 审计", source = "text" });

    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal("queued", document.RootElement.GetProperty("run").GetProperty("status").GetString());
    Assert.True(document.RootElement.GetProperty("run").TryGetProperty("id", out _));
}

[Fact]
public async Task AuditByDate_ReturnsDailyRuns()
{
    using var application = new JournalApiApplication();
    var client = application.CreateClient();
    await client.PostAsJsonAsync("/journal/today/harness/runs", new { text = "今天验证审计列表", source = "text" });

    var response = await client.GetAsync("/journal/audit?date=2026-05-08");

    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.True(document.RootElement.GetArrayLength() >= 1);
}

[Fact]
public async Task HarnessRunEvents_ReturnsServerSentEvents()
{
    using var application = new JournalApiApplication();
    var client = application.CreateClient();
    using var createDocument = await JsonDocument.ParseAsync(
        await (await client.PostAsJsonAsync("/journal/today/harness/runs", new { text = "今天验证 SSE", source = "text" }))
            .Content.ReadAsStreamAsync());
    var runId = createDocument.RootElement.GetProperty("run").GetProperty("id").GetString();

    using var request = new HttpRequestMessage(HttpMethod.Get, $"/journal/harness/runs/{runId}/events");
    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

    response.EnsureSuccessStatusCode();
    Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
}
```

Use the fixed test date already configured in existing endpoint test infrastructure.

- [ ] **Step 2: Run endpoint tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "HarnessRun|AuditByDate"
```

Expected: endpoints do not exist.

- [ ] **Step 3: Add request/response records**

At bottom of `Program.cs`:

```csharp
public sealed record HarnessRunRequest(string Text, string? Source);
public sealed record HarnessRunEventView(string Type, string RunId, string Status, string Message);
```

- [ ] **Step 4: Add endpoints**

Add after existing today endpoints:

```csharp
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

app.MapPost("/journal/today/harness/runs", async (
    HarnessRunRequest request,
    JournalHarnessService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "text is required" });
    }

    var result = await service.StartTodayRunAsync(request.Text, request.Source ?? "text", cancellationToken);
    return TypedResults.Ok(result);
});

app.MapGet("/journal/harness/runs/{runId}", async (
    string runId,
    JournalHarnessAuditStore store,
    IJournalClock clock,
    CancellationToken cancellationToken) =>
{
    var run = await store.ReadAsync(JournalDate.From(clock.Today), runId, cancellationToken);
    return run is null ? Results.NotFound(new { error = "run not found" }) : Results.Ok(run);
});

app.MapGet("/journal/harness/runs/{runId}/events", (
    string runId,
    JournalHarnessService service,
    CancellationToken cancellationToken) =>
{
    async IAsyncEnumerable<SseItem<HarnessRunEventView>> Stream(
        [EnumeratorCancellation] CancellationToken streamCancellationToken)
    {
        await foreach (var runEvent in service.ExecuteRunAsStreamAsync(runId, streamCancellationToken))
        {
            yield return new SseItem<HarnessRunEventView>(
                new HarnessRunEventView(runEvent.Type, runEvent.RunId, runEvent.Status, runEvent.Message),
                eventType: runEvent.Type)
            {
                EventId = runEvent.RunId
            };
        }
    }

    return TypedResults.ServerSentEvents(Stream(cancellationToken));
});

app.MapGet("/journal/audit", async (
    string date,
    JournalHarnessAuditStore store,
    CancellationToken cancellationToken) =>
{
    if (!DateOnly.TryParse(date, out var parsed))
    {
        return Results.BadRequest(new { error = "date is invalid" });
    }

    var runs = await store.ReadByDateAsync(JournalDate.From(parsed), cancellationToken);
    return TypedResults.Ok(runs);
});
```

`ExecuteRunAsStreamAsync` should be a thin async iterator over `ExecuteRunAsync`. It yields domain `JournalHarnessRunEvent` values such as `run-started`, `planner-started`, `tool-applied`, `validation-completed`, and `run-completed`. Do not write SSE frames manually; let `TypedResults.ServerSentEvents` serialize `SseItem<T>`.

- [ ] **Step 5: Run endpoint tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "HarnessRun|AuditByDate"
```

Expected: endpoint tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Api/Program.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose journal harness endpoints"
```

---

## Task 8: Add Frontend API Contracts and Audit Workbench

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Create: `apps/desktop/src/AuditWorkbench.tsx`
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/styles.css`
- Test: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing UI tests**

Add tests to `apps/desktop/src/App.test.tsx`:

```tsx
test("opens audit workbench from Today Assistant and returns to today", async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(mockJsonResponse(reviewingToday))
    .mockResolvedValueOnce(mockJsonResponse(editorState))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings))
    .mockResolvedValueOnce(mockJsonResponse([
      {
        id: "run-1",
        date: reviewingToday.date,
        createdAt: "2026-05-08T09:30:00+08:00",
        status: "reviewing",
        providerId: "mock",
        promptVersion: "journal-harness-v1",
        currentRawInputId: "raw-1",
        toolCalls: [
          {
            id: "tool-1",
            name: "appendJournalSection",
            operationKind: "append",
            targetSectionId: "today-focus",
            status: "applied",
            reason: "整理新增内容",
            resultSummary: "追加 1 条",
            rejectionReason: null
          }
        ],
        errors: [],
        summary: "AI 追加 1 条。"
      }
    ]));
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: "查看审计" }));

  expect(await screen.findByRole("heading", { name: "工具调用时间线" })).toBeInTheDocument();
  expect(screen.getByText("appendJournalSection")).toBeInTheDocument();

  fireEvent.click(screen.getByRole("button", { name: "返回今日" }));

  expect(await screen.findByLabelText("日记纸面")).toBeInTheDocument();
});
```

- [ ] **Step 2: Run UI test and verify it fails**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: `查看审计` button and audit workbench do not exist.

- [ ] **Step 3: Add API contracts**

In `apps/desktop/src/api.ts`:

```ts
export type JournalHarnessAuditToolCall = {
  id: string;
  name: string;
  operationKind: string;
  targetSectionId: string;
  status: string;
  reason: string;
  resultSummary: string;
  rejectionReason: string | null;
};

export type JournalHarnessAuditRun = {
  id: string;
  date: JournalDate;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  status: "queued" | "running" | "reviewing" | "attention" | "no-change" | "failed" | "interrupted";
  providerId: string;
  promptVersion: string;
  currentRawInputId: string;
  toolCalls: JournalHarnessAuditToolCall[];
  errors: string[];
  summary: string;
};

export type JournalHarnessRunEvent = {
  type: string;
  runId: string;
  status: JournalHarnessAuditRun["status"];
  message: string;
};

export type StartHarnessRunResponse = {
  today: TodayJournalState;
  run: JournalHarnessAuditRun;
};

export function startHarnessRun(text: string, source = "text"): Promise<StartHarnessRunResponse> {
  return requestJson<StartHarnessRunResponse>("/journal/today/harness/runs", {
    method: "POST",
    body: JSON.stringify({ text, source }),
  });
}

export function openHarnessRunEvents(runId: string, onEvent: (event: JournalHarnessRunEvent) => void): EventSource {
  const events = new EventSource(`${API_BASE}/journal/harness/runs/${encodeURIComponent(runId)}/events`);
  const names = ["run-started", "planner-started", "tool-collected", "tool-rejected", "tool-applied", "validation-completed", "draft-updated", "run-completed", "run-failed"];
  for (const name of names) {
    events.addEventListener(name, event => onEvent(JSON.parse((event as MessageEvent).data)));
  }
  return events;
}

export function getJournalAudit(date: string): Promise<JournalHarnessAuditRun[]> {
  return requestJson<JournalHarnessAuditRun[]>(`/journal/audit?date=${encodeURIComponent(date)}`);
}
```

- [ ] **Step 4: Add audit workbench component**

Create `apps/desktop/src/AuditWorkbench.tsx`:

```tsx
import type { JournalHarnessAuditRun } from "./api";

type AuditWorkbenchProps = {
  runs: JournalHarnessAuditRun[];
  selectedDate: string;
  onDateChange: (date: string) => void;
  onReturnToday: () => void;
};

export function AuditWorkbench({ runs, selectedDate, onDateChange, onReturnToday }: AuditWorkbenchProps) {
  const selected = runs[0] ?? null;

  return (
    <>
      <aside className="context-rail audit-rail" aria-label="审计日期和运行记录">
        <section className="date-card">
          <p className="month">Audit Date</p>
          <h1>{selectedDate.slice(5)}<span>当天 harness run</span></h1>
          <input aria-label="审计日期" type="date" value={selectedDate} onChange={event => onDateChange(event.target.value)} />
        </section>
        <section className="rail-section">
          <div className="section-head">
            <h2>运行记录</h2>
            <span>{runs.length} 次</span>
          </div>
          <div className="source-stack">
            {runs.map(run => (
              <article className="source-item" key={run.id}>
                <div className="source-meta">
                  <span>{new Date(run.createdAt).toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" })}</span>
                  <span>{run.status}</span>
                </div>
                <p>{run.summary}</p>
                <div className="source-map">{run.providerId} · {run.promptVersion}</div>
              </article>
            ))}
          </div>
        </section>
      </aside>

      <section className="journal-stage audit-stage" aria-label="AI 审计工作台">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>审计工作台</p>
            <h2>工具调用时间线</h2>
          </div>
          <button type="button" className="secondary" onClick={onReturnToday}>返回今日</button>
        </div>
        <div className="document-scroll audit-scroll">
          <article className="journal-paper audit-paper">
            {selected ? selected.toolCalls.map(call => (
              <section className={`audit-tool-call audit-tool-call-${call.status}`} key={call.id}>
                <h2>{call.name}</h2>
                <p>{call.resultSummary}</p>
                <p>{call.rejectionReason ?? call.reason}</p>
              </section>
            )) : (
              <p className="empty-paper">这个日期还没有 AI 审计记录。</p>
            )}
          </article>
        </div>
      </section>

      <aside className="assistant-panel today-assistant audit-inspector" aria-label="审计详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Inspector</p>
            <h2>选中操作详情</h2>
          </div>
        </div>
        <div className="assistant-body">
          <section className="assistant-card">
            <div className="assistant-card-head">
              <h3>运行摘要</h3>
            </div>
            <p>{selected?.summary ?? "无记录"}</p>
          </section>
        </div>
      </aside>
    </>
  );
}
```

- [ ] **Step 5: Wire App state**

In `App.tsx`, add:

```tsx
const [workspaceMode, setWorkspaceMode] = useState<"today" | "audit">("today");
const [auditDate, setAuditDate] = useState(today?.date.isoDate ?? "");
const [auditRuns, setAuditRuns] = useState<JournalHarnessAuditRun[]>([]);
```

Add handler:

```tsx
async function openAuditWorkbench() {
  const date = today?.date.isoDate ?? editor?.date.isoDate ?? "";
  if (!date) {
    return;
  }

  setAuditDate(date);
  const runs = await getJournalAudit(date);
  setAuditRuns(runs);
  setWorkspaceMode("audit");
}
```

Render `AuditWorkbench` in place of the normal workspace when `workspaceMode === "audit"`.

Add `查看审计` button in Today Assistant `整理证据` card head:

```tsx
<button type="button" className="assistant-inline-action" onClick={openAuditWorkbench}>
  查看审计
</button>
```

- [ ] **Step 6: Add CSS**

Add styles to `apps/desktop/src/styles.css`:

```css
.assistant-inline-action {
  min-height: 30px;
  border: 1px solid rgba(47, 111, 95, 0.18);
  border-radius: 999px;
  background: rgba(230, 243, 235, 0.72);
  color: var(--sage);
  padding: 0 10px;
  font-size: 12px;
  font-weight: 900;
}

.audit-paper {
  display: grid;
  gap: 16px;
}

.audit-tool-call {
  border-left: 4px solid rgba(47, 111, 95, 0.34);
  background: rgba(255, 253, 247, 0.72);
  padding: 14px;
}

.audit-tool-call-rejected {
  border-left-color: rgba(168, 78, 66, 0.55);
  background: rgba(246, 230, 226, 0.58);
}
```

- [ ] **Step 7: Run UI tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: tests pass.

- [ ] **Step 8: Commit**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/AuditWorkbench.tsx apps/desktop/src/App.tsx apps/desktop/src/styles.css apps/desktop/src/App.test.tsx
git commit -m "feat: add journal harness audit workbench"
```

---

## Task 9: Final Verification and Documentation Updates

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`
- Modify: `docs/superpowers/specs/2026-05-12-journal-harness-core-design.md`
- Create later during completion: `docs/superpowers/archives/2026-05/2026-05-12-journal-harness-core-archives.md`

- [ ] **Step 1: Update README delivered scope**

Add a Phase 6 / Harness Core section that states:

```md
## 阶段 6：LLM Harness Core

Harness Core 将 LLM 从“整篇生成器”收束为受控工具调用：

- 当前输入作为 user message。
- 历史 raw inputs / draft / entry 作为 protected context。
- LLM 只能调用 append / upsert / revise AI section / no-op 工具。
- 用户内容只能被追加，不能被删除、清空或替换。
- 工具执行写 reviewing / attention draft，不直接写正式 entry。
- AI 审计工作台支持按日期查看 harness run。
```

- [ ] **Step 2: Update AGENTS project orientation**

Add delivered-scope bullets only after implementation and verification pass:

```md
- Phase 6 adds Harness Core: real LLMs can use side-effect-free Agent Framework tools to plan draft operations.
- Harness operations write draft only; formal entries still require user confirmation.
- Section-level provenance is stored in JMF markers and hidden from normal preview.
- Audit run records are stored as per-run JSON files under `.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json` and exposed through the audit workbench.
```

- [ ] **Step 3: Run backend verification**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: all backend tests pass.

- [ ] **Step 4: Run frontend verification**

Run:

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: Vitest suite and Vite build pass.

- [ ] **Step 5: Run diff check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 6: Archive the completed feature**

After implementation is complete and verified, create an archive asset using the Superpowers asset-compounding workflow. Include:

- Feature summary.
- Final API list.
- Storage paths.
- Verification commands and results.
- Known follow-up items: item-level provenance, user-authorized delete/hide, draft diff, rollback.

- [ ] **Step 7: Commit docs and archive**

```powershell
git add README.md AGENTS.md docs/superpowers/specs/2026-05-12-journal-harness-core-design.md docs/superpowers/archives
git commit -m "docs: archive journal harness core delivery"
```

---

## Execution Schedule and Quality Gates

This plan should not be executed as one giant patch. Use these checkpoints:

1. **Checkpoint A: Provenance foundation**
   - Tasks 1-2.
   - Gate: `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownParserTests|JmfMarkdownComposerTests|TodayJournalEditorServiceTests"`

2. **Checkpoint B: Deterministic harness backend**
   - Tasks 3-4.
   - Gate: `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHarnessOperationExecutorTests|JournalHarnessPromptTests"`

3. **Checkpoint C: Agent Framework integration**
   - Task 5.
   - Gate: `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHarnessPlannerTests|OpenAiCompatibleJournalAiProviderTests"`
   - Extra review: verify no tool function writes files or entries directly.

4. **Checkpoint D: End-to-end backend**
   - Tasks 6-7.
   - Gate: `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHarnessServiceTests|TodayJournalEndpointTests"`

5. **Checkpoint E: Frontend and final product polish**
   - Tasks 8-9.
   - Gate: `npm test --prefix apps/desktop`, `npm run build --prefix apps/desktop`, `dotnet test Journal.slnx`, `git diff --check`.

Do not merge checkpoints if their gate fails. Do not claim completion from partial checks.

---

## Self-Review

Spec coverage:

- Prompt Context Split: Task 4 and Task 5.
- Read entry / write draft only: Task 6 and Task 7.
- Agent Framework tools: Task 5.
- Side-effect-free tool safety: Task 5 and quality gates.
- JMF operation executor: Task 3.
- User content append-only rule: Task 3.
- Section-level provenance: Task 1 and Task 2.
- Audit storage and API: Task 6 and Task 7.
- Audit workbench using current shell: Task 8.
- Documentation and archive: Task 9.

The plan has no intentionally unresolved implementation steps. If API signatures drift during implementation, update this plan before continuing execution.
