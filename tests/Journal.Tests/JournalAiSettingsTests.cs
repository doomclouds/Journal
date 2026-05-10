using System.Text.Json;
using System.Text;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalAiSettingsTests
{
    [Fact]
    public async Task ReadEffectiveAsync_ReturnsMockWhenNothingIsConfigured()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("mock", view.ActiveProviderId);
        Assert.Equal("OpenAI-compatible runtime · Agent Framework 1.5.0", view.Runtime);
        Assert.Contains(view.Providers, provider => provider.Id == "mock" && provider.IsActive && provider.Source == "default");
        Assert.Contains(view.Providers, provider => provider.Id == "openai" && provider.Model == "gpt-5.4" && provider.Source == "preset");
        Assert.Contains(view.Providers, provider => provider.Id == "deepseek" && provider.Model == "deepseek-v4-flash" && provider.Source == "preset");
        Assert.Contains(view.Providers, provider => provider.Id == "zhipu" && provider.Model == "glm-5.1" && provider.Source == "preset");
    }

    [Fact]
    public async Task ReadEffectiveAsync_EnvironmentOverridesFileAndDoesNotExposeApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAiSettingsStore(paths);
        await store.WriteAsync(JournalAiSettings.CreateDefault() with { ActiveProviderId = "mock" }, CancellationToken.None);

        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["JOURNAL_AI_PROVIDER"] = "deepseek",
            ["JOURNAL_AI_BASE_URL"] = "https://api.deepseek.com",
            ["JOURNAL_AI_MODEL"] = "deepseek-v4-flash",
            ["JOURNAL_AI_API_KEY"] = "secret-value"
        });

        var view = await service.ReadViewAsync(CancellationToken.None);

        var active = Assert.Single(view.Providers, provider => provider.IsActive);
        Assert.Equal("deepseek", active.Id);
        Assert.Equal("environment", active.Source);
        Assert.True(active.HasApiKey);
        Assert.DoesNotContain("secret-value", JsonSerializer.Serialize(view));
    }

    [Fact]
    public async Task ReadViewAsync_ReturnsFileSourceWhenSavedValuesMatchPresetDefaults()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAiSettingsStore(paths);
        await store.WriteAsync(JournalAiSettings.CreateDefault() with { ActiveProviderId = "openai" }, CancellationToken.None);
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("openai", view.ActiveProviderId);
        var openAi = Assert.Single(view.Providers, provider => provider.Id == "openai");
        Assert.True(openAi.IsActive);
        Assert.Equal("gpt-5.4", openAi.Model);
        Assert.Equal("file", openAi.Source);
    }

    [Fact]
    public async Task ReadViewAsync_ReturnsEnvironmentSourceWhenEnvironmentSelectsSameFileBackedProvider()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAiSettingsStore(paths);
        var fileSettings = JournalAiSettings.CreateDefault();
        await store.WriteAsync(
            fileSettings with
            {
                ActiveProviderId = "openai",
                Providers = fileSettings.Providers
                    .Select(provider => provider.Id == "openai" ? provider with { IsEnabled = true } : provider)
                    .ToArray()
            },
            CancellationToken.None);
        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["JOURNAL_AI_PROVIDER"] = "openai"
        });

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("openai", view.ActiveProviderId);
        var openAi = Assert.Single(view.Providers, provider => provider.Id == "openai");
        Assert.True(openAi.IsActive);
        Assert.Equal("gpt-5.4", openAi.Model);
        Assert.Equal("environment", openAi.Source);
    }

    [Fact]
    public async Task SaveAsync_DoesNotOverwriteEnvironmentBackedApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["JOURNAL_AI_PROVIDER"] = "openai",
            ["OPENAI_API_KEY"] = "env-key"
        });

        await service.SaveAsync(new JournalAiSettingsSaveRequest(
            "openai",
            [
                new JournalAiProviderSaveRequest(
                    "openai",
                    "openai-compatible",
                    "OpenAI",
                    "openai",
                    "https://api.openai.com/v1",
                    "gpt-5.4",
                    "",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var settingsPath = new LocalJournalPaths(new JournalStorageOptions(workspace.Root)).AiSettingsPath();
        var fileText = await File.ReadAllTextAsync(settingsPath, CancellationToken.None);

        Assert.DoesNotContain("env-key", fileText);
    }

    [Fact]
    public async Task SaveAsync_PreservesExistingFileBackedApiKeyWhenRequestApiKeyIsBlank()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAiSettingsStore(paths);

        await service.SaveAsync(new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-v4-flash",
                    "file-backed-secret",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        await service.SaveAsync(new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-v4-flash",
                    "",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var persisted = await store.ReadAsync(CancellationToken.None);
        var provider = Assert.Single(persisted.Providers, item => item.Id == "deepseek");
        Assert.Equal("file-backed-secret", provider.ApiKey);

        var view = await service.ReadViewAsync(CancellationToken.None);
        var providerView = Assert.Single(view.Providers, item => item.Id == "deepseek");
        Assert.True(providerView.HasApiKey);
    }

    [Fact]
    public async Task SaveAsync_RejectsMalformedRequestsWithArgumentException()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        var nullProviders = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new JournalAiSettingsSaveRequest("openai", null!), CancellationToken.None));
        Assert.Contains("providers", nullProviders.Message, StringComparison.OrdinalIgnoreCase);

        var emptyProviders = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new JournalAiSettingsSaveRequest("openai", []), CancellationToken.None));
        Assert.Contains("providers", emptyProviders.Message, StringComparison.OrdinalIgnoreCase);

        var nullProvider = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new JournalAiSettingsSaveRequest("openai", [null!]), CancellationToken.None));
        Assert.Contains("provider", nullProvider.Message, StringComparison.OrdinalIgnoreCase);

        var nullProviderId = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new JournalAiSettingsSaveRequest(
                "openai",
                [
                    new JournalAiProviderSaveRequest(
                        null!,
                        "openai-compatible",
                        "OpenAI",
                        "openai",
                        "https://api.openai.com/v1",
                        "gpt-5.4",
                        "",
                        true,
                        45,
                        0.2,
                        1200,
                        "faithful")
                ]), CancellationToken.None));
        Assert.Contains("provider id", nullProviderId.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadViewAsync_FallsBackToDefaultWhenSettingsFileHasNullProviders()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var settingsPath = paths.AiSettingsPath();
        LocalJournalPaths.EnsureParentDirectory(settingsPath);
        await File.WriteAllTextAsync(
            settingsPath,
            """{ "activeProviderId": "openai", "providers": null }""",
            Encoding.UTF8,
            CancellationToken.None);
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("mock", view.ActiveProviderId);
        Assert.Contains(view.Providers, provider => provider.Id == "mock" && provider.IsActive && provider.Source == "default");
    }

    [Fact]
    public async Task ReadViewAsync_FallsBackToDefaultWhenSettingsFileContainsNullProviderFields()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var settingsPath = paths.AiSettingsPath();
        LocalJournalPaths.EnsureParentDirectory(settingsPath);
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "activeProviderId": "openai",
              "providers": [
                {
                  "id": null,
                  "type": "openai-compatible",
                  "displayName": null,
                  "preset": "openai",
                  "baseUrl": "https://api.openai.com/v1",
                  "model": "gpt-5.4",
                  "apiKey": "",
                  "isEnabled": true,
                  "timeoutSeconds": 45,
                  "temperature": 0.2,
                  "maxTokens": 1200,
                  "stylePreset": "faithful"
                }
              ]
            }
            """,
            Encoding.UTF8,
            CancellationToken.None);
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("mock", view.ActiveProviderId);
        Assert.DoesNotContain(view.Providers, provider => provider.Id is null);
        Assert.Contains(view.Providers, provider => provider.Id == "openai" && provider.Source == "preset");
    }

    [Fact]
    public async Task ReadViewAsync_AutoDetectsProviderSpecificOpenAiApiKeyWithoutExposingValue()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["OPENAI_API_KEY"] = "openai-secret"
        });

        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("openai", view.ActiveProviderId);
        var openAi = Assert.Single(view.Providers, provider => provider.Id == "openai");
        Assert.True(openAi.IsActive);
        Assert.Equal("environment", openAi.Source);
        Assert.True(openAi.HasApiKey);
        Assert.DoesNotContain("openai-secret", JsonSerializer.Serialize(view));
    }

    [Fact]
    public async Task ReadViewAsync_ReturnsMaskedPreviewForFileBackedApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        await service.SaveAsync(new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-v4-flash",
                    "sk-file-backed-secret-4A7C",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var view = await service.ReadViewAsync(CancellationToken.None);
        var provider = Assert.Single(view.Providers, item => item.Id == "deepseek");
        var serialized = JsonSerializer.Serialize(view);

        Assert.True(provider.HasApiKey);
        Assert.True(provider.CanRevealApiKey);
        Assert.Equal("sk-••••••••••••••••4A7C", provider.ApiKeyPreview);
        Assert.DoesNotContain("sk-file-backed-secret-4A7C", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadViewAsync_DoesNotRevealEnvironmentBackedApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["OPENAI_API_KEY"] = "sk-env-secret"
        });

        var view = await service.ReadViewAsync(CancellationToken.None);
        var provider = Assert.Single(view.Providers, item => item.Id == "openai");
        var serialized = JsonSerializer.Serialize(view);

        Assert.True(provider.HasApiKey);
        Assert.False(provider.CanRevealApiKey);
        Assert.Equal("", provider.ApiKeyPreview);
        Assert.DoesNotContain("sk-env-secret", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFileApiKeyAsync_ReturnsOnlyFileBackedProviderKey()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>());

        await service.SaveAsync(new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-v4-flash",
                    "sk-file-backed-secret",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var key = await service.ReadFileApiKeyAsync("deepseek", CancellationToken.None);
        var missing = await service.ReadFileApiKeyAsync("openai", CancellationToken.None);

        Assert.NotNull(key);
        Assert.Equal("deepseek", key.ProviderId);
        Assert.Equal("file", key.Source);
        Assert.Equal("sk-file-backed-secret", key.ApiKey);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ReadFileApiKeyAsync_ReturnsNullWhenProviderIsEnvironmentBacked()
    {
        using var workspace = TempWorkspace.Create();
        var fileService = CreateService(workspace.Root, new Dictionary<string, string?>());

        await fileService.SaveAsync(new JournalAiSettingsSaveRequest(
            "openai",
            [
                new JournalAiProviderSaveRequest(
                    "openai",
                    "openai-compatible",
                    "OpenAI",
                    "openai",
                    "https://api.openai.com/v1",
                    "gpt-5.4",
                    "sk-file-secret",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var envService = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["OPENAI_API_KEY"] = "sk-env-secret"
        });

        var view = await envService.ReadViewAsync(CancellationToken.None);
        var provider = Assert.Single(view.Providers, item => item.Id == "openai");
        var apiKey = await envService.ReadFileApiKeyAsync("openai", CancellationToken.None);

        Assert.Equal("environment", provider.Source);
        Assert.False(provider.CanRevealApiKey);
        Assert.Null(apiKey);
    }

    [Fact]
    public async Task BuildEffectiveCandidateAsync_PreservesBlankFileKeyAndAppliesEnvironmentOverlay()
    {
        using var workspace = TempWorkspace.Create();
        var fileService = CreateService(workspace.Root, new Dictionary<string, string?>());
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var store = new JournalAiSettingsStore(paths);

        await fileService.SaveAsync(new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-v4-flash",
                    "file-backed-secret",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]), CancellationToken.None);

        var candidateRequest = new JournalAiSettingsSaveRequest(
            "deepseek",
            [
                new JournalAiProviderSaveRequest(
                    "deepseek",
                    "openai-compatible",
                    "DeepSeek",
                    "deepseek",
                    "https://api.deepseek.com",
                    "deepseek-candidate",
                    "",
                    true,
                    45,
                    0.2,
                    1200,
                    "faithful")
            ]);

        var fileCandidate = await fileService.BuildEffectiveCandidateAsync(candidateRequest, CancellationToken.None);
        var fileProvider = Assert.Single(fileCandidate.Providers, item => item.Id == "deepseek");

        Assert.Equal("file-backed-secret", fileProvider.ApiKey);
        Assert.Equal("deepseek-candidate", fileProvider.Model);

        var envService = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["DEEPSEEK_API_KEY"] = "env-secret"
        });

        var envCandidate = await envService.BuildEffectiveCandidateAsync(candidateRequest, CancellationToken.None);
        var envProvider = Assert.Single(envCandidate.Providers, item => item.Id == "deepseek");

        Assert.Equal("env-secret", envProvider.ApiKey);
        Assert.Equal("deepseek-candidate", envProvider.Model);

        var persisted = await store.ReadAsync(CancellationToken.None);
        var persistedProvider = Assert.Single(persisted.Providers, item => item.Id == "deepseek");
        Assert.Equal("file-backed-secret", persistedProvider.ApiKey);
    }

    [Fact]
    public async Task ReadEffectiveAsync_PreservesUnknownEnvironmentProviderOverride()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateService(workspace.Root, new Dictionary<string, string?>
        {
            ["JOURNAL_AI_PROVIDER"] = "missing-provider"
        });

        var effective = await service.ReadEffectiveAsync(CancellationToken.None);
        var view = await service.ReadViewAsync(CancellationToken.None);

        Assert.Equal("missing-provider", effective.ActiveProviderId);
        Assert.Equal("missing-provider", view.ActiveProviderId);
        Assert.DoesNotContain(view.Providers, provider => provider.IsActive);
    }

    private static JournalAiSettingsService CreateService(string root, IReadOnlyDictionary<string, string?> env) =>
        new(
            new JournalAiSettingsStore(new LocalJournalPaths(new JournalStorageOptions(root))),
            new DictionaryJournalAiEnvironment(env));

    private sealed class DictionaryJournalAiEnvironment(IReadOnlyDictionary<string, string?> values) : IJournalAiEnvironment
    {
        public string? Get(string name) => values.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-ai-settings-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
