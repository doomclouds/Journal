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

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var lineNumber = index + 1;
            try
            {
                var raw = JsonSerializer.Deserialize<RawInputLine>(line, JsonOptions)
                    ?? throw new FormatException("Raw input line is empty.");
                raw.Validate();
                inputs.Add(raw.ToRawInput());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Invalid raw input line in {path} at line {lineNumber}.", exception);
            }
        }

        return inputs;
    }

    private sealed record RawInputLine(string Id, string Date, DateTimeOffset CreatedAt, string Source, string Text)
    {
        public static RawInputLine From(RawInput input) =>
            new(input.Id, input.Date.IsoDate, input.CreatedAt, input.Source, input.Text);

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)
                || string.IsNullOrWhiteSpace(Date)
                || string.IsNullOrWhiteSpace(Source)
                || string.IsNullOrWhiteSpace(Text))
            {
                throw new FormatException("Raw input line is missing required fields.");
            }
        }

        public RawInput ToRawInput() =>
            new(Id, JournalDate.From(DateOnly.Parse(Date, CultureInfo.InvariantCulture)), CreatedAt, Source, Text);
    }
}
