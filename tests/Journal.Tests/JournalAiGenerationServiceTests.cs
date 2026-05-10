using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Tests;

public sealed class JournalAiGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesMockWhenEffectiveProviderIsMock()
    {
        var settings = JournalAiSettings.CreateDefault();
        var service = new JournalAiGenerationService(
            new StaticSettingsService(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new ThrowingRuntime()));
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var rawInputs = new[]
        {
            new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-10T08:01:00+08:00"), "text", "今天继续推进 Journal。")
        };

        var result = await service.GenerateAsync(
            date,
            rawInputs,
            DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
            providerIdOverride: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("mock", result.Metadata.Provider);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsValidationFailureWhenProviderJsonIsInvalid()
    {
        var settings = JournalAiSettings.CreateDefault();
        var custom = settings.Providers.Single(item => item.Id == "custom") with
        {
            BaseUrl = "http://localhost:11434/v1",
            Model = "local-model",
            ApiKey = "local-key",
            IsEnabled = true
        };
        settings = settings with
        {
            ActiveProviderId = "custom",
            Providers = settings.Providers.Select(item => item.Id == "custom" ? custom : item).ToArray()
        };
        var service = new JournalAiGenerationService(
            new StaticSettingsService(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new StaticRuntime(OpenAiCompatibleRunResult.Success(CreateInvalidAiJson(), "{}", TimeSpan.Zero))));
        var date = JournalDate.From(new DateOnly(2026, 5, 10));

        var result = await service.GenerateAsync(
            date,
            [],
            DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
            providerIdOverride: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains("schema must be journal-entry/v1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_WithUnknownProviderOverrideReturnsProviderNotFound()
    {
        var settings = JournalAiSettings.CreateDefault();
        var service = new JournalAiGenerationService(
            new StaticSettingsService(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new ThrowingRuntime()));
        var date = JournalDate.From(new DateOnly(2026, 5, 10));

        var result = await service.GenerateAsync(
            date,
            [],
            DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
            providerIdOverride: "missing-provider",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.AiJson);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("provider_not_found", error.Code);
        Assert.Contains("missing-provider", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WithUnknownProviderReturnsProviderNotFound()
    {
        var settings = JournalAiSettings.CreateDefault();
        var service = new JournalAiGenerationService(
            new StaticSettingsService(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new ThrowingRuntime()));

        var result = await service.CheckAsync("missing-provider", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("provider_not_found", result.Status);
        var error = Assert.IsType<JournalAiSafeError>(result.Error);
        Assert.Equal("provider_not_found", error.Code);
        Assert.Contains("missing-provider", error.Message, StringComparison.Ordinal);
    }

    private static JournalAiJson CreateInvalidAiJson() =>
        new(
            "legacy",
            "2026-05-10",
            "05-10",
            "draft",
            [],
            [],
            "未标注",
            [],
            [],
            [],
            []);

    private sealed class StaticSettingsService(JournalAiSettings settings) : IJournalAiSettingsReader
    {
        public Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(settings);
    }

    private sealed class StaticRuntime(OpenAiCompatibleRunResult result) : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingRuntime : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("OpenAI-compatible runtime should not be called for mock provider.");
    }
}
