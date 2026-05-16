# Phase 8 Memory Corridor Anniversary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 8 memory corridor: timeline cards based on journal focus sections, formal-entry reading mode, and a local anniversary data domain with next-year notes.

**Architecture:** Keep formal Markdown, raw-input JSONL, version files, and the new anniversaries JSON file as durable source material. Extend the existing history/anniversary query path with display-only preview fields, and add a separate anniversary store/service/API for user-saved dates and next-year notes. The frontend keeps anniversary mode read-only for journal entries, while anniversary metadata writes only to `.journal/anniversaries/anniversaries.json` or user-approved raw input.

**Tech Stack:** .NET 10 minimal API, C# records/services, local JSON storage, SQLite-backed history index, React + TypeScript desktop frontend, Vitest, xUnit.

---

## File Structure

- Create `src/Journal.Domain/Entries/JournalAnniversaryModels.cs`: source-of-truth anniversary records, request/response models, and note status constants.
- Modify `src/Journal.Domain/Entries/JournalHistoryModels.cs`: add `EntryUpdatedAt` and `JournalHistoryCardPreview`.
- Modify `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`: add anniversary source paths.
- Create `src/Journal.Infrastructure/Storage/JournalAnniversaryStore.cs`: atomic JSON read/write for `.journal/anniversaries/anniversaries.json`.
- Create `src/Journal.Infrastructure/Today/JournalAnniversaryService.cs`: validation, save/update, next-year note targeting, adopt/dismiss behavior.
- Modify `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`: derive `cardPreview` from indexed sections and expose current formal entry update time.
- Modify `src/Journal.Infrastructure/Today/JournalHistoryService.cs`: pass new history fields through existing APIs.
- Modify `src/Journal.Infrastructure/Storage/JournalDataExportService.cs`: export anniversaries as source material.
- Modify `src/Journal.Infrastructure/Storage/JournalDataImportService.cs`: import, backup, restore, and validate anniversaries source material.
- Modify `src/Journal.Api/Program.cs`: register services and add `/journal/anniversaries*` endpoints.
- Modify `apps/desktop/src/api.ts`: add anniversary types/functions and new history preview fields.
- Modify `apps/desktop/src/App.tsx`: load anniversary data with anniversary mode, wire save/update/note/adopt/dismiss handlers.
- Modify `apps/desktop/src/AnniversaryWheelWorkbench.tsx`: implement timeline/read mode and connect left/right panels to real anniversary data.
- Modify `apps/desktop/src/styles.css`: replace history-like anniversary layout with memory corridor layout.
- Add/update tests in `tests/Journal.Tests/*`, `apps/desktop/src/AnniversaryWheelWorkbench.test.tsx`, and `apps/desktop/src/App.test.tsx`.

---

## Task 1: Anniversary Source Model And Store

**Files:**
- Create: `src/Journal.Domain/Entries/JournalAnniversaryModels.cs`
- Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Create: `src/Journal.Infrastructure/Storage/JournalAnniversaryStore.cs`
- Test: `tests/Journal.Tests/JournalAnniversaryStoreTests.cs`

- [ ] **Step 1: Write failing store tests**

Create `tests/Journal.Tests/JournalAnniversaryStoreTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalAnniversaryStoreTests
{
    [Fact]
    public async Task ReadAsync_WhenFileMissing_ReturnsEmptyDocument()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);

        var document = await store.ReadAsync(CancellationToken.None);

        Assert.Equal("journal-anniversaries/v1", document.Schema);
        Assert.Empty(document.Items);
    }

    [Fact]
    public async Task WriteAsync_RoundTripsAnniversaryDocument()
    {
        using var workspace = TempWorkspace.Create();
        var store = CreateStore(workspace.Root);
        var item = new JournalAnniversaryItem(
            "anniv-20260516-journal-stage",
            "05-16",
            "Journal 阶段日",
            "project-milestone",
            "2024-05-16",
            "从记录习惯逐渐走向个人记忆核心。",
            true,
            DateTimeOffset.Parse("2026-05-16T23:42:00+08:00"),
            DateTimeOffset.Parse("2026-05-16T23:42:00+08:00"),
            [
                new JournalNextYearNote(
                    "note-20260516-001",
                    "2027-05-16",
                    "明年回来看 Journal 是否真正进入日常。",
                    JournalNextYearNoteStatus.Pending,
                    DateTimeOffset.Parse("2026-05-16T23:45:00+08:00"),
                    null,
                    null)
            ]);
        var document = new JournalAnniversaryDocument(
            "journal-anniversaries/v1",
            [item]);

        await store.WriteAsync(document, CancellationToken.None);
        var loaded = await store.ReadAsync(CancellationToken.None);

        var loadedItem = Assert.Single(loaded.Items);
        Assert.Equal("05-16", loadedItem.MonthDay);
        Assert.Equal("Journal 阶段日", loadedItem.Title);
        Assert.True(loadedItem.Pinned);
        Assert.Equal(JournalNextYearNoteStatus.Pending, Assert.Single(loadedItem.NextYearNotes).Status);
    }

    [Fact]
    public async Task ReadAsync_WhenJsonInvalid_ThrowsClearError()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
        await File.WriteAllTextAsync(paths.AnniversaryPath(), "{ bad json", CancellationToken.None);
        var store = new JournalAnniversaryStore(paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadAsync(CancellationToken.None));

        Assert.Contains("Anniversary data is invalid", exception.Message, StringComparison.Ordinal);
    }

    private static JournalAnniversaryStore CreateStore(string root)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        return new JournalAnniversaryStore(paths);
    }
}
```

- [ ] **Step 2: Run the failing store tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalAnniversaryStoreTests
```

Expected: compile failure because `JournalAnniversaryStore`, `JournalAnniversaryDocument`, `JournalAnniversaryItem`, `JournalNextYearNote`, `JournalNextYearNoteStatus`, and `LocalJournalPaths.AnniversaryPath()` do not exist.

- [ ] **Step 3: Add domain records**

Create `src/Journal.Domain/Entries/JournalAnniversaryModels.cs`:

```csharp
namespace Journal.Domain.Entries;

public static class JournalNextYearNoteStatus
{
    public const string Pending = "pending";
    public const string Adopted = "adopted";
    public const string Dismissed = "dismissed";
}

public sealed record JournalAnniversaryDocument(
    string Schema,
    IReadOnlyList<JournalAnniversaryItem> Items)
{
    public static JournalAnniversaryDocument Empty() =>
        new("journal-anniversaries/v1", []);
}

