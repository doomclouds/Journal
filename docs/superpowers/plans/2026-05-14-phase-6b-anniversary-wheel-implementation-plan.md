# Phase 6B Anniversary Wheel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 6B same-day anniversary wheel so the Today Assistant can open a history workbench mode that compares entries and raw material from the same month/day across years.

**Architecture:** Reuse the rebuildable SQLite history index as the query boundary, because `entries.month_day` already exists and raw-only days are already represented as indexed entries. Add a focused anniversary query model, service method, API endpoint, TypeScript API client, and a dedicated React workbench that keeps the existing History Workbench visual language without changing the normal Today journal UI.

**Tech Stack:** .NET 10 minimal API, `Microsoft.Data.Sqlite`, xUnit, React + TypeScript + Vite, Vitest + Testing Library, existing `MarkdownPreview` renderer.

---

## Reference Inputs

- Spec: `docs/superpowers/specs/2026-05-14-phase-6b-anniversary-wheel-design.md`
- Prototype: `docs/superpowers/specs/2026-05-14-phase-6b-anniversary-wheel-prototype.html`
- Existing history endpoint and route helpers: `src/Journal.Api/Program.cs`
- Existing history service: `src/Journal.Infrastructure/Today/JournalHistoryService.cs`
- Existing history index store: `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
- Existing frontend history workspace: `apps/desktop/src/HistoryWorkbench.tsx`
- Existing app state and entry buttons: `apps/desktop/src/App.tsx`

## File Map

- Modify `src/Journal.Domain/Entries/JournalHistoryModels.cs`
  - Add `JournalAnniversaryWheelResult` as a small API result wrapper.
- Modify `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
  - Add `ReadAnniversaryAsync(string monthDay, int limit, CancellationToken)`.
  - Reuse existing `ReadSummary` and `JournalHistoryEntrySummary`.
- Modify `src/Journal.Infrastructure/Today/JournalHistoryService.cs`
  - Add `GetAnniversaryAsync(string monthDay, int limit, CancellationToken)` and keep the scan-before-read invariant.
- Modify `src/Journal.Api/Program.cs`
  - Add `GET /journal/history/anniversary/{monthDay}` before `/journal/history/{date}`.
  - Add strict `MM-dd` validation helper that accepts `02-29` and rejects malformed or impossible month/day values.
- Modify `apps/desktop/src/api.ts`
  - Add anniversary result type and client function.
- Create `apps/desktop/src/AnniversaryWheelWorkbench.tsx`
  - Dedicated three-column anniversary workbench based on the approved prototype.
- Modify `apps/desktop/src/App.tsx`
  - Add anniversary state, load/select handlers, stale request guard, Today Assistant entry button, and conditional workspace rendering.
- Modify `apps/desktop/src/styles.css`
  - Add `anniversary-*` styles aligned with the current history workbench.
- Modify or create tests:
  - `tests/Journal.Tests/JournalIndexStoreTests.cs`
  - `tests/Journal.Tests/JournalHistoryServiceTests.cs`
  - `tests/Journal.Tests/TodayJournalEndpointTests.cs`
  - `apps/desktop/src/App.test.tsx`
  - `apps/desktop/src/AnniversaryWheelWorkbench.test.tsx`
- Update docs after code is verified:
  - `README.md`
  - `AGENTS.md`
  - `docs/superpowers/archives/2026-05-14-phase-6b-anniversary-wheel.md`

## Task 1: Backend Domain And Index Query

**Files:**
- Modify: `src/Journal.Domain/Entries/JournalHistoryModels.cs`
- Modify: `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
- Test: `tests/Journal.Tests/JournalIndexStoreTests.cs`

- [ ] **Step 1: Add failing index tests**

Add these tests near the other `SearchAsync` tests in `tests/Journal.Tests/JournalIndexStoreTests.cs`:

```csharp
[Fact]
public async Task ReadAnniversaryAsync_ReturnsSameMonthDayEntriesNewestFirst()
{
    using var workspace = TempWorkspace.Create();
    var store = CreateStore(workspace.Root);
    var target2026 = JournalDate.From(new DateOnly(2026, 5, 14));
    var target2025 = JournalDate.From(new DateOnly(2025, 5, 14));
    var otherDay = JournalDate.From(new DateOnly(2024, 5, 13));
    await store.EnsureReadyAsync(CancellationToken.None);
    await store.UpsertEntryAsync(
        CreateEntry(target2025, mood: "期待"),
        [new JournalIndexedSection(target2025, "today-focus", "今天想推进", 10, "- 去年也在做日记")],
        CancellationToken.None);
    await store.UpsertEntryAsync(
        CreateEntry(target2026, mood: "平静"),
        [new JournalIndexedSection(target2026, "work", "工作推进", 20, "- 今年继续打磨同日年轮")],
        CancellationToken.None);
    await store.UpsertEntryAsync(
        CreateEntry(otherDay),
        [new JournalIndexedSection(otherDay, "today-focus", "今天想推进", 10, "- 不应该出现在 05-14")],
        CancellationToken.None);

    var result = await store.ReadAnniversaryAsync("05-14", 50, CancellationToken.None);

    Assert.Equal("05-14", result.MonthDay);
    Assert.Equal([target2026, target2025], result.Items.Select(item => item.Date).ToArray());
    Assert.Equal("工作推进", result.Items[0].Hits[0].Title);
    Assert.Equal("今年继续打磨同日年轮", result.Items[0].Hits[0].Snippet);
}

