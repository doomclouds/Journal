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

    public string AiSettingsPath() =>
        Path.Combine(_rootDirectory, ".journal", "settings", "ai-providers.json");

    public string HarnessAuditDirectory(JournalDate date) =>
        Path.Combine(_rootDirectory, ".journal", "audit", date.Year, date.Month, date.IsoDate);

    public string HarnessAuditRunPath(JournalDate date, string runId) =>
        IsValidHarnessRunId(runId)
            ? Path.Combine(HarnessAuditDirectory(date), $"{runId}.json")
            : throw new ArgumentException("runId contains invalid path characters.", nameof(runId));

    public static bool IsValidHarnessRunId(string? runId) =>
        !string.IsNullOrWhiteSpace(runId)
        && runId.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character == '-');

    public static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
