namespace Journal.Infrastructure.Ai;

public interface IJournalAiSettingsReader
{
    Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken);
}

public sealed class JournalAiSettingsService : IJournalAiSettingsReader
{
    private const string Runtime = "OpenAI-compatible runtime · Agent Framework 1.5.0";

    private static readonly string[] ProviderApiKeyNames =
    [
        "OPENAI_API_KEY",
        "DEEPSEEK_API_KEY",
        "ZHIPU_API_KEY"
    ];

    private readonly JournalAiSettingsStore _store;
    private readonly IJournalAiEnvironment _environment;

    public JournalAiSettingsService(JournalAiSettingsStore store, IJournalAiEnvironment environment)
    {
        _store = store;
        _environment = environment;
    }

    public async Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken)
    {
        var readResult = await _store.ReadWithMetadataAsync(cancellationToken);
        var overlay = ResolveEnvironmentOverlay(readResult.Settings);
        return ApplyEnvironment(readResult.Settings, overlay);
    }

    public async Task<JournalAiSettingsView> ReadViewAsync(CancellationToken cancellationToken)
    {
        var readResult = await _store.ReadWithMetadataAsync(cancellationToken);
        var isFileBacked = readResult.IsFileBacked;
        var fileSettings = readResult.Settings;
        var overlay = ResolveEnvironmentOverlay(fileSettings);
        var effective = ApplyEnvironment(fileSettings, overlay);

        return new JournalAiSettingsView(
            effective.ActiveProviderId,
            Runtime,
            effective.Providers.Select(provider => ToView(provider, effective.ActiveProviderId, SourceOf(provider, fileSettings, isFileBacked, overlay))).ToArray());
    }

    public async Task SaveAsync(JournalAiSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSaveRequest(request);

        var activeProviderId = string.IsNullOrWhiteSpace(request.ActiveProviderId)
            ? "mock"
            : request.ActiveProviderId.Trim();
        if (!request.Providers.Any(provider =>
            string.Equals(provider.Id.Trim(), activeProviderId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"active provider '{activeProviderId}' was not found in providers.", nameof(request));
        }

        var settings = new JournalAiSettings(
            activeProviderId,
            request.Providers.Select(ToSettings).ToArray());

        await _store.WriteAsync(settings, cancellationToken);
    }

    private static void ValidateSaveRequest(JournalAiSettingsSaveRequest request)
    {
        if (request.Providers is null || request.Providers.Count == 0)
        {
            throw new ArgumentException("providers are required.", nameof(request));
        }

        for (var index = 0; index < request.Providers.Count; index++)
        {
            var provider = request.Providers[index];
            if (provider is null)
            {
                throw new ArgumentException($"provider at index {index} is required.", nameof(request));
            }

            ValidateRequired(provider.Id, "provider id");
            ValidateRequired(provider.Type, "provider type");
            ValidateRequired(provider.DisplayName, "provider display name");
            ValidateRequired(provider.Preset, "provider preset");
            ValidateRequired(provider.BaseUrl, "provider base URL");
            ValidateNotNull(provider.Model, "provider model");
            ValidateNotNull(provider.ApiKey, "provider API key");
            ValidateRequired(provider.StylePreset, "provider style preset");
        }
    }

    private static void ValidateRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }
    }

    private static void ValidateNotNull(string? value, string name)
    {
        if (value is null)
        {
            throw new ArgumentException($"{name} is required.");
        }
    }

    private JournalAiSettings ApplyEnvironment(JournalAiSettings settings, EnvironmentOverlay overlay)
    {
        if (!overlay.HasOverride)
        {
            return settings;
        }

        var activeProviderId = overlay.ProviderId;
        var baseUrl = GetTrimmed("JOURNAL_AI_BASE_URL");
        var model = GetTrimmed("JOURNAL_AI_MODEL");
        var apiKey = GetTrimmed("JOURNAL_AI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = FirstConfiguredProviderKeyValue(activeProviderId);
        }

        var providers = settings.Providers.Select(provider =>
        {
            if (!string.Equals(provider.Id, activeProviderId, StringComparison.OrdinalIgnoreCase))
            {
                return provider;
            }

            return provider with
            {
                BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? provider.BaseUrl : baseUrl,
                Model = string.IsNullOrWhiteSpace(model) ? provider.Model : model,
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? provider.ApiKey : apiKey,
                IsEnabled = true
            };
        }).ToArray();

        return settings with { ActiveProviderId = activeProviderId, Providers = providers };
    }

    private EnvironmentOverlay ResolveEnvironmentOverlay(JournalAiSettings settings)
    {
        var providerId = GetTrimmed("JOURNAL_AI_PROVIDER");
        var hasOverride = !string.IsNullOrWhiteSpace(providerId)
            || !string.IsNullOrWhiteSpace(GetTrimmed("JOURNAL_AI_BASE_URL"))
            || !string.IsNullOrWhiteSpace(GetTrimmed("JOURNAL_AI_MODEL"))
            || !string.IsNullOrWhiteSpace(GetTrimmed("JOURNAL_AI_API_KEY"));
        if (string.IsNullOrWhiteSpace(providerId))
        {
            var providerKeyName = FirstConfiguredProviderKeyName();
            hasOverride = hasOverride || !string.IsNullOrWhiteSpace(providerKeyName);
            providerId = providerKeyName switch
            {
                "OPENAI_API_KEY" => "openai",
                "DEEPSEEK_API_KEY" => "deepseek",
                "ZHIPU_API_KEY" => "zhipu",
                _ => settings.ActiveProviderId
            };
        }

        var resolvedProviderId = settings.Providers.Any(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase))
            ? providerId
            : settings.ActiveProviderId;

        return new EnvironmentOverlay(resolvedProviderId, hasOverride);
    }

    private string? FirstConfiguredProviderKeyName()
    {
        foreach (var name in ProviderApiKeyNames)
        {
            if (!string.IsNullOrWhiteSpace(_environment.Get(name)))
            {
                return name;
            }
        }

        return null;
    }

    private string? FirstConfiguredProviderKeyValue(string providerId) =>
        providerId.ToLowerInvariant() switch
        {
            "openai" => GetTrimmed("OPENAI_API_KEY"),
            "deepseek" => GetTrimmed("DEEPSEEK_API_KEY"),
            "zhipu" => GetTrimmed("ZHIPU_API_KEY"),
            _ => null
        };

    private string? GetTrimmed(string name)
    {
        var value = _environment.Get(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static JournalAiProviderSettings ToSettings(JournalAiProviderSaveRequest provider) =>
        new(
            provider.Id.Trim(),
            provider.Type.Trim(),
            provider.DisplayName.Trim(),
            provider.Preset.Trim(),
            provider.BaseUrl.Trim(),
            provider.Model.Trim(),
            provider.ApiKey.Trim(),
            provider.IsEnabled,
            provider.TimeoutSeconds,
            provider.Temperature,
            provider.MaxTokens,
            provider.StylePreset.Trim());

    private static JournalAiProviderView ToView(JournalAiProviderSettings provider, string activeProviderId, string source) =>
        new(
            provider.Id,
            provider.Type,
            provider.DisplayName,
            provider.Preset,
            provider.BaseUrl,
            provider.Model,
            provider.IsEnabled,
            string.Equals(provider.Id, activeProviderId, StringComparison.OrdinalIgnoreCase),
            provider.IsMock || !string.IsNullOrWhiteSpace(provider.ApiKey),
            source,
            provider.TimeoutSeconds,
            provider.Temperature,
            provider.MaxTokens,
            provider.StylePreset,
            "not-tested");

    private static string SourceOf(
        JournalAiProviderSettings provider,
        JournalAiSettings fileSettings,
        bool isFileBacked,
        EnvironmentOverlay overlay)
    {
        if (overlay.HasOverride && string.Equals(provider.Id, overlay.ProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return "environment";
        }

        var fileProvider = fileSettings.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, provider.Id, StringComparison.OrdinalIgnoreCase));

        if (isFileBacked && fileProvider is not null)
        {
            return "file";
        }

        var defaultProvider = JournalAiSettings.CreateDefault().Providers.FirstOrDefault(item =>
            string.Equals(item.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
        if (defaultProvider is not null && EqualsProvider(provider, defaultProvider))
        {
            return provider.IsMock ? "default" : "preset";
        }

        return "file";
    }

    private static bool EqualsProvider(JournalAiProviderSettings left, JournalAiProviderSettings right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal)
        && string.Equals(left.Type, right.Type, StringComparison.Ordinal)
        && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
        && string.Equals(left.Preset, right.Preset, StringComparison.Ordinal)
        && string.Equals(left.BaseUrl, right.BaseUrl, StringComparison.Ordinal)
        && string.Equals(left.Model, right.Model, StringComparison.Ordinal)
        && string.Equals(left.ApiKey, right.ApiKey, StringComparison.Ordinal)
        && left.IsEnabled == right.IsEnabled
        && left.TimeoutSeconds == right.TimeoutSeconds
        && left.Temperature.Equals(right.Temperature)
        && left.MaxTokens == right.MaxTokens
        && string.Equals(left.StylePreset, right.StylePreset, StringComparison.Ordinal);

    private sealed record EnvironmentOverlay(string ProviderId, bool HasOverride);
}
