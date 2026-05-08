# Phase 2 JMF Generation Confirmation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 2 vertical slice: natural-language input -> raw input JSONL -> rule-based Mock AI JSON -> JMF v1 Markdown draft -> read-only preview -> user confirmation -> today's formal Markdown entry.

**Architecture:** Keep `Journal.Api` as a thin Minimal API layer, place stable diary concepts in `Journal.Domain`, and put filesystem, Mock AI, validation, rendering, and orchestration in `Journal.Infrastructure`. The desktop React app becomes the Today Workbench from the V3 prototype, with Markdown rendered read-only and editing left for Phase 3.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, xUnit, Microsoft.AspNetCore.Mvc.Testing, System.Text.Json JSONL storage, React, TypeScript, Vite, Vitest, Testing Library, react-markdown, remark-gfm.

---

## File Structure

- `src/Journal.Domain/Entries/JournalDate.cs`: date value object and path-safe date helpers.
- `src/Journal.Domain/Entries/JournalStatus.cs`: stable status enum serialized as lowercase JSON.
- `src/Journal.Domain/Entries/RawInput.cs`: original user input model.
- `src/Journal.Domain/Entries/JournalAiJson.cs`: structured Mock AI output.
- `src/Journal.Domain/Entries/JournalDraft.cs`: draft markdown state and error metadata.
- `src/Journal.Domain/Entries/JournalEntry.cs`: confirmed markdown entry state.
- `src/Journal.Domain/Entries/TodayJournalState.cs`: API-facing today aggregate.
- `src/Journal.Domain/Entries/JournalAiValidationResult.cs`: validation result contract.
- `src/Journal.Infrastructure/Clock/IJournalClock.cs`: injectable date/time source.
- `src/Journal.Infrastructure/Clock/SystemJournalClock.cs`: production date/time source.
- `src/Journal.Infrastructure/Storage/JournalStorageOptions.cs`: local journal root configuration.
- `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`: all `%LocalAppData%/Journal` path construction.
- `src/Journal.Infrastructure/Storage/RawInputStore.cs`: append/read raw input JSONL.
- `src/Journal.Infrastructure/Storage/DraftStore.cs`: write/read draft markdown and draft metadata sidecar.
- `src/Journal.Infrastructure/Storage/EntryStore.cs`: write/read confirmed markdown entry.
- `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`: Mock AI provider boundary for tests.
- `src/Journal.Infrastructure/Ai/MockAiProvider.cs`: deterministic rules from raw inputs to `JournalAiJson`.
- `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs`: required-field and renderability validation.
- `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`: JMF v1 Markdown renderer.
- `src/Journal.Infrastructure/Today/TodayJournalService.cs`: orchestration for GET/add input/confirm.
- `src/Journal.Api/Program.cs`: service registration, JSON enum settings, and three `/journal/today` endpoints.
- `tests/Journal.Tests/JournalDateTests.cs`: date/path unit tests.
- `tests/Journal.Tests/LocalJournalStorageTests.cs`: path, JSONL, draft, and entry persistence tests.
- `tests/Journal.Tests/MockAiAndJmfTests.cs`: Mock AI, validator, and renderer tests.
- `tests/Journal.Tests/TodayJournalServiceTests.cs`: workflow tests without HTTP.
- `tests/Journal.Tests/TodayJournalEndpointTests.cs`: HTTP contract tests.
- `apps/desktop/package.json`: add Markdown rendering dependencies.
- `apps/desktop/src/api.ts`: frontend API client and TypeScript contracts.
- `apps/desktop/src/MarkdownPreview.tsx`: read-only Markdown renderer.
- `apps/desktop/src/App.tsx`: Today Workbench state and interactions.
- `apps/desktop/src/App.test.tsx`: frontend workflow tests.
- `apps/desktop/src/styles.css`: V3 responsive desktop layout.
- `README.md`: Phase 2 run, data path, and verification notes.

## Task 1: Define Domain Models

**Files:**
- Create: `src/Journal.Domain/Entries/JournalDate.cs`
- Create: `src/Journal.Domain/Entries/JournalStatus.cs`
- Create: `src/Journal.Domain/Entries/RawInput.cs`
- Create: `src/Journal.Domain/Entries/JournalAiJson.cs`
- Create: `src/Journal.Domain/Entries/JournalDraft.cs`
- Create: `src/Journal.Domain/Entries/JournalEntry.cs`
- Create: `src/Journal.Domain/Entries/TodayJournalState.cs`
- Create: `src/Journal.Domain/Entries/JournalAiValidationResult.cs`
- Create: `tests/Journal.Tests/JournalDateTests.cs`

- [ ] **Step 1: Write failing date model tests**

Create `tests/Journal.Tests/JournalDateTests.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Tests;

public sealed class JournalDateTests
{
    [Fact]
    public void JournalDate_FormatsStorageParts()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));

        Assert.Equal("2026", date.Year);
        Assert.Equal("05", date.Month);
        Assert.Equal("2026-05-08", date.IsoDate);
        Assert.Equal("05-08", date.MonthDay);
        Assert.Equal("2026-05-08.md", date.MarkdownFileName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalDateTests
```

Expected: FAIL because `Journal.Domain.Entries.JournalDate` does not exist.

- [ ] **Step 3: Add domain model files**

Create `src/Journal.Domain/Entries/JournalDate.cs`:

```csharp
namespace Journal.Domain.Entries;

public readonly record struct JournalDate(DateOnly Value)
{
    public static JournalDate From(DateOnly value) => new(value);

    public string Year => Value.Year.ToString("0000");

    public string Month => Value.Month.ToString("00");

    public string IsoDate => Value.ToString("yyyy-MM-dd");

    public string MonthDay => Value.ToString("MM-dd");

    public string MarkdownFileName => $"{IsoDate}.md";

    public override string ToString() => IsoDate;
}
```

Create `src/Journal.Domain/Entries/JournalStatus.cs`:

```csharp
namespace Journal.Domain.Entries;

public enum JournalStatus
{
    Empty,
    Draft,
    Reviewing,
    Processed,
    Updated,
    Attention
}
```

Create `src/Journal.Domain/Entries/RawInput.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record RawInput(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Source,
    string Text);
```

Create `src/Journal.Domain/Entries/JournalAiJson.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalAiJson(
    string Schema,
    string Date,
    string MonthDay,
    string Status,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Topics,
    string Mood,
    IReadOnlyList<string> RawInputs,
    IReadOnlyList<string> YesterdayReview,
    IReadOnlyList<string> TodayFocus,
    IReadOnlyList<string> Inspiration);
```

Create `src/Journal.Domain/Entries/JournalDraft.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalDraft(
    JournalDate Date,
    JournalStatus Status,
    string Markdown,
    IReadOnlyList<string> SourceRawInputIds,
    IReadOnlyList<string> Errors,
    DateTimeOffset UpdatedAt);
```

Create `src/Journal.Domain/Entries/JournalEntry.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalEntry(
    JournalDate Date,
    string Markdown,
    string Path,
    DateTimeOffset UpdatedAt);
```

Create `src/Journal.Domain/Entries/TodayJournalState.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record TodayJournalState(
    JournalDate Date,
    JournalStatus Status,
    IReadOnlyList<RawInput> RawInputs,
    JournalDraft? Draft,
    JournalEntry? Entry,
    IReadOnlyList<string> Errors);
```

Create `src/Journal.Domain/Entries/JournalAiValidationResult.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalAiValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static JournalAiValidationResult Valid { get; } = new(true, Array.Empty<string>());

    public static JournalAiValidationResult Invalid(params string[] errors) => new(false, errors);
}
```

