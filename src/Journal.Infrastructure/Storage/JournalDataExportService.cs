using System.IO.Compression;
using System.Text.Json;
using Journal.Domain.Application;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalDataExportService(LocalJournalPaths paths)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JournalDataExportManifest CreateManifest() =>
        new(
            "journal-export/v1",
            DateTimeOffset.Now,
            ApplicationBuildInfo.Current.ReleaseVersion,
            ApplicationBuildInfo.Current.BackendVersion,
            ApplicationBuildInfo.Current.FrontendVersion,
            CountFiles(paths.EntryRootDirectory(), "*.md"),
            CountJsonLines(paths.RawInputRootDirectory()),
            CountFiles(paths.VersionRootDirectory(), "*.md"),
            false);

    public async Task<JournalDataExportResult> ExportAsync(string exportPath, CancellationToken cancellationToken)
    {
        LocalJournalPaths.EnsureParentDirectory(exportPath);
        var exportDirectory = Path.GetDirectoryName(exportPath);
        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(exportDirectory) ? "." : exportDirectory,
            $"{Path.GetFileName(exportPath)}.{Guid.NewGuid():N}.tmp");
        var tempCreated = false;

        var manifest = CreateManifest();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                tempCreated = true;
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

                await AddJsonAsync(archive, "manifest.json", manifest, cancellationToken);
                AddDirectory(archive, paths.EntryRootDirectory(), "entries", cancellationToken);
                AddDirectory(archive, paths.RawInputRootDirectory(), ".journal/raw-inputs", cancellationToken);
                AddDirectory(archive, paths.AnniversaryDirectory(), ".journal/anniversaries", cancellationToken);
                AddDirectory(archive, paths.DraftRootDirectory(), ".journal/drafts", cancellationToken);
                AddDirectory(archive, paths.VersionRootDirectory(), ".journal/versions", cancellationToken);
                AddDirectory(archive, paths.AuditRootDirectory(), ".journal/audit", cancellationToken);
                await AddJsonAsync(
                    archive,
                    ".journal/settings/ai-providers.safe.json",
                    new SafeAiSettingsExport(false),
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, exportPath, overwrite: false);
        }
        catch
        {
            if (tempCreated)
            {
                TryDeleteFile(tempPath);
            }

            throw;
        }

        return new JournalDataExportResult(exportPath, manifest);
    }

    private static int CountFiles(string directory, string pattern) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Length
            : 0;

    private static int CountJsonLines(string directory) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.jsonl", SearchOption.AllDirectories)
                .Sum(file => File.ReadLines(file).Count(line => !string.IsNullOrWhiteSpace(line)))
            : 0;

    private static async Task AddJsonAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
    }

    private static void AddDirectory(
        ZipArchive archive,
        string sourceDirectory,
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, file)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(file, $"{archiveRoot}/{relativePath}");
        }
    }

    private sealed record SafeAiSettingsExport(bool ContainsFullApiKeys);

    private static void TryDeleteFile(string path)
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
