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