[Fact]
public async Task ReadAnniversaryAsync_IncludesRawOnlyAndAttentionEntries()
{
    using var workspace = TempWorkspace.Create();
    var store = CreateStore(workspace.Root);
    var rawOnlyDate = JournalDate.From(new DateOnly(2026, 2, 29));
    var attentionDate = JournalDate.From(new DateOnly(2024, 2, 29));
    await store.EnsureReadyAsync(CancellationToken.None);
    await store.UpsertEntryAsync(CreateEntry(rawOnlyDate, status: "raw-only"), [], CancellationToken.None);
    await store.UpsertRawInputAsync(
        new JournalIndexedRawInput("raw-1", rawOnlyDate, DateTimeOffset.Parse("2026-02-29T08:00:00+08:00"), "text", "闰日只有原始材料，也要能被年轮看到"),
        CancellationToken.None);
    await store.UpsertEntryAsync(
        CreateEntry(attentionDate, status: "attention", mood: null),
        [new JournalIndexedSection(attentionDate, "mood", "情绪状态", 10, "格式需要处理")],
        CancellationToken.None);

    var result = await store.ReadAnniversaryAsync("02-29", 50, CancellationToken.None);

    Assert.Equal([rawOnlyDate, attentionDate], result.Items.Select(item => item.Date).ToArray());
    Assert.Equal("raw-only", result.Items[0].Status);
    Assert.Equal("raw-input", result.Items[0].Hits[0].SourceType);
    Assert.Equal("闰日只有原始材料，也要能被年轮看到", result.Items[0].Hits[0].Snippet);
    Assert.Equal("attention", result.Items[1].Status);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexStoreTests
```

Expected: build fails because `ReadAnniversaryAsync` and `JournalAnniversaryWheelResult` do not exist.

- [ ] **Step 3: Add the result model**

Append this record after `JournalHistorySearchResult` in `src/Journal.Domain/Entries/JournalHistoryModels.cs`:

```csharp
public sealed record JournalAnniversaryWheelResult(
    string MonthDay,
    IReadOnlyList<JournalHistoryEntrySummary> Items);
```

- [ ] **Step 4: Add the index query implementation**

Add this public method after `SearchAsync` in `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`:

```csharp
public async Task<JournalAnniversaryWheelResult> ReadAnniversaryAsync(
    string monthDay,
    int limit,
    CancellationToken cancellationToken)
{
    await using var connection = await OpenConnectionAsync(cancellationToken);
    await ConfigureConnectionAsync(connection, cancellationToken);
    var normalizedLimit = NormalizeLimit(limit);
    var command = connection.CreateCommand();
    command.CommandText = """
        WITH selected_entries AS (
            SELECT e.date,
                   e.status,
                   e.mood,
                   e.attention_reason,
                   (SELECT COUNT(*) FROM raw_inputs r WHERE r.date = e.date) AS raw_input_count,
                   (SELECT COUNT(*) FROM entry_versions v WHERE v.date = e.date) AS version_count
            FROM entries e
            WHERE e.month_day = $monthDay
            ORDER BY e.date DESC
            LIMIT $limit
        ),
        candidate_hits AS (
            SELECT se.date,
                   se.status,
                   se.mood,
                   se.attention_reason,
                   'section' AS source_type,
                   s.section_id AS section_id,
                   NULL AS raw_input_id,
                   s.title AS title,
                   CASE
                       WHEN length(s.content) > $snippetMaxLength
                       THEN substr(s.content, 1, $snippetMaxLength - 3) || '...'
                       ELSE s.content
                   END AS snippet,
                   se.raw_input_count,
                   se.version_count,
                   ROW_NUMBER() OVER (
                       PARTITION BY se.date
                       ORDER BY s.display_order, s.section_id
                   ) AS hit_rank
            FROM selected_entries se
            INNER JOIN entry_sections s ON s.date = se.date
            UNION ALL
            SELECT se.date,
                   se.status,
                   se.mood,
                   se.attention_reason,
                   'raw-input' AS source_type,
                   NULL AS section_id,
                   r.id AS raw_input_id,
                   r.source AS title,
                   CASE
                       WHEN length(r.text) > $snippetMaxLength
                       THEN substr(r.text, 1, $snippetMaxLength - 3) || '...'
                       ELSE r.text
                   END AS snippet,
                   se.raw_input_count,
                   se.version_count,
                   ROW_NUMBER() OVER (
                       PARTITION BY se.date
                       ORDER BY r.created_at_utc, r.id
                   ) AS hit_rank
            FROM selected_entries se
            INNER JOIN raw_inputs r ON r.date = se.date
        )
        SELECT se.date,
               se.status,
               se.mood,
               se.attention_reason,
               COALESCE(h.source_type, 'section') AS source_type,
               h.section_id,
               h.raw_input_id,
               COALESCE(h.title, '日记') AS title,
               COALESCE(h.snippet, '') AS snippet,
               se.raw_input_count,
               se.version_count
        FROM selected_entries se
        LEFT JOIN candidate_hits h ON h.date = se.date AND h.hit_rank <= $hitLimit
        ORDER BY se.date DESC,
                 CASE h.source_type WHEN 'section' THEN 0 WHEN 'raw-input' THEN 1 ELSE 2 END,
                 h.section_id,
                 h.raw_input_id;
        """;
    command.Parameters.AddWithValue("$monthDay", monthDay);
    command.Parameters.AddWithValue("$limit", normalizedLimit);
    command.Parameters.AddWithValue("$hitLimit", SearchHitsPerDateLimit);
    command.Parameters.AddWithValue("$snippetMaxLength", LikeSnippetMaxLength);

    return new JournalAnniversaryWheelResult(
        monthDay,
        await ReadGroupedHitsAsync(command, normalizedLimit, cancellationToken));
}
```

If the `LEFT JOIN` produces an empty placeholder hit for an entry with no sections and no raw inputs, adjust `ReadGroupedHitsAsync` so it skips empty fallback rows:

```csharp
var snippet = reader.GetString(8);
if (!string.IsNullOrWhiteSpace(snippet))
{
    summary.Hits.Add(new JournalHistoryHit(
        reader.GetString(4),
        GetNullableString(reader, 5),
        GetNullableString(reader, 6),
        reader.GetString(7),
        snippet));
}
```

- [ ] **Step 5: Run the focused tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexStoreTests
```

Expected: `JournalIndexStoreTests` pass.

- [ ] **Step 6: Commit backend index slice**

```powershell
git add src/Journal.Domain/Entries/JournalHistoryModels.cs src/Journal.Infrastructure/Storage/JournalIndexStore.cs tests/Journal.Tests/JournalIndexStoreTests.cs
git commit -m "feat: add anniversary history index query"
```

## Task 2: History Service And API Endpoint

**Files:**
- Modify: `src/Journal.Infrastructure/Today/JournalHistoryService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalHistoryServiceTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Add failing service test**

Add this test to `tests/Journal.Tests/JournalHistoryServiceTests.cs`:

```csharp
[Fact]
public async Task GetAnniversaryAsync_ScansBeforeReturningSameMonthDayResults()
{
    using var workspace = TempWorkspace.Create();
    var date = JournalDate.From(new DateOnly(2026, 5, 14));
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    Directory.CreateDirectory(Path.GetDirectoryName(paths.EntryPath(date))!);
    await File.WriteAllTextAsync(
        paths.EntryPath(date),
        """
        ---
        date: 2026-05-14
        status: processed
        mood: 期待
        month_day: "05-14"
        ---

        # 2026-05-14

        <!-- journal:section today-focus origin="ai" created_by="ai" last_touched_by="ai" last_operation="upsert" based_on_raw_inputs="" -->
        ## 今天想推进

        - 同日年轮查询
        <!-- journal:section:end -->
        """,
        Encoding.UTF8,
        CancellationToken.None);
    var (_, service) = CreateSubject(workspace.Root);

    var result = await service.SearchAsync(new JournalHistoryQuery(null, null, null, null, null, 20), CancellationToken.None);
    var anniversary = await service.GetAnniversaryAsync("05-14", 50, CancellationToken.None);

    Assert.Single(result.Items);
    var item = Assert.Single(anniversary.Items);
    Assert.Equal(date, item.Date);
    Assert.Equal("同日年轮查询", item.Hits[0].Snippet);
}
```

Add this `using` at the top if it is not present:

```csharp
using System.Text;
```

- [ ] **Step 2: Add failing endpoint tests**

Add these tests near the existing history endpoint tests in `tests/Journal.Tests/TodayJournalEndpointTests.cs`:

```csharp
[Fact]
public async Task GetHistoryAnniversary_ReturnsSameMonthDayResults()
{
    using var factory = new JournalApiFactory();
    using var client = factory.CreateClient();
    await client.PostAsJsonAsync("/journal/today/inputs", new { text = "今天继续打磨同日年轮", source = "text" });
    await client.PostAsync("/journal/today/draft/confirm", content: null);

    using var response = await client.GetAsync("/journal/history/anniversary/05-08?limit=50");

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<JournalAnniversaryWheelResult>();
    Assert.NotNull(result);
    Assert.Equal("05-08", result.MonthDay);
    Assert.Contains(result.Items, item => item.Date.IsoDate == "2026-05-08");
}

[Theory]
[InlineData("/journal/history/anniversary/not-a-day", "monthDay must use MM-dd")]
[InlineData("/journal/history/anniversary/02-30", "monthDay is invalid")]
public async Task GetHistoryAnniversary_RejectsInvalidMonthDay(string url, string expectedError)
{
    using var factory = new JournalApiFactory();
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(url);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains(expectedError, body);
}

[Fact]
public async Task GetHistoryAnniversary_AcceptsLeapDay()
{
    using var factory = new JournalApiFactory();
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/journal/history/anniversary/02-29");

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<JournalAnniversaryWheelResult>();
    Assert.NotNull(result);
    Assert.Equal("02-29", result.MonthDay);
}
```

Add these `using` directives if they are missing:

```csharp
using System.Net;
using System.Net.Http.Json;
using Journal.Domain.Entries;
```

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHistoryServiceTests|TodayJournalEndpointTests"
```

Expected: build fails because `GetAnniversaryAsync` and the endpoint do not exist.

- [ ] **Step 4: Add service method**

Add this method after `SearchAsync` in `src/Journal.Infrastructure/Today/JournalHistoryService.cs`:

```csharp
public async Task<JournalAnniversaryWheelResult> GetAnniversaryAsync(
    string monthDay,
    int limit,
    CancellationToken cancellationToken)
{
    await indexingService.ScanAsync(clock.Now, cancellationToken);
    return await indexStore.ReadAnniversaryAsync(monthDay, limit, cancellationToken);
}
```

- [ ] **Step 5: Add API endpoint and helper**

In `src/Journal.Api/Program.cs`, insert this route between `/journal/history` and `/journal/history/{date}`:

```csharp
app.MapGet("/journal/history/anniversary/{monthDay}", async Task<IResult> (
    string monthDay,
    int? limit,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseMonthDay(monthDay, out var normalizedMonthDay, out var error))
    {
        return Results.BadRequest(new { error });
    }

    return Results.Ok(await service.GetAnniversaryAsync(
        normalizedMonthDay,
        limit.GetValueOrDefault(50),
        cancellationToken));
});
```

Add this helper near `TryParseJournalDate`:

```csharp
static bool TryParseMonthDay(string? value, out string monthDay, out string error)
{
    monthDay = "";
    error = "";
    if (string.IsNullOrWhiteSpace(value)
        || !Regex.IsMatch(value, "^\\d{2}-\\d{2}$", RegexOptions.CultureInvariant))
    {
        error = "monthDay must use MM-dd";
        return false;
    }

    var month = int.Parse(value[..2], CultureInfo.InvariantCulture);
    var day = int.Parse(value[3..], CultureInfo.InvariantCulture);
    if (month is < 1 or > 12)
    {
        error = "monthDay is invalid";
        return false;
    }

    var maxDay = month == 2 ? 29 : DateTime.DaysInMonth(2000, month);
    if (day < 1 || day > maxDay)
    {
        error = "monthDay is invalid";
        return false;
    }

    monthDay = value;
    return true;
}
```

Add this `using` if missing:

```csharp
using System.Text.RegularExpressions;
```

- [ ] **Step 6: Run endpoint and service tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHistoryServiceTests|TodayJournalEndpointTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit API slice**

```powershell
git add src/Journal.Infrastructure/Today/JournalHistoryService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalHistoryServiceTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose anniversary history endpoint"
```

## Task 3: Frontend API Contract

**Files:**
- Modify: `apps/desktop/src/api.ts`

- [ ] **Step 1: Add TypeScript types and client function**

In `apps/desktop/src/api.ts`, add this type after `JournalHistorySearchResult`:

```ts
export type JournalAnniversaryWheelResult = {
  monthDay: string;
  items: JournalHistoryEntrySummary[];
};
```

Add this function after `getJournalHistory`:

```ts
export function getJournalAnniversaryWheel(monthDay: string, limit = 50): Promise<JournalAnniversaryWheelResult> {
  const search = new URLSearchParams();
  search.set("limit", String(limit));
  return requestJson<JournalAnniversaryWheelResult>(
    `/journal/history/anniversary/${encodeURIComponent(monthDay)}?${search.toString()}`
  );
}
```

- [ ] **Step 2: Run type check through the frontend test runner**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: existing tests pass or fail only because later UI integration is not present. If TypeScript reports an API type error, fix it before continuing.

- [ ] **Step 3: Commit API client slice**

```powershell
git add apps/desktop/src/api.ts
git commit -m "feat: add anniversary wheel api client"
```

## Task 4: Anniversary Workbench Component

**Files:**
- Create: `apps/desktop/src/AnniversaryWheelWorkbench.tsx`
- Create: `apps/desktop/src/AnniversaryWheelWorkbench.test.tsx`
- Modify: `apps/desktop/src/styles.css`

- [ ] **Step 1: Add failing component tests**

Create `apps/desktop/src/AnniversaryWheelWorkbench.test.tsx`:

```tsx
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { AnniversaryWheelWorkbench } from "./AnniversaryWheelWorkbench";
import type { JournalAnniversaryWheelResult, JournalHistoryEntryDetail, JournalEntryVersion } from "./api";

const date2026 = {
  value: "2026-05-14",
  year: "2026",
  month: "05",
  isoDate: "2026-05-14",
  monthDay: "05-14",
  markdownFileName: "2026-05-14.md"
};

const date2025 = {
  ...date2026,
  value: "2025-05-14",
  year: "2025",
  isoDate: "2025-05-14",
  markdownFileName: "2025-05-14.md"
};

const result: JournalAnniversaryWheelResult = {
  monthDay: "05-14",
  items: [
    {
      date: date2026,
      status: "processed",
      mood: "期待",
      rawInputCount: 2,
      versionCount: 1,
      attentionReason: null,
      hits: [{
        sourceType: "section",
        sectionId: "today-focus",
        rawInputId: null,
        title: "今天想推进",
        snippet: "- 打磨同日年轮"
      }]
    },
    {
      date: date2025,
      status: "raw-only",
      mood: null,
      rawInputCount: 1,
      versionCount: 0,
      attentionReason: null,
      hits: [{
        sourceType: "raw-input",
        sectionId: null,
        rawInputId: "raw-1",
        title: "text",
        snippet: "去年今天只有原始材料"
      }]
    }
  ]
};

const detail: JournalHistoryEntryDetail = {
  date: date2026,
  status: "processed",
  attentionReason: null,
  markdown: "# 2026-05-14\n\n## 今天想推进\n\n- 打磨同日年轮",
  sections: [],
  versions: []
};

const version: JournalEntryVersion = {
  id: "version-1",
  date: date2026,
  createdAt: "2026-05-14T08:00:00+08:00",
  reason: "confirm-draft",
  sourceEntryPath: "entries/2026/05/2026-05-14.md",
  markdownPath: ".journal/versions/2026/05/2026-05-14/version-1.md",
  metaPath: ".journal/versions/2026/05/2026-05-14/version-1.meta.json",
  contentHash: "sha256:abc"
};

describe("AnniversaryWheelWorkbench", () => {
  test("renders same-day year cards and selected markdown detail", () => {
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    expect(screen.getByRole("region", { name: "同日年轮预览" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /2026/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /2025/ })).toBeInTheDocument();
    expect(screen.getByText("打磨同日年轮")).toBeInTheDocument();
    expect(screen.getByText("去年今天只有原始材料")).toBeInTheDocument();
  });

  test("emits month-day changes and selected date changes", () => {
    const onMonthDayChange = vi.fn();
    const onSelectDate = vi.fn();
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={onMonthDayChange}
        onSelectDate={onSelectDate}
        onViewVersion={vi.fn()}
        onClearVersion={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText("选择同日年轮日期"), { target: { value: "02-29" } });
    fireEvent.click(screen.getByRole("button", { name: /2025/ }));

    expect(onMonthDayChange).toHaveBeenCalledWith("02-29");
    expect(onSelectDate).toHaveBeenCalledWith("2025-05-14");
  });

  test("shows selected version preview and can return to current entry", () => {
    const onClearVersion = vi.fn();
    render(
      <AnniversaryWheelWorkbench
        isBusy={false}
        monthDay="05-14"
        result={result}
        selectedDate="2026-05-14"
        detail={detail}
        versions={[version]}
        selectedVersionDetail={{ version, markdown: "# Snapshot\n\n历史版本内容" }}
        error=""
        onBack={vi.fn()}
        onRefresh={vi.fn()}
        onMonthDayChange={vi.fn()}
        onSelectDate={vi.fn()}
        onViewVersion={vi.fn()}
        onClearVersion={onClearVersion}
        onRestoreVersion={vi.fn()}
      />
    );

    const preview = screen.getByRole("region", { name: "同日年轮预览" });
    expect(within(preview).getByText("历史版本内容")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /查看当前日记/ }));
    expect(onClearVersion).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run the component test and verify it fails**

Run:

```powershell
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx
```

Expected: module not found because `AnniversaryWheelWorkbench.tsx` does not exist.

- [ ] **Step 3: Create the component**

Create `apps/desktop/src/AnniversaryWheelWorkbench.tsx`:

```tsx
import { ArrowLeft, CalendarDays, Eye, RefreshCw, RotateCcw } from "lucide-react";
import type {
  JournalAnniversaryWheelResult,
  JournalEntryVersion,
  JournalHistoryEntryDetail,
  JournalHistoryEntrySummary,
  JournalVersionDetail
} from "./api";
import { MarkdownPreview } from "./MarkdownPreview";

type AnniversaryWheelWorkbenchProps = {
  isBusy: boolean;
  monthDay: string;
  result: JournalAnniversaryWheelResult | null;
  selectedDate: string;
  detail: JournalHistoryEntryDetail | null;
  versions: JournalEntryVersion[];
  selectedVersionDetail?: JournalVersionDetail | null;
  error: string;
  onBack: () => void;
  onRefresh: () => void;
  onMonthDayChange: (monthDay: string) => void;
  onSelectDate: (date: string) => void;
  onViewVersion?: (version: JournalEntryVersion) => void;
  onClearVersion?: () => void;
  onRestoreVersion: (version: JournalEntryVersion) => void;
};

const quickMonthDays = ["01-01", "05-14", "10-01", "12-31"];

function getStatusLabel(status: string) {
  switch (status) {
    case "processed":
      return "已保存";
    case "updated":
      return "已更新";
    case "reviewing":
      return "待确认";
    case "attention":
      return "需处理";
    case "missing":
      return "缺失";
    case "raw-only":
      return "仅材料";
    default:
      return status;
  }
}

function formatHistoryTime(value: string) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function firstLine(item: JournalHistoryEntrySummary) {
  return item.hits[0]?.snippet?.replace(/^[-\s]+/, "") || item.mood || `${item.rawInputCount} 条材料`;
}

export function AnniversaryWheelWorkbench({
  isBusy,
  monthDay,
  result,
  selectedDate,
  detail,
  versions,
  selectedVersionDetail = null,
  error,
  onBack,
  onRefresh,
  onMonthDayChange,
  onSelectDate,
  onViewVersion,
  onClearVersion,
  onRestoreVersion
}: AnniversaryWheelWorkbenchProps) {
  const items = result?.items ?? [];
  const selected = items.find(item => item.date.isoDate === selectedDate) ?? items[0] ?? null;
  const matchingDetail = detail?.date.isoDate === selected?.date.isoDate ? detail : null;
  const matchingVersions = selected
    ? versions.filter(version => version.date.isoDate === selected.date.isoDate)
    : [];
  const matchingVersionDetail = selectedVersionDetail?.version.date.isoDate === selected?.date.isoDate
    ? selectedVersionDetail
    : null;
  const markdown = matchingVersionDetail?.markdown ?? matchingDetail?.markdown ?? "";

  return (
    <>
      <aside className="context-rail history-rail anniversary-rail" aria-label="同日年轮日期">
        <section className="date-card history-date-card">
          <p className="month">Anniversary</p>
          <h1>{monthDay}<span>同日年轮</span></h1>
        </section>

        <section className="rail-section">
          <label className="anniversary-picker">
            <CalendarDays size={15} aria-hidden="true" />
            <input
              aria-label="选择同日年轮日期"
              type="text"
              inputMode="numeric"
              pattern="\\d{2}-\\d{2}"
              value={monthDay}
              onChange={event => onMonthDayChange(event.target.value)}
              placeholder="MM-DD"
            />
          </label>
          <div className="anniversary-quick-days" aria-label="快捷日期">
            {quickMonthDays.map(day => (
              <button
                key={day}
                type="button"
                className={monthDay === day ? "active" : ""}
                onClick={() => onMonthDayChange(day)}
              >
                {day}
              </button>
            ))}
          </div>
        </section>

        <section className="rail-section">
          <div className="section-head">
            <h2>年份</h2>
            <span>{items.length} 年</span>
          </div>
          <div className="history-result-list anniversary-year-list" aria-label="同日年份列表">
            {items.length > 0 ? items.map(item => (
              <button
                key={item.date.isoDate}
                type="button"
                className={`source-item history-result anniversary-year ${selected?.date.isoDate === item.date.isoDate ? "is-active" : ""}`}
                onClick={() => onSelectDate(item.date.isoDate)}
                aria-pressed={selected?.date.isoDate === item.date.isoDate}
              >
                <span className="source-meta">
                  <span>{item.date.year}</span>
                  <span>{getStatusLabel(item.status)}</span>
                </span>
                <strong>{item.hits[0]?.title ?? item.mood ?? "日记"}</strong>
                <p>{firstLine(item)}</p>
              </button>
            )) : (
              <p className="muted">这一天还没有可回看的历史。</p>
            )}
          </div>
        </section>
      </aside>

      <section className="journal-stage history-stage anniversary-stage" aria-label="同日年轮预览">
        <div className="stage-toolbar">
          <div className="stage-title">
            <p>同日年轮</p>
            <h2>{monthDay} 的历年回声</h2>
          </div>
          <div className="history-stage-actions">
            {matchingVersionDetail ? (
              <button type="button" className="secondary-action secondary" onClick={onClearVersion}>
                <Eye size={15} aria-hidden="true" />
                查看当前日记
              </button>
            ) : null}
            <button type="button" className="secondary-action secondary" onClick={onRefresh} disabled={isBusy}>
              <RefreshCw size={15} aria-hidden="true" />
              刷新
            </button>
            <button type="button" className="secondary-action secondary" onClick={onBack}>
              <ArrowLeft size={15} aria-hidden="true" />
              返回今日
            </button>
          </div>
        </div>

        <div className="document-scroll history-scroll">
          <article className="journal-paper history-paper anniversary-paper">
            {error ? <p className="api-error history-error" role="alert">{error}</p> : null}
            {selected ? (
              <>
                <header className="history-document-head">
                  <p className="kicker">{matchingVersionDetail ? "Version Snapshot" : "Anniversary Wheel"}</p>
                  <h1>{selected.date.isoDate}</h1>
                  <p>
                    <span>{getStatusLabel(matchingDetail?.status ?? selected.status)}</span>
                    <span>{selected.rawInputCount} 条材料</span>
                    <span>{matchingVersions.length || selected.versionCount} 个版本</span>
                  </p>
                </header>

                {(matchingDetail?.attentionReason ?? selected.attentionReason) ? (
                  <p className="attention-copy">{matchingDetail?.attentionReason ?? selected.attentionReason}</p>
                ) : null}

                {markdown.trim() ? (
                  <MarkdownPreview markdown={markdown} />
                ) : (
                  <div className="history-hit-list">
                    {selected.hits.map(hit => (
                      <section key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`} className="history-hit">
                        <span>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</span>
                        <p>{hit.snippet}</p>
                      </section>
                    ))}
                  </div>
                )}
              </>
            ) : (
              <section className="empty-paper audit-empty-state">
                <h2>没有同日记录</h2>
                <p>换一个日期看看，也许年轮还没长到这里。</p>
              </section>
            )}
          </article>
        </div>
      </section>

      <aside className="assistant-panel today-assistant history-inspector anniversary-inspector" aria-label="同日年轮详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Raw & Versions</p>
            <h2>材料与版本</h2>
          </div>
          <span className="assistant-time">{matchingVersions.length} 个版本</span>
        </div>

        <div className="assistant-body">
          {selected?.hits.map(hit => (
            <section className="assistant-card anniversary-raw-card" key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`}>
              <div className="assistant-card-head">
                <h3>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</h3>
                <span>{hit.sourceType === "raw-input" ? "Raw" : "Section"}</span>
              </div>
              <p>{hit.snippet}</p>
            </section>
          ))}

          {matchingVersions.map(version => (
            <section className="assistant-card history-version-card" key={version.id}>
              <div className="assistant-card-head">
                <h3>{formatHistoryTime(version.createdAt)}</h3>
                <span>{version.reason}</span>
              </div>
              <p>{version.contentHash}</p>
              <button type="button" className="assistant-inline-action" onClick={() => onViewVersion?.(version)} disabled={isBusy}>
                <Eye size={14} aria-hidden="true" />
                查看版本
              </button>
              <button type="button" className="assistant-inline-action" onClick={() => onRestoreVersion(version)} disabled={isBusy}>
                <RotateCcw size={14} aria-hidden="true" />
                恢复为草稿
              </button>
            </section>
          ))}
        </div>
      </aside>
    </>
  );
}
```

- [ ] **Step 4: Add focused styles**

Append to the history workbench style area in `apps/desktop/src/styles.css`:

```css
.anniversary-picker {
  align-items: center;
  background: var(--surface-soft);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  display: flex;
  gap: 8px;
  padding: 9px 10px;
}

.anniversary-picker input {
  background: transparent;
  border: 0;
  color: var(--text-primary);
  font: inherit;
  min-width: 0;
  outline: 0;
  width: 100%;
}

.anniversary-quick-days {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin-top: 10px;
}

.anniversary-quick-days button {
  background: var(--surface);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  color: var(--text-secondary);
  cursor: pointer;
  font: inherit;
  padding: 7px 8px;
}

.anniversary-quick-days button.active {
  border-color: var(--accent);
  color: var(--accent-strong);
  font-weight: 700;
}

.anniversary-year strong {
  font-size: 0.95rem;
}

.anniversary-paper .markdown-preview {
  margin-top: 16px;
}

.anniversary-raw-card p {
  white-space: pre-wrap;
}
```

- [ ] **Step 5: Run component test**

Run:

```powershell
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx
```

Expected: tests pass.

- [ ] **Step 6: Commit component slice**

```powershell
git add apps/desktop/src/AnniversaryWheelWorkbench.tsx apps/desktop/src/AnniversaryWheelWorkbench.test.tsx apps/desktop/src/styles.css
git commit -m "feat: add anniversary wheel workbench"
```

## Task 5: App Integration And Stale Request Guard

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing App integration tests**

Add these fixtures near `historySummary`:

```ts
const anniversaryResult = {
  monthDay: "05-08",
  items: [historySummary]
};
```

Add these tests near the existing history workbench tests:

```tsx
test("opens anniversary wheel from Today Assistant with today's month-day", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(mockJsonResponse(healthResponse))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState(processedToday())))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings))
    .mockResolvedValueOnce(mockJsonResponse(anniversaryResult))
    .mockResolvedValueOnce(mockJsonResponse(historyDetail()))
    .mockResolvedValueOnce(mockJsonResponse([historyVersion]));
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);
  fireEvent.click(await screen.findByRole("button", { name: /同日年轮/ }));

  await waitFor(() =>
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5057/journal/history/anniversary/05-08?limit=50",
      undefined
    )
  );
  expect(await screen.findByRole("region", { name: "同日年轮预览" })).toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-08", undefined);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/history/2026-05-08/versions", undefined);
});

