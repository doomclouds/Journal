using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalVersionStore : IJournalVersionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalJournalPaths _paths;

    public JournalVersionStore(LocalJournalPaths paths)
    {
        _paths = paths;
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

        var id = CreateVersionId(createdAt);
        var markdownPath = _paths.VersionMarkdownPath(date, id);
        var metaPath = _paths.VersionMetaPath(date, id);
        var contentHash = ComputeContentHash(markdown);
        var version = new JournalEntryVersion(
            id,
            date,
            createdAt,
            reason,
            sourceEntryPath,
            markdownPath,
            metaPath,
            contentHash);

        LocalJournalPaths.EnsureParentDirectory(markdownPath);
        await File.WriteAllTextAsync(markdownPath, markdown, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(VersionMeta.From(version), JsonOptions), Encoding.UTF8, cancellationToken);

        return version;
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
        var markdown = await File.ReadAllTextAsync(markdownPath, Encoding.UTF8, cancellationToken);

        return (meta.ToVersion(), markdown);
    }

    private static string CreateVersionId(DateTimeOffset createdAt) =>
        "version-" + createdAt
            .ToString("yyyy-MM-ddTHH-mm-sszzz", CultureInfo.InvariantCulture)
            .Replace(':', '-');

    private static string ComputeContentHash(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
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
