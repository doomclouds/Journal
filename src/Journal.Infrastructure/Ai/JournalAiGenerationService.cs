using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Infrastructure.Ai;

public sealed class JournalAiGenerationService
{
    private readonly IJournalAiSettingsReader _settingsReader;
    private readonly MockAiProvider _mockProvider;
    private readonly OpenAiCompatibleJournalAiProvider _openAiCompatibleProvider;

    public JournalAiGenerationService(
        IJournalAiSettingsReader settingsReader,
        MockAiProvider mockProvider,
        OpenAiCompatibleJournalAiProvider openAiCompatibleProvider)
    {
        _settingsReader = settingsReader;
        _mockProvider = mockProvider;
        _openAiCompatibleProvider = openAiCompatibleProvider;
    }

    public async Task<JournalAiProviderResult> GenerateAsync(
        JournalDate date,
        IReadOnlyList<RawInput> rawInputs,
        DateTimeOffset generatedAt,
        string? providerIdOverride,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        if (!TryResolveProvider(settings, providerIdOverride, out var providerSettings, out var error))
        {
            return JournalAiProviderResult.Failure(
                CreateUnknownProviderMetadata(providerIdOverride),
                error);
        }

        IJournalAiProvider provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(date, rawInputs, generatedAt, providerSettings),
            cancellationToken);

        if (!result.IsSuccess || result.AiJson is null)
        {
            return result;
        }

        var sanitizedAiJson = OverwriteRawInputs(result.AiJson, rawInputs);
        var validation = JournalAiJsonValidator.Validate(sanitizedAiJson);
        if (validation.IsValid)
        {
            return result with { AiJson = sanitizedAiJson };
        }

        var message = string.Join(" ", validation.Errors);
        return JournalAiProviderResult.Failure(
            result.Metadata,
            JournalAiSafeError.Create("validation", "validation_failed", message, message));
    }

    public async Task<JournalAiProviderHealthResult> CheckAsync(
        string? providerId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        if (!TryResolveProvider(settings, providerId, out var providerSettings, out var error))
        {
            return JournalAiProviderHealthResult.Failure(error.Code, null, null, error);
        }

        IJournalAiProvider provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
        return await provider.CheckAsync(providerSettings, cancellationToken);
    }

    private static bool TryResolveProvider(
        JournalAiSettings settings,
        string? providerIdOverride,
        out JournalAiProviderSettings providerSettings,
        out JournalAiSafeError error)
    {
        var isExplicitOverride = !string.IsNullOrWhiteSpace(providerIdOverride);
        var providerId = isExplicitOverride
            ? providerIdOverride!.Trim()
            : settings.ActiveProviderId;
        var resolvedProvider = settings.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (resolvedProvider is not null)
        {
            providerSettings = resolvedProvider;
            error = null!;
            return true;
        }

        providerSettings = null!;
        error = CreateProviderNotFoundError(providerId);
        return false;
    }

    private static JournalAiSafeError CreateProviderNotFoundError(string providerId) =>
        JournalAiSafeError.Create(
            "settings",
            "provider_not_found",
            $"LLM '{providerId}' was not found.",
            $"Provider '{providerId}' was not found in effective AI settings.");

    private static JournalAiMetadata CreateUnknownProviderMetadata(string? providerIdOverride) =>
        new(
            string.IsNullOrWhiteSpace(providerIdOverride) ? "unknown" : providerIdOverride.Trim(),
            "unknown",
            JournalAiPrompt.Version);

    private static JournalAiJson OverwriteRawInputs(JournalAiJson aiJson, IReadOnlyList<RawInput> rawInputs) =>
        aiJson with
        {
            RawInputs = rawInputs.Select(rawInput => rawInput.Text).ToArray()
        };
}
