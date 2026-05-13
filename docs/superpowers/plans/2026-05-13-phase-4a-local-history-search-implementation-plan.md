# Phase 4A Local History Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the local history foundation: version snapshots before formal entry overwrite, rebuildable SQLite/FTS5 history index, history/search API, and a real history workbench wired into the current desktop UI.

**Architecture:** Formal entry writes go through a single `EntryWritePipeline`: snapshot old entry first, write Markdown second, update the rebuildable SQLite index last. SQLite is a cache over Markdown/raw-input/version files, so scan/rebuild always trusts files on disk and never treats the database as durable truth. The desktop app opens history as a full workspace mode, mirroring audit, with search/date results on the left, entry preview in the center, and versions/restore actions on the right.

**Tech Stack:** .NET 10 minimal API, `Microsoft.Data.Sqlite`, SQLite FTS5 trigram, xUnit, Electron + React + Vite + TypeScript, Vitest + Testing Library.

---

## References And Constraints

- Spec: `docs/superpowers/specs/2026-05-13-phase-4a-local-history-search-design.md`
- Prototype: `docs/superpowers/specs/2026-05-13-phase-4a-history-search-layout-prototype.html`
- Microsoft.Data.Sqlite docs:
  - `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/`
  - `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors`
  - `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings`
  - `https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions`
- Use one SQLite connection per operation. Do not share `SqliteConnection` instances across threads.
- Let `dotnet add package Microsoft.Data.Sqlite` choose the current stable package during implementation, then commit the generated version in `src/Journal.Infrastructure/Journal.Infrastructure.csproj`.
- The default Microsoft.Data.Sqlite bundle includes SQLite features required here, including FTS5.
- Do not slow Today startup with a full index scan. Scan when opening history and after explicit index endpoints.
- Recovery from version writes a reviewing draft only. It never overwrites `entries/` directly.
- Keep the history entry inside the existing `Today Assistant`; do not add a top-level mode tab.

## File Map

### Backend Create

- `src/Journal.Domain/Entries/JournalEntryVersion.cs`  
  Version metadata record returned by the store/API.
- `src/Journal.Domain/Entries/JournalHistoryModels.cs`  
  History search/detail/scan result records shared by API and tests.
- `src/Journal.Infrastructure/Storage/IJournalVersionStore.cs`  
  Small seam for testing snapshot failure without filesystem tricks.
- `src/Journal.Infrastructure/Storage/JournalVersionStore.cs`  
  Writes `.journal/versions/yyyy/MM/yyyy-MM-dd/*.md` and metadata.
- `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`  
  Owns SQLite schema, schema version checks, query/upsert, backup/rebuild setup.
- `src/Journal.Infrastructure/Storage/JournalIndexingService.cs`  
  Parses Markdown/raw inputs/versions and synchronizes index rows plus FTS.
- `src/Journal.Infrastructure/Storage/EntryWritePipeline.cs`  
  Central formal entry write flow: snapshot, write, index.
- `src/Journal.Infrastructure/Today/JournalHistoryService.cs`  
  Composes history API operations and version restore-to-draft.

### Backend Modify

- `src/Journal.Infrastructure/Journal.Infrastructure.csproj`  
  Add `Microsoft.Data.Sqlite`.
- `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`  
  Add version and index paths plus safe version id validation.
- `src/Journal.Infrastructure/Today/TodayJournalService.cs`  
  Replace direct formal entry write with `EntryWritePipeline`.
- `src/Journal.Api/Program.cs`  
  Register new services and add history/index endpoints.
- `src/Journal.Domain/Entries/JournalStatus.cs`  
  Add `Missing` only if the existing enum lacks it during implementation.

### Frontend Create

- `apps/desktop/src/HistoryWorkbench.tsx`  
  Full workspace view for search, entry preview, versions, restore.
- `apps/desktop/src/HistoryWorkbench.test.tsx`  
  Component-level tests for search, status filter, selection, restore.

### Frontend Modify

- `apps/desktop/src/api.ts`  
  Add history/index DTOs and functions.
- `apps/desktop/src/App.tsx`  
  Extend `workspaceMode` to include `history`, add assistant entry and refresh flow.
- `apps/desktop/src/App.test.tsx`  
  Cover entry point, return-to-today, restore behavior, and state preservation.
- `apps/desktop/src/styles.css`  
  Add history styles using current desktop shell classes.

### Tests Create/Modify

- Create `tests/Journal.Tests/JournalVersionStoreTests.cs`
- Create `tests/Journal.Tests/JournalIndexStoreTests.cs`
- Create `tests/Journal.Tests/JournalIndexingServiceTests.cs`
- Create `tests/Journal.Tests/EntryWritePipelineTests.cs`
- Create `tests/Journal.Tests/JournalHistoryServiceTests.cs`
- Modify `tests/Journal.Tests/TodayJournalServiceTests.cs`
- Modify `tests/Journal.Tests/TodayJournalEndpointTests.cs`
- Modify `tests/Journal.Tests/LocalJournalStorageTests.cs`

## Subagent Split

- Worker A owns storage and version snapshots: paths, `JournalVersionStore`, `EntryWritePipeline`.
- Worker B owns SQLite and indexing: schema, scan, FTS, rebuild.
- Worker C owns API/service integration: `JournalHistoryService`, endpoints, endpoint tests.
- Worker D owns frontend: `api.ts`, `HistoryWorkbench`, `App.tsx`, CSS, Vitest tests.
- Parent agent owns cross-worker integration, final test suite, feature archive, development-problem archive, and commits/merge.

Workers are not alone in the codebase. Each worker must avoid reverting edits made by other workers and should adapt to already-merged changes.

If a worker hits a non-trivial implementation problem, the worker reports symptom, root cause, fix, affected files, and verification evidence in its final handoff. The parent agent decides whether it belongs in `docs/superpowers/problems/` or `docs/superpowers/inbox/`, writes or updates that asset, and updates the relevant index. Workers do not independently scatter problem archives.

---

## Task 1: Add Storage Paths And SQLite Package

**Files:**
- Modify: `src/Journal.Infrastructure/Journal.Infrastructure.csproj`
- Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Modify: `tests/Journal.Tests/LocalJournalStorageTests.cs`

- [ ] **Step 1: Add failing path tests**

Add tests to `tests/Journal.Tests/LocalJournalStorageTests.cs`:

```csharp
[Fact]
public void VersionPaths_AreUnderHiddenJournalVersionDirectory()
{
    var paths = new LocalJournalPaths(new JournalStorageOptions(@"C:\JournalTest"));
    var date = JournalDate.From(new DateOnly(2026, 5, 13));

    Assert.Equal(
        Path.Combine(@"C:\JournalTest", ".journal", "versions", "2026", "05", "2026-05-13"),
        paths.VersionDirectory(date));
    Assert.Equal(
        Path.Combine(@"C:\JournalTest", ".journal", "versions", "2026", "05", "2026-05-13", "version-2026-05-13T07-11-14+08-00.md"),
        paths.VersionMarkdownPath(date, "version-2026-05-13T07-11-14+08-00"));
    Assert.Equal(
        Path.Combine(@"C:\JournalTest", ".journal", "versions", "2026", "05", "2026-05-13", "version-2026-05-13T07-11-14+08-00.meta.json"),
        paths.VersionMetaPath(date, "version-2026-05-13T07-11-14+08-00"));
}

[Theory]
[InlineData("version-2026-05-13T07-11-14+08-00", true)]
[InlineData("version-2026-05-13_07-11-14", true)]
[InlineData("../escape", false)]
[InlineData("version:bad", false)]
[InlineData("", false)]
public void IsValidVersionId_RejectsPathEscapes(string value, bool expected)
{
    Assert.Equal(expected, LocalJournalPaths.IsValidVersionId(value));
}

[Fact]
public void IndexPaths_AreUnderHiddenJournalIndexDirectory()
{
    var paths = new LocalJournalPaths(new JournalStorageOptions(@"C:\JournalTest"));

    Assert.Equal(Path.Combine(@"C:\JournalTest", ".journal", "index"), paths.IndexDirectory());
    Assert.Equal(Path.Combine(@"C:\JournalTest", ".journal", "index", "journal.db"), paths.IndexPath());
    Assert.Equal(Path.Combine(@"C:\JournalTest", ".journal", "index", "backups"), paths.IndexBackupDirectory());
}
```