- [ ] **Step 4: Run domain tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalDateTests
```

Expected: PASS.

- [ ] **Step 5: Commit domain models**

Run:

```powershell
git add src/Journal.Domain/Entries tests/Journal.Tests/JournalDateTests.cs
git commit -m "feat(domain): add journal entry models"
```

## Task 2: Implement Local Journal Storage

**Files:**
- Modify: `tests/Journal.Tests/Journal.Tests.csproj`
- Create: `src/Journal.Infrastructure/Storage/JournalStorageOptions.cs`
- Create: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Create: `src/Journal.Infrastructure/Storage/RawInputStore.cs`
- Create: `src/Journal.Infrastructure/Storage/DraftStore.cs`
- Create: `src/Journal.Infrastructure/Storage/EntryStore.cs`
- Create: `tests/Journal.Tests/LocalJournalStorageTests.cs`

- [ ] **Step 1: Add infrastructure test reference**

Modify `tests/Journal.Tests/Journal.Tests.csproj` so the project references include `Journal.Infrastructure`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\Journal.Api\Journal.Api.csproj" />
    <ProjectReference Include="..\..\src\Journal.Domain\Journal.Domain.csproj" />
    <ProjectReference Include="..\..\src\Journal.Infrastructure\Journal.Infrastructure.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Write failing storage tests**

Create `tests/Journal.Tests/LocalJournalStorageTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class LocalJournalStorageTests
{
    [Fact]
    public void LocalJournalPaths_BuildsPhase2Paths()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(new DateOnly(2026, 5, 8));

        Assert.EndsWith(Path.Combine("entries", "2026", "05", "2026-05-08.md"), paths.EntryPath(date));
        Assert.EndsWith(Path.Combine(".journal", "raw-inputs", "2026", "05", "2026-05-08.jsonl"), paths.RawInputPath(date));
        Assert.EndsWith(Path.Combine(".journal", "drafts", "2026", "05", "2026-05-08.md"), paths.DraftPath(date));
        Assert.EndsWith(Path.Combine(".journal", "drafts", "2026", "05", "2026-05-08.meta.json"), paths.DraftMetaPath(date));
    }

    [Fact]
    public async Task RawInputStore_AppendsJsonLines()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var store = new RawInputStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));

        await store.AppendAsync(new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-08T08:00:00+08:00"), "text", "昨天完成骨架。"), CancellationToken.None);
        await store.AppendAsync(new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-08T08:03:00+08:00"), "text", "今天做 JMF。"), CancellationToken.None);

        var inputs = await store.ReadAsync(date, CancellationToken.None);

        Assert.Equal(["raw-1", "raw-2"], inputs.Select(input => input.Id).ToArray());
        Assert.Equal("今天做 JMF。", inputs[1].Text);
    }

    [Fact]
    public async Task DraftAndEntryStores_WriteReadableMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var draftStore = new DraftStore(paths);
        var entryStore = new EntryStore(paths);
        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            "# 2026-05-08\n\n<!-- journal:section raw-inputs -->",
            ["raw-1"],
            [],
            DateTimeOffset.Parse("2026-05-08T08:05:00+08:00"));

        await draftStore.WriteAsync(draft, CancellationToken.None);
        var loadedDraft = await draftStore.ReadAsync(date, CancellationToken.None);
        await entryStore.WriteAsync(date, loadedDraft!.Markdown, DateTimeOffset.Parse("2026-05-08T08:06:00+08:00"), CancellationToken.None);
        var entry = await entryStore.ReadAsync(date, CancellationToken.None);

        Assert.NotNull(loadedDraft);
        Assert.Equal(JournalStatus.Reviewing, loadedDraft.Status);
        Assert.Equal(["raw-1"], loadedDraft.SourceRawInputIds);
        Assert.NotNull(entry);
        Assert.Contains("journal:section raw-inputs", entry.Markdown);
        Assert.EndsWith(Path.Combine("entries", "2026", "05", "2026-05-08.md"), entry.Path);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter LocalJournalStorageTests
```

Expected: FAIL because storage classes do not exist.

- [ ] **Step 4: Implement storage options and paths**

Create `src/Journal.Infrastructure/Storage/JournalStorageOptions.cs`:

```csharp
namespace Journal.Infrastructure.Storage;

public sealed record JournalStorageOptions(string RootDirectory)
{
    public static JournalStorageOptions FromLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new JournalStorageOptions(Path.Combine(localAppData, "Journal"));
    }
}
```

Create `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class LocalJournalPaths
{
    private readonly string _rootDirectory;

    public LocalJournalPaths(JournalStorageOptions options)
    {
        _rootDirectory = options.RootDirectory;
    }

    public string EntryPath(JournalDate date) =>
        Path.Combine(_rootDirectory, "entries", date.Year, date.Month, date.MarkdownFileName);

    public string RawInputPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "raw-inputs", date.Year, date.Month, $"{date.IsoDate}.jsonl");

    public string DraftPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "drafts", date.Year, date.Month, date.MarkdownFileName);

    public string DraftMetaPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "drafts", date.Year, date.Month, $"{date.IsoDate}.meta.json");

    public static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
```

- [ ] **Step 5: Implement stores**

Create `src/Journal.Infrastructure/Storage/RawInputStore.cs`:

```csharp
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class RawInputStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LocalJournalPaths _paths;

    public RawInputStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task AppendAsync(RawInput input, CancellationToken cancellationToken)
    {
        var path = _paths.RawInputPath(input.Date);
        LocalJournalPaths.EnsureParentDirectory(path);
        var line = JsonSerializer.Serialize(RawInputLine.From(input), JsonOptions);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
    }

    public async Task<IReadOnlyList<RawInput>> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var path = _paths.RawInputPath(date);
        if (!File.Exists(path))
        {
            return Array.Empty<RawInput>();
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var inputs = new List<RawInput>();
        foreach (var line in lines.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var raw = JsonSerializer.Deserialize<RawInputLine>(line, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid raw input line in {path}.");
            inputs.Add(raw.ToRawInput());
        }

        return inputs;
    }

    private sealed record RawInputLine(string Id, string Date, DateTimeOffset CreatedAt, string Source, string Text)
    {
        public static RawInputLine From(RawInput input) =>
            new(input.Id, input.Date.IsoDate, input.CreatedAt, input.Source, input.Text);

        public RawInput ToRawInput() =>
            new(Id, JournalDate.From(DateOnly.Parse(Date)), CreatedAt, Source, Text);
    }
}
```

Create `src/Journal.Infrastructure/Storage/DraftStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class DraftStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly LocalJournalPaths _paths;

    public DraftStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(JournalDraft draft, CancellationToken cancellationToken)
    {
        var markdownPath = _paths.DraftPath(draft.Date);
        var metaPath = _paths.DraftMetaPath(draft.Date);
        LocalJournalPaths.EnsureParentDirectory(markdownPath);
        await File.WriteAllTextAsync(markdownPath, draft.Markdown, cancellationToken);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(DraftMeta.From(draft), JsonOptions), cancellationToken);
    }

    public async Task<JournalDraft?> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var markdownPath = _paths.DraftPath(date);
        var metaPath = _paths.DraftMetaPath(date);
        if (!File.Exists(markdownPath) || !File.Exists(metaPath))
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken);
        var meta = JsonSerializer.Deserialize<DraftMeta>(await File.ReadAllTextAsync(metaPath, cancellationToken), JsonOptions)
            ?? throw new InvalidOperationException($"Invalid draft metadata in {metaPath}.");
        return new JournalDraft(date, meta.Status, markdown, meta.SourceRawInputIds, meta.Errors, meta.UpdatedAt);
    }

    private sealed record DraftMeta(
        JournalStatus Status,
        IReadOnlyList<string> SourceRawInputIds,
        IReadOnlyList<string> Errors,
        DateTimeOffset UpdatedAt)
    {
        public static DraftMeta From(JournalDraft draft) =>
            new(draft.Status, draft.SourceRawInputIds, draft.Errors, draft.UpdatedAt);
    }
}
```

Create `src/Journal.Infrastructure/Storage/EntryStore.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class EntryStore
{
    private readonly LocalJournalPaths _paths;

    public EntryStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public bool Exists(JournalDate date) => File.Exists(_paths.EntryPath(date));

    public async Task WriteAsync(JournalDate date, string markdown, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var path = _paths.EntryPath(date);
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, markdown, cancellationToken);
    }

    public async Task<JournalEntry?> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var path = _paths.EntryPath(date);
        if (!File.Exists(path))
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(path, cancellationToken);
        var updatedAt = File.GetLastWriteTimeUtc(path);
        return new JournalEntry(date, markdown, path, new DateTimeOffset(updatedAt));
    }
}
```

- [ ] **Step 6: Run storage tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter LocalJournalStorageTests
```