test("keeps newest anniversary date selection when requests resolve out of order", async () => {
  const firstDetailDeferred = createDeferred<Response>();
  const secondDetailDeferred = createDeferred<Response>();
  const firstDate = {
    ...journalDate,
    value: "2026-05-08",
    isoDate: "2026-05-08"
  };
  const secondDate = {
    ...journalDate,
    value: "2025-05-08",
    year: "2025",
    isoDate: "2025-05-08",
    markdownFileName: "2025-05-08.md"
  };
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(mockJsonResponse(healthResponse))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState(processedToday())))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings))
    .mockResolvedValueOnce(mockJsonResponse({
      monthDay: "05-08",
      items: [
        { ...historySummary, date: firstDate },
        { ...historySummary, date: secondDate, hits: [{ ...historySummary.hits[0], snippet: "去年摘要" }] }
      ]
    }))
    .mockReturnValueOnce(firstDetailDeferred.promise)
    .mockResolvedValueOnce(mockJsonResponse([historyVersion]))
    .mockReturnValueOnce(secondDetailDeferred.promise)
    .mockResolvedValueOnce(mockJsonResponse([]));
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);
  fireEvent.click(await screen.findByRole("button", { name: /同日年轮/ }));
  fireEvent.click(await screen.findByRole("button", { name: /2025/ }));

  secondDetailDeferred.resolve(mockJsonResponse(historyDetail(secondDate, "- 去年详情")));
  const preview = screen.getByRole("region", { name: "同日年轮预览" });
  expect(await within(preview).findByText("去年详情")).toBeInTheDocument();

  await act(async () => {
    firstDetailDeferred.resolve(mockJsonResponse(historyDetail(firstDate, "- 今年详情")));
  });

  expect(within(preview).getByText("去年详情")).toBeInTheDocument();
  expect(within(preview).queryByText("今年详情")).not.toBeInTheDocument();
});

