using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalVersionStore : IJournalVersionStore
{
    private const int MaxCreateAttempts = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalJournalPaths _paths;
    private readonly Func<string, string, CancellationToken, Task> _writeNewTextFileAsync;

    public JournalVersionStore(LocalJournalPaths paths)
        : this(paths, WriteNewTextFileAsync)
    {
    }

    internal JournalVersionStore(
        LocalJournalPaths paths,
        Func<string, string, CancellationToken, Task> writeNewTextFileAsync)
    {
        _paths = paths;
        _writeNewTextFileAsync = writeNewTextFileAsync;
    }

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
            throw new ArgumentException("Markdown must not be blank.", nameof(markdown));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason must not be blank.", nameof(reason));
        }

        var trimmedReason = reason.Trim();
        var contentHash = ComputeContentHash(markdown);
        var baseId = CreateVersionId(createdAt);

        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            var id = attempt == 0
                ? baseId
                : $"{baseId}-{attempt:000}";
            var markdownPath = _paths.VersionMarkdownPath(date, id);
            var metaPath = _paths.VersionMetaPath(date, id);
            var version = new JournalEntryVersion(
                id,
                date,
                createdAt,
                trimmedReason,
                sourceEntryPath,
                markdownPath,
                metaPath,
                contentHash);

            var markdownCreated = false;
            try
            {
                LocalJournalPaths.EnsureParentDirectory(markdownPath);
                await _writeNewTextFileAsync(markdownPath, markdown, cancellationToken);
                markdownCreated = true;
            }
            catch (IOException exception) when (IsCreateNewCollision(exception))
            {
                continue;
            }
            catch (IOException)
            {
                TryDelete(markdownPath);
                TryDelete(metaPath);
                throw;
            }

            try
            {
                await _writeNewTextFileAsync(metaPath, JsonSerializer.Serialize(VersionMeta.From(version), JsonOptions), cancellationToken);

                return version;
            }
            catch (IOException exception) when (IsCreateNewCollision(exception))
            {
                if (markdownCreated)
                {
                    TryDelete(markdownPath);
                }
            }
            catch (IOException)
            {
                TryDelete(markdownPath);
                TryDelete(metaPath);
                throw;
            }
        }

        throw new IOException($"Could not create a unique journal version snapshot for {date.IsoDate}.");
    }

    public async Task<IReadOnlyList<JournalEntryVersion>> ReadByDateAsync(
        JournalDate date,
        CancellationToken cancellationToken)
    {
        var directory = _paths.VersionDirectory(date);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var versions = new List<JournalEntryVersion>();
        foreach (var metaPath in Directory.EnumerateFiles(directory, "*.meta.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metaJson = await File.ReadAllTextAsync(metaPath, Encoding.UTF8, cancellationToken);
            var meta = JsonSerializer.Deserialize<VersionMeta>(metaJson, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid version metadata in {metaPath}.");

            versions.Add(meta.ToVersion());
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

        var markdownPath = _paths.VersionMarkdownPath(date, versionId);
        var metaPath = _paths.VersionMetaPath(date, versionId);
        if (!File.Exists(markdownPath) || !File.Exists(metaPath))
        {
            return null;
        }

        var metaJson = await File.ReadAllTextAsync(metaPath, Encoding.UTF8, cancellationToken);
        var meta = JsonSerializer.Deserialize<VersionMeta>(metaJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid version metadata in {metaPath}.");
        var version = meta.ToVersion();
        if (version.Id != versionId || version.Date != date)
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(markdownPath, Encoding.UTF8, cancellationToken);
        if (!string.Equals(ComputeContentHash(markdown), version.ContentHash, StringComparison.Ordinal))
        {
            return null;
        }

        return (version, markdown);
    }

    private static string CreateVersionId(DateTimeOffset createdAt) =>
        "version-" + createdAt
            .ToString("yyyy-MM-ddTHH-mm-ss-fffffffzzz", CultureInfo.InvariantCulture)
            .Replace(':', '-');

    private static bool IsCreateNewCollision(IOException exception)
    {
        const int ErrorFileExists = unchecked((int)0x80070050); // HRESULT_FROM_WIN32(ERROR_FILE_EXISTS)
        const int ErrorAlreadyExists = unchecked((int)0x800700B7); // HRESULT_FROM_WIN32(ERROR_ALREADY_EXISTS)

        return exception.HResult is ErrorFileExists or ErrorAlreadyExists;
    }

    private static string ComputeContentHash(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task WriteNewTextFileAsync(
        string path,
        string contents,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(contents.AsMemory(), cancellationToken);
    }

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

    private sealed record VersionMeta(
        string Id,
        string Date,
        DateTimeOffset CreatedAt,
        string Reason,
        string SourceEntryPath,
        string MarkdownPath,
        string MetaPath,
        string ContentHash)
    {
        public static VersionMeta From(JournalEntryVersion version) =>
            new(
                version.Id,
                version.Date.IsoDate,
                version.CreatedAt,
                version.Reason,
                version.SourceEntryPath,
                version.MarkdownPath,
                version.MetaPath,
                version.ContentHash);

        public JournalEntryVersion ToVersion() =>
            new(
                Id,
                JournalDate.Parse(Date),
                CreatedAt,
                Reason,
                SourceEntryPath,
                MarkdownPath,
                MetaPath,
                ContentHash);
    }
}