Expected: PASS.

- [ ] **Step 7: Commit storage layer**

Run:

```powershell
git add src/Journal.Infrastructure/Storage tests/Journal.Tests/Journal.Tests.csproj tests/Journal.Tests/LocalJournalStorageTests.cs
git commit -m "feat(infrastructure): add local journal storage"
```

## Task 3: Implement Mock AI, Validation, And JMF Rendering

**Files:**
- Create: `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`
- Create: `src/Journal.Infrastructure/Ai/MockAiProvider.cs`
- Create: `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs`
- Create: `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`
- Create: `tests/Journal.Tests/MockAiAndJmfTests.cs`

- [ ] **Step 1: Write failing Mock AI and renderer tests**

Create `tests/Journal.Tests/MockAiAndJmfTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Jmf;

namespace Journal.Tests;

public sealed class MockAiAndJmfTests
{
    [Fact]
    public void MockAiProvider_ExtractsRulesFromAllRawInputs()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var provider = new MockAiProvider();
        var inputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-08T08:00:00+08:00"), "text", "昨天完成阶段 1，感觉有推进感 #工程"),
            new RawInput("raw-2", date, DateTimeOffset.Parse("2026-05-08T08:03:00+08:00"), "text", "今天准备做 JMF 主链路，想到可以先写规则测试。")
        };

        var result = provider.Generate(date, inputs, DateTimeOffset.Parse("2026-05-08T08:05:00+08:00"));

        Assert.Equal("journal-entry/v1", result.Schema);
        Assert.Equal("2026-05-08", result.Date);
        Assert.Contains("工程", result.Tags);
        Assert.Contains(result.YesterdayReview, item => item.Contains("阶段 1", StringComparison.Ordinal));
        Assert.Contains(result.TodayFocus, item => item.Contains("JMF 主链路", StringComparison.Ordinal));
        Assert.Contains(result.Inspiration, item => item.Contains("规则测试", StringComparison.Ordinal));
        Assert.Equal("有推进感", result.Mood);
    }

    [Fact]
    public void Validator_RequiresCoreSections()
    {
        var validator = new JournalAiJsonValidator();
        var invalid = new JournalAiJson(
            "journal-entry/v1",
            "2026-05-08",
            "05-08",
            "reviewing",
            [],
            [],
            "",
            [],
            [],
            [],
            []);

        var result = validator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains("rawInputs is required", result.Errors);
        Assert.Contains("yesterdayReview is required", result.Errors);
        Assert.Contains("todayFocus is required", result.Errors);
    }

    [Fact]
    public void Renderer_WritesJmfV1Markdown()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 8));
        var renderer = new JmfMarkdownRenderer();
        var json = new JournalAiJson(
            "journal-entry/v1",
            "2026-05-08",
            "05-08",
            "reviewing",
            ["工程"],
            ["JMF 主链路"],
            "有推进感",
            ["昨天完成阶段 1。", "今天做 JMF。"],
            ["完成阶段 1。"],
            ["做 JMF 主链路。"],
            ["先写规则测试。"]);

        var markdown = renderer.Render(date, json, DateTimeOffset.Parse("2026-05-08T08:05:00+08:00"));

        Assert.Contains("schema: journal-entry/v1", markdown);
        Assert.Contains("status: reviewing", markdown);
        Assert.Contains("provider: mock", markdown);
        Assert.Contains("model: mock-journal", markdown);
        Assert.Contains("<!-- journal:section raw-inputs -->", markdown);
        Assert.Contains("<!-- /journal:section raw-inputs -->", markdown);
        Assert.Contains("## 今日重点", markdown);
        Assert.Contains("- 做 JMF 主链路。", markdown);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter MockAiAndJmfTests
```

Expected: FAIL because Mock AI, validator, and renderer do not exist.

- [ ] **Step 3: Implement Mock AI provider**

Create `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public interface IJournalAiProvider
{
    JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt);
}
```

Create `src/Journal.Infrastructure/Ai/MockAiProvider.cs`:

```csharp
using System.Text.RegularExpressions;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed partial class MockAiProvider : IJournalAiProvider
{
    public JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt)
    {
        var texts = rawInputs.Select(input => input.Text.Trim()).Where(text => text.Length > 0).ToArray();
        var yesterday = Pick(texts, ["昨天", "昨晚", "上次", "完成了"], "还没有提到昨日回顾。");
        var today = Pick(texts, ["今天", "接下来", "准备", "要做", "计划"], "今天先保持一次清晰记录。");
        var inspiration = PickOptional(texts, ["想到", "灵感", "应该", "可以", "原则"]);
        var tags = ExtractTags(texts);
        var mood = ExtractMood(texts);
        var topics = tags.Count > 0 ? tags : ExtractTopics(today);

        return new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "reviewing",
            tags,
            topics,
            mood,
            texts.Length > 0 ? texts : ["今天还没有输入内容。"],
            yesterday,
            today,
            inspiration);
    }

    private static IReadOnlyList<string> Pick(IEnumerable<string> texts, string[] keywords, string fallback)
    {
        var matches = texts.Where(text => keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal))).ToArray();
        return matches.Length > 0 ? matches : [fallback];
    }

    private static IReadOnlyList<string> PickOptional(IEnumerable<string> texts, string[] keywords) =>
        texts.Where(text => keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal))).ToArray();

    private static IReadOnlyList<string> ExtractTags(IEnumerable<string> texts)
    {
        return texts
            .SelectMany(text => TagRegex().Matches(text).Select(match => match.Groups["tag"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ExtractMood(IEnumerable<string> texts)
    {
        var joined = string.Join(" ", texts);
        foreach (var mood in new[] { "有推进感", "平静", "开心", "焦虑", "累" })
        {
            if (joined.Contains(mood, StringComparison.Ordinal))
            {
                return mood;
            }
        }

        return "未标注";
    }

    private static IReadOnlyList<string> ExtractTopics(IReadOnlyList<string> todayFocus)
    {
        return todayFocus
            .Select(item => item.Length <= 18 ? item : item[..18])
            .Take(3)
            .ToArray();
    }

    [GeneratedRegex("#(?<tag>[\\p{L}\\p{N}_-]+)", RegexOptions.Compiled)]
    private static partial Regex TagRegex();
}
```

- [ ] **Step 4: Implement validator and renderer**