test("blocks anniversary wheel while inline block edits are dirty", async () => {
  const fetchMock = createInitialFetchMock();
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);
  fireEvent.click(await screen.findByRole("button", { name: /编辑/ }));
  fireEvent.change(screen.getByDisplayValue(/今天完成 Phase 2 API 连接/), {
    target: { value: "- 本地未保存修改" }
  });
  fireEvent.click(screen.getByRole("button", { name: /同日年轮/ }));

  expect(await screen.findByRole("alert")).toHaveTextContent("当前有未保存的块编辑");
  expect(fetchMock).not.toHaveBeenCalledWith(
    "http://localhost:5057/journal/history/anniversary/05-08?limit=50",
    undefined
  );
});
```

- [ ] **Step 2: Run App tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: tests fail because the button and anniversary state are not wired.

- [ ] **Step 3: Add imports and state in App**

In `apps/desktop/src/App.tsx`, add imports:

```ts
import { AnniversaryWheelWorkbench } from "./AnniversaryWheelWorkbench";
```

Add API imports from `./api`:

```ts
getJournalAnniversaryWheel,
type JournalAnniversaryWheelResult,
```

Add state near existing history state:

```ts
const [historyViewMode, setHistoryViewMode] = useState<"search" | "anniversary">("search");
const [anniversaryMonthDay, setAnniversaryMonthDay] = useState(journalDate.monthDay);
const [anniversaryResult, setAnniversaryResult] = useState<JournalAnniversaryWheelResult | null>(null);
```

- [ ] **Step 4: Add anniversary load handlers**

Add these functions near the history handlers in `apps/desktop/src/App.tsx`:

```ts
async function refreshAnniversary(monthDay = anniversaryMonthDay) {
  const historyRequestId = historyRequestIdRef.current + 1;
  historyRequestIdRef.current = historyRequestId;
  setHistoryError("");
  setHistoryDetail(null);
  setHistoryVersions([]);
  setHistoryVersionDetail(null);

  try {
    const result = await getJournalAnniversaryWheel(monthDay, 50);
    if (historyRequestId !== historyRequestIdRef.current) {
      return;
    }

    const selectedStillExists = result.items.some(item => item.date.isoDate === historySelectedDate);
    const selectedDate = selectedStillExists
      ? historySelectedDate
      : result.items[0]?.date.isoDate ?? "";
    setAnniversaryResult(result);
    setHistorySelectedDate(selectedDate);

    if (!selectedDate) {
      return;
    }

    await loadHistoryEntryForRequest(selectedDate, historyRequestId);
  } catch (caught) {
    if (historyRequestId === historyRequestIdRef.current) {
      setHistoryError(getErrorMessage(caught));
    }
  }
}

