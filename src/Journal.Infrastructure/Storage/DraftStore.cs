using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class DraftStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly LocalJournalPaths _paths;

    public DraftStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(JournalDraft draft, CancellationToken cancellationToken)
    {
        var markdownPath = _paths.DraftPath(draft.Date);
        var metaPath = _paths.DraftMetaPath(draft.Date);
        LocalJournalPaths.EnsureParentDirectory(markdownPath);

        await File.WriteAllTextAsync(markdownPath, draft.Markdown, cancellationToken);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(DraftMeta.From(draft), JsonOptions), cancellationToken);
    }

    public async Task<JournalDraft?> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var markdownPath = _paths.DraftPath(date);
        var metaPath = _paths.DraftMetaPath(date);
        if (!File.Exists(markdownPath) || !File.Exists(metaPath))
        {
            return null;
        }

        var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken);
        var metaJson = await File.ReadAllTextAsync(metaPath, cancellationToken);
        var meta = JsonSerializer.Deserialize<DraftMeta>(metaJson, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid draft metadata in {metaPath}.");

        return new JournalDraft(date, meta.Status, markdown, meta.SourceRawInputIds, meta.Errors, meta.UpdatedAt);
    }

    private sealed record DraftMeta(
        JournalStatus Status,
        IReadOnlyList<string> SourceRawInputIds,
        IReadOnlyList<string> Errors,
        DateTimeOffset UpdatedAt)
    {
        public static DraftMeta From(JournalDraft draft) =>
            new(draft.Status, draft.SourceRawInputIds, draft.Errors, draft.UpdatedAt);
    }
}