Create `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public sealed class JournalAiJsonValidator
{
    public JournalAiValidationResult Validate(JournalAiJson json)
    {
        var errors = new List<string>();

        if (json.Schema != "journal-entry/v1")
        {
            errors.Add("schema must be journal-entry/v1");
        }

        if (string.IsNullOrWhiteSpace(json.Date))
        {
            errors.Add("date is required");
        }

        if (string.IsNullOrWhiteSpace(json.MonthDay))
        {
            errors.Add("monthDay is required");
        }

        if (json.RawInputs.Count == 0)
        {
            errors.Add("rawInputs is required");
        }

        if (json.YesterdayReview.Count == 0)
        {
            errors.Add("yesterdayReview is required");
        }

        if (json.TodayFocus.Count == 0)
        {
            errors.Add("todayFocus is required");
        }

        return errors.Count == 0 ? JournalAiValidationResult.Valid : new JournalAiValidationResult(false, errors);
    }
}
```

Create `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`:

```csharp
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public sealed class JmfMarkdownRenderer
{
    public string Render(JournalDate date, JournalAiJson json, DateTimeOffset generatedAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("schema: journal-entry/v1");
        builder.AppendLine($"date: {date.IsoDate}");
        builder.AppendLine($"month_day: {date.MonthDay}");
        builder.AppendLine("status: reviewing");
        builder.AppendLine("tags:");
        AppendYamlList(builder, json.Tags);
        builder.AppendLine("topics:");
        AppendYamlList(builder, json.Topics);
        builder.AppendLine($"mood: {EscapeYaml(json.Mood)}");
        builder.AppendLine("version: 1");
        builder.AppendLine("provider: mock");
        builder.AppendLine("model: mock-journal");
        builder.AppendLine("prompt_version: mock-journal-entry-v1");
        builder.AppendLine($"generated_at: {generatedAt:O}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {date.IsoDate} 晨间日记");
        AppendSection(builder, "raw-inputs", "原始输入", json.RawInputs);
        AppendSection(builder, "yesterday-review", "昨日回顾", json.YesterdayReview);
        AppendSection(builder, "today-focus", "今日重点", json.TodayFocus);
        if (!string.IsNullOrWhiteSpace(json.Mood) && json.Mood != "未标注")
        {
            AppendSection(builder, "mood", "情绪", [json.Mood]);
        }

        if (json.Inspiration.Count > 0)
        {
            AppendSection(builder, "inspiration", "灵感", json.Inspiration);
        }

        return builder.ToString();
    }

    private static void AppendYamlList(StringBuilder builder, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            builder.AppendLine("  []");
            return;
        }

        foreach (var value in values)
        {
            builder.AppendLine($"  - {EscapeYaml(value)}");
        }
    }

    private static void AppendSection(StringBuilder builder, string id, string title, IReadOnlyList<string> values)
    {
        builder.AppendLine();
        builder.AppendLine($"<!-- journal:section {id} -->");
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var value in values)
        {
            builder.AppendLine($"- {value}");
        }

        builder.AppendLine($"<!-- /journal:section {id} -->");
    }

    private static string EscapeYaml(string value) =>
        value.Contains(':', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
```

- [ ] **Step 5: Run Mock AI and renderer tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter MockAiAndJmfTests
```

Expected: PASS.

- [ ] **Step 6: Commit Mock AI and renderer**

Run:

```powershell
git add src/Journal.Infrastructure/Ai src/Journal.Infrastructure/Jmf tests/Journal.Tests/MockAiAndJmfTests.cs
git commit -m "feat(jmf): add mock ai and markdown renderer"
```

## Task 4: Implement Today Workflow Service

**Files:**
- Create: `src/Journal.Infrastructure/Clock/IJournalClock.cs`
- Create: `src/Journal.Infrastructure/Clock/SystemJournalClock.cs`
- Create: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Create: `tests/Journal.Tests/TodayJournalServiceTests.cs`

- [ ] **Step 1: Write failing workflow tests**

Create `tests/Journal.Tests/TodayJournalServiceTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class TodayJournalServiceTests
{
    [Fact]
    public async Task AddInputAsync_AppendsRawInputAndCreatesReviewingDraft()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root);

        var state = await service.AddInputAsync("昨天完成阶段 1，今天准备做 JMF 主链路。 #工程", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, state.Status);
        Assert.Single(state.RawInputs);
        Assert.NotNull(state.Draft);
        Assert.Contains("journal:section raw-inputs", state.Draft.Markdown);
        Assert.Contains("JMF 主链路", state.Draft.Markdown);
        Assert.True(File.Exists(Path.Combine(workspace.Root, ".journal", "raw-inputs", "2026", "05", "2026-05-08.jsonl")));
        Assert.True(File.Exists(Path.Combine(workspace.Root, ".journal", "drafts", "2026", "05", "2026-05-08.md")));
    }

    [Fact]
    public async Task AddInputAsync_UsesAllRawInputsForRegeneration()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root);

        await service.AddInputAsync("昨天完成阶段 1。", "text", CancellationToken.None);
        var state = await service.AddInputAsync("今天准备做 JMF 主链路。", "text", CancellationToken.None);

        Assert.Equal(2, state.RawInputs.Count);
        Assert.Contains("昨天完成阶段 1。", state.Draft!.Markdown);
        Assert.Contains("今天准备做 JMF 主链路。", state.Draft.Markdown);
    }

    [Fact]
    public async Task ConfirmDraftAsync_WritesFormalEntry()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root);
        await service.AddInputAsync("昨天完成阶段 1，今天准备做 JMF 主链路。", "text", CancellationToken.None);

        var state = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, state.Status);
        Assert.NotNull(state.Entry);
        Assert.Contains("schema: journal-entry/v1", state.Entry.Markdown);
        Assert.True(File.Exists(Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-08.md")));
    }

    [Fact]
    public async Task ConfirmDraftAsync_ReturnsUpdatedWhenEntryAlreadyExists()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root);
        await service.AddInputAsync("今天准备做 JMF。", "text", CancellationToken.None);
        await service.ConfirmDraftAsync(CancellationToken.None);
        await service.AddInputAsync("再补充一个灵感，可以先写规则测试。", "text", CancellationToken.None);

        var state = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Updated, state.Status);
        Assert.Contains("规则测试", state.Entry!.Markdown);
    }

    [Fact]
    public async Task AddInputAsync_InvalidAiJsonCreatesAttentionDraftWithoutEntry()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, aiProvider: new InvalidAiProvider());

        var state = await service.AddInputAsync("今天准备做 JMF。", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Attention, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Contains("rawInputs is required", state.Errors);
        Assert.False(File.Exists(Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-08.md")));
    }

    private static TodayJournalService CreateService(string root, IJournalAiProvider? aiProvider = null)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        return new TodayJournalService(
            new FixedJournalClock(),
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            aiProvider ?? new MockAiProvider(),
            new JournalAiJsonValidator(),
            new JmfMarkdownRenderer());
    }

    private sealed class FixedJournalClock : IJournalClock
    {
        public DateOnly Today => new(2026, 5, 8);

        public DateTimeOffset Now => DateTimeOffset.Parse("2026-05-08T08:05:00+08:00");
    }

    private sealed class InvalidAiProvider : IJournalAiProvider
    {
        public JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt) =>
            new("journal-entry/v1", date.IsoDate, date.MonthDay, "reviewing", [], [], "", [], [], [], []);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalServiceTests
```

Expected: FAIL because `TodayJournalService` and clock classes do not exist.

- [ ] **Step 3: Implement clock types**

Create `src/Journal.Infrastructure/Clock/IJournalClock.cs`:

```csharp
namespace Journal.Infrastructure.Clock;

public interface IJournalClock
{
    DateOnly Today { get; }

    DateTimeOffset Now { get; }
}
```

Create `src/Journal.Infrastructure/Clock/SystemJournalClock.cs`:

```csharp
namespace Journal.Infrastructure.Clock;

public sealed class SystemJournalClock : IJournalClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public DateTimeOffset Now => DateTimeOffset.Now;
}
```

- [ ] **Step 4: Implement today workflow service**

Create `src/Journal.Infrastructure/Today/TodayJournalService.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed class TodayJournalService
{
    private readonly IJournalClock _clock;
    private readonly RawInputStore _rawInputStore;
    private readonly DraftStore _draftStore;
    private readonly EntryStore _entryStore;
    private readonly IJournalAiProvider _aiProvider;
    private readonly JournalAiJsonValidator _validator;
    private readonly JmfMarkdownRenderer _renderer;

    public TodayJournalService(
        IJournalClock clock,
        RawInputStore rawInputStore,
        DraftStore draftStore,
        EntryStore entryStore,
        IJournalAiProvider aiProvider,
        JournalAiJsonValidator validator,
        JmfMarkdownRenderer renderer)
    {
        _clock = clock;
        _rawInputStore = rawInputStore;
        _draftStore = draftStore;
        _entryStore = entryStore;
        _aiProvider = aiProvider;
        _validator = validator;
        _renderer = renderer;
    }

    public async Task<TodayJournalState> GetTodayAsync(CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        return await BuildStateAsync(date, statusOverride: null, cancellationToken);
    }

    public async Task<TodayJournalState> AddInputAsync(string text, string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text is required", nameof(text));
        }

        var date = JournalDate.From(_clock.Today);
        var input = new RawInput($"raw-{Guid.NewGuid():N}", date, _clock.Now, string.IsNullOrWhiteSpace(source) ? "text" : source, text.Trim());
        await _rawInputStore.AppendAsync(input, cancellationToken);
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var aiJson = _aiProvider.Generate(date, inputs, _clock.Now);
        var validation = _validator.Validate(aiJson);

        if (!validation.IsValid)
        {
            var attentionDraft = new JournalDraft(
                date,
                JournalStatus.Attention,
                BuildAttentionMarkdown(date, validation.Errors),
                inputs.Select(raw => raw.Id).ToArray(),
                validation.Errors,
                _clock.Now);
            await _draftStore.WriteAsync(attentionDraft, cancellationToken);
            return await BuildStateAsync(date, JournalStatus.Attention, cancellationToken);
        }

        var markdown = _renderer.Render(date, aiJson, _clock.Now);
        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            markdown,
            inputs.Select(raw => raw.Id).ToArray(),
            Array.Empty<string>(),
            _clock.Now);
        await _draftStore.WriteAsync(draft, cancellationToken);
        return await BuildStateAsync(date, JournalStatus.Reviewing, cancellationToken);
    }

    public async Task<TodayJournalState> ConfirmDraftAsync(CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var draft = await _draftStore.ReadAsync(date, cancellationToken)
            ?? throw new InvalidOperationException("No draft is available to confirm.");
        if (draft.Status != JournalStatus.Reviewing)
        {
            throw new InvalidOperationException("Only reviewing drafts can be confirmed.");
        }

        var existed = _entryStore.Exists(date);
        await _entryStore.WriteAsync(date, draft.Markdown, _clock.Now, cancellationToken);
        var confirmedStatus = existed ? JournalStatus.Updated : JournalStatus.Processed;
        await _draftStore.WriteAsync(draft with { Status = confirmedStatus, UpdatedAt = _clock.Now }, cancellationToken);
        return await BuildStateAsync(date, confirmedStatus, cancellationToken);
    }

    private async Task<TodayJournalState> BuildStateAsync(JournalDate date, JournalStatus? statusOverride, CancellationToken cancellationToken)
    {
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var draft = await _draftStore.ReadAsync(date, cancellationToken);
        var entry = await _entryStore.ReadAsync(date, cancellationToken);
        var status = statusOverride ?? ResolveStatus(draft, entry);
        var errors = draft?.Errors ?? Array.Empty<string>();
        return new TodayJournalState(date, status, inputs, draft, entry, errors);
    }

    private static JournalStatus ResolveStatus(JournalDraft? draft, JournalEntry? entry)
    {
        if (draft is not null)
        {
            return draft.Status;
        }

        return entry is not null ? JournalStatus.Processed : JournalStatus.Empty;
    }

    private static string BuildAttentionMarkdown(JournalDate date, IReadOnlyList<string> errors) =>
        $"# {date.IsoDate} 需要处理{Environment.NewLine}{Environment.NewLine}" +
        string.Join(Environment.NewLine, errors.Select(error => $"- {error}")) +
        Environment.NewLine;
}
```

- [ ] **Step 5: Run workflow tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalServiceTests
```