async function openAnniversaryWorkbench() {
  if (hasLocalUnsavedChanges) {
    resetPendingRegenerateDraft();
    setValidationError(localUnsavedChangeMessage);
    return;
  }

  const monthDay = editor.today.date.monthDay;
  resetPendingRegenerateDraft();
  setValidationError("");
  setHistoryViewMode("anniversary");
  setAnniversaryMonthDay(monthDay);
  setWorkspaceMode("history");
  await refreshAnniversary(monthDay);
}

function handleAnniversaryMonthDayChange(monthDay: string) {
  setAnniversaryMonthDay(monthDay);
  if (/^\d{2}-\d{2}$/.test(monthDay)) {
    void refreshAnniversary(monthDay);
  }
}
```

- [ ] **Step 5: Render anniversary workbench when selected**

Replace the single `HistoryWorkbench` render branch with this conditional inside `workspaceMode === "history"`:

```tsx
{historyViewMode === "anniversary" ? (
  <AnniversaryWheelWorkbench
    isBusy={isBusy}
    monthDay={anniversaryMonthDay}
    result={anniversaryResult}
    selectedDate={historySelectedDate}
    detail={historyDetail}
    versions={historyVersions}
    selectedVersionDetail={historyVersionDetail}
    error={historyError}
    onBack={() => setWorkspaceMode("today")}
    onRefresh={() => void refreshAnniversary()}
    onMonthDayChange={handleAnniversaryMonthDayChange}
    onSelectDate={date => void handleHistorySelectDate(date)}
    onViewVersion={version => void handleViewHistoryVersion(version)}
    onClearVersion={() => setHistoryVersionDetail(null)}
    onRestoreVersion={version => void handleRestoreHistoryVersion(version)}
  />
) : (
  <HistoryWorkbench
    isBusy={isBusy}
    query={historyQuery}
    status={historyStatus}
    entries={historyEntries}
    detail={historyDetail}
    selectedDate={historySelectedDate}
    versions={historyVersions}
    selectedVersionDetail={historyVersionDetail}
    error={historyError}
    onBack={() => setWorkspaceMode("today")}
    onQueryChange={value => {
      setHistoryQuery(value);
      void refreshHistory(value, historyStatus);
    }}
    onStatusChange={value => {
      setHistoryStatus(value);
      void refreshHistory(historyQuery, value);
    }}
    onSelectDate={date => void handleHistorySelectDate(date)}
    onRefresh={() => void refreshHistory()}
    onViewVersion={version => void handleViewHistoryVersion(version)}
    onClearVersion={() => setHistoryVersionDetail(null)}
    onRestoreVersion={version => void handleRestoreHistoryVersion(version)}
  />
)}
```

Update `openHistoryWorkbench` so it sets normal search mode:

```ts
setHistoryViewMode("search");
setWorkspaceMode("history");
```

- [ ] **Step 6: Add Today Assistant entry button**

Near the existing history entry button in the Today Assistant action area of `apps/desktop/src/App.tsx`, add:

```tsx
<button type="button" className="assistant-inline-action" onClick={openAnniversaryWorkbench}>
  <History size={14} aria-hidden="true" />
  同日年轮