- [ ] **Step 2: Run path tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter LocalJournalStorageTests
```

Expected: fail because `VersionDirectory`, `VersionMarkdownPath`, `VersionMetaPath`, `IsValidVersionId`, `IndexDirectory`, `IndexPath`, and `IndexBackupDirectory` do not exist.

- [ ] **Step 3: Add package**

Run from repository root:

```powershell
dotnet add src/Journal.Infrastructure/Journal.Infrastructure.csproj package Microsoft.Data.Sqlite
```

Expected: `src/Journal.Infrastructure/Journal.Infrastructure.csproj` gains a `PackageReference Include="Microsoft.Data.Sqlite"` entry.

- [ ] **Step 4: Implement new path methods**

Add to `LocalJournalPaths`:

```csharp
public string VersionDirectory(JournalDate date) =>
    Path.Combine(_rootDirectory, ".journal", "versions", date.Year, date.Month, date.IsoDate);

public string VersionMarkdownPath(JournalDate date, string versionId) =>
    IsValidVersionId(versionId)
        ? Path.Combine(VersionDirectory(date), $"{versionId}.md")
        : throw new ArgumentException("versionId contains invalid path characters.", nameof(versionId));

public string VersionMetaPath(JournalDate date, string versionId) =>
    IsValidVersionId(versionId)
        ? Path.Combine(VersionDirectory(date), $"{versionId}.meta.json")
        : throw new ArgumentException("versionId contains invalid path characters.", nameof(versionId));

public string IndexDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "index");

public string IndexPath() =>
    Path.Combine(IndexDirectory(), "journal.db");

public string IndexBackupDirectory() =>
    Path.Combine(IndexDirectory(), "backups");

public static bool IsValidVersionId(string? versionId) =>
    !string.IsNullOrWhiteSpace(versionId)
    && versionId.All(character =>
        char.IsAsciiLetterOrDigit(character)
        || character == '-'
        || character == '_'
        || character == '+');
```

- [ ] **Step 5: Run path tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter LocalJournalStorageTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Infrastructure/Journal.Infrastructure.csproj src/Journal.Infrastructure/Storage/LocalJournalPaths.cs tests/Journal.Tests/LocalJournalStorageTests.cs
git commit -m "feat: add history storage paths"
```

---

## Task 2: Implement Version Snapshot Store

**Files:**
- Create: `src/Journal.Domain/Entries/JournalEntryVersion.cs`
- Create: `src/Journal.Infrastructure/Storage/IJournalVersionStore.cs`
- Create: `src/Journal.Infrastructure/Storage/JournalVersionStore.cs`
- Create: `tests/Journal.Tests/JournalVersionStoreTests.cs`

- [ ] **Step 1: Add failing version store tests**

Create `tests/Journal.Tests/JournalVersionStoreTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalVersionStoreTests
{
    [Fact]
    public async Task CreateSnapshotAsync_WritesMarkdownAndMetadata()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));
        var now = DateTimeOffset.Parse("2026-05-13T07:11:14+08:00");

        var version = await store.CreateSnapshotAsync(
            date,
            "# Existing entry\n",
            "entries/2026/05/2026-05-13.md",
            "confirm-draft",
            now,
            CancellationToken.None);

        Assert.Equal(date, version.Date);
        Assert.Equal("confirm-draft", version.Reason);
        Assert.StartsWith("version-2026-05-13T07-11-14", version.Id, StringComparison.Ordinal);
        Assert.True(File.Exists(version.MarkdownPath));
        Assert.True(File.Exists(version.MetaPath));
        Assert.Equal("# Existing entry\n", await File.ReadAllTextAsync(version.MarkdownPath));
        Assert.Contains("\"reason\": \"confirm-draft\"", await File.ReadAllTextAsync(version.MetaPath));
        Assert.StartsWith("sha256:", version.ContentHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadByDateAsync_ReturnsVersionsNewestFirst()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalVersionStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await store.CreateSnapshotAsync(date, "old", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T07:00:00+08:00"), CancellationToken.None);
        await store.CreateSnapshotAsync(date, "new", "entry.md", "confirm-draft", DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), CancellationToken.None);

        var versions = await store.ReadByDateAsync(date, CancellationToken.None);

        Assert.Collection(
            versions,
            first => Assert.Equal(DateTimeOffset.Parse("2026-05-13T08:00:00+08:00"), first.CreatedAt),
            second => Assert.Equal(DateTimeOffset.Parse("2026-05-13T07:00:00+08:00"), second.CreatedAt));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-version-store-tests", Guid.NewGuid().ToString("N"));

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

- [ ] **Step 2: Run version tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalVersionStoreTests
```

Expected: fail because version store types do not exist.

- [ ] **Step 3: Add domain and interface records**

Create `src/Journal.Domain/Entries/JournalEntryVersion.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalEntryVersion(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Reason,
    string SourceEntryPath,
    string MarkdownPath,
    string MetaPath,
    string ContentHash);
```