Expected: PASS.

- [ ] **Step 6: Commit workflow service**

Run:

```powershell
git add src/Journal.Infrastructure/Clock src/Journal.Infrastructure/Today tests/Journal.Tests/TodayJournalServiceTests.cs
git commit -m "feat(journal): add today workflow service"
```

## Task 5: Expose Today Workflow API

**Files:**
- Modify: `src/Journal.Api/Program.cs`
- Create: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Create `tests/Journal.Tests/TodayJournalEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class TodayJournalEndpointTests
{
    [Fact]
    public async Task GetToday_ReturnsEmptyState()
    {
        using var workspace = TempWorkspace.Create();
        await using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/journal/today");

        using var document = JsonDocument.Parse(json);
        Assert.Equal("empty", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("2026-05-08", document.RootElement.GetProperty("date").GetProperty("isoDate").GetString());
    }

    [Fact]
    public async Task PostInput_CreatesReviewingDraft()
    {
        using var workspace = TempWorkspace.Create();
        await using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/journal/today/inputs", new { text = "昨天完成阶段 1，今天准备做 JMF 主链路。", source = "text" });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal("reviewing", document.RootElement.GetProperty("status").GetString());
        Assert.Contains("journal:section raw-inputs", document.RootElement.GetProperty("draft").GetProperty("markdown").GetString());
    }

    [Fact]
    public async Task PostInput_RejectsBlankText()
    {
        using var workspace = TempWorkspace.Create();
        await using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/journal/today/inputs", new { text = " ", source = "text" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmDraft_WritesEntry()
    {
        using var workspace = TempWorkspace.Create();
        await using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/journal/today/inputs", new { text = "今天准备做 JMF 主链路。", source = "text" });

        var response = await client.PostAsync("/journal/today/draft/confirm", content: null);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal("processed", document.RootElement.GetProperty("status").GetString());
        Assert.True(File.Exists(Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-08.md")));
    }

    private static WebApplicationFactory<Program> CreateFactory(string root)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ENVIRONMENT", "Development");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<JournalStorageOptions>();
                    services.AddSingleton(new JournalStorageOptions(root));
                });
            });
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-api-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 2: Run endpoint tests to verify failure**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEndpointTests
```

Expected: FAIL because `/journal/today` endpoints and DI registration do not exist.

- [ ] **Step 3: Register services and endpoints**