</button>
```

`History` is already imported from `lucide-react` in `apps/desktop/src/App.tsx`, so no new icon import is needed.

- [ ] **Step 7: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx AnniversaryWheelWorkbench.test.tsx
```

Expected: tests pass.

- [ ] **Step 8: Commit frontend integration slice**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: wire anniversary wheel workspace"
```

## Task 6: Full Verification And Documentation

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`
- Create: `docs/superpowers/archives/2026-05-14-phase-6b-anniversary-wheel.md`
- Modify: `docs/superpowers/archives/INDEX.md`
- Possibly modify: `docs/superpowers/problems/` or `docs/superpowers/inbox/` if the problem gate routes a reusable issue.

- [ ] **Step 1: Run backend verification**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: all backend tests pass.

- [ ] **Step 2: Run frontend verification**

Run:

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: all frontend tests pass and Vite build succeeds.

- [ ] **Step 3: Update README delivered scope**

In `README.md`, add a short Phase 6B bullet under delivered scope:

```markdown
- Phase 6B adds the same-day anniversary wheel: Today Assistant can open a history workbench mode for a selected `MM-DD`, compare entries across years, inspect raw material snippets, and open version snapshots without changing the normal Today journal layout.
```

- [ ] **Step 4: Update AGENTS project orientation**