Create `src/Journal.Infrastructure/Storage/IJournalVersionStore.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public interface IJournalVersionStore
{
    Task<JournalEntryVersion> CreateSnapshotAsync(
        JournalDate date,
        string markdown,
        string sourceEntryPath,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
        JournalDate date,
        CancellationToken cancellationToken);

    Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement `JournalVersionStore`**

Create `src/Journal.Infrastructure/Storage/JournalVersionStore.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalVersionStore(LocalJournalPaths paths) : IJournalVersionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<JournalEntryVersion> CreateSnapshotAsync(
        JournalDate date,
        string markdown,
        string sourceEntryPath,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new ArgumentException("markdown is required", nameof(markdown));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("reason is required", nameof(reason));
        }

        var id = CreateVersionId(createdAt);
        var markdownPath = paths.VersionMarkdownPath(date, id);
        var metaPath = paths.VersionMetaPath(date, id);
        var hash = ComputeSha256(markdown);
        var version = new JournalEntryVersion(id, date, createdAt, reason.Trim(), sourceEntryPath, markdownPath, metaPath, hash);

        LocalJournalPaths.EnsureParentDirectory(markdownPath);
        await File.WriteAllTextAsync(markdownPath, markdown, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(version, JsonOptions), Encoding.UTF8, cancellationToken);

        return version;
    }

    public async Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
        JournalDate date,
        CancellationToken cancellationToken)
    {
        var directory = paths.VersionDirectory(date);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<JournalEntryVersion>();
        }

        var versions = new List<JournalEntryVersion>();
        foreach (var metaPath in Directory.EnumerateFiles(directory, "*.meta.json"))
        {
            var json = await File.ReadAllTextAsync(metaPath, Encoding.UTF8, cancellationToken);
            var version = JsonSerializer.Deserialize<JournalEntryVersion>(json, JsonOptions);
            if (version is not null)
            {
                versions.Add(version);
            }
        }

        return versions
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken)
    {
        if (!LocalJournalPaths.IsValidVersionId(versionId))
        {
            return null;
        }

        var metaPath = paths.VersionMetaPath(date, versionId);
        var markdownPath = paths.VersionMarkdownPath(date, versionId);
        if (!File.Exists(metaPath) || !File.Exists(markdownPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metaPath, Encoding.UTF8, cancellationToken);
        var version = JsonSerializer.Deserialize<JournalEntryVersion>(json, JsonOptions);
        if (version is null)
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(markdownPath, Encoding.UTF8, cancellationToken);
        return (version, markdown);
    }

    private static string CreateVersionId(DateTimeOffset createdAt)
    {
        var timestamp = createdAt
            .ToString("yyyy-MM-ddTHH-mm-sszzz", CultureInfo.InvariantCulture)
            .Replace(":", "-", StringComparison.Ordinal);
        return $"version-{timestamp}";
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
```

- [ ] **Step 5: Run version tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalVersionStoreTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalEntryVersion.cs src/Journal.Infrastructure/Storage/IJournalVersionStore.cs src/Journal.Infrastructure/Storage/JournalVersionStore.cs tests/Journal.Tests/JournalVersionStoreTests.cs
git commit -m "feat: add entry version snapshots"
```

---

## Task 3: Add SQLite Schema And Index Store

**Files:**
- Create: `src/Journal.Domain/Entries/JournalHistoryModels.cs`
- Create: `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
- Create: `tests/Journal.Tests/JournalIndexStoreTests.cs`

- [ ] **Step 1: Add failing schema tests**

Create `tests/Journal.Tests/JournalIndexStoreTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalIndexStoreTests
{
    [Fact]
    public async Task EnsureReadyAsync_CreatesSchemaAndMetaVersion()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalIndexStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));

        await store.EnsureReadyAsync(CancellationToken.None);

        var meta = await store.ReadMetaAsync("schema_version", CancellationToken.None);
        Assert.Equal("1", meta);
    }

    [Fact]
    public async Task SearchAsync_UsesTrigramFtsForChineseSubstring()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JournalIndexStore(new LocalJournalPaths(new JournalStorageOptions(workspace.Root)));
        var date = JournalDate.From(new DateOnly(2026, 5, 13));

        await store.EnsureReadyAsync(CancellationToken.None);
        await store.UpsertEntryAsync(
            new JournalIndexedEntry(
                date,
                "entries/2026/05/2026-05-13.md",
                "processed",
                "平静",
                "[]",
                "[\"接口整理\"]",
                "sha256:test",
                DateTimeOffset.Parse("2026-05-13T00:00:00Z"),
                42,
                DateTimeOffset.Parse("2026-05-13T01:00:00Z"),
                null),
            [
                new JournalIndexedSection(date, "today-focus", "今天想推进", 20, "- 测试新整理的接口")
            ],
            CancellationToken.None);

        var result = await store.SearchAsync(new JournalHistoryQuery("整理", null, null, null, 20), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("2026-05-13", item.Date.IsoDate);
        Assert.Contains(item.Hits, hit => hit.SourceType == "section" && hit.SectionId == "today-focus");
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-index-store-tests", Guid.NewGuid().ToString("N"));

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

- [ ] **Step 2: Run schema tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexStoreTests
```

Expected: fail because index models and store do not exist.

- [ ] **Step 3: Add history model records**

Create `src/Journal.Domain/Entries/JournalHistoryModels.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalIndexedEntry(
    JournalDate Date,
    string EntryPath,
    string Status,
    string? Mood,
    string TagsJson,
    string TopicsJson,
    string ContentHash,
    DateTimeOffset LastWriteTimeUtc,
    long FileSize,
    DateTimeOffset IndexedAtUtc,
    string? AttentionReason);

public sealed record JournalIndexedSection(
    JournalDate Date,
    string SectionId,
    string Title,
    int DisplayOrder,
    string Content);

public sealed record JournalIndexedRawInput(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    string Source,
    string Text);

public sealed record JournalHistoryQuery(
    string? Query,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    int Limit);

public sealed record JournalHistorySearchResult(IReadOnlyList<JournalHistoryEntrySummary> Items);

public sealed record JournalHistoryEntrySummary(
    JournalDate Date,
    string Status,
    string? Mood,
    int RawInputCount,
    int VersionCount,
    IReadOnlyList<JournalHistoryHit> Hits,
    string? AttentionReason);

public sealed record JournalHistoryHit(
    string SourceType,
    string? SectionId,
    string? RawInputId,
    string Title,
    string Snippet);
```

- [ ] **Step 4: Implement schema and search**

Create `src/Journal.Infrastructure/Storage/JournalIndexStore.cs` with:

```csharp
using System.Globalization;
using Journal.Domain.Entries;
using Microsoft.Data.Sqlite;

namespace Journal.Infrastructure.Storage;

public sealed class JournalIndexStore(LocalJournalPaths paths)
{
    private const int SchemaVersion = 1;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        LocalJournalPaths.EnsureParentDirectory(paths.IndexPath());
        await using var connection = OpenConnection();
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA busy_timeout = 5000;", cancellationToken);
        await ExecuteAsync(connection, SchemaSql, cancellationToken);
        await SetMetaAsync("schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task<string?> ReadMetaAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM journal_meta WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetMetaAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO journal_meta(key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertEntryAsync(
        JournalIndexedEntry entry,
        IReadOnlyList<JournalIndexedSection> sections,
        CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var entryCommand = connection.CreateCommand();
        entryCommand.Transaction = (SqliteTransaction)transaction;
        entryCommand.CommandText = """
            INSERT INTO entries(date, month_day, entry_path, status, mood, tags_json, topics_json, content_hash, last_write_time_utc, file_size, indexed_at_utc, attention_reason)
            VALUES ($date, $monthDay, $entryPath, $status, $mood, $tagsJson, $topicsJson, $contentHash, $lastWriteTimeUtc, $fileSize, $indexedAtUtc, $attentionReason)
            ON CONFLICT(date) DO UPDATE SET
                status = excluded.status,
                mood = excluded.mood,
                tags_json = excluded.tags_json,
                topics_json = excluded.topics_json,
                content_hash = excluded.content_hash,
                last_write_time_utc = excluded.last_write_time_utc,
                file_size = excluded.file_size,
                indexed_at_utc = excluded.indexed_at_utc,
                attention_reason = excluded.attention_reason;
            """;
        AddEntryParameters(entryCommand, entry);
        await entryCommand.ExecuteNonQueryAsync(cancellationToken);

        await ExecuteAsync(connection, "DELETE FROM entry_sections WHERE date = $date;", cancellationToken, ("$date", entry.Date.IsoDate));
        await ExecuteAsync(connection, "DELETE FROM section_fts WHERE date = $date;", cancellationToken, ("$date", entry.Date.IsoDate));

        foreach (var section in sections)
        {
            var sectionCommand = connection.CreateCommand();
            sectionCommand.Transaction = (SqliteTransaction)transaction;
            sectionCommand.CommandText = """
                INSERT INTO entry_sections(date, section_id, title, display_order, content)
                VALUES ($date, $sectionId, $title, $displayOrder, $content);
                INSERT INTO section_fts(date, section_id, title, content, metadata)
                VALUES ($date, $sectionId, $title, $content, $metadata);
                """;
            sectionCommand.Parameters.AddWithValue("$date", section.Date.IsoDate);
            sectionCommand.Parameters.AddWithValue("$sectionId", section.SectionId);
            sectionCommand.Parameters.AddWithValue("$title", section.Title);
            sectionCommand.Parameters.AddWithValue("$displayOrder", section.DisplayOrder);
            sectionCommand.Parameters.AddWithValue("$content", section.Content);
            sectionCommand.Parameters.AddWithValue("$metadata", $"{entry.Mood} {entry.TagsJson} {entry.TopicsJson}");
            await sectionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<JournalHistorySearchResult> SearchAsync(
        JournalHistoryQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        var limit = query.Limit is > 0 and <= 100 ? query.Limit : 50;
        await using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(query.Query)
            ? """
                SELECT e.date, e.status, e.mood, e.attention_reason, 0 AS raw_count, 0 AS version_count
                FROM entries e
                WHERE ($status IS NULL OR e.status = $status)
                ORDER BY e.date DESC
                LIMIT $limit;
                """
            : """
                SELECT DISTINCT e.date, e.status, e.mood, e.attention_reason, 0 AS raw_count, 0 AS version_count
                FROM entries e
                JOIN section_fts f ON f.date = e.date
                WHERE section_fts MATCH $query AND ($status IS NULL OR e.status = $status)
                ORDER BY e.date DESC
                LIMIT $limit;
                """;
        command.Parameters.AddWithValue("$query", query.Query ?? string.Empty);
        command.Parameters.AddWithValue("$status", (object?)query.Status ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<JournalHistoryEntrySummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = JournalDate.Parse(reader.GetString(0));
            var status = reader.GetString(1);
            var mood = reader.IsDBNull(2) ? null : reader.GetString(2);
            var attentionReason = reader.IsDBNull(3) ? null : reader.GetString(3);
            var hits = string.IsNullOrWhiteSpace(query.Query)
                ? Array.Empty<JournalHistoryHit>()
                : await ReadSectionHitsAsync(connection, date, query.Query!, cancellationToken);

            items.Add(new JournalHistoryEntrySummary(date, status, mood, 0, 0, hits, attentionReason));
        }

        return new JournalHistorySearchResult(items);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={paths.IndexPath()}");
        connection.DefaultTimeout = 5;
        connection.Open();
        return connection;
    }

    private static async Task<IReadOnlyList<JournalHistoryHit>> ReadSectionHitsAsync(
        SqliteConnection connection,
        JournalDate date,
        string query,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT section_id, title, snippet(section_fts, 3, '[', ']', '...', 12)
            FROM section_fts
            WHERE section_fts MATCH $query AND date = $date
            LIMIT 5;
            """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$date", date.IsoDate);

        var hits = new List<JournalHistoryHit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new JournalHistoryHit("section", reader.GetString(0), null, reader.GetString(1), reader.GetString(2)));
        }

        return hits;
    }

    private static void AddEntryParameters(SqliteCommand command, JournalIndexedEntry entry)
    {
        command.Parameters.AddWithValue("$date", entry.Date.IsoDate);
        command.Parameters.AddWithValue("$monthDay", entry.Date.MonthDay);
        command.Parameters.AddWithValue("$entryPath", entry.EntryPath);
        command.Parameters.AddWithValue("$status", entry.Status);
        command.Parameters.AddWithValue("$mood", (object?)entry.Mood ?? DBNull.Value);
        command.Parameters.AddWithValue("$tagsJson", entry.TagsJson);
        command.Parameters.AddWithValue("$topicsJson", entry.TopicsJson);
        command.Parameters.AddWithValue("$contentHash", entry.ContentHash);
        command.Parameters.AddWithValue("$lastWriteTimeUtc", entry.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$fileSize", entry.FileSize);
        command.Parameters.AddWithValue("$indexedAtUtc", entry.IndexedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$attentionReason", (object?)entry.AttentionReason ?? DBNull.Value);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS journal_meta (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS entries (
            date TEXT PRIMARY KEY,
            month_day TEXT NOT NULL,
            entry_path TEXT NOT NULL,
            status TEXT NOT NULL,
            mood TEXT NULL,
            tags_json TEXT NOT NULL,
            topics_json TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            last_write_time_utc TEXT NOT NULL,
            file_size INTEGER NOT NULL,
            indexed_at_utc TEXT NOT NULL,
            attention_reason TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS entry_sections (
            date TEXT NOT NULL,
            section_id TEXT NOT NULL,
            title TEXT NOT NULL,
            display_order INTEGER NOT NULL,
            content TEXT NOT NULL,
            PRIMARY KEY(date, section_id)
        );

        CREATE TABLE IF NOT EXISTS entry_versions (
            id TEXT PRIMARY KEY,
            date TEXT NOT NULL,
            version_path TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            reason TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            source_entry_path TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS raw_inputs (
            id TEXT PRIMARY KEY,
            date TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            source TEXT NOT NULL,
            text TEXT NOT NULL
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS section_fts USING fts5(
            date UNINDEXED,
            section_id UNINDEXED,
            title,
            content,
            metadata,
            tokenize = 'trigram'
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS raw_input_fts USING fts5(
            raw_input_id UNINDEXED,
            date UNINDEXED,
            source UNINDEXED,
            text,
            tokenize = 'trigram'
        );
        """;
}
```

- [ ] **Step 5: Run schema tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexStoreTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalHistoryModels.cs src/Journal.Infrastructure/Storage/JournalIndexStore.cs tests/Journal.Tests/JournalIndexStoreTests.cs
git commit -m "feat: add journal history index schema"
```

---

## Task 4: Implement Indexing Service And Scan Rules

**Files:**
- Create: `src/Journal.Infrastructure/Storage/JournalIndexingService.cs`
- Modify: `src/Journal.Infrastructure/Storage/JournalIndexStore.cs`
- Create: `tests/Journal.Tests/JournalIndexingServiceTests.cs`

- [ ] **Step 1: Add failing indexing tests**

Create `tests/Journal.Tests/JournalIndexingServiceTests.cs` with tests named:

```csharp
[Fact]
public async Task IndexEntryAsync_IndexesValidJmfSectionsAndMetadata()

[Fact]
public async Task ScanAsync_MarksMissingEntryWithoutDeletingIndexRow()

[Fact]
public async Task ScanAsync_ReindexesExternallyChangedValidMarkdown()

[Fact]
public async Task ScanAsync_MarksInvalidExternallyChangedMarkdownAsAttention()

[Fact]
public async Task SyncRawInputsAsync_IndexesRawInputJsonlIntoFts()
```

Use existing renderer/parser fixtures from `MockAiAndJmfTests` and raw input writing style from `RawInputStore` tests. Each test should:

```csharp
using var workspace = TempWorkspace.Create();
var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
var indexStore = new JournalIndexStore(paths);
var service = new JournalIndexingService(paths, indexStore);
```

- [ ] **Step 2: Run indexing tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexingServiceTests
```

Expected: fail because `JournalIndexingService` and required index-store helpers do not exist.

- [ ] **Step 3: Add index-store helpers**

Add these methods to `JournalIndexStore`:

```csharp
public Task UpsertRawInputAsync(JournalIndexedRawInput rawInput, CancellationToken cancellationToken);

public Task MarkEntryStatusAsync(
    JournalDate date,
    string status,
    string? attentionReason,
    DateTimeOffset indexedAtUtc,
    CancellationToken cancellationToken);

public Task<IReadOnlyDictionary<string, JournalIndexedEntry>> ReadEntryIndexAsync(
    CancellationToken cancellationToken);

public Task UpsertVersionAsync(
    JournalEntryVersion version,
    CancellationToken cancellationToken);

public Task<JournalHistoryEntrySummary?> ReadSummaryAsync(
    JournalDate date,
    CancellationToken cancellationToken);
```

`UpsertRawInputAsync` must write both `raw_inputs` and `raw_input_fts`.

- [ ] **Step 4: Implement `JournalIndexingService`**

Create `src/Journal.Infrastructure/Storage/JournalIndexingService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Infrastructure.Storage;

public sealed class JournalIndexingService(LocalJournalPaths paths, JournalIndexStore indexStore)
{
    public async Task IndexEntryAsync(
        JournalDate date,
        string markdown,
        string entryPath,
        string status,
        DateTimeOffset indexedAtUtc,
        CancellationToken cancellationToken)
    {
        await indexStore.EnsureReadyAsync(cancellationToken);

        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validation = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        var fileInfo = File.Exists(entryPath) ? new FileInfo(entryPath) : null;

        if (!validation.IsValid)
        {
            await indexStore.MarkEntryStatusAsync(date, "attention", "invalid_jmf", indexedAtUtc, cancellationToken);
            return;
        }

        var entry = new JournalIndexedEntry(
            date,
            entryPath,
            status,
            parseResult.Document.Metadata.Mood,
            JsonSerializer.Serialize(parseResult.Document.Metadata.Tags),
            JsonSerializer.Serialize(parseResult.Document.Metadata.Topics),
            ComputeSha256(markdown),
            fileInfo?.LastWriteTimeUtc is { } lastWriteTimeUtc
                ? new DateTimeOffset(lastWriteTimeUtc, TimeSpan.Zero)
                : indexedAtUtc.ToUniversalTime(),
            fileInfo?.Length ?? Encoding.UTF8.GetByteCount(markdown),
            indexedAtUtc.ToUniversalTime(),
            null);

        var sections = parseResult.Document.Sections
            .Select((section, index) => new JournalIndexedSection(date, section.Id, section.Title, index * 10, section.Content))
            .ToArray();

        await indexStore.UpsertEntryAsync(entry, sections, cancellationToken);
    }

    public async Task ScanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await indexStore.EnsureReadyAsync(cancellationToken);

        foreach (var entryPath in Directory.Exists(Path.Combine(Path.GetDirectoryName(paths.EntryPath(JournalDate.From(DateOnly.FromDateTime(now.DateTime)))!)!, ".."))
            ? Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(paths.EntryPath(JournalDate.From(DateOnly.FromDateTime(now.DateTime)))!)!, ".."), "*.md", SearchOption.AllDirectories)
            : Array.Empty<string>())
        {
            var fileName = Path.GetFileNameWithoutExtension(entryPath);
            if (DateOnly.TryParse(fileName, out var parsedDate))
            {
                var date = JournalDate.From(parsedDate);
                var markdown = await File.ReadAllTextAsync(entryPath, Encoding.UTF8, cancellationToken);
                await IndexEntryAsync(date, markdown, entryPath, "processed", now, cancellationToken);
            }
        }

        var indexed = await indexStore.ReadEntryIndexAsync(cancellationToken);
        foreach (var entry in indexed.Values)
        {
            if (!File.Exists(entry.EntryPath))
            {
                await indexStore.MarkEntryStatusAsync(entry.Date, "missing", "entry_file_missing", now, cancellationToken);
            }
        }
    }

    public async Task SyncRawInputsAsync(
        JournalDate date,
        IReadOnlyList<RawInput> rawInputs,
        CancellationToken cancellationToken)
    {
        await indexStore.EnsureReadyAsync(cancellationToken);
        foreach (var rawInput in rawInputs)
        {
            await indexStore.UpsertRawInputAsync(
                new JournalIndexedRawInput(rawInput.Id, date, rawInput.CreatedAt, rawInput.Source, rawInput.Text),
                cancellationToken);
        }
    }

    public async Task SyncVersionAsync(JournalEntryVersion version, CancellationToken cancellationToken)
    {
        await indexStore.EnsureReadyAsync(cancellationToken);
        await indexStore.UpsertVersionAsync(version, cancellationToken);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
```

During implementation, replace the `ScanAsync` entries-root expression with a clean `LocalJournalPaths.EntryRootDirectory()` helper if the expression becomes hard to read. Add a focused path test if that helper is introduced.

- [ ] **Step 5: Run indexing tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalIndexingServiceTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Infrastructure/Storage/JournalIndexStore.cs src/Journal.Infrastructure/Storage/JournalIndexingService.cs tests/Journal.Tests/JournalIndexingServiceTests.cs
git commit -m "feat: index journal entries and raw inputs"
```

---

## Task 5: Route Formal Entry Writes Through Snapshot Pipeline

**Files:**
- Create: `src/Journal.Infrastructure/Storage/EntryWritePipeline.cs`
- Modify: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Create: `tests/Journal.Tests/EntryWritePipelineTests.cs`
- Modify: `tests/Journal.Tests/TodayJournalServiceTests.cs`

- [ ] **Step 1: Add failing pipeline tests**

Create `tests/Journal.Tests/EntryWritePipelineTests.cs` with tests:

```csharp
[Fact]
public async Task WriteFormalEntryAsync_FirstWriteDoesNotCreateSnapshot()

[Fact]
public async Task WriteFormalEntryAsync_ExistingEntryCreatesSnapshotBeforeOverwrite()

[Fact]
public async Task WriteFormalEntryAsync_WhenSnapshotFails_DoesNotOverwriteExistingEntry()

[Fact]
public async Task WriteFormalEntryAsync_WhenIndexFails_KeepsFormalMarkdownAndReportsWarning()
```

For snapshot-failure test, register a fake `IJournalVersionStore`:

```csharp
private sealed class ThrowingVersionStore : IJournalVersionStore
{
    public Task<JournalEntryVersion> CreateSnapshotAsync(
        JournalDate date,
        string markdown,
        string sourceEntryPath,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
        throw new IOException("snapshot failed");

    public Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(JournalDate date, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JournalEntryVersion>>(Array.Empty<JournalEntryVersion>());

    public Task<(JournalEntryVersion Version, string Markdown)?> ReadAsync(JournalDate date, string versionId, CancellationToken cancellationToken) =>
        Task.FromResult<(JournalEntryVersion Version, string Markdown)?>(null);
}
```

- [ ] **Step 2: Run pipeline tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter EntryWritePipelineTests
```

Expected: fail because `EntryWritePipeline` does not exist.

- [ ] **Step 3: Implement pipeline result and service**

Create `src/Journal.Infrastructure/Storage/EntryWritePipeline.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed record EntryWriteResult(
    JournalStatus Status,
    JournalEntryVersion? Snapshot,
    string? IndexWarning);

public sealed class EntryWritePipeline(
    EntryStore entryStore,
    IJournalVersionStore versionStore,
    JournalIndexingService indexingService,
    LocalJournalPaths paths)
{
    public async Task<EntryWriteResult> WriteFormalEntryAsync(
        JournalDate date,
        string markdown,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var existing = await entryStore.ReadAsync(date, cancellationToken);
        JournalEntryVersion? snapshot = null;

        if (existing is not null)
        {
            snapshot = await versionStore.CreateSnapshotAsync(
                date,
                existing.Markdown,
                existing.Path,
                reason,
                now,
                cancellationToken);
        }

        await entryStore.WriteAsync(date, markdown, now, cancellationToken);

        string? indexWarning = null;
        try
        {
            await indexingService.IndexEntryAsync(
                date,
                markdown,
                paths.EntryPath(date),
                existing is null ? "processed" : "updated",
                now,
                cancellationToken);

            if (snapshot is not null)
            {
                await indexingService.SyncVersionAsync(snapshot, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            indexWarning = exception.Message;
        }

        return new EntryWriteResult(
            existing is null ? JournalStatus.Processed : JournalStatus.Updated,
            snapshot,
            indexWarning);
    }
}
```

- [ ] **Step 4: Modify Today service confirm path**

Change `TodayJournalService` constructor to accept `EntryWritePipeline entryWritePipeline` and store it in a field.

Replace direct write in `ConfirmDraftAsync`:

```csharp
var now = _clock.Now;
var result = await _entryWritePipeline.WriteFormalEntryAsync(
    date,
    draft.Markdown,
    now,
    "confirm-draft",
    cancellationToken);

await _draftStore.WriteAsync(draft with { Status = result.Status, UpdatedAt = now }, cancellationToken);

return await BuildStateAsync(date, result.Status, cancellationToken);
```

- [ ] **Step 5: Register services**

Add in `src/Journal.Api/Program.cs`:

```csharp
builder.Services.AddSingleton<IJournalVersionStore, JournalVersionStore>();
builder.Services.AddSingleton<JournalIndexStore>();
builder.Services.AddSingleton<JournalIndexingService>();
builder.Services.AddSingleton<EntryWritePipeline>();
```

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "EntryWritePipelineTests|TodayJournalServiceTests|TodayJournalEndpointTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Infrastructure/Storage/EntryWritePipeline.cs src/Journal.Infrastructure/Today/TodayJournalService.cs src/Journal.Api/Program.cs tests/Journal.Tests/EntryWritePipelineTests.cs tests/Journal.Tests/TodayJournalServiceTests.cs
git commit -m "feat: snapshot formal entry writes"
```

---

## Task 6: Add History Service And API Endpoints

**Files:**
- Create: `src/Journal.Infrastructure/Today/JournalHistoryService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Create: `tests/Journal.Tests/JournalHistoryServiceTests.cs`
- Modify: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Add failing service tests**

Create `tests/Journal.Tests/JournalHistoryServiceTests.cs` with tests:

```csharp
[Fact]
public async Task SearchAsync_ScansBeforeReturningResults()

[Fact]
public async Task GetEntryAsync_ReturnsMetadataSectionsAndVersions()

[Fact]
public async Task RestoreVersionAsDraftAsync_WritesReviewingDraftOnly()
```

The restore test must assert:

```csharp
Assert.True(File.Exists(paths.DraftPath(date)));
Assert.False(File.ReadAllText(paths.EntryPath(date)).Contains("restored version", StringComparison.Ordinal));
```

- [ ] **Step 2: Add failing endpoint tests**

Add to `tests/Journal.Tests/TodayJournalEndpointTests.cs`:

```csharp
[Fact]
public async Task GetJournalHistory_ReturnsSearchResults()

[Fact]
public async Task GetJournalHistoryDate_ReturnsEntryDetail()

[Fact]
public async Task GetJournalHistoryVersions_ReturnsVersionList()

[Fact]
public async Task PostJournalHistoryVersionRestoreDraft_WritesDraftAndReturnsTodayEditor()
```

- [ ] **Step 3: Run service and endpoint tests and verify they fail**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHistoryServiceTests|TodayJournalEndpointTests"
```

Expected: fail because history service and endpoints do not exist.

- [ ] **Step 4: Implement `JournalHistoryService`**

Create `src/Journal.Infrastructure/Today/JournalHistoryService.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed record JournalHistoryEntryDetail(
    JournalDate Date,
    string Status,
    string? AttentionReason,
    string? Markdown,
    IReadOnlyList<JmfSection> Sections,
    IReadOnlyList<JournalEntryVersion> Versions);

public sealed class JournalHistoryService(
    JournalIndexStore indexStore,
    JournalIndexingService indexingService,
    IJournalVersionStore versionStore,
    DraftStore draftStore,
    TodayJournalService todayService,
    IJournalClock clock)
{
    public async Task<JournalHistorySearchResult> SearchAsync(
        JournalHistoryQuery query,
        CancellationToken cancellationToken)
    {
        await indexingService.ScanAsync(clock.Now, cancellationToken);
        return await indexStore.SearchAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<JournalEntryVersion>> ReadVersionsAsync(
        JournalDate date,
        CancellationToken cancellationToken) =>
        await versionStore.ReadByDateAsync(date, cancellationToken);

    public async Task<(JournalEntryVersion Version, string Markdown)?> ReadVersionAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken) =>
        await versionStore.ReadAsync(date, versionId, cancellationToken);

    public async Task<TodayEditorState> RestoreVersionAsDraftAsync(
        JournalDate date,
        string versionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await versionStore.ReadAsync(date, versionId, cancellationToken)
            ?? throw new InvalidOperationException("version was not found.");

        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            snapshot.Markdown,
            Array.Empty<string>(),
            Array.Empty<string>(),
            clock.Now);

        await draftStore.WriteAsync(draft, cancellationToken);
        return await todayService.GetTodayEditorAsync(cancellationToken);
    }
}
```

During implementation, add missing `using Journal.Infrastructure.Jmf;` if `JmfSection` is not in scope, or move `JournalHistoryEntryDetail` to `JournalHistoryModels.cs` if API serialization needs the record in the domain layer.

- [ ] **Step 5: Register service and map endpoints**

Add registration:

```csharp
builder.Services.AddSingleton<JournalHistoryService>();
```

Add endpoints to `Program.cs`:

```csharp
app.MapGet("/journal/history", async Task<IResult> (
    string? query,
    string? status,
    string? from,
    string? to,
    int? limit,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    var request = new JournalHistoryQuery(
        query,
        string.IsNullOrWhiteSpace(status) ? null : status,
        DateOnly.TryParse(from, out var fromDate) ? fromDate : null,
        DateOnly.TryParse(to, out var toDate) ? toDate : null,
        limit.GetValueOrDefault(50));

    return Results.Ok(await service.SearchAsync(request, cancellationToken));
});

app.MapGet("/journal/history/{date}/versions", async Task<IResult> (
    string date,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    return Results.Ok(await service.ReadVersionsAsync(journalDate, cancellationToken));
});

app.MapGet("/journal/history/{date}/versions/{versionId}", async Task<IResult> (
    string date,
    string versionId,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    var version = await service.ReadVersionAsync(journalDate, versionId, cancellationToken);
    return version is null
        ? Results.NotFound(new { error = "version was not found" })
        : Results.Ok(version);
});

app.MapPost("/journal/history/{date}/versions/{versionId}/restore-draft", async Task<IResult> (
    string date,
    string versionId,
    JournalHistoryService service,
    CancellationToken cancellationToken) =>
{
    if (!TryParseJournalDate(date, out var journalDate))
    {
        return Results.BadRequest(new { error = "date must use yyyy-MM-dd" });
    }

    try
    {
        return Results.Ok(await service.RestoreVersionAsDraftAsync(journalDate, versionId, cancellationToken));
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/journal/index/scan", async (
    JournalIndexingService service,
    IJournalClock clock,
    CancellationToken cancellationToken) =>
{
    await service.ScanAsync(clock.Now, cancellationToken);
    return Results.Ok(new { status = "ok" });
});
```

- [ ] **Step 6: Run service and endpoint tests**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHistoryServiceTests|TodayJournalEndpointTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Infrastructure/Today/JournalHistoryService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalHistoryServiceTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose journal history APIs"
```

---

## Task 7: Build History Workbench UI

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Create: `apps/desktop/src/HistoryWorkbench.tsx`
- Create: `apps/desktop/src/HistoryWorkbench.test.tsx`
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`
- Modify: `apps/desktop/src/styles.css`

- [ ] **Step 1: Add frontend API types and tests**

Add to `apps/desktop/src/api.ts`:

```ts
export type JournalHistoryHit = {
  sourceType: "section" | "raw-input";
  sectionId: string | null;
  rawInputId: string | null;
  title: string;
  snippet: string;
};

export type JournalHistoryEntrySummary = {
  date: JournalDate;
  status: JournalStatus | "missing";
  mood: string | null;
  rawInputCount: number;
  versionCount: number;
  hits: JournalHistoryHit[];
  attentionReason: string | null;
};

export type JournalHistorySearchResult = {
  items: JournalHistoryEntrySummary[];
};

export type JournalEntryVersion = {
  id: string;
  date: JournalDate;
  createdAt: string;
  reason: string;
  sourceEntryPath: string;
  markdownPath: string;
  metaPath: string;
  contentHash: string;
};

export type JournalVersionDetail = {
  version: JournalEntryVersion;
  markdown: string;
};
```

Add API functions:

```ts
export function getJournalHistory(params: {
  query?: string;
  status?: string;
  limit?: number;
}): Promise<JournalHistorySearchResult> {
  const search = new URLSearchParams();
  if (params.query) search.set("query", params.query);
  if (params.status) search.set("status", params.status);
  if (params.limit) search.set("limit", String(params.limit));
  const suffix = search.toString();
  return requestJson<JournalHistorySearchResult>(`/journal/history${suffix ? `?${suffix}` : ""}`);
}

export function getJournalHistoryVersions(date: string): Promise<JournalEntryVersion[]> {
  return requestJson<JournalEntryVersion[]>(`/journal/history/${encodeURIComponent(date)}/versions`);
}

export function getJournalHistoryVersion(date: string, versionId: string): Promise<JournalVersionDetail> {
  return requestJson<JournalVersionDetail>(
    `/journal/history/${encodeURIComponent(date)}/versions/${encodeURIComponent(versionId)}`
  );
}

export function restoreJournalHistoryVersionDraft(date: string, versionId: string): Promise<TodayEditorState> {
  return requestJson<TodayEditorState>(
    `/journal/history/${encodeURIComponent(date)}/versions/${encodeURIComponent(versionId)}/restore-draft`,
    { method: "POST" }
  );
}
```

- [ ] **Step 2: Add failing workbench tests**

Create `apps/desktop/src/HistoryWorkbench.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { HistoryWorkbench } from "./HistoryWorkbench";

describe("HistoryWorkbench", () => {
  it("renders search results and selects an entry", async () => {
    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[{
          date: { value: "2026-05-13", year: "2026", month: "05", isoDate: "2026-05-13", monthDay: "05-13", markdownFileName: "2026-05-13.md" },
          status: "processed",
          mood: "平静",
          rawInputCount: 2,
          versionCount: 1,
          attentionReason: null,
          hits: [{ sourceType: "section", sectionId: "today-focus", rawInputId: null, title: "今天想推进", snippet: "测试新[整理]的接口" }]
        }]}
        selectedDate="2026-05-13"
        versions={[]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={vi.fn()}
      />
    );

    expect(screen.getByText("历史与版本")).toBeInTheDocument();
    expect(screen.getByText("2026-05-13")).toBeInTheDocument();
    expect(screen.getByText(/测试新/)).toBeInTheDocument();
  });

  it("requests restore for selected version", async () => {
    const onRestoreVersion = vi.fn();
    const user = userEvent.setup();

    render(
      <HistoryWorkbench
        isBusy={false}
        query=""
        status=""
        entries={[]}
        selectedDate="2026-05-13"
        versions={[{
          id: "version-2026-05-13T07-11-14+08-00",
          date: { value: "2026-05-13", year: "2026", month: "05", isoDate: "2026-05-13", monthDay: "05-13", markdownFileName: "2026-05-13.md" },
          createdAt: "2026-05-13T07:11:14+08:00",
          reason: "confirm-draft",
          sourceEntryPath: "entries/2026/05/2026-05-13.md",
          markdownPath: ".journal/versions/2026/05/2026-05-13/version.md",
          metaPath: ".journal/versions/2026/05/2026-05-13/version.meta.json",
          contentHash: "sha256:test"
        }]}
        error=""
        onBack={vi.fn()}
        onQueryChange={vi.fn()}
        onStatusChange={vi.fn()}
        onSelectDate={vi.fn()}
        onRefresh={vi.fn()}
        onRestoreVersion={onRestoreVersion}
      />
    );

    await user.click(screen.getByRole("button", { name: "恢复为草稿" }));

    expect(onRestoreVersion).toHaveBeenCalledWith("version-2026-05-13T07-11-14+08-00");
  });
});
```

- [ ] **Step 3: Run frontend tests and verify they fail**

Run:

```powershell
npm test --prefix apps/desktop -- HistoryWorkbench.test.tsx
```

Expected: fail because `HistoryWorkbench` does not exist.

- [ ] **Step 4: Implement `HistoryWorkbench`**

Create `apps/desktop/src/HistoryWorkbench.tsx`:

```tsx
import { ArrowLeft, RefreshCw, RotateCcw, Search } from "lucide-react";
import type { JournalEntryVersion, JournalHistoryEntrySummary } from "./api";

type HistoryWorkbenchProps = {
  isBusy: boolean;
  query: string;
  status: string;
  entries: JournalHistoryEntrySummary[];
  selectedDate: string;
  versions: JournalEntryVersion[];
  error: string;
  onBack: () => void;
  onQueryChange: (value: string) => void;
  onStatusChange: (value: string) => void;
  onSelectDate: (date: string) => void;
  onRefresh: () => void;
  onRestoreVersion: (versionId: string) => void;
};

const statusOptions = [
  { value: "", label: "全部" },
  { value: "processed", label: "已保存" },
  { value: "updated", label: "已更新" },
  { value: "attention", label: "需处理" },
  { value: "missing", label: "缺失" }
];

export function HistoryWorkbench({
  isBusy,
  query,
  status,
  entries,
  selectedDate,
  versions,
  error,
  onBack,
  onQueryChange,
  onStatusChange,
  onSelectDate,
  onRefresh,
  onRestoreVersion
}: HistoryWorkbenchProps) {
  const selected = entries.find(entry => entry.date.isoDate === selectedDate) ?? entries[0] ?? null;

  return (
    <>
      <aside className="context-rail history-rail" aria-label="历史搜索">
        <div className="rail-block">
          <label className="history-search">
            <Search size={15} aria-hidden="true" />
            <input
              aria-label="搜索历史日记"
              value={query}
              onChange={event => onQueryChange(event.target.value)}
              placeholder="搜索日记或原始材料"
            />
          </label>
          <div className="history-filter" aria-label="状态筛选">
            {statusOptions.map(option => (
              <button
                key={option.value || "all"}
                type="button"
                className={status === option.value ? "active" : ""}
                onClick={() => onStatusChange(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>

        <div className="history-result-list">
          {entries.map(entry => (
            <button
              key={entry.date.isoDate}
              type="button"
              className={`history-result ${selected?.date.isoDate === entry.date.isoDate ? "active" : ""}`}
              onClick={() => onSelectDate(entry.date.isoDate)}
            >
              <strong>{entry.date.isoDate}</strong>
              <span>{entry.status}</span>
              {entry.hits[0] ? <p>{entry.hits[0].snippet}</p> : null}
            </button>
          ))}
        </div>
      </aside>

      <section className="journal-stage history-stage" aria-label="历史日记预览">
        <div className="stage-toolbar">
          <button type="button" className="secondary-action secondary" onClick={onBack}>
            <ArrowLeft size={15} aria-hidden="true" />
            返回今日
          </button>
          <button type="button" className="secondary-action secondary" onClick={onRefresh} disabled={isBusy}>
            <RefreshCw size={15} aria-hidden="true" />
            刷新索引
          </button>
        </div>

        <article className="history-document">
          <p className="paper-eyebrow">Local History</p>
          <h2>历史与版本</h2>
          {error ? <p className="attention-copy">{error}</p> : null}
          {selected ? (
            <>
              <div className="history-document-head">
                <strong>{selected.date.isoDate}</strong>
                <span>{selected.status}</span>
              </div>
              {selected.attentionReason ? <p className="attention-copy">{selected.attentionReason}</p> : null}
              <div className="history-hit-list">
                {selected.hits.map(hit => (
                  <section key={`${hit.sourceType}-${hit.sectionId ?? hit.rawInputId ?? hit.title}`} className="history-hit">
                    <span>{hit.sourceType === "raw-input" ? "原始材料" : hit.title}</span>
                    <p>{hit.snippet}</p>
                  </section>
                ))}
              </div>
            </>
          ) : (
            <p className="muted">还没有可显示的历史日记。</p>
          )}
        </article>
      </section>

      <aside className="assistant-panel today-assistant history-inspector" aria-label="版本详情">
        <div className="assistant-head">
          <div>
            <p className="assistant-eyebrow">Versions</p>
            <h2>版本快照</h2>
          </div>
        </div>
        <div className="assistant-body">
          {versions.length === 0 ? (
            <section className="assistant-card">
              <p className="muted">这一天还没有覆盖前快照。</p>
            </section>
          ) : versions.map(version => (
            <section className="assistant-card" key={version.id}>
              <div className="assistant-card-head">
                <h3>{new Date(version.createdAt).toLocaleString("zh-CN")}</h3>
                <span>{version.reason}</span>
              </div>
              <p>{version.contentHash}</p>
              <button type="button" className="assistant-inline-action" onClick={() => onRestoreVersion(version.id)}>
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

- [ ] **Step 5: Wire `App.tsx` state and assistant entry**

Modify imports:

```ts
import {
  getJournalHistory,
  getJournalHistoryVersions,
  restoreJournalHistoryVersionDraft,
  type JournalEntryVersion,
  type JournalHistoryEntrySummary
} from "./api";
import { HistoryWorkbench } from "./HistoryWorkbench";
```

Extend state:

```ts
const [workspaceMode, setWorkspaceMode] = useState<"today" | "audit" | "history">("today");
const [historyQuery, setHistoryQuery] = useState("");
const [historyStatus, setHistoryStatus] = useState("");
const [historyEntries, setHistoryEntries] = useState<JournalHistoryEntrySummary[]>([]);
const [historySelectedDate, setHistorySelectedDate] = useState("");
const [historyVersions, setHistoryVersions] = useState<JournalEntryVersion[]>([]);
const [historyError, setHistoryError] = useState("");
```

Add handlers:

```ts
async function openHistoryWorkbench() {
  resetPendingRegenerateDraft();
  setWorkspaceMode("history");
  await refreshHistory(historyQuery, historyStatus);
}

async function refreshHistory(query = historyQuery, status = historyStatus) {
  try {
    const result = await getJournalHistory({ query, status, limit: 50 });
    setHistoryEntries(result.items);
    const selectedDate = historySelectedDate || result.items[0]?.date.isoDate || "";
    setHistorySelectedDate(selectedDate);
    setHistoryError("");
    if (selectedDate) {
      setHistoryVersions(await getJournalHistoryVersions(selectedDate));
    }
  } catch (caught) {
    setHistoryError(getErrorMessage(caught));
  }
}

async function handleHistorySelectDate(date: string) {
  setHistorySelectedDate(date);
  setHistoryVersions(await getJournalHistoryVersions(date));
}

async function handleRestoreHistoryVersion(versionId: string) {
  if (!historySelectedDate) {
    return;
  }

  const restored = await restoreJournalHistoryVersionDraft(historySelectedDate, versionId);
  setEditor(restored);
  setWorkspaceMode("today");
  setWorkbenchView("assistant");
}
```

Add assistant card near the current `正式文件` card:

```tsx
<section className="assistant-card path-panel">
  <div className="assistant-card-head">
    <h3>历史与版本</h3>
    <button type="button" className="assistant-inline-action" onClick={openHistoryWorkbench}>
      查看历史
    </button>
  </div>
  <p>搜索正式日记、查看覆盖前快照，并把旧版本恢复成待确认草稿。</p>
</section>
```

Render history in the workspace branch:

```tsx
{workspaceMode === "history" ? (
  <HistoryWorkbench
    isBusy={isBusy}
    query={historyQuery}
    status={historyStatus}
    entries={historyEntries}
    selectedDate={historySelectedDate}
    versions={historyVersions}
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
    onRestoreVersion={versionId => void handleRestoreHistoryVersion(versionId)}
  />
) : null}
```

- [ ] **Step 6: Add CSS**

Add styles to `apps/desktop/src/styles.css`:

```css
.history-rail,
.history-inspector {
  min-width: 0;
}

.history-search {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  padding: 8px 10px;
  background: var(--surface);
}

.history-search input {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-main);
}

.history-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 10px;
}

.history-filter button,
.history-result {
  border: 1px solid var(--border-subtle);
  background: var(--surface);
  color: var(--text-main);
  border-radius: 8px;
}

.history-filter button {
  padding: 6px 9px;
}

.history-filter button.active,
.history-result.active {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.history-result-list {
  display: grid;
  gap: 8px;
  overflow: auto;
}

.history-result {
  display: grid;
  gap: 4px;
  padding: 10px;
  text-align: left;
}

.history-result p,
.history-hit p {
  margin: 0;
  color: var(--text-muted);
}

.history-document {
  display: grid;
  gap: 16px;
  max-width: 880px;
  margin: 0 auto;
  padding: 24px;
}

.history-document-head,
.history-hit {
  border-bottom: 1px solid var(--border-subtle);
  padding-bottom: 12px;
}

.history-hit-list {
  display: grid;
  gap: 14px;
}
```

- [ ] **Step 7: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop -- HistoryWorkbench.test.tsx
npm test --prefix apps/desktop -- App.test.tsx
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/HistoryWorkbench.tsx apps/desktop/src/HistoryWorkbench.test.tsx apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx apps/desktop/src/styles.css
git commit -m "feat: add history workbench"
```

---

## Task 8: Full Verification, Docs, And Asset Compounding

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`
- Create: `docs/superpowers/archives/2026-05-13-phase-4a-local-history-search.md`
- Modify: `docs/superpowers/archives/INDEX.md`
- Create/modify: `docs/superpowers/problems/*` or `docs/superpowers/inbox/*` when implementation uncovers reusable failure modes or uncertain but useful signals.

- [ ] **Step 1: Run full backend tests**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: PASS.

- [ ] **Step 2: Run full frontend tests and build**

Run:

```powershell
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected: PASS for tests and successful Vite build.

- [ ] **Step 3: Update docs**

Update `README.md` and `AGENTS.md` delivered scope from Phase 5/6 wording to include Phase 4A:

```text
Phase 4A adds local history reliability:
- formal entry overwrite snapshots under .journal/versions
- rebuildable SQLite history index under .journal/index
- FTS5 trigram search across entry sections and raw inputs
- history workbench with restore-version-to-draft
```

Keep the product invariants:

```text
SQLite remains rebuildable cache. Markdown/raw-input/version files remain source material.
Restoring a version writes a reviewing draft only and never directly overwrites entries/.
```

- [ ] **Step 4: Archive completed feature**

Create `docs/superpowers/archives/2026-05-13-phase-4a-local-history-search.md`:

```markdown
# Phase 4A Local History Search

> Date: 2026-05-13
> Status: Completed

## Delivered

- Formal entry overwrite snapshot pipeline.
- Version store under `.journal/versions`.
- SQLite index under `.journal/index/journal.db`.
- FTS5 trigram search across entry sections and raw inputs.
- History API and index scan API.
- History workbench with search, status filter, version list, and restore-to-draft.

## Verification

- `dotnet test Journal.slnx`
- `npm test --prefix apps/desktop`
- `npm run build --prefix apps/desktop`

## Safety Boundaries

- Snapshot failure blocks formal overwrite.
- Index failure does not delete or roll back Markdown.
- Restore writes reviewing draft only.
```

Update `docs/superpowers/archives/INDEX.md` with the new archive entry.

- [ ] **Step 5: Archive development problems from worker handoffs**

The parent agent must review every worker handoff and the parent integration log before final close-out. For each non-trivial issue, classify it with the asset-compounding gate:

```text
assetization_decision: none | inbox | update-existing | new-problem
reason: one concrete sentence
next_step: none | write-superpowers-problem
```

Write a problem asset when the issue has stable recognition clues and a root cause. Use inbox when the signal is useful but not mature enough for a formal problem.

Problem asset minimum content:

```markdown
# <Problem Title>

> Date: 2026-05-13
> Status: Active

## Symptoms

- <exact error, failing test, rejected operation, or user-visible behavior>

## Trigger

- <what action or code path exposed it>

## Root Cause

- <confirmed cause, not speculation>

## Fix

- <files changed and behavior changed>

## Verification

- `<command that proved the fix>`

## Recognition Clues

- <phrases, exception names, endpoint names, or UI states future agents would search for>
```

Update `docs/superpowers/problems/INDEX.md` or `docs/superpowers/inbox/INDEX.md` for every written or updated asset. If no development problem deserves an asset, add a short note to the final delivery summary saying the parent agent reviewed worker handoffs and chose `none`.

- [ ] **Step 6: Run asset index checks**

If the Superpowers Asset Compounding scripts are available, run:

```powershell
python C:\Users\10062\.codex\plugins\cache\local-home\superpowers-asset-compounding\0.1.0\skills\archive-superpowers-feature\scripts\validate_archive_asset.py docs/superpowers/archives/2026-05-13-phase-4a-local-history-search.md
python C:\Users\10062\.codex\plugins\cache\local-home\superpowers-asset-compounding\0.1.0\skills\write-superpowers-problem\scripts\check_indexes.py docs/superpowers
```

Expected: archive validation passes and indexes contain no orphan/dead-link errors.

- [ ] **Step 7: Commit final docs**

```powershell
git add README.md AGENTS.md docs/superpowers/archives docs/superpowers/problems docs/superpowers/inbox
git commit -m "docs: archive phase 4a history search"
```

- [ ] **Step 8: Final status**

Run:

```powershell
git status --short --branch
```

Expected: branch is clean and ahead of `origin/main` by the implementation commits.

## Self-Review Checklist

- [ ] Spec 4.1 snapshot strategy is covered by Tasks 2 and 5.
- [ ] Spec 4.2 version path is covered by Tasks 1 and 2.
- [ ] Spec 4.3 external change detection is covered by Task 4.
- [ ] Spec 4.4 SQLite segmented schema is covered by Task 3.
- [ ] Spec 4.5 FTS5 trigram and grouped search are covered by Tasks 3, 4, and 6.
- [ ] Spec 4.6 no Today startup full scan is covered by Task 6 service boundary.
- [ ] Spec 5 actual UI placement is covered by Task 7: assistant entry plus workspace mode.
- [ ] Spec 6 APIs are covered by Task 6.
- [ ] Spec 8 snapshot/index/restore errors are covered by Tasks 5 and 6.
- [ ] Full verification and docs are covered by Task 8.