Modify `src/Journal.Api/Program.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Application;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopDevelopment", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton(JournalStorageOptions.FromLocalAppData());
builder.Services.AddSingleton<LocalJournalPaths>();
builder.Services.AddSingleton<IJournalClock, SystemJournalClock>();
builder.Services.AddSingleton<RawInputStore>();
builder.Services.AddSingleton<DraftStore>();
builder.Services.AddSingleton<EntryStore>();
builder.Services.AddSingleton<IJournalAiProvider, MockAiProvider>();
builder.Services.AddSingleton<JournalAiJsonValidator>();
builder.Services.AddSingleton<JmfMarkdownRenderer>();
builder.Services.AddSingleton<TodayJournalService>();

var app = builder.Build();

app.UseCors("DesktopDevelopment");

app.MapGet("/health", (IHostEnvironment environment) =>
{
    return Results.Ok(new HealthResponse(
        ApplicationInfo.Name,
        "ok",
        ApplicationInfo.Version,
        environment.EnvironmentName,
        DateTimeOffset.Now));
});

app.MapGet("/journal/today", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    return Results.Ok(await service.GetTodayAsync(cancellationToken));
});

app.MapPost("/journal/today/inputs", async (
    AddTodayInputRequest request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "text is required" });
    }

    var state = await service.AddInputAsync(request.Text, request.Source ?? "text", cancellationToken);
    return Results.Ok(state);
});

app.MapPost("/journal/today/draft/confirm", async (TodayJournalService service, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.ConfirmDraftAsync(cancellationToken));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.Run();

public partial class Program
{
}

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);

public sealed record AddTodayInputRequest(string Text, string? Source);
```

- [ ] **Step 4: Run endpoint tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEndpointTests
```

Expected: PASS.

- [ ] **Step 5: Run full .NET tests**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: PASS.

- [ ] **Step 6: Commit API endpoints**

Run:

```powershell
git add src/Journal.Api/Program.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat(api): expose today journal workflow"
```

## Task 6: Add Frontend API Client And Markdown Preview

**Files:**
- Modify: `apps/desktop/package.json`
- Modify: `apps/desktop/package-lock.json`
- Create: `apps/desktop/src/api.ts`
- Create: `apps/desktop/src/MarkdownPreview.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Install Markdown rendering dependencies**

Run:

```powershell
npm install --prefix apps/desktop react-markdown remark-gfm
```

Expected: `apps/desktop/package.json` and `apps/desktop/package-lock.json` include `react-markdown` and `remark-gfm`.

- [ ] **Step 2: Create API client**

Create `apps/desktop/src/api.ts`:

```ts
export type JournalStatus = "empty" | "draft" | "reviewing" | "processed" | "updated" | "attention";

export type JournalDate = {
  value: string;
  year: string;
  month: string;
  isoDate: string;
  monthDay: string;
  markdownFileName: string;
};

export type RawInput = {
  id: string;
  date: JournalDate;
  createdAt: string;
  source: string;
  text: string;
};

export type JournalDraft = {
  date: JournalDate;
  status: JournalStatus;
  markdown: string;
  sourceRawInputIds: string[];
  errors: string[];
  updatedAt: string;
};

export type JournalEntry = {
  date: JournalDate;
  markdown: string;
  path: string;
  updatedAt: string;
};

export type TodayJournalState = {
  date: JournalDate;
  status: JournalStatus;
  rawInputs: RawInput[];
  draft: JournalDraft | null;
  entry: JournalEntry | null;
  errors: string[];
};

export type HealthResponse = {
  app: string;
  status: string;
  version: string;
  environment: string;
  serverTime: string;
};

const apiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    throw new Error(`${path} failed: ${response.status}`);
  }

  return await response.json() as T;
}

export function getHealth(): Promise<HealthResponse> {
  return requestJson<HealthResponse>("/health");
}

export function getToday(): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today");
}

export function addTodayInput(text: string): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/inputs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text, source: "text" })
  });
}

export function confirmTodayDraft(): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/draft/confirm", {
    method: "POST"
  });
}
```

- [ ] **Step 3: Create Markdown preview component**

Create `apps/desktop/src/MarkdownPreview.tsx`:

```tsx
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

type MarkdownPreviewProps = {
  markdown: string;
};

export default function MarkdownPreview({ markdown }: MarkdownPreviewProps) {
  return (
    <div className="markdown-preview" data-testid="markdown-preview">
      <ReactMarkdown remarkPlugins={[remarkGfm]}>
        {markdown}
      </ReactMarkdown>
    </div>
  );
}
```

- [ ] **Step 4: Replace frontend tests with Phase 2 expectations**

Replace `apps/desktop/src/App.test.tsx`:

```tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";
import type { TodayJournalState } from "./api";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("App", () => {
  test("renders empty today workbench", async () => {
    vi.stubGlobal("fetch", createFetchMock([healthResponse(), todayState({ status: "empty" })]));

    render(<App />);

    await waitFor(() => expect(screen.getByRole("heading", { name: "2026-05-08 晨间日记" })).toBeInTheDocument());
    expect(screen.getByText("empty")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "补充今天的自然语言输入" })).toBeInTheDocument();
    expect(screen.getByText("还没有草稿")).toBeInTheDocument();
  });

  test("submits input and renders reviewing draft", async () => {
    const fetchMock = createFetchMock([
      healthResponse(),
      todayState({ status: "empty" }),
      todayState({
        status: "reviewing",
        rawInputs: [{ id: "raw-1", text: "今天准备做 JMF 主链路。" }],
        draftMarkdown: "# 2026-05-08 晨间日记\n\n## 今日重点\n\n- 今天准备做 JMF 主链路。"
      })
    ]);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    await screen.findByRole("textbox", { name: "补充今天的自然语言输入" });
    fireEvent.change(screen.getByRole("textbox", { name: "补充今天的自然语言输入" }), {
      target: { value: "今天准备做 JMF 主链路。" }
    });
    fireEvent.click(screen.getByRole("button", { name: "生成草稿" }));

    await waitFor(() => expect(screen.getByText("reviewing")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "确认写入正式日记" })).toBeInTheDocument();
    expect(screen.getAllByText("今天准备做 JMF 主链路。").length).toBeGreaterThan(0);
  });

  test("renders attention state without confirm button", async () => {
    vi.stubGlobal("fetch", createFetchMock([
      healthResponse(),
      todayState({
        status: "attention",
        errors: ["todayFocus is required"],
        draftMarkdown: "# 2026-05-08 需要处理\n\n- todayFocus is required"
      })
    ]));

    render(<App />);

    await waitFor(() => expect(screen.getByText("attention")).toBeInTheDocument());
    expect(screen.getAllByText("todayFocus is required").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "确认写入正式日记" })).not.toBeInTheDocument();
  });

  test("confirms reviewing draft", async () => {
    const fetchMock = createFetchMock([
      healthResponse(),
      todayState({
        status: "reviewing",
        draftMarkdown: "# 2026-05-08 晨间日记"
      }),
      todayState({
        status: "processed",
        draftMarkdown: "# 2026-05-08 晨间日记",
        entryPath: "C:/Users/test/AppData/Local/Journal/entries/2026/05/2026-05-08.md"
      })
    ]);
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    await screen.findByRole("button", { name: "确认写入正式日记" });
    fireEvent.click(screen.getByRole("button", { name: "确认写入正式日记" }));

    await waitFor(() => expect(screen.getByText("processed")).toBeInTheDocument());
    expect(screen.getByText(/2026-05-08.md/)).toBeInTheDocument();
  });
});

function createFetchMock(payloads: unknown[]) {
  return vi.fn().mockImplementation(() => {
    const payload = payloads.shift();
    return Promise.resolve({
      ok: true,
      json: async () => payload
    });
  });
}

function healthResponse() {
  return {
    app: "Journal.Api",
    status: "ok",
    version: "0.1.0",
    environment: "Development",
    serverTime: "2026-05-08T08:00:00+08:00"
  };
}

function todayState(overrides: {
  status: TodayJournalState["status"];
  rawInputs?: Array<{ id: string; text: string }>;
  draftMarkdown?: string;
  errors?: string[];
  entryPath?: string;
}): TodayJournalState {
  const date = {
    value: "2026-05-08",
    year: "2026",
    month: "05",
    isoDate: "2026-05-08",
    monthDay: "05-08",
    markdownFileName: "2026-05-08.md"
  };

  return {
    date,
    status: overrides.status,
    rawInputs: (overrides.rawInputs ?? []).map(input => ({
      id: input.id,
      date,
      createdAt: "2026-05-08T08:00:00+08:00",
      source: "text",
      text: input.text
    })),
    draft: overrides.draftMarkdown
      ? {
          date,
          status: overrides.status,
          markdown: overrides.draftMarkdown,
          sourceRawInputIds: ["raw-1"],
          errors: overrides.errors ?? [],
          updatedAt: "2026-05-08T08:05:00+08:00"
        }
      : null,
    entry: overrides.entryPath
      ? {
          date,
          markdown: overrides.draftMarkdown ?? "",
          path: overrides.entryPath,
          updatedAt: "2026-05-08T08:06:00+08:00"
        }
      : null,
    errors: overrides.errors ?? []
  };
}
```