In `AGENTS.md`, update the delivered scope paragraph and current invariant list with:

```markdown
- Phase 6B adds Same-Day Anniversary Wheel: the History Workbench can open an anniversary mode from Today Assistant, query entries by `MM-DD`, render year-card summaries, inspect selected historical Markdown, and preserve existing version restore constraints.
```

Keep the existing restore invariant unchanged:

```markdown
- Restoring a version writes a `reviewing` draft only and never writes `entries/` directly.
```

- [ ] **Step 5: Archive the completed requirement**

Create `docs/superpowers/archives/2026-05-14-phase-6b-anniversary-wheel.md`:

```markdown
# Phase 6B Same-Day Anniversary Wheel

**Date:** 2026-05-14
**Status:** Completed

## Delivered

- Added `GET /journal/history/anniversary/{monthDay}` with strict `MM-DD` validation.
- Reused the rebuildable SQLite history index through `entries.month_day`.
- Included processed, attention, missing, and raw-only indexed days in anniversary results.
- Added a Today Assistant entry to open the anniversary workbench for today's month/day.
- Added a dedicated anniversary workbench with year cards, selected Markdown preview, raw material snippets, version preview, and existing draft-only restore action.

## Verification

- `dotnet test Journal.slnx`
- `npm test --prefix apps/desktop`
- `npm run build --prefix apps/desktop`

## Notes

- SQLite remains a rebuildable cache. Markdown entries, raw-input jsonl files, and version files remain the source material.
- Version restore remains limited to today's date through the existing history restore guard.
```

