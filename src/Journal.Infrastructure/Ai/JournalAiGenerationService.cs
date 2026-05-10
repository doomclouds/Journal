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
        var providerSettings = ResolveProvider(settings, providerIdOverride);
        IJournalAiProvider provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(date, rawInputs, generatedAt, providerSettings),
            cancellationToken);

        if (!result.IsSuccess || result.AiJson is null)
        {
            return result;
        }

        var validation = JournalAiJsonValidator.Validate(result.AiJson);
        if (validation.IsValid)
        {
            return result;
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
        var providerSettings = ResolveProvider(settings, providerId);
        IJournalAiProvider provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
        return await provider.CheckAsync(providerSettings, cancellationToken);
    }

    private static JournalAiProviderSettings ResolveProvider(JournalAiSettings settings, string? providerIdOverride)
    {
        var providerId = string.IsNullOrWhiteSpace(providerIdOverride)
            ? settings.ActiveProviderId
            : providerIdOverride.Trim();
        return settings.Providers.FirstOrDefault(provider =>
                string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase))
            ?? settings.Providers.First(provider => provider.IsMock);
    }
}
