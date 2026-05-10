using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiProviderSettings(
    string Id,
    string Type,
    string DisplayName,
    string Preset,
    string BaseUrl,
    string Model,
    string ApiKey,
    bool IsEnabled,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens,
    string StylePreset)
{
    public bool IsMock =>
        string.Equals(Id, "mock", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "mock", StringComparison.OrdinalIgnoreCase);

    public JournalAiMetadata ToMetadata() =>
        IsMock
            ? JournalAiMetadata.Mock
            : new(Id, string.IsNullOrWhiteSpace(Model) ? "mock-journal" : Model, JournalAiPrompt.Version);
}

public sealed record JournalAiProviderView(
    string Id,
    string Type,
    string DisplayName,
    string Preset,
    string BaseUrl,
    string Model,
    bool IsEnabled,
    bool IsActive,
    bool HasApiKey,
    string ApiKeyPreview,
    bool CanRevealApiKey,
    string Source,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens,
    string StylePreset,
    string LastTestStatus);

public sealed record JournalAiSettingsView(
    string ActiveProviderId,
    string Runtime,
    IReadOnlyList<JournalAiProviderView> Providers);

public sealed record JournalAiProviderApiKeyView(
    string ProviderId,
    string Source,
    string ApiKey);

public sealed record JournalAiSettingsActivationResult(
    bool Saved,
    JournalAiSettingsView Settings,
    JournalAiProviderHealthResult TestResult);

public sealed record JournalAiProviderSaveRequest(
    string Id,
    string Type,
    string DisplayName,
    string Preset,
    string BaseUrl,
    string Model,
    string ApiKey,
    bool IsEnabled,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens,
    string StylePreset);

public sealed record JournalAiSettingsSaveRequest(
    string ActiveProviderId,
    IReadOnlyList<JournalAiProviderSaveRequest> Providers);
