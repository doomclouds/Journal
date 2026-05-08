using System.Globalization;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class RawInputStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LocalJournalPaths _paths;

    public RawInputStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task AppendAsync(RawInput input, CancellationToken cancellationToken)
    {
        var path = _paths.RawInputPath(input.Date);
        LocalJournalPaths.EnsureParentDirectory(path);

        var line = JsonSerializer.Serialize(RawInputLine.From(input), JsonOptions);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
    }

    public async Task<IReadOnlyList<RawInput>> ReadAsync(JournalDate date, CancellationToken cancellationToken)
    {
        var path = _paths.RawInputPath(date);
        if (!File.Exists(path))
        {
            return Array.Empty<RawInput>();
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var inputs = new List<RawInput>();

        foreach (var line in lines.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var raw = JsonSerializer.Deserialize<RawInputLine>(line, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid raw input line in {path}.");
            inputs.Add(raw.ToRawInput());
        }

        return inputs;
    }

    private sealed record RawInputLine(string Id, string Date, DateTimeOffset CreatedAt, string Source, string Text)
    {
        public static RawInputLine From(RawInput input) =>
            new(input.Id, input.Date.IsoDate, input.CreatedAt, input.Source, input.Text);

        public RawInput ToRawInput() =>
            new(Id, JournalDate.From(DateOnly.Parse(Date, CultureInfo.InvariantCulture)), CreatedAt, Source, Text);
    }
}
