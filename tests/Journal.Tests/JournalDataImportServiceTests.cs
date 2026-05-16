using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Journal.Domain.Application;
using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace Journal.Tests;

public sealed class JournalDataImportServiceTests
{
    [Fact]
    public async Task ImportAsync_CreatesBackupAndRestoresEntries()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before import");
        var importedMarkdown = CreateMarkdown(date, "imported entry after import");
        var originalAiSettings = """{"providers":[{"id":"deepseek","apiKey":"sk-existing"}]}""";
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        await WriteTextAsync(paths.AiSettingsPath(), originalAiSettings);
        var packagePath = Path.Combine(workspace.Root, "import.zip");
        await CreatePackageAsync(
            packagePath,
            [
                ("entries/2026/05/2026-05-15.md", importedMarkdown),
                (".journal/settings/ai-providers.safe.json", """{"containsFullApiKeys":false,"source":"package"}""")
            ]);

        var result = await service.ImportAsync(packagePath, CancellationToken.None);

        Assert.Equal("journal-export/v1", result.Manifest.Format);
        Assert.StartsWith(
            Path.Combine(workspace.Root, ".journal", "import-backups"),
            result.BackupDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory, "entries", "2026", "05", "2026-05-15.md")));
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(
            Path.Combine(result.BackupDirectory, "entries", "2026", "05", "2026-05-15.md"),
            Encoding.UTF8));
        Assert.Equal(importedMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.True(File.Exists(paths.IndexPath()));
        Assert.Equal(originalAiSettings, await File.ReadAllTextAsync(paths.AiSettingsPath(), Encoding.UTF8));
        Assert.Equal(originalAiSettings, await File.ReadAllTextAsync(
            Path.Combine(result.BackupDirectory, ".journal", "settings", "ai-providers.json"),
            Encoding.UTF8));
        Assert.False(File.Exists(SafeSettingsPath(paths)));

        var importedResult = await indexStore.SearchAsync(
            new JournalHistoryQuery("imported", null, null, null, null, 20),
            CancellationToken.None);
        Assert.Contains(importedResult.Items, item => item.Date == date);
    }

    [Fact]
    public async Task ImportAsync_WithInvalidManifest_DoesNotModifyCurrentData()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before bad manifest");
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        var packagePath = Path.Combine(workspace.Root, "bad-manifest.zip");
        await CreatePackageAsync(
            packagePath,
            [("entries/2026/05/2026-05-15.md", CreateMarkdown(date, "should not import"))],
            manifest: new JournalDataExportManifest(
                "wrong-format",
                DateTimeOffset.Parse("2026-05-15T08:00:00+08:00"),
                ApplicationInfo.Version,
                ApplicationInfo.Version,
                "0.1.0",
                1,
                0,
                0,
                false));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("manifest", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, ".journal", "import-backups")));
    }

    [Fact]
    public async Task ImportAsync_RestoresAnniversarySourceMaterial()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var packagePath = Path.Combine(workspace.Root, "import.zip");
        Directory.CreateDirectory(workspace.Root);
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
            await using var writer = new StreamWriter(anniversaryStream, Encoding.UTF8);
            await writer.WriteAsync("""{"schema":"journal-anniversaries/v1","items":[]}""");
        }

        await new JournalDataImportService(paths, new JournalIndexingService(paths, new JournalIndexStore(paths)))
            .ImportAsync(packagePath, CancellationToken.None);

        Assert.True(File.Exists(paths.AnniversaryPath()));
    }

    [Fact]
    public async Task ImportAsync_WithZipSlipEntry_DoesNotWriteOutsideRootAndRestoresBackup()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before zip slip");
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        var packagePath = Path.Combine(workspace.Root, "zip-slip.zip");
        var outsidePath = Path.Combine(Path.GetDirectoryName(workspace.Root)!, "escape.md");
        if (File.Exists(outsidePath))
        {
            File.Delete(outsidePath);
        }

        await CreatePackageAsync(
            packagePath,
            [
                ("entries/2026/05/2026-05-15.md", CreateMarkdown(date, "should be rolled back")),
                ("entries/../../escape.md", "escape")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outsidePath));
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
    }

    [Fact]
    public async Task ImportAsync_WithTraversalPackageFailsBeforeModifyingEntryOrAiSettings()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before preflight traversal");
        var originalAiSettings = """{"providers":[{"id":"deepseek","apiKey":"sk-existing"}]}""";
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        await WriteTextAsync(paths.AiSettingsPath(), originalAiSettings);
        var packagePath = Path.Combine(workspace.Root, "preflight-traversal.zip");
        await CreatePackageAsync(
            packagePath,
            [
                ("entries/2026/05/2026-05-15.md", CreateMarkdown(date, "should not import")),
                (".journal/raw-inputs/../../.journal/index/journal.db", "crafted sqlite cache")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.Equal(originalAiSettings, await File.ReadAllTextAsync(paths.AiSettingsPath(), Encoding.UTF8));
        Assert.False(Directory.Exists(Path.Combine(paths.RootDirectory(), ".journal", "import-backups")));
        Assert.False(File.Exists(paths.IndexPath()));
    }

    [Theory]
    [InlineData("README.txt")]
    [InlineData("entries/misc.txt")]
    public async Task ImportAsync_WithNonAllowlistedFileRejectsBeforeModifyingCurrentData(string rejectedEntryName)
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before unknown file");
        var originalAiSettings = """{"providers":[{"id":"deepseek","apiKey":"sk-existing"}]}""";
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        await WriteTextAsync(paths.AiSettingsPath(), originalAiSettings);
        var packagePath = Path.Combine(workspace.Root, "unknown-file.zip");
        await CreatePackageAsync(
            packagePath,
            [
                ("entries/2026/05/2026-05-15.md", CreateMarkdown(date, "should not import")),
                (rejectedEntryName, "unknown file")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("not allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.Equal(originalAiSettings, await File.ReadAllTextAsync(paths.AiSettingsPath(), Encoding.UTF8));
        Assert.False(Directory.Exists(Path.Combine(paths.RootDirectory(), ".journal", "import-backups")));
    }

    [Fact]
    public async Task ImportAsync_WithInRootTraversalToIndexRejectsAndDoesNotWriteIndexMaterial()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before index traversal");
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        var packagePath = Path.Combine(workspace.Root, "index-traversal.zip");
        await CreatePackageAsync(
            packagePath,
            [
                (".journal/raw-inputs/../../.journal/index/journal.db", "crafted sqlite cache")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.False(File.Exists(paths.IndexPath()));
    }

    [Fact]
    public async Task ImportAsync_WithInRootTraversalToFullAiSettingsRejectsAndDoesNotImportApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, _, service) = CreateService(workspace.Root);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before settings traversal");
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        var packagePath = Path.Combine(workspace.Root, "settings-traversal.zip");
        await CreatePackageAsync(
            packagePath,
            [
                ("entries/../.journal/settings/ai-providers.json", """{"apiKey":"sk-full-secret"}""")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.False(File.Exists(paths.AiSettingsPath()));
    }

    [Fact]
    public async Task ImportAsync_WhenRebuildFails_RestoresBackupAndLeavesIndexRebuildable()
    {
        using var workspace = TempWorkspace.Create();
        var (paths, indexStore, service) = CreateService(workspace.Root);
        var indexingService = new JournalIndexingService(paths, indexStore);
        var date = JournalDate.From(new DateOnly(2026, 5, 15));
        var originalMarkdown = CreateMarkdown(date, "original entry before extraction failure");
        var originalAiSettings = """{"providers":[{"id":"deepseek","apiKey":"sk-existing"}]}""";
        await WriteTextAsync(paths.EntryPath(date), originalMarkdown);
        await WriteTextAsync(paths.AiSettingsPath(), originalAiSettings);
        Directory.CreateDirectory(paths.IndexPath());
        var packagePath = Path.Combine(workspace.Root, "file-conflict.zip");
        await CreatePackageAsync(
            packagePath,
            [
                ("entries/2026/05/2026-05-15.md", CreateMarkdown(date, "should be restored"))
            ]);

        await Assert.ThrowsAsync<SqliteException>(() => service.ImportAsync(packagePath, CancellationToken.None));

        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8));
        Assert.Equal(originalAiSettings, await File.ReadAllTextAsync(paths.AiSettingsPath(), Encoding.UTF8));

        await indexingService.RebuildAsync(DateTimeOffset.Parse("2026-05-15T01:00:00+00:00"), CancellationToken.None);
        Assert.True(File.Exists(paths.IndexPath()));
        var restoredResult = await indexStore.SearchAsync(
            new JournalHistoryQuery("original", null, null, null, null, 20),
            CancellationToken.None);
        Assert.Contains(restoredResult.Items, item => item.Date == date);
    }

    private static (LocalJournalPaths Paths, JournalIndexStore IndexStore, JournalDataImportService Service) CreateService(string root)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        var indexStore = new JournalIndexStore(paths);
        var indexingService = new JournalIndexingService(paths, indexStore);
        return (paths, indexStore, new JournalDataImportService(paths, indexingService));
    }

    private static async Task CreatePackageAsync(
        string packagePath,
        IEnumerable<(string Name, string Content)> entries,
        JournalDataExportManifest? manifest = null)
    {
        await using var stream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var manifestEntry = archive.CreateEntry("manifest.json");
        await using (var manifestStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(
                manifestStream,
                manifest ?? new JournalDataExportManifest(
                    "journal-export/v1",
                    DateTimeOffset.Parse("2026-05-15T08:00:00+08:00"),
                    ApplicationInfo.Version,
                    ApplicationInfo.Version,
                    "0.1.0",
                    1,
                    0,
                    0,
                    false),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            await writer.WriteAsync(content);
        }
    }

    private static async Task WriteTextAsync(string path, string text)
    {
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);
    }

    private static string SafeSettingsPath(LocalJournalPaths paths) =>
        Path.Combine(paths.RootDirectory(), ".journal", "settings", "ai-providers.safe.json");

    private static string CreateMarkdown(JournalDate date, string todayFocus)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "reviewing",
            ["#Journal"],
            ["Import"],
            "平静",
            ["import service test raw input"],
            ["yesterday import setup"],
            [todayFocus],
            ["keep validating"]);

        return JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse($"{date.IsoDate}T09:00:00+08:00"));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-data-import-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            TestWorkspaceCleanup.DeleteDirectory(Root);
        }
    }
}
