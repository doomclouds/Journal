using System.Text.Json;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Ai;

public sealed class JournalAiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalJournalPaths _paths;

    public JournalAiSettingsStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public bool Exists() => File.Exists(_paths.AiSettingsPath());

    public async Task<JournalAiSettings> ReadAsync(CancellationToken cancellationToken) =>
        (await ReadWithMetadataAsync(cancellationToken)).Settings;

    public async Task<JournalAiSettingsReadResult> ReadWithMetadataAsync(CancellationToken cancellationToken)
    {
        var path = _paths.AiSettingsPath();
        if (!File.Exists(path))
        {
            return JournalAiSettingsReadResult.Default;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return JournalAiSettingsReadResult.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<JournalAiSettings>(json, JsonOptions);
            return Normalize(settings) is { } normalized
                ? new JournalAiSettingsReadResult(normalized, true)
                : JournalAiSettingsReadResult.Default;
        }
        catch (JsonException)
        {
            return JournalAiSettingsReadResult.Default;
        }
    }

    public async Task WriteAsync(JournalAiSettings settings, CancellationToken cancellationToken)
    {
        var path = _paths.AiSettingsPath();
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
    }

    private static JournalAiSettings? Normalize(JournalAiSettings? settings)
    {
        if (settings?.Providers is null
            || settings.Providers.Count == 0
            || string.IsNullOrWhiteSpace(settings.ActiveProviderId))
        {
            return null;
        }

        foreach (var provider in settings.Providers)
        {
            if (provider is null
                || string.IsNullOrWhiteSpace(provider.Id)
                || string.IsNullOrWhiteSpace(provider.Type)
                || string.IsNullOrWhiteSpace(provider.DisplayName)
                || string.IsNullOrWhiteSpace(provider.Preset)
                || string.IsNullOrWhiteSpace(provider.BaseUrl)
                || provider.Model is null
                || provider.ApiKey is null
                || string.IsNullOrWhiteSpace(provider.StylePreset))
            {
                return null;
            }
        }

        return settings.Providers.Any(provider =>
            string.Equals(provider.Id, settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase))
            ? settings
            : null;
    }
}

public sealed record JournalAiSettingsReadResult(JournalAiSettings Settings, bool IsFileBacked)
{
    public static JournalAiSettingsReadResult Default { get; } =
        new(JournalAiSettings.CreateDefault(), false);
}
