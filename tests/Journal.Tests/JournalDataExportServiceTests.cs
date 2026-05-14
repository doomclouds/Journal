using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalDataExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesManifestAndSourceMaterialWithoutIndexOrFullApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var entryPath = Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-14.md");

        LocalJournalPaths.EnsureParentDirectory(entryPath);
        await File.WriteAllTextAsync(entryPath, "# entry", Encoding.UTF8);
        LocalJournalPaths.EnsureParentDirectory(paths.AiSettingsPath());
        await File.WriteAllTextAsync(
            paths.AiSettingsPath(),
            """{"providers":[{"id":"openai","apiKey":"sk-secret"}]}""",
            Encoding.UTF8);
        Directory.CreateDirectory(paths.IndexDirectory());
        await File.WriteAllTextAsync(paths.IndexPath(), "cache", Encoding.UTF8);
        Directory.CreateDirectory(Path.Combine(workspace.Root, ".journal", "logs"));
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Root, ".journal", "logs", "journal.log"),
            "log",
            Encoding.UTF8);
        Directory.CreateDirectory(Path.Combine(workspace.Root, "artifacts", "installer"));
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Root, "artifacts", "installer", "JournalSetup.exe"),
            "installer",
            Encoding.UTF8);

        var service = new JournalDataExportService(paths);

        var result = await service.ExportAsync(Path.Combine(workspace.Root, "export.zip"), CancellationToken.None);

        Assert.True(File.Exists(result.ExportPath));
        using var archive = ZipFile.OpenRead(result.ExportPath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "entries/2026/05/2026-05-14.md");
        Assert.Contains(archive.Entries, entry => entry.FullName == ".journal/settings/ai-providers.safe.json");
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("journal.db", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith(".journal/index/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith(".journal/logs/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in archive.Entries)
        {
            await using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            Assert.DoesNotContain("sk-secret", content, StringComparison.Ordinal);
        }

        var manifestEntry = archive.GetEntry("manifest.json")!;
        await using var manifestStream = manifestEntry.Open();
        using var document = await JsonDocument.ParseAsync(manifestStream);
        var manifest = document.RootElement;
        Assert.Equal("journal-export/v1", manifest.GetProperty("format").GetString());
        Assert.Equal(1, manifest.GetProperty("entryCount").GetInt32());
        Assert.False(manifest.GetProperty("containsFullApiKeys").GetBoolean());
    }

    [Fact]
    public async Task ExportAsync_WhenTargetExists_DoesNotOverwriteOrLeaveTempFile()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var targetPath = Path.Combine(workspace.Root, "export.zip");
        Directory.CreateDirectory(workspace.Root);
        await File.WriteAllTextAsync(targetPath, "existing export", Encoding.UTF8);
        var service = new JournalDataExportService(paths);

        await Assert.ThrowsAsync<IOException>(() => service.ExportAsync(targetPath, CancellationToken.None));

        Assert.Equal("existing export", await File.ReadAllTextAsync(targetPath, Encoding.UTF8));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task ExportAsync_WhenCanceled_DoesNotLeaveFinalZip()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var targetPath = Path.Combine(workspace.Root, "export.zip");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var service = new JournalDataExportService(paths);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExportAsync(targetPath, cancellation.Token));

        Assert.False(File.Exists(targetPath));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-data-export-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            TestWorkspaceCleanup.DeleteDirectory(Root);
        }
    }
}