Add an entry to `docs/superpowers/archives/INDEX.md` in newest-first order:

```markdown
- [2026-05-14 - Phase 6B Same-Day Anniversary Wheel](2026-05-14-phase-6b-anniversary-wheel.md)
```

- [ ] **Step 6: Run asset compounding problem gate**

Use `superpowers-asset-compounding:using-asset-compounding` after implementation, spec alignment review, code quality review, and verification. Route any reusable implementation issue to `docs/superpowers/inbox/` or `docs/superpowers/problems/` according to the repository guidance in `AGENTS.md`.

Report one of these route outcomes in the implementation handoff:

```text
Problem gate: none | inbox | update-existing | new-problem
Evidence: <validation command or asset path>
```

- [ ] **Step 7: Commit docs and archive**

```powershell
git add README.md AGENTS.md docs/superpowers/archives docs/superpowers/problems docs/superpowers/inbox
git commit -m "docs: archive anniversary wheel delivery"
```

## Final Verification Matrix

Run all commands before claiming the feature is complete:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
git diff --check
```

Expected:

- Backend tests pass.
- Frontend tests pass.
- Frontend production build passes.
- `git diff --check` reports no whitespace errors.

## Self-Review

**Spec coverage:**

- Today Assistant entry is covered by Task 5.
- Default today `MM-DD` anniversary query is covered by Task 5.
- Month/day picker and quick dates are covered by Task 4.
- Year-card summary stream is covered by Task 4.
- Selected historical Markdown preview is covered by Task 4 and Task 5.
- Raw input lightweight snippets are covered by Task 1 and Task 4.
- Version preview and restore reuse are covered by Task 4 and Task 5.
- Strict `MM-DD` validation, including `02-29`, is covered by Task 2.
- Raw-only and attention days are covered by Task 1.
- Stale request guard is covered by Task 5.
- Documentation and asset gates are covered by Task 6.

**Placeholder scan:**

- The plan contains concrete file paths, concrete tests, concrete commands, concrete snippets, and expected outcomes.
- The only future routing is the repository-mandated asset compounding gate, and its allowed outcomes are explicitly listed.

**Type consistency:**

- Backend result type is `JournalAnniversaryWheelResult`.
- Frontend result type uses the same JSON shape: `monthDay` and `items`.
- `items` reuses `JournalHistoryEntrySummary` across backend and frontend.
- App state reuses the existing history detail, versions, version detail, selected date, error, and request id guard.