public sealed record JournalAnniversaryItem(
    string Id,
    string MonthDay,
    string Title,
    string Type,
    string? OriginDate,
    string Description,
    bool Pinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<JournalNextYearNote> NextYearNotes);

public sealed record JournalNextYearNote(
    string Id,
    string TargetDate,
    string Text,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AdoptedAt,
    string? RawInputId);

public sealed record JournalAnniversarySaveRequest(
    string MonthDay,
    string Title,
    string Type,
    string? OriginDate,
    string Description,
    bool Pinned);

public sealed record JournalNextYearNoteCreateRequest(string Text);

public sealed record JournalAnniversaryAdoptResult(
    JournalAnniversaryItem Anniversary,
    RawInput RawInput);
```

- [ ] **Step 4: Add anniversary paths**

Modify `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`:

```csharp
public string AnniversaryDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "anniversaries");

public string AnniversaryPath() =>
    Path.Combine(AnniversaryDirectory(), "anniversaries.json");
```

Place these beside the other `.journal` source-material path helpers.

- [ ] **Step 5: Add atomic JSON store**

Create `src/Journal.Infrastructure/Storage/JournalAnniversaryStore.cs`:

```csharp
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalAnniversaryStore(LocalJournalPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<JournalAnniversaryDocument> ReadAsync(CancellationToken cancellationToken)
    {
        var path = paths.AnniversaryPath();
        if (!File.Exists(path))
        {
            return JournalAnniversaryDocument.Empty();
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous);
            var document = await JsonSerializer.DeserializeAsync<JournalAnniversaryDocument>(
                stream,
                JsonOptions,
                cancellationToken);
            return Normalize(document ?? JournalAnniversaryDocument.Empty());
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Anniversary data is invalid.", exception);
        }
    }

    public async Task WriteAsync(JournalAnniversaryDocument document, CancellationToken cancellationToken)
    {
        var path = paths.AnniversaryPath();
        LocalJournalPaths.EnsureParentDirectory(path);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, Normalize(document), JsonOptions, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static JournalAnniversaryDocument Normalize(JournalAnniversaryDocument document) =>
        new(
            string.IsNullOrWhiteSpace(document.Schema) ? "journal-anniversaries/v1" : document.Schema,
            document.Items ?? []);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

- [ ] **Step 6: Run store tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalAnniversaryStoreTests
```

Expected: `JournalAnniversaryStoreTests` pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalAnniversaryModels.cs src/Journal.Infrastructure/Storage/LocalJournalPaths.cs src/Journal.Infrastructure/Storage/JournalAnniversaryStore.cs tests/Journal.Tests/JournalAnniversaryStoreTests.cs
git commit -m "feat: add anniversary source store"
```

---

## Task 2: Anniversary Service And API

**Files:**
- Create: `src/Journal.Infrastructure/Today/JournalAnniversaryService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalAnniversaryServiceTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing service tests**

Create `tests/Journal.Tests/JournalAnniversaryServiceTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class JournalAnniversaryServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 16);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-16T10:00:00+08:00");

    [Fact]
    public async Task SaveAsync_CreatesPinnedAnniversary()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);

        var item = await service.SaveAsync(new JournalAnniversarySaveRequest(
            "05-16",
            "Journal 阶段日",
            "project-milestone",
            "2024-05-16",
            "从记录习惯逐渐走向个人记忆核心。",
            true), CancellationToken.None);

        Assert.Equal("05-16", item.MonthDay);
        Assert.Equal("Journal 阶段日", item.Title);
        Assert.True(item.Pinned);
        Assert.StartsWith("anniv-", item.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddNextYearNoteAsync_ForNormalDate_TargetsNextYear()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);

        var updated = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("明年回来看。"),
            CancellationToken.None);

        var note = Assert.Single(updated.NextYearNotes);
        Assert.Equal("2027-05-16", note.TargetDate);
        Assert.Equal(JournalNextYearNoteStatus.Pending, note.Status);
    }

    [Fact]
    public async Task AddNextYearNoteAsync_ForLeapDay_TargetsNextRealLeapDay()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root, new DateOnly(2026, 2, 28), DateTimeOffset.Parse("2026-02-28T10:00:00+08:00"));
        var item = await service.SaveAsync(CreateRequest("02-29"), CancellationToken.None);

        var updated = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("下一个闰日再看。"),
            CancellationToken.None);

        var note = Assert.Single(updated.NextYearNotes);
        Assert.Equal("2028-02-29", note.TargetDate);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WritesRawInputAndMarksAdopted()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("明年检查 Journal 是否进入日常。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);

        var result = await service.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        Assert.Equal(JournalNextYearNoteStatus.Adopted, Assert.Single(result.Anniversary.NextYearNotes).Status);
        Assert.StartsWith("raw-", result.RawInput.Id, StringComparison.Ordinal);
        var rawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(FixedDay), CancellationToken.None);
        Assert.Contains(rawInputs, raw => raw.Text.Contains("明年检查 Journal", StringComparison.Ordinal));
    }

    private static JournalAnniversarySaveRequest CreateRequest(string monthDay) =>
        new(monthDay, "纪念日", "self-reminder", null, "说明", true);

    private static JournalAnniversaryService CreateSubject(
        string root,
        DateOnly? today = null,
        DateTimeOffset? now = null)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        return new JournalAnniversaryService(
            new JournalAnniversaryStore(paths),
            new RawInputStore(paths),
            new FixedJournalClock(today ?? FixedDay, now ?? FixedNow));
    }

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;
        public DateTimeOffset Now => now;
    }
}
```

- [ ] **Step 2: Run the failing service tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalAnniversaryServiceTests
```

Expected: compile failure because `JournalAnniversaryService` does not exist.

- [ ] **Step 3: Implement anniversary service**

Create `src/Journal.Infrastructure/Today/JournalAnniversaryService.cs`:

```csharp
using System.Globalization;
using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed class JournalAnniversaryService(
    JournalAnniversaryStore store,
    RawInputStore rawInputStore,
    IJournalClock clock)
{
    public async Task<IReadOnlyList<JournalAnniversaryItem>> ListAsync(CancellationToken cancellationToken)
    {
        var document = await store.ReadAsync(cancellationToken);
        return document.Items
            .OrderByDescending(item => item.Pinned)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.MonthDay, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<JournalAnniversaryItem>> ListByMonthDayAsync(
        string monthDay,
        CancellationToken cancellationToken)
    {
        ValidateMonthDay(monthDay);
        var items = await ListAsync(cancellationToken);
        return items.Where(item => string.Equals(item.MonthDay, monthDay, StringComparison.Ordinal)).ToArray();
    }

    public async Task<JournalAnniversaryItem> SaveAsync(
        JournalAnniversarySaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSaveRequest(request);
        var document = await store.ReadAsync(cancellationToken);
        var now = clock.Now;
        var item = new JournalAnniversaryItem(
            $"anniv-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            request.MonthDay,
            request.Title.Trim(),
            request.Type.Trim(),
            NormalizeOriginDate(request.OriginDate),
            request.Description.Trim(),
            request.Pinned,
            now,
            now,
            []);
        await store.WriteAsync(document with { Items = [..document.Items, item] }, cancellationToken);
        return item;
    }

    public async Task<JournalAnniversaryItem> UpdateAsync(
        string id,
        JournalAnniversarySaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateId(id);
        ValidateSaveRequest(request);
        var document = await store.ReadAsync(cancellationToken);
        var existing = document.Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("anniversary was not found");
        var updated = existing with
        {
            MonthDay = request.MonthDay,
            Title = request.Title.Trim(),
            Type = request.Type.Trim(),
            OriginDate = NormalizeOriginDate(request.OriginDate),
            Description = request.Description.Trim(),
            Pinned = request.Pinned,
            UpdatedAt = clock.Now
        };
        await ReplaceAsync(document, updated, cancellationToken);
        return updated;
    }

    public async Task<JournalAnniversaryItem> AddNextYearNoteAsync(
        string id,
        JournalNextYearNoteCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateId(id);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("text is required", nameof(request));
        }

        var document = await store.ReadAsync(cancellationToken);
        var existing = document.Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("anniversary was not found");
        var note = new JournalNextYearNote(
            $"note-{clock.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            ResolveNextTargetDate(existing.MonthDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.Text.Trim(),
            JournalNextYearNoteStatus.Pending,
            clock.Now,
            null,
            null);
        var updated = existing with
        {
            UpdatedAt = clock.Now,
            NextYearNotes = [..existing.NextYearNotes, note]
        };
        await ReplaceAsync(document, updated, cancellationToken);
        return updated;
    }

    public async Task<JournalAnniversaryAdoptResult> AdoptNextYearNoteAsync(
        string id,
        string noteId,
        CancellationToken cancellationToken)
    {
        ValidateId(id);
        ValidateId(noteId);
        var document = await store.ReadAsync(cancellationToken);
        var existing = document.Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("anniversary was not found");
        var note = existing.NextYearNotes.FirstOrDefault(candidate => string.Equals(candidate.Id, noteId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("next-year note was not found");
        if (!string.Equals(note.Status, JournalNextYearNoteStatus.Pending, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("next-year note is not pending");
        }

        var rawInput = new RawInput(
            $"raw-{Guid.NewGuid():N}",
            JournalDate.From(clock.Today),
            clock.Now,
            "anniversary-note",
            note.Text);
        await rawInputStore.AppendAsync(rawInput, cancellationToken);

        var updatedNote = note with
        {
            Status = JournalNextYearNoteStatus.Adopted,
            AdoptedAt = clock.Now,
            RawInputId = rawInput.Id
        };
        var updated = existing with
        {
            UpdatedAt = clock.Now,
            NextYearNotes = existing.NextYearNotes
                .Select(candidate => string.Equals(candidate.Id, noteId, StringComparison.Ordinal) ? updatedNote : candidate)
                .ToArray()
        };
        await ReplaceAsync(document, updated, cancellationToken);
        return new JournalAnniversaryAdoptResult(updated, rawInput);
    }

    public async Task<JournalAnniversaryItem> DismissNextYearNoteAsync(
        string id,
        string noteId,
        CancellationToken cancellationToken)
    {
        ValidateId(id);
        ValidateId(noteId);
        var document = await store.ReadAsync(cancellationToken);
        var existing = document.Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("anniversary was not found");
        var updated = existing with
        {
            UpdatedAt = clock.Now,
            NextYearNotes = existing.NextYearNotes
                .Select(note => string.Equals(note.Id, noteId, StringComparison.Ordinal)
                    ? note with { Status = JournalNextYearNoteStatus.Dismissed }
                    : note)
                .ToArray()
        };
        await ReplaceAsync(document, updated, cancellationToken);
        return updated;
    }

    private async Task ReplaceAsync(
        JournalAnniversaryDocument document,
        JournalAnniversaryItem updated,
        CancellationToken cancellationToken)
    {
        var items = document.Items
            .Select(item => string.Equals(item.Id, updated.Id, StringComparison.Ordinal) ? updated : item)
            .ToArray();
        await store.WriteAsync(document with { Items = items }, cancellationToken);
    }

    private DateOnly ResolveNextTargetDate(string monthDay)
    {
        var parts = monthDay.Split('-');
        var month = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var day = int.Parse(parts[1], CultureInfo.InvariantCulture);
        for (var year = clock.Today.Year + 1; year <= clock.Today.Year + 8; year++)
        {
            if (DateTime.DaysInMonth(year, month) >= day)
            {
                return new DateOnly(year, month, day);
            }
        }

        throw new InvalidOperationException("Could not resolve next anniversary target date.");
    }

    private static void ValidateSaveRequest(JournalAnniversarySaveRequest request)
    {
        ValidateMonthDay(request.MonthDay);
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("title is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new ArgumentException("type is required", nameof(request));
        }

        _ = NormalizeOriginDate(request.OriginDate);
    }

    private static string? NormalizeOriginDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.ParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void ValidateMonthDay(string value)
    {
        if (value.Length != 5 || value[2] != '-')
        {
            throw new ArgumentException("monthDay must use MM-dd", nameof(value));
        }

        var month = int.Parse(value[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(value[3..], CultureInfo.InvariantCulture);
        if (month is < 1 or > 12 || day < 1 || DateTime.DaysInMonth(2028, month) < day)
        {
            throw new ArgumentException("monthDay is invalid", nameof(value));
        }
    }

    private static void ValidateId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("id is invalid", nameof(value));
        }
    }
}
```

- [ ] **Step 4: Register service and endpoints**

Modify `src/Journal.Api/Program.cs` service registration:

```csharp
builder.Services.AddSingleton<JournalAnniversaryStore>();
builder.Services.AddSingleton<JournalAnniversaryService>();
```

Add endpoints near existing history endpoints:

```csharp
app.MapGet("/journal/anniversaries", async (
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(cancellationToken)));

app.MapGet("/journal/anniversaries/{monthDay}", async Task<IResult> (
    string monthDay,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseMonthDay(monthDay, out var normalizedMonthDay, out var error))
    {
        return Results.BadRequest(new { error });
    }

    return Results.Ok(await service.ListByMonthDayAsync(normalizedMonthDay, cancellationToken));
});

app.MapPost("/journal/anniversaries", async Task<IResult> (
    JournalAnniversarySaveRequest request,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.SaveAsync(request, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPut("/journal/anniversaries/{id}", async Task<IResult> (
    string id,
    JournalAnniversarySaveRequest request,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.UpdateAsync(id, request, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/journal/anniversaries/{id}/next-year-notes", async Task<IResult> (
    string id,
    JournalNextYearNoteCreateRequest request,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.AddNextYearNoteAsync(id, request, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/journal/anniversaries/{id}/next-year-notes/{noteId}/adopt", async Task<IResult> (
    string id,
    string noteId,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.AdoptNextYearNoteAsync(id, noteId, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/journal/anniversaries/{id}/next-year-notes/{noteId}/dismiss", async Task<IResult> (
    string id,
    string noteId,
    JournalAnniversaryService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await service.DismissNextYearNoteAsync(id, noteId, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});
```

- [ ] **Step 5: Add endpoint tests**

Extend `tests/Journal.Tests/TodayJournalEndpointTests.cs` with one endpoint flow test:

```csharp
[Fact]
public async Task AnniversaryEndpoints_SaveReadAndAdoptNextYearNote()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    var saveResponse = await client.PostAsJsonAsync("/journal/anniversaries", new
    {
        monthDay = "05-16",
        title = "Journal 阶段日",
        type = "project-milestone",
        originDate = "2024-05-16",
        description = "从记录习惯走向记忆核心。",
        pinned = true
    });
    saveResponse.EnsureSuccessStatusCode();
    using var savedDocument = await JsonDocument.ParseAsync(await saveResponse.Content.ReadAsStreamAsync());
    var savedId = savedDocument.RootElement.GetProperty("id").GetString();
    Assert.False(string.IsNullOrWhiteSpace(savedId));

    var noteResponse = await client.PostAsJsonAsync(
        $"/journal/anniversaries/{savedId}/next-year-notes",
        new { text = "明年回来看。" });
    noteResponse.EnsureSuccessStatusCode();
    using var noteDocument = await JsonDocument.ParseAsync(await noteResponse.Content.ReadAsStreamAsync());
    var note = Assert.Single(noteDocument.RootElement.GetProperty("nextYearNotes").EnumerateArray());
    var noteId = note.GetProperty("id").GetString();
    Assert.False(string.IsNullOrWhiteSpace(noteId));

    var adoptResponse = await client.PostAsync(
        $"/journal/anniversaries/{savedId}/next-year-notes/{noteId}/adopt",
        null);
    adoptResponse.EnsureSuccessStatusCode();

    var listResponse = await client.GetAsync("/journal/anniversaries/05-16");
    listResponse.EnsureSuccessStatusCode();
    using var listDocument = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
    Assert.Contains(
        listDocument.RootElement.EnumerateArray(),
        item => item.GetProperty("title").GetString() == "Journal 阶段日");
}
```

- [ ] **Step 6: Run service and endpoint tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalAnniversaryServiceTests|TodayJournalEndpointTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Infrastructure/Today/JournalAnniversaryService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalAnniversaryServiceTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: add anniversary api"
```

---

## Task 3: Export And Import Anniversary Source Material

**Files:**
- Modify: `src/Journal.Infrastructure/Storage/JournalDataExportService.cs`
- Modify: `src/Journal.Infrastructure/Storage/JournalDataImportService.cs`
- Test: `tests/Journal.Tests/JournalDataExportServiceTests.cs`
- Test: `tests/Journal.Tests/JournalDataImportServiceTests.cs`

- [ ] **Step 1: Write failing export/import tests**

Add export assertion to the existing export tests or create `JournalDataExportServiceTests` if no focused file exists:

```csharp
[Fact]
public async Task ExportAsync_IncludesAnniversarySourceMaterial()
{
    using var workspace = TempWorkspace.Create();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    LocalJournalPaths.EnsureParentDirectory(paths.AnniversaryPath());
    await File.WriteAllTextAsync(paths.AnniversaryPath(), """{"schema":"journal-anniversaries/v1","items":[]}""");
    var exportPath = Path.Combine(workspace.Root, "export.zip");

    await new JournalDataExportService(paths).ExportAsync(exportPath, CancellationToken.None);

    using var archive = ZipFile.OpenRead(exportPath);
    Assert.Contains(archive.Entries, entry => entry.FullName == ".journal/anniversaries/anniversaries.json");
}
```

Add import assertion:

```csharp
[Fact]
public async Task ImportAsync_RestoresAnniversarySourceMaterial()
{
    using var workspace = TempWorkspace.Create();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    var packagePath = Path.Combine(workspace.Root, "import.zip");
    using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
    {
        var manifest = archive.CreateEntry("manifest.json");
        await using (var stream = manifest.Open())
        {
            await JsonSerializer.SerializeAsync(stream, new JournalDataExportManifest(
                "journal-export/v1",
                DateTimeOffset.Parse("2026-05-16T10:00:00+08:00"),
                "0.1.1",
                "0.1.1",
                "0.1.1",
                0,
                0,
                0,
                false));
        }

        var anniversaries = archive.CreateEntry(".journal/anniversaries/anniversaries.json");
        await using var anniversaryStream = anniversaries.Open();
        await using var writer = new StreamWriter(anniversaryStream);
        await writer.WriteAsync("""{"schema":"journal-anniversaries/v1","items":[]}""");
    }

    await new JournalDataImportService(paths, new JournalIndexingService(paths, new JournalIndexStore(paths)))
        .ImportAsync(packagePath, CancellationToken.None);

    Assert.True(File.Exists(paths.AnniversaryPath()));
}
```

- [ ] **Step 2: Run failing data package tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalDataExportServiceTests|JournalDataImportServiceTests"
```

Expected: export/import tests fail because anniversaries are not included or allowed yet.

- [ ] **Step 3: Include anniversaries in export**

Modify `JournalDataExportService.ExportAsync`:

```csharp
AddDirectory(archive, paths.AnniversaryDirectory(), ".journal/anniversaries", cancellationToken);
```

Place it with the other `.journal` source-material directories.

- [ ] **Step 4: Backup, clear, restore, and import anniversaries**

Modify `JournalDataImportService`:

```csharp
CopyDirectory(
    paths.AnniversaryDirectory(),
    Path.Combine(backupDirectory, ".journal", "anniversaries"),
    cancellationToken);
```

Add the matching restore:

```csharp
CopyDirectory(
    Path.Combine(backupDirectory, ".journal", "anniversaries"),
    paths.AnniversaryDirectory(),
    cancellationToken);
```

Add to `ClearImportTargets`:

```csharp
DeleteDirectory(paths.AnniversaryDirectory(), cancellationToken);
```

Add `anniversaries` to the `.journal` import allowlist. If helpers are named `IsJournalSourceDirectory` and `IsJournalDirectory`, add:

```csharp
|| IsJournalSourceDirectory(segments, "anniversaries")
```

and:

```csharp
&& IsJournalDirectory(segments, "anniversaries")
```

following the existing `raw-inputs`, `drafts`, `versions`, and `audit` pattern.

- [ ] **Step 5: Run data package tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalDataExportServiceTests|JournalDataImportServiceTests"
```

Expected: selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Infrastructure/Storage/JournalDataExportService.cs src/Journal.Infrastructure/Storage/JournalDataImportService.cs tests/Journal.Tests/JournalDataExportServiceTests.cs tests/Journal.Tests/JournalDataImportServiceTests.cs
git commit -m "feat: include anniversaries in data packages"
```

---

## Task 4: History Preview Fields For Memory Corridor Cards

