using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Entries;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Harness;

public sealed class JournalHarnessAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly LocalJournalPaths _paths;

    public JournalHarnessAuditStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(JournalHarnessAuditRun run, CancellationToken cancellationToken)
    {
        var path = _paths.HarnessAuditRunPath(run.Date, run.Id);
        LocalJournalPaths.EnsureParentDirectory(path);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(run, JsonOptions), cancellationToken);

        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<IReadOnlyList<JournalHarnessAuditRun>> ReadByDateAsync(
        JournalDate date,
        CancellationToken cancellationToken)
    {
        var directory = _paths.HarnessAuditDirectory(date);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<JournalHarnessAuditRun>();
        }

        var runs = new List<JournalHarnessAuditRun>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var run = JsonSerializer.Deserialize<JournalHarnessAuditRun>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid harness audit run in {path}.");
            runs.Add(run);
        }

        return runs
            .OrderByDescending(run => run.CreatedAt)
            .ToArray();
    }

    public async Task<JournalHarnessAuditRun?> ReadAsync(
        JournalDate date,
        string runId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return null;
        }

        var path = _paths.HarnessAuditRunPath(date, runId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<JournalHarnessAuditRun>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid harness audit run in {path}.");
    }
}
