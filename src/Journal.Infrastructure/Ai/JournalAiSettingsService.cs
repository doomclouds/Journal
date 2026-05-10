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
            effective.Providers.Select(provider =>
            {
                var source = SourceOf(provider, fileSettings, isFileBacked, overlay);
                var fileProvider = fileSettings.Providers.FirstOrDefault(item =>
                    string.Equals(item.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
                var canRevealApiKey = CanRevealFileBackedApiKey(provider, fileProvider, isFileBacked);
                var apiKeyPreview = canRevealApiKey ? MaskApiKey(fileProvider!.ApiKey) : string.Empty;
                return ToView(provider, effective.ActiveProviderId, source, apiKeyPreview, canRevealApiKey);
            }).ToArray());
    }

    public async Task SaveAsync(JournalAiSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        var settings = await CreateFileSettingsFromRequestAsync(request, cancellationToken);
        await _store.WriteAsync(settings, cancellationToken);
    }

    public async Task<JournalAiSettings> BuildEffectiveCandidateAsync(
        JournalAiSettingsSaveRequest request,
        CancellationToken cancellationToken)
    {
        var fileCandidate = await CreateFileSettingsFromRequestAsync(request, cancellationToken);
        var overlay = ResolveEnvironmentOverlay(fileCandidate);
        return ApplyEnvironment(fileCandidate, overlay);
    }

    public async Task<JournalAiProviderApiKeyView?> ReadFileApiKeyAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        var readResult = await _store.ReadWithMetadataAsync(cancellationToken);
        if (!readResult.IsFileBacked)
        {
            return null;
        }

        var provider = readResult.Settings.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (provider is null || provider.IsMock || string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            return null;
        }

        if (!CanRevealFileBackedApiKey(provider, provider, readResult.IsFileBacked))
        {
            return null;
        }

        return new JournalAiProviderApiKeyView(provider.Id, "file", provider.ApiKey);
    }

    private async Task<JournalAiSettings> CreateFileSettingsFromRequestAsync(
        JournalAiSettingsSaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSaveRequest(request);
        var currentFileSettings = (await _store.ReadWithMetadataAsync(cancellationToken)).Settings;

        var activeProviderId = string.IsNullOrWhiteSpace(request.ActiveProviderId)
            ? "mock"
            : request.ActiveProviderId.Trim();
        if (!request.Providers.Any(provider =>
            string.Equals(provider.Id.Trim(), activeProviderId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"active provider '{activeProviderId}' was not found in providers.", nameof(request));
        }

        return new JournalAiSettings(
            activeProviderId,
            request.Providers.Select(provider => ToSettings(provider, currentFileSettings)).ToArray());
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

        return new EnvironmentOverlay(providerId, hasOverride);
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

    private bool HasEnvironmentApiKey(string providerId) =>
        !string.IsNullOrWhiteSpace(GetTrimmed("JOURNAL_AI_API_KEY"))
        || !string.IsNullOrWhiteSpace(FirstConfiguredProviderKeyValue(providerId));

    private static JournalAiProviderSettings ToSettings(
        JournalAiProviderSaveRequest provider,
        JournalAiSettings currentFileSettings)
    {
        var currentProvider = currentFileSettings.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, provider.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        var apiKey = string.IsNullOrWhiteSpace(provider.ApiKey) && !string.IsNullOrWhiteSpace(currentProvider?.ApiKey)
            ? currentProvider.ApiKey
            : provider.ApiKey.Trim();

        return new JournalAiProviderSettings(
            provider.Id.Trim(),
            provider.Type.Trim(),
            provider.DisplayName.Trim(),
            provider.Preset.Trim(),
            provider.BaseUrl.Trim(),
            provider.Model.Trim(),
            apiKey,
            provider.IsEnabled,
            provider.TimeoutSeconds,
            provider.Temperature,
            provider.MaxTokens,
            provider.StylePreset.Trim());
    }

    private bool CanRevealFileBackedApiKey(
        JournalAiProviderSettings provider,
        JournalAiProviderSettings? fileProvider,
        bool isFileBacked)
    {
        return isFileBacked
            && !provider.IsMock
            && fileProvider is not null
            && !string.IsNullOrWhiteSpace(fileProvider.ApiKey)
            && !HasEnvironmentApiKey(provider.Id);
    }

    private static JournalAiProviderView ToView(
        JournalAiProviderSettings provider,
        string activeProviderId,
        string source,
        string apiKeyPreview,
        bool canRevealApiKey)
    {
        var hasApiKey = provider.IsMock || !string.IsNullOrWhiteSpace(provider.ApiKey);

        return new JournalAiProviderView(
            provider.Id,
            provider.Type,
            provider.DisplayName,
            provider.Preset,
            provider.BaseUrl,
            provider.Model,
            provider.IsEnabled,
            string.Equals(provider.Id, activeProviderId, StringComparison.OrdinalIgnoreCase),
            hasApiKey,
            apiKeyPreview,
            canRevealApiKey,
            source,
            provider.TimeoutSeconds,
            provider.Temperature,
            provider.MaxTokens,
            provider.StylePreset,
            "not-tested");
    }

    private static string MaskApiKey(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8)
        {
            return "••••";
        }

        var prefix = trimmed.Length >= 3 ? trimmed[..3] : string.Empty;
        var suffix = trimmed[^4..];
        return $"{prefix}••••••••••••••••{suffix}";
    }

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