**Files:**
- Modify: `src/Journal.Domain/Entries/JournalHistoryModels.cs`
- Modify: `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
- Modify: `src/Journal.Infrastructure/Today/JournalHistoryService.cs`
- Test: `tests/Journal.Tests/JournalIndexStoreTests.cs`
- Test: `tests/Journal.Tests/JournalHistoryServiceTests.cs`

- [ ] **Step 1: Write failing preview tests**

Extend `JournalIndexStoreTests.ReadAnniversaryAsync_ReturnsSameMonthDayEntriesNewestFirst` or add a new test:

```csharp
[Fact]
public async Task ReadAnniversaryAsync_ReturnsCardPreviewFromPrioritySections()
{
    using var workspace = TempWorkspace.Create();
    var store = CreateStore(workspace.Root);
    var date = JournalDate.From(new DateOnly(2026, 5, 16));
    await store.EnsureReadyAsync(DateTimeOffset.Parse("2026-05-16T09:00:00+08:00"), CancellationToken.None);
    await store.UpsertEntryAsync(
        CreateEntry(date, lastWriteTimeUtc: DateTimeOffset.Parse("2026-05-16T01:37:50Z")),
        [
            new JournalIndexedSection(date, "raw-inputs", "原始输入", 0, "- 原始长句不应成为卡片简介"),
            new JournalIndexedSection(date, "work", "工作与学习", 40, "- 优化 AI 系统提示\n- 修复小 bug，提高体验"),
            new JournalIndexedSection(date, "relationship", "生活与关系", 50, "- 周末带家人出去逛逛")
        ],
        CancellationToken.None);

    var result = await store.ReadAnniversaryAsync("05-16", 50, CancellationToken.None);

    var item = Assert.Single(result.Items);
    Assert.Equal(DateTimeOffset.Parse("2026-05-16T01:37:50Z"), item.EntryUpdatedAt);
    Assert.Equal("工作与学习", item.CardPreview.Title);
    Assert.Equal(["优化 AI 系统提示", "修复小 bug，提高体验", "周末带家人出去逛逛"], item.CardPreview.Lines);
    Assert.DoesNotContain(item.CardPreview.Lines, line => line.Contains("原始长句", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run failing preview tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "ReadAnniversaryAsync_ReturnsCardPreviewFromPrioritySections|JournalHistoryServiceTests"
```

Expected: compile failure because `EntryUpdatedAt` and `CardPreview` do not exist.

- [ ] **Step 3: Extend history models**

Modify `JournalHistoryModels.cs`:

```csharp
public sealed record JournalHistoryCardPreview(
    string Title,
    IReadOnlyList<string> Lines);
```

Extend `JournalHistoryEntrySummary`:

```csharp
public sealed record JournalHistoryEntrySummary(
    JournalDate Date,
    string Status,
    string? Mood,
    int RawInputCount,
    int VersionCount,
    IReadOnlyList<JournalHistoryHit> Hits,
    string? AttentionReason,
    DateTimeOffset? EntryUpdatedAt,
    JournalHistoryCardPreview CardPreview);
```

Use `new JournalHistoryCardPreview("日记", [])` at all construction sites before deriving real previews.

- [ ] **Step 4: Derive card preview in index store**

Add helper methods in `JournalIndexStore.cs`:

```csharp
private static readonly string[] CardPreviewSectionPriority =
[
    "today-focus",
    "work",
    "relationship",
    "mood",
    "yesterday-review",
    "inspiration"
];

private static JournalHistoryCardPreview CreateCardPreview(IReadOnlyList<JournalHistoryHit> hits, string? mood, int rawInputCount)
{
    var sectionHits = hits
        .Where(hit => string.Equals(hit.SourceType, "section", StringComparison.Ordinal)
            && !string.Equals(hit.SectionId, "raw-inputs", StringComparison.Ordinal)
            && !string.Equals(hit.SectionId, "metadata-note", StringComparison.Ordinal)
            && !string.Equals(hit.SectionId, "keywords", StringComparison.Ordinal))
        .OrderBy(hit => Array.IndexOf(CardPreviewSectionPriority, hit.SectionId ?? "") is var index && index >= 0 ? index : 999)
        .ToArray();

    var title = sectionHits.FirstOrDefault()?.Title ?? "日记";
    var lines = sectionHits
        .SelectMany(hit => SplitPreviewLines(hit.Snippet))
        .Take(3)
        .ToArray();

    if (lines.Length > 0)
    {
        return new JournalHistoryCardPreview(title, lines);
    }

    if (!string.IsNullOrWhiteSpace(mood))
    {
        return new JournalHistoryCardPreview("状态与情绪", [mood.Trim()]);
    }

    return new JournalHistoryCardPreview("日记", [$"{rawInputCount} 条材料"]);
}

private static IEnumerable<string> SplitPreviewLines(string value)
{
    foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var normalized = line.Trim();
        if (normalized.StartsWith("- ", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }

        if (normalized.Length > 0)
        {
            yield return normalized.Length > 90 ? normalized[..87] + "…" : normalized;
        }
    }
}
```

Update `ReadSummary` and grouped summary builders so each `JournalHistoryEntrySummary` receives:

```csharp
entryUpdatedAt: reader.GetDateTimeOffset("last_write_time_utc"),
cardPreview: CreateCardPreview(hits, mood, rawInputCount)
```

Use the existing reader helper style in `JournalIndexStore`. If no helper exists for nullable date-time offset, add one beside the existing reader helpers.

- [ ] **Step 5: Include last write time in anniversary SQL**

In `ReadAnniversaryAsync` SQL, add `last_write_time_utc` to both projection levels:

```sql
-- selected_entries projection
e.last_write_time_utc,

-- final select projection
se.last_write_time_utc,
```

Ensure search queries and summary queries also select the field when they construct `JournalHistoryEntrySummary`.

- [ ] **Step 6: Run history tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalIndexStoreTests|JournalHistoryServiceTests"
```

Expected: selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalHistoryModels.cs src/Journal.Infrastructure/Storage/JournalIndexStore.cs src/Journal.Infrastructure/Today/JournalHistoryService.cs tests/Journal.Tests/JournalIndexStoreTests.cs tests/Journal.Tests/JournalHistoryServiceTests.cs
git commit -m "feat: derive anniversary card previews"
```

---

## Task 5: Frontend API Contracts And App State

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Modify: `apps/desktop/src/App.tsx`
- Test: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Write failing frontend API/state tests**

Add to `App.test.tsx` an anniversary save flow around the existing same-day wheel tests:

```tsx
test("loads anniversaries for selected month day and saves current date anniversary", async () => {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(emptyToday))
    .mockResolvedValueOnce(jsonResponse(appInfo))
    .mockResolvedValueOnce(jsonResponse(anniversaryResult))
    .mockResolvedValueOnce(jsonResponse([]))
    .mockResolvedValueOnce(jsonResponse(historyDetail("2026-05-14", "- 打磨同日年轮")))
    .mockResolvedValueOnce(jsonResponse([]))
    .mockResolvedValueOnce(jsonResponse({
      id: "anniv-1",
      monthDay: "05-14",
      title: "同日年轮阶段日",
      type: "project-milestone",
      originDate: "2026-05-14",
      description: "阶段性纪念。",
      pinned: true,
      createdAt: "2026-05-16T10:00:00+08:00",
      updatedAt: "2026-05-16T10:00:00+08:00",
      nextYearNotes: []
    }))
    .mockResolvedValueOnce(jsonResponse([{
      id: "anniv-1",
      monthDay: "05-14",
      title: "同日年轮阶段日",
      type: "project-milestone",
      originDate: "2026-05-14",
      description: "阶段性纪念。",
      pinned: true,
      createdAt: "2026-05-16T10:00:00+08:00",
      updatedAt: "2026-05-16T10:00:00+08:00",
      nextYearNotes: []
    }]));

  render(<App />);
  await clickJournalCorridorItem("同日年轮");
  fireEvent.change(await screen.findByLabelText("纪念日名称"), { target: { value: "同日年轮阶段日" } });
  fireEvent.click(screen.getByRole("button", { name: "保存纪念日" }));

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/anniversaries/05-14", undefined);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/anniversaries", expect.objectContaining({ method: "POST" }));
  expect(await screen.findByText("同日年轮阶段日")).toBeInTheDocument();
});
```

Keep this test near the existing same-day wheel tests and extend the same `fetchMock` startup sequence already used by `App.test.tsx`.

- [ ] **Step 2: Run failing App test**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: fail because frontend anniversary API and component props do not exist.

- [ ] **Step 3: Add API types and functions**

Modify `apps/desktop/src/api.ts`:

```ts
export type JournalHistoryCardPreview = {
  title: string;
  lines: string[];
};
```

Extend `JournalHistoryEntrySummary`:

```ts
entryUpdatedAt: string | null;
cardPreview: JournalHistoryCardPreview;
```

Add anniversary types:

```ts
export type JournalNextYearNoteStatus = "pending" | "adopted" | "dismissed";

export type JournalNextYearNote = {
  id: string;
  targetDate: string;
  text: string;
  status: JournalNextYearNoteStatus;
  createdAt: string;
  adoptedAt: string | null;
  rawInputId: string | null;
};

export type JournalAnniversaryItem = {
  id: string;
  monthDay: string;
  title: string;
  type: string;
  originDate: string | null;
  description: string;
  pinned: boolean;
  createdAt: string;
  updatedAt: string;
  nextYearNotes: JournalNextYearNote[];
};

export type JournalAnniversarySaveRequest = {
  monthDay: string;
  title: string;
  type: string;
  originDate: string | null;
  description: string;
  pinned: boolean;
};
```

Add functions:

```ts
export async function getJournalAnniversaries(monthDay?: string) {
  const path = monthDay ? `/journal/anniversaries/${encodeURIComponent(monthDay)}` : "/journal/anniversaries";
  return await requestJson<JournalAnniversaryItem[]>(path);
}

export async function saveJournalAnniversary(request: JournalAnniversarySaveRequest) {
  return await requestJson<JournalAnniversaryItem>("/journal/anniversaries", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
}

export async function updateJournalAnniversary(id: string, request: JournalAnniversarySaveRequest) {
  return await requestJson<JournalAnniversaryItem>(`/journal/anniversaries/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
}

export async function addJournalNextYearNote(id: string, text: string) {
  return await requestJson<JournalAnniversaryItem>(`/journal/anniversaries/${encodeURIComponent(id)}/next-year-notes`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text })
  });
}
```

- [ ] **Step 4: Add App state and handlers**

Modify `App.tsx` imports and state:

```tsx
const [anniversaryItems, setAnniversaryItems] = useState<JournalAnniversaryItem[]>([]);
const [anniversaryError, setAnniversaryError] = useState("");
```

When opening or changing anniversary month day, load:

```tsx
const [result, anniversaries] = await Promise.all([
  getJournalAnniversaryWheel(monthDay, 50),
  getJournalAnniversaries(monthDay)
]);
setAnniversaryResult(result);
setAnniversaryItems(anniversaries);
```

Add save handler:

```tsx
async function handleSaveAnniversary(id: string | null, request: JournalAnniversarySaveRequest) {
  setAnniversaryError("");
  try {
    const saved = id
      ? await updateJournalAnniversary(id, request)
      : await saveJournalAnniversary(request);
    const refreshed = await getJournalAnniversaries(saved.monthDay);
    setAnniversaryItems(refreshed);
  } catch (error) {
    setAnniversaryError(error instanceof Error ? error.message : "纪念日保存失败");
  }
}
```

- [ ] **Step 5: Pass props to AnniversaryWheelWorkbench**

Add props:

```tsx
anniversaries={anniversaryItems}
anniversaryError={anniversaryError}
onSaveAnniversary={handleSaveAnniversary}
onAddNextYearNote={handleAddNextYearNote}
```

- [ ] **Step 6: Run App tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: App tests pass after component changes from Task 6 are complete. If this task is run before Task 6, keep the failing test committed only after Task 6 implementation.

- [ ] **Step 7: Commit after Task 6 passes**

Commit this task together with Task 6 because the API state and component props must compile together:

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: wire anniversary frontend state"
```

---

## Task 6: Memory Corridor UI And Reading Mode

**Files:**
- Modify: `apps/desktop/src/AnniversaryWheelWorkbench.tsx`
- Modify: `apps/desktop/src/AnniversaryWheelWorkbench.test.tsx`
- Modify: `apps/desktop/src/styles.css`

- [ ] **Step 1: Write failing component tests**

Update `AnniversaryWheelWorkbench.test.tsx` with tests:

```tsx
test("renders card preview lines instead of raw hit snippets", () => {
  const previewResult: JournalAnniversaryWheelResult = {
    monthDay: "05-14",
    items: [{
      date: date2026,
      status: "processed",
      mood: "期待",
      rawInputCount: 2,
      versionCount: 1,
      attentionReason: null,
      entryUpdatedAt: "2026-05-14T08:30:00+08:00",
      cardPreview: {
        title: "今日重点",
        lines: ["发布 v0.1.1 修正版本", "优化 AI 系统提示"]
      },
      hits: [{
        sourceType: "raw-input",
        sectionId: null,
        rawInputId: "raw-1",
        title: "text",
        snippet: "原始输入长句不应该成为卡片简介"
      }]
    }]
  };

  render(
    <AnniversaryWheelWorkbench
      isBusy={false}
      monthDay="05-14"
      result={previewResult}
      selectedDate="2026-05-14"
      detail={detail}
      versions={[]}
      selectedVersionDetail={null}
      anniversaries={[]}
      anniversaryError=""
      error=""
      onBack={() => {}}
      onRefresh={() => {}}
      onMonthDayChange={() => {}}
      onSelectDate={() => {}}
      onSaveAnniversary={() => {}}
      onAddNextYearNote={() => {}}
    />
  );

  expect(screen.getByText("发布 v0.1.1 修正版本")).toBeInTheDocument();
  expect(screen.queryByText("原始输入长句不应该成为卡片简介")).not.toBeInTheDocument();
});

test("opens selected date markdown in reading mode and returns to timeline", () => {
  render(
    <AnniversaryWheelWorkbench
      isBusy={false}
      monthDay="05-14"
      result={result}
      selectedDate="2026-05-14"
      detail={detail}
      versions={[version]}
      selectedVersionDetail={null}
      anniversaries={[]}
      anniversaryError=""
      error=""
      onBack={() => {}}
      onRefresh={() => {}}
      onMonthDayChange={() => {}}
      onSelectDate={() => {}}
      onSaveAnniversary={() => {}}
      onAddNextYearNote={() => {}}
    />
  );

  fireEvent.click(screen.getByRole("button", { name: /阅读 2026-05-14 日记/ }));

  expect(screen.getByRole("button", { name: "返回年轮" })).toBeInTheDocument();
  expect(screen.getByLabelText("同日当前日记内容")).toHaveTextContent("打磨同日年轮");

  fireEvent.click(screen.getByRole("button", { name: "返回年轮" }));

  expect(screen.getByRole("region", { name: "同日时间线" })).toBeInTheDocument();
});

test("shows empty anniversary list until user saves a pinned anniversary", () => {
  render(
    <AnniversaryWheelWorkbench
      isBusy={false}
      monthDay="05-14"
      result={result}
      selectedDate="2026-05-14"
      detail={detail}
      versions={[]}
      selectedVersionDetail={null}
      anniversaries={[]}
      anniversaryError=""
      error=""
      onBack={() => {}}
      onRefresh={() => {}}
      onMonthDayChange={() => {}}
      onSelectDate={() => {}}
      onSaveAnniversary={() => {}}
      onAddNextYearNote={() => {}}
    />
  );

  expect(screen.getByText("还没有保存常看日期")).toBeInTheDocument();
});
```

Before adding these tests, update the existing `result` fixture entries with `entryUpdatedAt` and `cardPreview` so the file compiles after Task 5 extends `JournalHistoryEntrySummary`.

- [ ] **Step 2: Run failing component tests**

Run:

```powershell
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx
```

Expected: tests fail because props/UI are not implemented yet.

- [ ] **Step 3: Extend component props**

Add props to `AnniversaryWheelWorkbenchProps`:

```tsx
anniversaries: JournalAnniversaryItem[];
anniversaryError: string;
onSaveAnniversary: (id: string | null, request: JournalAnniversarySaveRequest) => void;
onAddNextYearNote: (anniversaryId: string, text: string) => void;
```

Import the new types from `api.ts`.

- [ ] **Step 4: Add local read mode state**

Inside `AnniversaryWheelWorkbench`:

```tsx
const [isReading, setIsReading] = useState(false);

function openReading(date: string) {
  setIsReading(true);
  onSelectDate(date);
}

function returnToTimeline() {
  setIsReading(false);
}
```

If selecting another year from the left list while reading, call `setIsReading(false)` before `onSelectDate`.

- [ ] **Step 5: Render timeline cards from cardPreview**

Use:

```tsx
const previewLines = item.cardPreview?.lines?.length
  ? item.cardPreview.lines
  : [firstLine(item)];
```

Render:

```tsx
<h3>{item.cardPreview?.title ?? item.hits[0]?.title ?? item.mood ?? "日记"}</h3>
<ul className="anniversary-card-preview">
  {previewLines.slice(0, 3).map(line => <li key={line}>{line}</li>)}
</ul>
```

Do not render raw input hits as the first-choice preview.

- [ ] **Step 6: Render reading mode**

When `isReading` is true:

```tsx
<button type="button" className="secondary-action secondary" onClick={returnToTimeline}>
  <ArrowLeft size={15} aria-hidden="true" />
  返回年轮
</button>
```

Main body:

```tsx
{isReading ? (
  <section className="history-current-main-preview anniversary-reading-paper" aria-label="同日当前日记内容">
    {isEntryDetailLoading ? (
      <JournalPaperLoading label="同日当前日记读取中" />
    ) : currentDetailMarkdown ? (
      <MarkdownPreview markdown={currentDetailMarkdown} />
    ) : (
      <section className="empty-paper audit-empty-state">
        <h2>没有正式日记</h2>
        <p>这一年还没有可阅读的正式 Markdown。</p>
      </section>
    )}
  </section>
) : (
  <section className="anniversary-timeline" aria-label="同日时间线">
    {items.map(item => {
      const previewLines = item.cardPreview?.lines?.length
        ? item.cardPreview.lines
        : [firstLine(item)];

      return (
        <article className="anniversary-timeline-card" key={item.date.isoDate}>
          <span className="anniversary-timeline-node" aria-hidden="true" />
          <div className="anniversary-timeline-card-body">
            <p className="source-meta">
              <span>{item.date.year}</span>
              <span>{getStatusLabel(item.status)}</span>
            </p>
            <h3>{item.cardPreview?.title ?? item.hits[0]?.title ?? item.mood ?? "日记"}</h3>
            <ul className="anniversary-card-preview">
              {previewLines.slice(0, 3).map(line => <li key={line}>{line}</li>)}
            </ul>
            <button
              type="button"
              className="assistant-inline-action"
              aria-label={`阅读 ${item.date.isoDate} 日记`}
              onClick={() => openReading(item.date.isoDate)}
            >
              <Eye size={14} aria-hidden="true" />
              阅读
            </button>
          </div>
        </article>
      );
    })}
  </section>
)}
```

Keep existing `返回今日` in the main stage toolbar; do not move it to a fake topbar.

- [ ] **Step 7: Render real anniversary side panels**

Left panel:

```tsx
const pinnedAnniversaries = anniversaries.filter(item => item.pinned);
```

Render empty state:

```tsx
{pinnedAnniversaries.length === 0 ? (
  <p className="muted">还没有保存常看日期。</p>
) : pinnedAnniversaries.map(item => (
  <button
    key={item.id}
    type="button"
    className={`source-item anniversary-saved-day ${item.monthDay === monthDay ? "is-active" : ""}`}
    onClick={() => onMonthDayChange(item.monthDay)}
  >
    <span className="source-meta">
      <span>{item.monthDay}</span>
      <span>{item.type}</span>
    </span>
    <strong>{item.title}</strong>
    {item.description ? <p>{item.description}</p> : null}
  </button>
))}
```

Right panel form:

- input label `纪念日名称`
- select label `纪念日类型`
- input/select label `起点日期`
- textarea label `说明`
- button `保存纪念日`
- textarea/input label `写给下一年同一天`
- button `保存下一年提醒`

On submit, call `onSaveAnniversary(selectedAnniversary?.id ?? null, request)`.

- [ ] **Step 8: Update CSS**

Add classes in `apps/desktop/src/styles.css`:

```css
.anniversary-timeline {
  display: grid;
  gap: 18px;
}

.anniversary-card-preview {
  display: grid;
  gap: 5px;
  margin: 10px 0 0;
  padding-left: 18px;
  color: var(--text-muted);
  line-height: 1.6;
}

.anniversary-reading-paper .markdown-preview {
  max-width: none;
  min-height: 0;
  margin: 0;
  padding: 0;
}

.anniversary-form {
  display: grid;
  gap: 10px;
}
```

Use existing color variables from nearby history/assistant styles; keep cards at `8px` radius or less.

- [ ] **Step 9: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx App.test.tsx
```

Expected: selected frontend tests pass.

- [ ] **Step 10: Commit**

```powershell
git add apps/desktop/src/AnniversaryWheelWorkbench.tsx apps/desktop/src/AnniversaryWheelWorkbench.test.tsx apps/desktop/src/styles.css apps/desktop/src/api.ts apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: implement memory corridor interface"
```

---

## Task 7: Final Verification And Documentation Sync

**Files:**
- Modify: `docs/agents/PROJECT_CONTEXT.md`
- Modify: `docs/agents/DEVELOPMENT_REFERENCE.md`
- Modify: `docs/superpowers/archives/INDEX.md` only if archiving after implementation is accepted
- Create archive under `docs/superpowers/archives/2026-05/` only after implementation is verified and accepted

- [ ] **Step 1: Update project context**

In `docs/agents/PROJECT_CONTEXT.md`, update delivered scope only after implementation passes:

```markdown
- Same-Day Memory Corridor includes same-day timeline cards, formal-entry reading mode, saved anniversaries, and next-year notes backed by `.journal/anniversaries/anniversaries.json`.
```

Do not add this line before the implementation works.

- [ ] **Step 2: Update development reference**

In `docs/agents/DEVELOPMENT_REFERENCE.md`, add focused paths:

```markdown
- Anniversary source data: `JournalAnniversaryStore.cs`, `JournalAnniversaryService.cs`, `.journal/anniversaries/anniversaries.json`.
- Memory corridor UI: `apps/desktop/src/AnniversaryWheelWorkbench.tsx`.
```

Add focused test commands:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalAnniversary|JournalIndexStoreTests|JournalHistoryServiceTests|JournalDataImportServiceTests|JournalDataExportServiceTests"
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx App.test.tsx
```

- [ ] **Step 3: Run full verification**

Run:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
git diff --check
```

Expected:

- `dotnet test Journal.slnx`: all tests pass.
- `npm test --prefix apps/desktop`: all tests pass.
- `npm run build --prefix apps/desktop`: Vite build completes.
- `git diff --check`: no whitespace errors; CRLF warnings are acceptable if they match existing repository behavior.

- [ ] **Step 4: Commit docs sync**

```powershell
git add docs/agents/PROJECT_CONTEXT.md docs/agents/DEVELOPMENT_REFERENCE.md
git commit -m "docs: update phase 8 development context"
```

- [ ] **Step 5: Run asset compounding gate**

After implementation is verified and user accepts the behavior, run the repository asset-compounding gate:

```text
event_type: implementation-boundary
route: archive or both
reason: Phase 8 delivers a coherent memory corridor and anniversary data-domain requirement.
evidence: dotnet test Journal.slnx; npm test --prefix apps/desktop; npm run build --prefix apps/desktop; git diff --check
```

If the implementation uncovers a reusable failure mode, create/update a problem asset before final close-out.

---

## Self-Review

- Spec coverage: covered same-day timeline, formal diary reading mode, real return locations, saved anniversaries, next-year notes, `hits` demotion, section-derived card previews, export/import, leap day rules, and user-approved raw input adoption.
- Placeholder scan: no task relies on unspecified placeholder work; each task lists exact files, commands, and expected results.
- Type consistency: anniversary model names, API paths, and frontend prop names are consistent across tasks.