- [ ] **Step 5: Run frontend tests to verify failure**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: FAIL because `App.tsx` still renders the Phase 1 skeleton.

- [ ] **Step 6: Keep the frontend tests red and continue to Task 7**

Do not commit the frontend changes yet. Task 7 turns this red test suite green and commits the tested UI implementation together with the API client.

Expected: the working tree contains the frontend API client, Markdown preview component, and red Phase 2 React tests.

## Task 7: Implement Today Workbench UI

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/styles.css`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Replace `App.tsx` with Today Workbench**

Replace `apps/desktop/src/App.tsx`:

```tsx
import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  addTodayInput,
  confirmTodayDraft,
  getHealth,
  getToday,
  type HealthResponse,
  type TodayJournalState
} from "./api";
import MarkdownPreview from "./MarkdownPreview";
import "./styles.css";

type LoadState = "loading" | "ready" | "error";

export default function App() {
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [today, setToday] = useState<TodayJournalState | null>(null);
  const [input, setInput] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [healthResult, todayResult] = await Promise.all([getHealth(), getToday()]);
        if (!cancelled) {
          setHealth(healthResult);
          setToday(todayResult);
          setLoadState("ready");
          setError("");
        }
      } catch (caught) {
        if (!cancelled) {
          setLoadState("error");
          setError(caught instanceof Error ? caught.message : "unknown error");
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  const title = useMemo(() => {
    return today ? `${today.date.isoDate} 晨间日记` : "今日晨间日记";
  }, [today]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!input.trim()) {
      setError("请输入一段今天的自然语言内容。");
      return;
    }

    setIsSubmitting(true);
    try {
      const next = await addTodayInput(input);
      setToday(next);
      setInput("");
      setError("");
      setLoadState("ready");
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "unknown error");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleConfirm() {
    setIsSubmitting(true);
    try {
      const next = await confirmTodayDraft();
      setToday(next);
      setError("");
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "unknown error");
    } finally {
      setIsSubmitting(false);
    }
  }

  const canConfirm = today?.status === "reviewing" && today.draft !== null;
  const markdown = today?.draft?.markdown ?? today?.entry?.markdown ?? "";

  return (
    <main className="today-shell">
      <header className="top-context">
        <div>
          <span className="eyebrow">Journal</span>
          <h1>{title}</h1>
        </div>
        <div className="status-strip" aria-label="运行状态">
          <span className={`status-pill ${today?.status ?? "loading"}`}>{today?.status ?? loadState}</span>
          <span>{health ? `API ${health.status}` : "API checking"}</span>
        </div>
      </header>

      <section className="workspace">
        <aside className="context-rail" aria-label="今日上下文">
          <div>
            <strong>Raw inputs</strong>
            <span>{today?.rawInputs.length ?? 0} 条</span>
          </div>
          <ol>
            {(today?.rawInputs ?? []).map(raw => (
              <li key={raw.id}>{raw.text}</li>
            ))}
          </ol>
        </aside>

        <section className="journal-stage" aria-label="日记预览">
          <div className="compact-actions" aria-label="紧凑窗口操作">
            <button type="button" onClick={() => document.getElementById("today-input")?.focus()}>
              补充输入
            </button>
            {canConfirm ? (
              <button type="button" className="primary" onClick={handleConfirm} disabled={isSubmitting}>
                确认保存
              </button>
            ) : null}
          </div>

          <article className="journal-paper">
            {loadState === "loading" ? <p>正在读取今天的日记状态...</p> : null}
            {loadState === "error" ? <p className="error-text">{error}</p> : null}
            {markdown ? <MarkdownPreview markdown={markdown} /> : <p className="empty-paper">还没有草稿</p>}
          </article>
        </section>

        <aside className="input-dock" aria-label="输入与确认">
          <form onSubmit={handleSubmit}>
            <label htmlFor="today-input">补充今天的自然语言输入</label>
            <textarea
              id="today-input"
              value={input}
              onChange={event => setInput(event.target.value)}
              placeholder="例如：昨天把阶段 1 跑通了，今天准备做 JMF 主链路。"
            />
            <button type="submit" className="primary" disabled={isSubmitting}>
              生成草稿
            </button>
          </form>

          {today?.errors.length ? (
            <section className="attention-panel" aria-label="需要处理">
              <strong>需要处理</strong>
              <ul>
                {today.errors.map(item => <li key={item}>{item}</li>)}
              </ul>
            </section>
          ) : null}

          {canConfirm ? (
            <section className="confirm-panel">
              <strong>草稿可以确认</strong>
              <p>确认后更新当天正式 Markdown；阶段 2 不创建版本快照。</p>
              <button type="button" className="primary" onClick={handleConfirm} disabled={isSubmitting}>
                确认写入正式日记
              </button>
            </section>
          ) : null}

          {today?.entry ? (
            <section className="file-panel">
              <strong>正式文件</strong>
              <p>{today.entry.path}</p>
            </section>
          ) : null}
        </aside>
      </section>
    </main>
  );
}
```

- [ ] **Step 2: Replace styles with V3 responsive layout**

Replace `apps/desktop/src/styles.css` with CSS that follows the V3 prototype and keeps the diary paper as the visual center:

```css
:root {
  color: #27231f;
  background: #f4eee5;
  font-family: Inter, "Microsoft YaHei", "PingFang SC", system-ui, sans-serif;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-width: 960px;
  min-height: 100vh;
  background: linear-gradient(135deg, #f7f1e8 0%, #e8efe8 45%, #f5eee3 100%);
}

button,
textarea {
  font: inherit;
}

button {
  min-height: 38px;
  border: 1px solid rgba(48, 42, 35, 0.2);
  background: #fffaf1;
  color: #27231f;
  cursor: pointer;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.58;
}

.primary {
  border-color: #1d6f64;
  background: #1d6f64;
  color: #fffaf1;
}

.today-shell {
  min-height: 100vh;
  padding: 20px;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 16px;
}

.top-context {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 20px;
}

.eyebrow {
  display: block;
  margin-bottom: 6px;
  color: #1d6f64;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

h1 {
  margin: 0;
  font-size: 30px;
  line-height: 1.1;
}

.status-strip {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #6b6258;
  font-size: 13px;
}

.status-pill {
  min-width: 92px;
  padding: 7px 10px;
  text-align: center;
  border: 1px solid rgba(48, 42, 35, 0.16);
  background: #fffaf1;
}

.status-pill.reviewing,
.status-pill.processed,
.status-pill.updated {
  color: #0c5d52;
  background: #d9eee8;
}

.status-pill.attention {
  color: #8b2d21;
  background: #f6d3ca;
}

.workspace {
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 318px;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 16px;
}

.context-rail {
  grid-column: 1 / -1;
  min-height: 72px;
  display: grid;
  grid-template-columns: 150px minmax(0, 1fr);
  gap: 14px;
  align-items: center;
  padding: 14px 16px;
  border: 1px solid rgba(48, 42, 35, 0.14);
  background: rgba(255, 250, 241, 0.72);
}

.context-rail div {
  display: grid;
  gap: 4px;
}

.context-rail span,
.context-rail li {
  color: #6b6258;
  font-size: 13px;
}

.context-rail ol {
  margin: 0;
  padding-left: 20px;
  display: flex;
  gap: 20px;
  overflow: hidden;
}

.context-rail li {
  max-width: 280px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.journal-stage {
  min-width: 0;
  min-height: 0;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 10px;
}

.compact-actions {
  display: none;
}

.journal-paper {
  min-height: 0;
  overflow: auto;
  padding: 42px 52px;
  border: 1px solid rgba(48, 42, 35, 0.13);
  background: #fffdf7;
  box-shadow: 0 22px 60px rgba(80, 63, 42, 0.12);
}

.empty-paper,
.error-text {
  margin: 0;
  color: #7b7063;
}

.markdown-preview {
  max-width: 760px;
  margin: 0 auto;
  line-height: 1.72;
}

.markdown-preview h1 {
  margin-bottom: 24px;
  font-size: 32px;
}

.markdown-preview h2 {
  margin: 28px 0 10px;
  font-size: 19px;
}

.markdown-preview p,
.markdown-preview li {
  color: #403a33;
}

.input-dock {
  min-width: 0;
  min-height: 0;
  overflow: auto;
  display: grid;
  align-content: start;
  gap: 12px;
}

.input-dock form,
.attention-panel,
.confirm-panel,
.file-panel {
  padding: 16px;
  border: 1px solid rgba(48, 42, 35, 0.14);
  background: rgba(255, 250, 241, 0.82);
}

label,
.attention-panel strong,
.confirm-panel strong,
.file-panel strong {
  display: block;
  margin-bottom: 10px;
  font-weight: 800;
}

textarea {
  width: 100%;
  min-height: 150px;
  resize: vertical;
  padding: 12px;
  border: 1px solid rgba(48, 42, 35, 0.18);
  background: #fffdf7;
  color: #27231f;
}

form button {
  width: 100%;
  margin-top: 10px;
}

.attention-panel {
  border-color: rgba(139, 45, 33, 0.26);
  background: #fff3ef;
}

.attention-panel ul {
  margin: 0;
  padding-left: 18px;
  color: #8b2d21;
}

.confirm-panel p,
.file-panel p {
  margin: 0 0 12px;
  color: #6b6258;
  font-size: 13px;
  word-break: break-word;
}

@media (min-width: 1360px) {
  .workspace {
    grid-template-columns: 230px minmax(0, 1fr) 340px;
    grid-template-rows: minmax(0, 1fr);
  }

  .context-rail {
    grid-column: auto;
    grid-template-columns: 1fr;
    align-content: start;
  }

  .context-rail ol {
    display: grid;
    gap: 10px;
  }
}

@media (max-width: 1080px) {
  .today-shell {
    padding: 14px;
  }

  .workspace {
    grid-template-columns: minmax(0, 1fr);
  }

  .input-dock {
    max-height: 220px;
  }

  .compact-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }

  .journal-paper {
    padding: 28px 34px;
  }
}

@media (max-height: 720px) {
  .journal-paper {
    padding-top: 24px;
    padding-bottom: 24px;
  }

  textarea {
    min-height: 108px;
  }
}
```

- [ ] **Step 3: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: PASS.

- [ ] **Step 4: Run frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: PASS.

- [ ] **Step 5: Commit frontend implementation**

Run:

```powershell
git add apps/desktop/package.json apps/desktop/package-lock.json apps/desktop/src
git commit -m "feat(desktop): add today workbench"
```

## Task 8: Update README And Run Full Verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update README with Phase 2 workflow**

Modify `README.md` to include these Phase 2 endpoints and data files under the existing Phase 2 section:

````markdown
阶段 2 API：

```text
GET  http://localhost:5057/journal/today
POST http://localhost:5057/journal/today/inputs
POST http://localhost:5057/journal/today/draft/confirm
```

阶段 2 开发期会写入：

```text
%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md
%LocalAppData%/Journal/.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl
%LocalAppData%/Journal/.journal/drafts/yyyy/MM/yyyy-MM-dd.md
%LocalAppData%/Journal/.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
```

今日工作台仍然是只读 Markdown 预览，不提供块编辑和源码编辑。
````

- [ ] **Step 2: Run backend verification**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: PASS.

- [ ] **Step 3: Run frontend verification**

Run:

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: PASS.

- [ ] **Step 4: Run formatting whitespace check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 5: Commit docs**

Run:

```powershell
git add README.md
git commit -m "docs: document phase 2 journal workflow"
```

## Task 9: Runtime Smoke Test

**Files:**
- No source files.

- [ ] **Step 1: Start API**

Run:

```powershell
dotnet run --project src/Journal.Api --urls http://localhost:5057
```

Expected:

```text
Now listening on: http://localhost:5057
```

- [ ] **Step 2: Verify API manually**

In another terminal, run:

```powershell
Invoke-RestMethod -Uri http://localhost:5057/journal/today
Invoke-RestMethod -Method Post -Uri http://localhost:5057/journal/today/inputs -ContentType 'application/json' -Body '{"text":"昨天完成阶段 1，今天准备做 JMF 主链路。","source":"text"}'
Invoke-RestMethod -Method Post -Uri http://localhost:5057/journal/today/draft/confirm
```

Expected:

```text
First response status is empty or current persisted status
Second response status is reviewing
Third response status is processed or updated
```

- [ ] **Step 3: Check generated files**

Run:

```powershell
$root = Join-Path $env:LOCALAPPDATA 'Journal'
Get-ChildItem -Recurse $root | Select-Object FullName
```

Expected: output includes raw input JSONL, draft Markdown, draft metadata JSON, and formal entry Markdown for today's date.

- [ ] **Step 4: Start desktop app**

Run:

```powershell
npm run desktop --prefix apps/desktop
```

Expected: Electron opens the Today Workbench, the diary paper is centered at the default `1180 x 780` window, and the confirmed Markdown entry path is visible after confirmation.

- [ ] **Step 5: Capture final git state**

Run:

```powershell
git status --short --branch
git log --oneline -8
```

Expected:

```text
No unstaged source changes remain unless runtime data outside the repository was created
Recent commits include Phase 2 domain, storage, JMF, API, desktop, and docs commits
```

## Self-Review

- Spec coverage: Tasks implement raw input JSONL, Mock AI JSON, validator, JMF Markdown draft, attention state, confirm-to-entry flow, same-day regeneration, Today Workbench, frontend tests, backend tests, docs, and runtime verification.
- Scope control: The plan does not implement block editing, source editing, version snapshots, SQLite indexing, real AI Provider, Provider settings, multi-date browsing, installer, or Electron-managed API lifecycle.
- TDD coverage: Domain, storage, Mock AI/JMF, service, HTTP endpoints, and React workflow all start with failing tests before implementation.
- Type consistency: `JournalDate`, `JournalStatus`, `RawInput`, `JournalDraft`, `JournalEntry`, `TodayJournalState`, and frontend TypeScript contracts use the same property names expected from ASP.NET Core camelCase JSON.
- V3 prototype alignment: CSS keeps the diary paper as the center; `1360px+` uses three columns, the default `1180 x 780` window uses diary plus dock with top context, and the `960 x 640` floor keeps compact actions visible.
