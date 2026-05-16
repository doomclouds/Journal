using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalAnniversaryStore
{
    private const string Schema = "journal-anniversaries/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalJournalPaths _paths;

    public JournalAnniversaryStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task<JournalAnniversaryDocument> ReadAsync(CancellationToken cancellationToken)
    {
        var path = _paths.AnniversaryPath();
        if (!File.Exists(path))
        {
            return JournalAnniversaryDocument.Empty();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var document = JsonSerializer.Deserialize<JournalAnniversaryDocument>(json, JsonOptions);
            return Normalize(document);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException("Anniversary data is invalid.", exception);
        }
    }

    public async Task WriteAsync(JournalAnniversaryDocument document, CancellationToken cancellationToken)
    {
        var path = _paths.AnniversaryPath();
        LocalJournalPaths.EnsureParentDirectory(path);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var normalized = Normalize(document);
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static JournalAnniversaryDocument Normalize(JournalAnniversaryDocument? document)
    {
        if (document is null)
        {
            return JournalAnniversaryDocument.Empty();
        }

        var schema = string.IsNullOrWhiteSpace(document.Schema)
            ? Schema
            : document.Schema;
        var items = document.Items ?? [];

        return new JournalAnniversaryDocument(
            schema,
            items.Select(Normalize).ToArray());
    }

    private static JournalAnniversaryItem Normalize(JournalAnniversaryItem? item)
    {
        if (item is null)
        {
            throw new InvalidOperationException("Anniversary data is invalid.");
        }

        return item.NextYearNotes is null
            ? item with { NextYearNotes = [] }
            : item;
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
}
