using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class LocalJournalPaths
{
    private readonly string _rootDirectory;

    public LocalJournalPaths(JournalStorageOptions options)
    {
        _rootDirectory = options.RootDirectory;
    }

    public string RootDirectory() =>
        _rootDirectory;

    public string EntryPath(JournalDate date) =>
        Path.Combine(_rootDirectory, "entries", date.Year, date.Month, date.MarkdownFileName);

    public string EntryRootDirectory() =>
        Path.Combine(_rootDirectory, "entries");

    public string RawInputPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "raw-inputs", date.Year, date.Month, $"{date.IsoDate}.jsonl");

    public string RawInputRootDirectory() =>
        Path.Combine(_rootDirectory, ".journal", "raw-inputs");

    public string DraftPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "drafts", date.Year, date.Month, date.MarkdownFileName);

    public string DraftMetaPath(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "drafts", date.Year, date.Month, $"{date.IsoDate}.meta.json");

    public string DraftRootDirectory() =>
        Path.Combine(_rootDirectory, ".journal", "drafts");

    public string VersionDirectory(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "versions", date.Year, date.Month, date.IsoDate);

    public string VersionRootDirectory() =>
        Path.Combine(_rootDirectory, ".journal", "versions");

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

    public string AiSettingsPath() =>
        Path.Combine(_rootDirectory, ".journal", "settings", "ai-providers.json");

    public string HarnessAuditDirectory(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "audit", date.Year, date.Month, date.IsoDate);

    public string AuditRootDirectory() =>
        Path.Combine(_rootDirectory, ".journal", "audit");

    public string ExportDirectory() =>
        Path.Combine(_rootDirectory, ".journal", "exports");

    public string HarnessAuditRunPath(JournalDate date, string runId) =>
        IsValidHarnessRunId(runId)
            ? Path.Combine(HarnessAuditDirectory(date), $"{runId}.json")
            : throw new ArgumentException("runId contains invalid path characters.", nameof(runId));

    public static bool IsValidHarnessRunId(string? runId) =>
        !string.IsNullOrWhiteSpace(runId)
        && runId.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character == '-');

    public static bool IsValidVersionId(string? versionId) =>
        !string.IsNullOrWhiteSpace(versionId)
        && versionId.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character == '-'
            || character == '_'
            || character == '+');

    public static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
