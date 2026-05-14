using System.Globalization;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalDataImportService(
    LocalJournalPaths paths,
    JournalIndexingService indexingService)
{
    private const string ManifestPath = "manifest.json";
    private const string ExportFormat = "journal-export/v1";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<JournalDataImportResult> ImportAsync(string zipPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Import package was not found.", zipPath);
        }

        using var archive = ZipFile.OpenRead(zipPath);
        var manifest = await ReadManifestAsync(archive, cancellationToken);
        ValidateManifest(manifest);
        PreflightImportEntries(archive, cancellationToken);

        var backupDirectory = CreateBackupDirectory();
        var backupCreated = false;

        try
        {
            Directory.CreateDirectory(paths.RootDirectory());
            BackupSourceMaterial(backupDirectory, cancellationToken);
            backupCreated = true;

            ClearImportTargets(cancellationToken);
            await ExtractSourceMaterialAsync(archive, cancellationToken);

            // Import is conservative: if rebuilding the cache fails, roll back source material too.
            // The SQLite index is rebuildable, but leaving imported sources after a failed API call
            // would make the simple result contract ambiguous for callers.
            await indexingService.RebuildAsync(DateTimeOffset.Now, cancellationToken);
        }
        catch (Exception exception) when (backupCreated)
        {
            RestoreSourceMaterial(backupDirectory, CancellationToken.None);
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return new JournalDataImportResult(backupDirectory, manifest);
    }

    private static async Task<JournalDataExportManifest> ReadManifestAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var manifestEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, ManifestPath, StringComparison.OrdinalIgnoreCase));
        if (manifestEntry is null)
        {
            throw new InvalidOperationException("Import manifest is missing.");
        }

        try
        {
            await using var stream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<JournalDataExportManifest>(
                stream,
                SerializerOptions,
                cancellationToken);
            return manifest ?? throw new InvalidOperationException("Import manifest is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Import manifest is invalid.", exception);
        }
    }

    private static void ValidateManifest(JournalDataExportManifest manifest)
    {
        if (!string.Equals(manifest.Format, ExportFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Import manifest format is invalid.");
        }

        if (manifest.ContainsFullApiKeys)
        {
            throw new InvalidOperationException("Import manifest is invalid.");
        }
    }

    private string CreateBackupDirectory()
    {
        var backupRoot = Path.Combine(paths.RootDirectory(), ".journal", "import-backups");
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (var index = 0; index < 100; index++)
        {
            var suffix = index == 0 ? "" : $"-{index:00}";
            var candidate = Path.Combine(backupRoot, stamp + suffix);
            if (Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch (IOException) when (Directory.Exists(candidate))
            {
            }
        }

        throw new IOException("Could not create a unique import backup directory.");
    }

    private void BackupSourceMaterial(string backupDirectory, CancellationToken cancellationToken)
    {
        CopyDirectory(paths.EntryRootDirectory(), Path.Combine(backupDirectory, "entries"), cancellationToken);
        CopyDirectory(
            paths.RawInputRootDirectory(),
            Path.Combine(backupDirectory, ".journal", "raw-inputs"),
            cancellationToken);
        CopyDirectory(
            paths.DraftRootDirectory(),
            Path.Combine(backupDirectory, ".journal", "drafts"),
            cancellationToken);
        CopyDirectory(
            paths.VersionRootDirectory(),
            Path.Combine(backupDirectory, ".journal", "versions"),
            cancellationToken);
        CopyDirectory(
            paths.AuditRootDirectory(),
            Path.Combine(backupDirectory, ".journal", "audit"),
            cancellationToken);
        CopyFileIfExists(paths.AiSettingsPath(), Path.Combine(backupDirectory, ".journal", "settings", "ai-providers.json"));
        BackupIndexCache(backupDirectory);
    }

    private void RestoreSourceMaterial(string backupDirectory, CancellationToken cancellationToken)
    {
        ClearImportTargets(cancellationToken);
        CopyDirectory(Path.Combine(backupDirectory, "entries"), paths.EntryRootDirectory(), cancellationToken);
        CopyDirectory(
            Path.Combine(backupDirectory, ".journal", "raw-inputs"),
            paths.RawInputRootDirectory(),
            cancellationToken);
        CopyDirectory(
            Path.Combine(backupDirectory, ".journal", "drafts"),
            paths.DraftRootDirectory(),
            cancellationToken);
        CopyDirectory(
            Path.Combine(backupDirectory, ".journal", "versions"),
            paths.VersionRootDirectory(),
            cancellationToken);
        CopyDirectory(
            Path.Combine(backupDirectory, ".journal", "audit"),
            paths.AuditRootDirectory(),
            cancellationToken);
        RestoreOptionalFile(Path.Combine(backupDirectory, ".journal", "settings", "ai-providers.json"), paths.AiSettingsPath());
        RestoreIndexCache(backupDirectory);
    }

    private void ClearImportTargets(CancellationToken cancellationToken)
    {
        DeleteDirectory(paths.EntryRootDirectory(), cancellationToken);
        DeleteDirectory(paths.RawInputRootDirectory(), cancellationToken);
        DeleteDirectory(paths.DraftRootDirectory(), cancellationToken);
        DeleteDirectory(paths.VersionRootDirectory(), cancellationToken);
        DeleteDirectory(paths.AuditRootDirectory(), cancellationToken);
    }

    private async Task ExtractSourceMaterialAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var canonicalName = CanonicalizeArchiveRelativePath(entry.FullName);
            if (!IsAllowedImportEntry(canonicalName))
            {
                continue;
            }

            if (IsDirectoryEntry(entry.FullName))
            {
                continue;
            }

            if (IsSafeSettingsMetadata(canonicalName))
            {
                continue;
            }

            var destinationPath = GetSafeDestinationPath(canonicalName);
            LocalJournalPaths.EnsureParentDirectory(destinationPath);
            await using var source = entry.Open();
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous);
            await source.CopyToAsync(destination, cancellationToken);
        }
    }

    private static void PreflightImportEntries(ZipArchive archive, CancellationToken cancellationToken)
    {
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(entry.FullName, ManifestPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var canonicalName = CanonicalizeArchiveRelativePath(entry.FullName);
            if (IsDirectoryEntry(entry.FullName))
            {
                if (!IsAllowedImportDirectory(canonicalName))
                {
                    throw new InvalidOperationException($"Import entry path is not allowed: {entry.FullName}");
                }

                continue;
            }

            if (!IsAllowedImportEntry(canonicalName))
            {
                throw new InvalidOperationException($"Import entry path is not allowed: {entry.FullName}");
            }
        }
    }

    private string GetSafeDestinationPath(string canonicalName)
    {
        var root = Path.GetFullPath(paths.RootDirectory());
        var destinationPath = Path.GetFullPath(Path.Combine(root, canonicalName.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsUnderRoot(destinationPath, root))
        {
            throw new InvalidOperationException($"Import entry path escapes data root: {canonicalName}");
        }

        return destinationPath;
    }

    private void BackupIndexCache(string backupDirectory)
    {
        var backupIndexDirectory = Path.Combine(backupDirectory, ".journal", "index");
        CopyFileIfExists(paths.IndexPath(), Path.Combine(backupIndexDirectory, "journal.db"));
        CopyFileIfExists(paths.IndexPath() + "-wal", Path.Combine(backupIndexDirectory, "journal.db-wal"));
        CopyFileIfExists(paths.IndexPath() + "-shm", Path.Combine(backupIndexDirectory, "journal.db-shm"));
    }

    private void RestoreIndexCache(string backupDirectory)
    {
        ClearIndexCachePath(paths.IndexPath());
        ClearIndexCachePath(paths.IndexPath() + "-wal");
        ClearIndexCachePath(paths.IndexPath() + "-shm");

        var backupIndexDirectory = Path.Combine(backupDirectory, ".journal", "index");
        CopyFileIfExists(Path.Combine(backupIndexDirectory, "journal.db"), paths.IndexPath());
        CopyFileIfExists(Path.Combine(backupIndexDirectory, "journal.db-wal"), paths.IndexPath() + "-wal");
        CopyFileIfExists(Path.Combine(backupIndexDirectory, "journal.db-shm"), paths.IndexPath() + "-shm");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceSubdirectory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            LocalJournalPaths.EnsureParentDirectory(destinationPath);
            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
    }

    private static void CopyFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        LocalJournalPaths.EnsureParentDirectory(destinationPath);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void RestoreOptionalFile(string sourcePath, string destinationPath)
    {
        DeleteFileOrDirectory(destinationPath);
        CopyFileIfExists(sourcePath, destinationPath);
    }

    private static void DeleteDirectory(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ClearIndexCachePath(string path)
    {
        DeleteFileOrDirectory(path);
    }

    private static void DeleteFileOrDirectory(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CanonicalizeArchiveRelativePath(string name)
    {
        var normalizedName = name.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedName)
            || normalizedName.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(name)
            || Path.IsPathRooted(normalizedName))
        {
            throw new InvalidOperationException($"Import entry path is invalid: {name}");
        }

        var pathWithoutTrailingSlash = normalizedName.TrimEnd('/');
        var segments = pathWithoutTrailingSlash.Split('/');
        if (segments.Length == 0
            || segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment)
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Import entry path is invalid: {name}");
        }

        return string.Join('/', segments);
    }

    private static bool IsDirectoryEntry(string name) =>
        name.EndsWith("/", StringComparison.Ordinal)
        || name.EndsWith("\\", StringComparison.Ordinal);

    private static bool IsAllowedImportEntry(string normalizedName)
    {
        var segments = normalizedName.Split('/');
        return IsEntryMarkdownPath(segments)
            || IsRawInputPath(segments)
            || IsDraftPath(segments)
            || IsVersionPath(segments)
            || IsAuditPath(segments)
            || IsSafeSettingsMetadata(normalizedName);
    }

    private static bool IsAllowedImportDirectory(string normalizedName)
    {
        var segments = normalizedName.Split('/');
        return IsEntryDirectory(segments)
            || IsJournalSourceDirectory(segments, "raw-inputs")
            || IsJournalSourceDirectory(segments, "drafts")
            || IsJournalSourceDirectory(segments, "versions")
            || IsJournalSourceDirectory(segments, "audit")
            || IsSettingsDirectory(segments);
    }

    private static bool IsEntryMarkdownPath(string[] segments) =>
        segments.Length == 4
        && string.Equals(segments[0], "entries", StringComparison.OrdinalIgnoreCase)
        && IsDatedFile(segments[1], segments[2], segments[3], ".md");

    private static bool IsRawInputPath(string[] segments) =>
        segments.Length == 5
        && IsJournalDirectory(segments, "raw-inputs")
        && IsDatedFile(segments[2], segments[3], segments[4], ".jsonl");

    private static bool IsDraftPath(string[] segments) =>
        segments.Length == 5
        && IsJournalDirectory(segments, "drafts")
        && (IsDatedFile(segments[2], segments[3], segments[4], ".md")
            || IsDatedFile(segments[2], segments[3], segments[4], ".meta.json"));

    private static bool IsVersionPath(string[] segments)
    {
        if (segments.Length != 6
            || !IsJournalDirectory(segments, "versions")
            || !TryParsePathDate(segments[2], segments[3], segments[4], out _))
        {
            return false;
        }

        var versionId = TryRemoveSuffix(segments[5], ".meta.json")
            ?? TryRemoveSuffix(segments[5], ".md");
        return LocalJournalPaths.IsValidVersionId(versionId);
    }

    private static bool IsAuditPath(string[] segments)
    {
        if (segments.Length != 6
            || !IsJournalDirectory(segments, "audit")
            || !TryParsePathDate(segments[2], segments[3], segments[4], out _))
        {
            return false;
        }

        var runId = TryRemoveSuffix(segments[5], ".json");
        return LocalJournalPaths.IsValidHarnessRunId(runId);
    }

    private static bool IsEntryDirectory(string[] segments) =>
        segments.Length == 1 && string.Equals(segments[0], "entries", StringComparison.OrdinalIgnoreCase)
        || segments.Length == 2 && string.Equals(segments[0], "entries", StringComparison.OrdinalIgnoreCase) && IsYear(segments[1])
        || segments.Length == 3 && string.Equals(segments[0], "entries", StringComparison.OrdinalIgnoreCase) && IsYear(segments[1]) && IsMonth(segments[2]);

    private static bool IsJournalSourceDirectory(string[] segments, string sourceDirectory)
    {
        if (segments.Length < 1 || segments.Length > 5)
        {
            return false;
        }

        if (segments.Length == 1)
        {
            return string.Equals(segments[0], ".journal", StringComparison.OrdinalIgnoreCase);
        }

        if (!IsJournalDirectory(segments, sourceDirectory))
        {
            return false;
        }

        return segments.Length switch
        {
            2 => true,
            3 => IsYear(segments[2]),
            4 => IsYear(segments[2]) && IsMonth(segments[3]),
            5 => sourceDirectory is "versions" or "audit"
                && TryParsePathDate(segments[2], segments[3], segments[4], out _),
            _ => false
        };
    }

    private static bool IsSettingsDirectory(string[] segments) =>
        segments.Length == 1 && string.Equals(segments[0], ".journal", StringComparison.OrdinalIgnoreCase)
        || segments.Length == 2
            && string.Equals(segments[0], ".journal", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "settings", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeSettingsMetadata(string normalizedName) =>
        string.Equals(
            normalizedName,
            ".journal/settings/ai-providers.safe.json",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsJournalDirectory(string[] segments, string sourceDirectory) =>
        segments.Length >= 2
        && string.Equals(segments[0], ".journal", StringComparison.OrdinalIgnoreCase)
        && string.Equals(segments[1], sourceDirectory, StringComparison.OrdinalIgnoreCase);

    private static bool IsDatedFile(string year, string month, string fileName, string suffix)
    {
        var dateText = TryRemoveSuffix(fileName, suffix);
        return TryParsePathDate(year, month, dateText, out _);
    }

    private static bool TryParsePathDate(string year, string month, string? isoDate, out JournalDate date)
    {
        if (JournalDate.TryParse(isoDate, out date)
            && string.Equals(date.Year, year, StringComparison.Ordinal)
            && string.Equals(date.Month, month, StringComparison.Ordinal))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static string? TryRemoveSuffix(string value, string suffix) =>
        value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : null;

    private static bool IsYear(string value) =>
        value.Length == 4 && value.All(char.IsAsciiDigit);

    private static bool IsMonth(string value) =>
        value.Length == 2
        && int.TryParse(value, CultureInfo.InvariantCulture, out var month)
        && month is >= 1 and <= 12;

    private static bool IsUnderRoot(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, comparison);
    }
}
