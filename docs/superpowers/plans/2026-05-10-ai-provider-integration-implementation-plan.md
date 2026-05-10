# LLM Provider Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 5 real AI provider slice so Journal can use OpenAI-compatible LLMs through Microsoft Agent Framework while keeping Mock as the unconfigured default.

**Architecture:** Add an AI settings layer, environment overlay, provider runtime, and generation service behind the existing `TodayJournalService`. Real providers produce `JournalAiJson` only; `TodayJournalService` still owns raw input persistence, JMF rendering, draft state, and formal entry writes. The desktop app exposes the Huashu-style LLM configuration panel from the top status strip and calls the new settings/regenerate endpoints.

**Tech Stack:** .NET 10, ASP.NET Core minimal API, xUnit, Microsoft Agent Framework `Microsoft.Agents.AI` / `Microsoft.Agents.AI.OpenAI` 1.5.0, OpenAI .NET SDK 2.10.0, React + TypeScript + Vite + Vitest.

---

## Scope Check

This is one vertical slice because the backend provider runtime, settings API, and desktop configuration UI must ship together to make the feature testable. Keep the implementation to Phase 5 only:

- No streaming UI.
- No model list endpoint.
- No token pricing.
- No encryption for the settings file.
- No SQLite index or version snapshots.
- No production Electron-managed API process.

The approved design is `docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md`; the approved UI direction is `docs/superpowers/specs/2026-05-10-ai-provider-integration-prototype.html`.

## Provider Model Defaults

Default model IDs are calibrated from official provider docs and product decisions on 2026-05-10:

- OpenAI / ChatGPT: `gpt-5.4`, by explicit product decision for this integration slice.
- DeepSeek: `deepseek-v4-flash`; do not use the older `deepseek-chat` / `deepseek-reasoner` IDs because DeepSeek marks them deprecated and scheduled to stop on 2026-07-24.
- 智谱 GLM: `glm-5.1`.

Sources: [DeepSeek API docs](https://api-docs.deepseek.com/), [DeepSeek updates](https://api-docs.deepseek.com/zh-cn/updates), [智谱 GLM-5.1](https://docs.bigmodel.cn/cn/guide/models/text/glm-5.1).

## Current Baseline

- `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs` is synchronous and returns only `JournalAiJson`.
- `src/Journal.Infrastructure/Ai/MockAiProvider.cs` is deterministic and safe for tests.
- `src/Journal.Infrastructure/Today/TodayJournalService.cs` directly calls `_aiProvider.Generate(...)`.
- `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs` currently hard-codes `provider: mock`, `model: mock-journal`, and `prompt_version: mock-journal-entry-v1`.
- `src/Journal.Api/Program.cs` registers `IJournalAiProvider` as `MockAiProvider` and exposes only journal/editor endpoints.
- `apps/desktop/src/App.tsx` shows status and API health, but no LLM provider status or settings panel.

## File Structure

### Backend Creates

- `src/Journal.Domain/Entries/JournalAiMetadata.cs`
  - Durable front matter metadata: provider, model, prompt version.
- `src/Journal.Infrastructure/Ai/JournalAiGenerationRequest.cs`
  - Request passed from today workflow into provider generation.
- `src/Journal.Infrastructure/Ai/JournalAiProviderResult.cs`
  - Success/failure union for provider generation.
- `src/Journal.Infrastructure/Ai/JournalAiProviderHealthResult.cs`
  - Safe health-check result.
- `src/Journal.Infrastructure/Ai/JournalAiSafeError.cs`
  - Safe error summary and redacted technical details.
- `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`
  - Persisted provider settings and public safe view projection.
- `src/Journal.Infrastructure/Ai/JournalAiSettings.cs`
  - Persisted settings root.
- `src/Journal.Infrastructure/Ai/JournalAiSettingsStore.cs`
  - Reads/writes `%LocalAppData%/Journal/.journal/settings/ai-providers.json`.
- `src/Journal.Infrastructure/Ai/JournalAiEnvironment.cs`
  - Reads environment variables through an injectable abstraction.
- `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`
  - Resolves effective settings, overlays environment variables, and returns safe views.
- `src/Journal.Infrastructure/Ai/JournalAiPrompt.cs`
  - Faithful prompt builder and prompt version constant.
- `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`
  - Testable runtime boundary for OpenAI-compatible calls.
- `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`
  - Microsoft Agent Framework + OpenAI SDK runtime implementation.
- `src/Journal.Infrastructure/Ai/OpenAiCompatibleJournalAiProvider.cs`
  - Real provider adapter with safe error classification.
- `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`
  - Orchestrates active provider selection, generation, validation, and metadata.

### Backend Modifies

- `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`
  - Convert to async result-based provider contract.
- `src/Journal.Infrastructure/Ai/MockAiProvider.cs`
  - Implement async provider contract and health check.
- `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`
  - Accept dynamic `JournalAiMetadata` while preserving Mock defaults.
- `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
  - Add `AiSettingsPath()`.
- `src/Journal.Infrastructure/Today/TodayJournalService.cs`
  - Use `JournalAiGenerationService`; add regenerate draft flow.
- `src/Journal.Infrastructure/Journal.Infrastructure.csproj`
  - Add package references.
- `src/Journal.Api/Program.cs`
  - Register AI services and expose settings/regenerate endpoints.

### Backend Tests

- `tests/Journal.Tests/MockAiAndJmfTests.cs`
- `tests/Journal.Tests/TodayJournalServiceTests.cs`
- `tests/Journal.Tests/TodayJournalEndpointTests.cs`
- Create `tests/Journal.Tests/JournalAiSettingsTests.cs`
- Create `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`
- Create `tests/Journal.Tests/OpenAiCompatibleJournalAiProviderTests.cs`

### Frontend Creates

- `apps/desktop/src/LlmSettingsPanel.tsx`
  - LLM configuration panel matching the approved prototype.

### Frontend Modifies

- `apps/desktop/src/api.ts`
  - Add AI settings DTOs and client methods.
- `apps/desktop/src/App.tsx`
  - Load AI settings, show `LLM <provider>`, open panel, refresh after save/test/regenerate.
- `apps/desktop/src/App.test.tsx`
  - Update initial fetch expectations and add AI panel tests.
- `apps/desktop/src/styles.css`
  - Add panel, provider cards, safe technical details, and top AI pill styles.

---

## Task 1: Add Packages, Metadata, and Async AI Contracts

**Files:**
- Modify: `src/Journal.Infrastructure/Journal.Infrastructure.csproj`
- Create: `src/Journal.Domain/Entries/JournalAiMetadata.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiGenerationRequest.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiSafeError.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiProviderResult.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiProviderHealthResult.cs`
- Modify: `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`
- Modify: `src/Journal.Infrastructure/Ai/MockAiProvider.cs`
- Modify: `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`
- Test: `tests/Journal.Tests/MockAiAndJmfTests.cs`

- [ ] **Step 1: Write failing metadata renderer tests**

Add these tests to `tests/Journal.Tests/MockAiAndJmfTests.cs`:

```csharp
[Fact]
public void JmfMarkdownRenderer_WritesProviderMetadataFromGenerationResult()
{
    var markdown = JmfMarkdownRenderer.Render(
        CreateAiJson(),
        DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
        new JournalAiMetadata(
            Provider: "deepseek",
            Model: "deepseek-v4-flash",
            PromptVersion: "journal-entry-json-v1"));

    Assert.Contains("provider: deepseek", markdown);
    Assert.Contains("model: deepseek-v4-flash", markdown);
    Assert.Contains("prompt_version: journal-entry-json-v1", markdown);
    Assert.Contains("generated_at: \"2026-05-10T08:30:00.0000000+08:00\"", markdown);
}

[Fact]
public void JmfMarkdownRenderer_DoesNotWriteProviderSecrets()
{
    var markdown = JmfMarkdownRenderer.Render(
        CreateAiJson(),
        DateTimeOffset.Parse("2026-05-10T08:30:00+08:00"),
        new JournalAiMetadata("custom", "local-model", "journal-entry-json-v1"));

    Assert.DoesNotContain("api_key", markdown, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("base_url", markdown, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("request_id", markdown, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("raw_response", markdown, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run metadata tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~MockAiAndJmfTests"
```

Expected: fail because `JournalAiMetadata` does not exist and `JmfMarkdownRenderer.Render` has no metadata overload.

- [ ] **Step 3: Add package references**

Modify `src/Journal.Infrastructure/Journal.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.Agents.AI" Version="1.5.0" />
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.5.0" />
    <PackageReference Include="OpenAI" Version="2.10.0" />
    <ProjectReference Include="..\Journal.Domain\Journal.Domain.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Add AI metadata record**

Create `src/Journal.Domain/Entries/JournalAiMetadata.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalAiMetadata(
    string Provider,
    string Model,
    string PromptVersion)
{
    public static JournalAiMetadata Mock { get; } =
        new("mock", "mock-journal", "mock-journal-entry-v1");
}
```

- [ ] **Step 5: Add provider result contracts**

Create `src/Journal.Infrastructure/Ai/JournalAiGenerationRequest.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiGenerationRequest(
    JournalDate Date,
    IReadOnlyList<RawInput> RawInputs,
    DateTimeOffset GeneratedAt,
    JournalAiProviderSettings Settings);
```

Create `src/Journal.Infrastructure/Ai/JournalAiSafeError.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public sealed record JournalAiSafeError(
    string Stage,
    string Code,
    string Message,
    string TechnicalDetails)
{
    public static JournalAiSafeError Create(string stage, string code, string message, string technicalDetails) =>
        new(stage, code, message, Redact(technicalDetails));

    public static string Redact(string value) =>
        value
            .Replace("Authorization", "[redacted-header]", StringComparison.OrdinalIgnoreCase)
            .Replace("api_key", "[redacted-key-name]", StringComparison.OrdinalIgnoreCase)
            .Replace("apikey", "[redacted-key-name]", StringComparison.OrdinalIgnoreCase);
}
```

Create `src/Journal.Infrastructure/Ai/JournalAiProviderResult.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiProviderResult(
    bool IsSuccess,
    JournalAiJson? AiJson,
    JournalAiMetadata Metadata,
    JournalAiSafeError? Error)
{
    public static JournalAiProviderResult Success(JournalAiJson aiJson, JournalAiMetadata metadata) =>
        new(true, aiJson, metadata, null);

    public static JournalAiProviderResult Failure(JournalAiMetadata metadata, JournalAiSafeError error) =>
        new(false, null, metadata, error);
}
```

Create `src/Journal.Infrastructure/Ai/JournalAiProviderHealthResult.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public sealed record JournalAiProviderHealthResult(
    bool IsSuccess,
    string Status,
    string SafeResponseSnippet,
    int? HttpStatus,
    TimeSpan? Latency,
    JournalAiSafeError? Error)
{
    public static JournalAiProviderHealthResult Success(string safeResponseSnippet, TimeSpan latency, int? httpStatus = 200) =>
        new(true, "success", safeResponseSnippet, httpStatus, latency, null);

    public static JournalAiProviderHealthResult Failure(string status, int? httpStatus, TimeSpan? latency, JournalAiSafeError error) =>
        new(false, status, string.Empty, httpStatus, latency, error);
}
```

- [ ] **Step 6: Replace provider interface**

Replace `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs` with:

```csharp
namespace Journal.Infrastructure.Ai;

public interface IJournalAiProvider
{
    string ProviderId { get; }

    Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken);

    Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 7: Update Mock provider**

In `src/Journal.Infrastructure/Ai/MockAiProvider.cs`, keep the deterministic extraction helpers and change the public contract to:

```csharp
public sealed class MockAiProvider : IJournalAiProvider
{
    private const string Schema = "journal-entry/v1";

    public string ProviderId => "mock";

    public Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var aiJson = GenerateJson(request.Date, request.RawInputs);
        return Task.FromResult(JournalAiProviderResult.Success(aiJson, JournalAiMetadata.Mock));
    }

    public Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken) =>
        Task.FromResult(JournalAiProviderHealthResult.Success("{\"ok\":true}", TimeSpan.Zero));

    private static JournalAiJson GenerateJson(JournalDate date, IReadOnlyList<RawInput> rawInputs)
    {
        var inputTexts = rawInputs
            .Select(input => input.Text.Trim())
            .Where(text => text.Length > 0)
            .ToArray();

        if (inputTexts.Length == 0)
        {
            inputTexts = ["（无原始输入）"];
        }

        var tags = ExtractTags(inputTexts);

        return new JournalAiJson(
            Schema,
            date.IsoDate,
            date.MonthDay,
            "draft",
            tags,
            tags.Count > 0 ? tags : ["日记"],
            ExtractMood(inputTexts),
            inputTexts,
            ExtractSection(inputTexts, YesterdayKeywords, "记录了今天之前的上下文。"),
            ExtractSection(inputTexts, TodayKeywords, "整理今天的重点。"),
            ExtractSection(inputTexts, InspirationKeywords, "暂无灵感补充。"));
    }
}
```

Keep the existing regex and helper methods below `GenerateJson`.

- [ ] **Step 8: Update renderer metadata**

Change the `Render` signature in `src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs`:

```csharp
public static string Render(
    JournalAiJson aiJson,
    DateTimeOffset generatedAt,
    JournalAiMetadata? metadata = null)
{
    metadata ??= JournalAiMetadata.Mock;
```

Replace the hard-coded front matter writes:

```csharp
AppendScalar(builder, "provider", metadata.Provider);
AppendScalar(builder, "model", metadata.Model);
AppendScalar(builder, "prompt_version", metadata.PromptVersion);
```

Remove the old constants `Provider`, `Model`, and `PromptVersion`.

- [ ] **Step 9: Run renderer tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~MockAiAndJmfTests"
```

Expected: pass after any call sites are updated to the async provider contract in later tasks. If the build fails because `TodayJournalService` still calls `Generate`, proceed to Task 4 before running the full suite.

- [ ] **Step 10: Commit Task 1**

```powershell
git add src/Journal.Domain/Entries/JournalAiMetadata.cs src/Journal.Infrastructure/Journal.Infrastructure.csproj src/Journal.Infrastructure/Ai src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs tests/Journal.Tests/MockAiAndJmfTests.cs
git commit -m "feat: add ai provider contracts"
```

---

## Task 2: Settings Store and Environment Overlay

**Files:**
- Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiSettings.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiEnvironment.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiSettingsStore.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`
- Test: `tests/Journal.Tests/JournalAiSettingsTests.cs`

- [ ] **Step 1: Write failing settings tests**

Create `tests/Journal.Tests/JournalAiSettingsTests.cs`:

```csharp
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
        Assert.Contains(view.Providers, provider => provider.Id == "mock" && provider.IsActive);
        Assert.Contains(view.Providers, provider => provider.Id == "deepseek" && provider.Source == "preset");
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

        var active = Assert.Single(view.Providers.Where(provider => provider.IsActive));
        Assert.Equal("deepseek", active.Id);
        Assert.Equal("environment", active.Source);
        Assert.True(active.HasApiKey);
        Assert.DoesNotContain("secret-value", System.Text.Json.JsonSerializer.Serialize(view));
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
```

- [ ] **Step 2: Run settings tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiSettingsTests"
```

Expected: fail because settings types do not exist.

- [ ] **Step 3: Add AI settings path**

Add this method to `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`:

```csharp
public string AiSettingsPath() =>
    Path.Combine(_rootDirectory, ".journal", "settings", "ai-providers.json");
```

- [ ] **Step 4: Add settings records and safe view DTOs**

Create `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`:

```csharp
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
    public bool IsMock => string.Equals(Type, "mock", StringComparison.OrdinalIgnoreCase);

    public JournalAiMetadata ToMetadata() =>
        new(Id, string.IsNullOrWhiteSpace(Model) ? "mock-journal" : Model, JournalAiPrompt.Version);
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
```

Create `src/Journal.Infrastructure/Ai/JournalAiSettings.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public sealed record JournalAiSettings(
    string ActiveProviderId,
    IReadOnlyList<JournalAiProviderSettings> Providers)
{
    public static JournalAiSettings CreateDefault() =>
        new(
            "mock",
            [
                new("mock", "mock", "Mock", "mock", "local", "mock-journal", "", true, 1, 0.0, 0, "faithful"),
                new("openai", "openai-compatible", "OpenAI", "openai", "https://api.openai.com/v1", "gpt-5.4", "", false, 45, 0.2, 1200, "faithful"),
                new("deepseek", "openai-compatible", "DeepSeek", "deepseek", "https://api.deepseek.com", "deepseek-v4-flash", "", false, 45, 0.2, 1200, "faithful"),
                new("zhipu", "openai-compatible", "智谱 GLM", "zhipu", "https://open.bigmodel.cn/api/paas/v4", "glm-5.1", "", false, 45, 0.2, 1200, "faithful"),
                new("custom", "openai-compatible", "Custom", "custom", "http://localhost:11434/v1", "", "", false, 45, 0.2, 1200, "faithful")
            ]);
}
```

- [ ] **Step 5: Add environment abstraction**

Create `src/Journal.Infrastructure/Ai/JournalAiEnvironment.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public interface IJournalAiEnvironment
{
    string? Get(string name);
}

public sealed class SystemJournalAiEnvironment : IJournalAiEnvironment
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
```

- [ ] **Step 6: Add settings store**

Create `src/Journal.Infrastructure/Ai/JournalAiSettingsStore.cs`:

```csharp
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

    public async Task<JournalAiSettings> ReadAsync(CancellationToken cancellationToken)
    {
        var path = _paths.AiSettingsPath();
        if (!File.Exists(path))
        {
            return JournalAiSettings.CreateDefault();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<JournalAiSettings>(json, JsonOptions)
            ?? JournalAiSettings.CreateDefault();
    }

    public async Task WriteAsync(JournalAiSettings settings, CancellationToken cancellationToken)
    {
        var path = _paths.AiSettingsPath();
        LocalJournalPaths.EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
    }
}
```

- [ ] **Step 7: Add settings service**

Create `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public sealed class JournalAiSettingsService
{
    private readonly JournalAiSettingsStore _store;
    private readonly IJournalAiEnvironment _environment;

    public JournalAiSettingsService(JournalAiSettingsStore store, IJournalAiEnvironment environment)
    {
        _store = store;
        _environment = environment;
    }

    public async Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken)
    {
        var fileSettings = await _store.ReadAsync(cancellationToken);
        return ApplyEnvironment(fileSettings);
    }

    public async Task<JournalAiSettingsView> ReadViewAsync(CancellationToken cancellationToken)
    {
        var fileSettings = await _store.ReadAsync(cancellationToken);
        var effective = ApplyEnvironment(fileSettings);

        return new JournalAiSettingsView(
            effective.ActiveProviderId,
            "OpenAI-compatible runtime · Agent Framework 1.5.0",
            effective.Providers.Select(provider => ToView(provider, effective.ActiveProviderId, SourceOf(provider, fileSettings))).ToArray());
    }

    public async Task SaveAsync(JournalAiSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        var settings = new JournalAiSettings(
            string.IsNullOrWhiteSpace(request.ActiveProviderId) ? "mock" : request.ActiveProviderId.Trim(),
            request.Providers.Select(provider => new JournalAiProviderSettings(
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
                provider.StylePreset.Trim())).ToArray());

        await _store.WriteAsync(settings, cancellationToken);
    }

    private JournalAiSettings ApplyEnvironment(JournalAiSettings settings)
    {
        var providerId = _environment.Get("JOURNAL_AI_PROVIDER")?.Trim();
        var baseUrl = _environment.Get("JOURNAL_AI_BASE_URL")?.Trim();
        var model = _environment.Get("JOURNAL_AI_MODEL")?.Trim();
        var apiKey = _environment.Get("JOURNAL_AI_API_KEY")?.Trim();

        if (string.IsNullOrWhiteSpace(providerId))
        {
            providerId = FirstConfiguredProviderKeyName() switch
            {
                "OPENAI_API_KEY" => "openai",
                "DEEPSEEK_API_KEY" => "deepseek",
                "ZHIPU_API_KEY" => "zhipu",
                _ => settings.ActiveProviderId
            };
        }

        apiKey = string.IsNullOrWhiteSpace(apiKey) ? FirstConfiguredProviderKeyValue(providerId) : apiKey;
        if (string.IsNullOrWhiteSpace(apiKey) && string.Equals(providerId, "mock", StringComparison.OrdinalIgnoreCase))
        {
            return settings with { ActiveProviderId = "mock" };
        }

        var providers = settings.Providers.Select(provider =>
        {
            if (!string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase))
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

        return settings with { ActiveProviderId = providerId, Providers = providers };
    }

    private string? FirstConfiguredProviderKeyName()
    {
        foreach (var name in new[] { "OPENAI_API_KEY", "DEEPSEEK_API_KEY", "ZHIPU_API_KEY" })
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
            "openai" => _environment.Get("OPENAI_API_KEY"),
            "deepseek" => _environment.Get("DEEPSEEK_API_KEY"),
            "zhipu" => _environment.Get("ZHIPU_API_KEY"),
            _ => null
        };

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
            !string.IsNullOrWhiteSpace(provider.ApiKey) || provider.IsMock,
            source,
            provider.TimeoutSeconds,
            provider.Temperature,
            provider.MaxTokens,
            provider.StylePreset,
            "not-tested");

    private static string SourceOf(JournalAiProviderSettings provider, JournalAiSettings fileSettings)
    {
        var fileProvider = fileSettings.Providers.FirstOrDefault(item => item.Id == provider.Id);
        if (fileProvider is null)
        {
            return "preset";
        }

        if (!string.Equals(fileProvider.ApiKey, provider.ApiKey, StringComparison.Ordinal)
            || !string.Equals(fileProvider.Model, provider.Model, StringComparison.Ordinal)
            || !string.Equals(fileProvider.BaseUrl, provider.BaseUrl, StringComparison.Ordinal))
        {
            return "environment";
        }

        return provider.IsMock ? "default" : "file";
    }
}
```

- [ ] **Step 8: Run settings tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiSettingsTests"
```

Expected: pass.

- [ ] **Step 9: Commit Task 2**

```powershell
git add src/Journal.Infrastructure/Storage/LocalJournalPaths.cs src/Journal.Infrastructure/Ai tests/Journal.Tests/JournalAiSettingsTests.cs
git commit -m "feat: add ai settings resolution"
```

---

## Task 3: Prompt, Agent Runtime, and OpenAI-Compatible Provider

**Files:**
- Create: `src/Journal.Infrastructure/Ai/JournalAiPrompt.cs`
- Create: `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`
- Create: `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`
- Create: `src/Journal.Infrastructure/Ai/OpenAiCompatibleJournalAiProvider.cs`
- Create: `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`
- Test: `tests/Journal.Tests/OpenAiCompatibleJournalAiProviderTests.cs`
- Test: `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`

- [ ] **Step 1: Write failing provider tests**

Create `tests/Journal.Tests/OpenAiCompatibleJournalAiProviderTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Tests;

public sealed class OpenAiCompatibleJournalAiProviderTests
{
    [Fact]
    public async Task GenerateAsync_SendsFaithfulPromptAndJsonObjectMode()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var settings = new JournalAiProviderSettings(
            "deepseek",
            "openai-compatible",
            "DeepSeek",
            "deepseek",
            "https://api.deepseek.com",
            "deepseek-v4-flash",
            "secret-key",
            true,
            45,
            0.2,
            1200,
            "faithful");
        var runtime = new CapturingRuntime(CreateAiJson(date));
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);

        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(
                date,
                [new RawInput("raw-1", date, DateTimeOffset.Parse("2026-05-10T08:00:00+08:00"), "text", "今天接入真实 AI #Journal")],
                DateTimeOffset.Parse("2026-05-10T08:10:00+08:00"),
                settings),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("deepseek", result.Metadata.Provider);
        Assert.Equal("deepseek-v4-flash", result.Metadata.Model);
        Assert.Equal("journal-entry-json-v1", result.Metadata.PromptVersion);
        Assert.NotNull(runtime.LastRequest);
        Assert.Equal("deepseek-v4-flash", runtime.LastRequest.Model);
        Assert.Equal("json_object", runtime.LastRequest.ResponseFormat);
        Assert.Contains("只输出 JSON", runtime.LastRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("今天接入真实 AI #Journal", runtime.LastRequest.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSafeFailureWhenApiKeyMissing()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var provider = new OpenAiCompatibleJournalAiProvider(new CapturingRuntime(CreateAiJson(date)));

        var result = await provider.GenerateAsync(
            new JournalAiGenerationRequest(
                date,
                [],
                DateTimeOffset.Parse("2026-05-10T08:10:00+08:00"),
                new JournalAiProviderSettings("openai", "openai-compatible", "OpenAI", "openai", "https://api.openai.com/v1", "gpt-5.4", "", true, 45, 0.2, 1200, "faithful")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_api_key", result.Error!.Code);
        Assert.DoesNotContain("api_key", result.Error.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_MapsRuntimeFailureWithoutLeakingSecret()
    {
        var runtime = CapturingRuntime.Failure("unauthorized", 401, "Authorization: Bearer secret-key");
        var provider = new OpenAiCompatibleJournalAiProvider(runtime);

        var result = await provider.CheckAsync(
            new JournalAiProviderSettings("openai", "openai-compatible", "OpenAI", "openai", "https://api.openai.com/v1", "gpt-5.4", "secret-key", true, 45, 0.2, 1200, "faithful"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Status);
        Assert.DoesNotContain("secret-key", result.Error!.TechnicalDetails, StringComparison.Ordinal);
    }

    private static JournalAiJson CreateAiJson(JournalDate date) =>
        new("journal-entry/v1", date.IsoDate, date.MonthDay, "draft", ["Journal"], ["AI"], "平静", ["今天接入真实 AI #Journal"], ["昨天完成设计"], ["今天实现 Provider"], ["保持原话优先"]);

    private sealed class CapturingRuntime : IJournalAiAgentRuntime
    {
        private readonly JournalAiJson? _response;
        private readonly JournalAiSafeError? _error;
        private readonly int? _httpStatus;

        public CapturingRuntime(JournalAiJson response)
        {
            _response = response;
        }

        private CapturingRuntime(string code, int? httpStatus, string details)
        {
            _error = JournalAiSafeError.Create("provider-call", code, code, details);
            _httpStatus = httpStatus;
        }

        public OpenAiCompatibleRunRequest? LastRequest { get; private set; }

        public static CapturingRuntime Failure(string code, int? httpStatus, string details) =>
            new(code, httpStatus, details);

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(OpenAiCompatibleRunRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_error is null
                ? OpenAiCompatibleRunResult.Success(_response!, "{\"schema\":\"journal-entry/v1\"}", TimeSpan.FromMilliseconds(10), 200)
                : OpenAiCompatibleRunResult.Failure(_error.Code, _httpStatus, TimeSpan.FromMilliseconds(10), _error));
        }
    }
}
```

- [ ] **Step 2: Write failing generation service tests**

Create `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`:

```csharp
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;

namespace Journal.Tests;

public sealed class JournalAiGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesMockWhenEffectiveProviderIsMock()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var service = new JournalAiGenerationService(
            new StaticSettingsService(JournalAiSettings.CreateDefault()),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new ThrowingRuntime()));

        var result = await service.GenerateAsync(date, [new RawInput("raw-1", date, DateTimeOffset.Now, "text", "今天继续 #Journal")], DateTimeOffset.Now, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("mock", result.Metadata.Provider);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsValidationFailureWhenProviderJsonIsInvalid()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 10));
        var settings = JournalAiSettings.CreateDefault() with
        {
            ActiveProviderId = "custom",
            Providers =
            [
                new("custom", "openai-compatible", "Custom", "custom", "http://localhost:11434/v1", "local", "key", true, 45, 0.2, 1200, "faithful")
            ]
        };
        var invalidJson = new JournalAiJson("invalid-schema", date.IsoDate, date.MonthDay, "draft", [], [], "未标注", [], [], [], []);
        var service = new JournalAiGenerationService(
            new StaticSettingsService(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new StaticRuntime(invalidJson)));

        var result = await service.GenerateAsync(date, [], DateTimeOffset.Now, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains("schema must be journal-entry/v1", result.Error.Message, StringComparison.Ordinal);
    }

    private sealed class StaticSettingsService(JournalAiSettings settings) : IJournalAiSettingsReader
    {
        public Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken) => Task.FromResult(settings);
    }

    private sealed class StaticRuntime(JournalAiJson response) : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(OpenAiCompatibleRunRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Success(response, "{}", TimeSpan.Zero, 200));
    }

    private sealed class ThrowingRuntime : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(OpenAiCompatibleRunRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("runtime should not be used");
    }
}
```

- [ ] **Step 3: Run provider tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~OpenAiCompatibleJournalAiProviderTests|FullyQualifiedName~JournalAiGenerationServiceTests"
```

Expected: fail because runtime/provider/generation service types do not exist.

- [ ] **Step 4: Add prompt builder**

Create `src/Journal.Infrastructure/Ai/JournalAiPrompt.cs`:

```csharp
using System.Text;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public static class JournalAiPrompt
{
    public const string Version = "journal-entry-json-v1";

    public static string SystemInstructions => """
        你是 Journal 的晨间日记整理器。
        只输出 JSON，不输出 Markdown，不输出解释文本。
        JSON 必须匹配 JournalAiJson 字段：schema, date, monthDay, status, tags, topics, mood, rawInputs, yesterdayReview, todayFocus, inspiration。
        schema 固定为 journal-entry/v1，status 固定为 draft。
        rawInputs 必须完整保留用户原始输入，不要改写、总结或删除。
        如果 yesterdayReview、todayFocus 或 inspiration 没有足够信息，输出空数组，不要猜测或硬凑条目。
        只整理用户已经说过的事实，不虚构事实，不写鸡汤，不写营销文案。
        风格为 faithful：轻度整理，保留原话优先。
        """;

    public static string BuildUserPrompt(JournalDate date, IReadOnlyList<RawInput> rawInputs)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"date: {date.IsoDate}");
        builder.AppendLine($"monthDay: {date.MonthDay}");
        builder.AppendLine("raw inputs:");

        if (rawInputs.Count == 0)
        {
            builder.AppendLine("- （无原始输入）");
            return builder.ToString();
        }

        foreach (var input in rawInputs)
        {
            builder.Append("- [")
                .Append(input.CreatedAt.ToString("O"))
                .Append("] ")
                .AppendLine(input.Text);
        }

        return builder.ToString();
    }
}
```

- [ ] **Step 5: Add runtime boundary records**

Create `src/Journal.Infrastructure/Ai/IJournalAiAgentRuntime.cs`:

```csharp
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public interface IJournalAiAgentRuntime
{
    Task<OpenAiCompatibleRunResult> RunJsonAsync(OpenAiCompatibleRunRequest request, CancellationToken cancellationToken);
}

public sealed record OpenAiCompatibleRunRequest(
    string ProviderId,
    string BaseUrl,
    string Model,
    string ApiKey,
    string SystemPrompt,
    string UserPrompt,
    string ResponseFormat,
    int TimeoutSeconds,
    double Temperature,
    int MaxTokens);

public sealed record OpenAiCompatibleRunResult(
    bool IsSuccess,
    JournalAiJson? AiJson,
    string SafeResponseSnippet,
    TimeSpan Latency,
    int? HttpStatus,
    JournalAiSafeError? Error)
{
    public static OpenAiCompatibleRunResult Success(JournalAiJson aiJson, string safeResponseSnippet, TimeSpan latency, int? httpStatus) =>
        new(true, aiJson, safeResponseSnippet, latency, httpStatus, null);

    public static OpenAiCompatibleRunResult Failure(string status, int? httpStatus, TimeSpan latency, JournalAiSafeError error) =>
        new(false, null, string.Empty, latency, httpStatus, error);
}
```

- [ ] **Step 6: Add Agent Framework runtime**

Create `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`:

```csharp
using System.ClientModel;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Journal.Infrastructure.Ai;

public sealed class OpenAiCompatibleAgentRuntime : IJournalAiAgentRuntime
{
    public async Task<OpenAiCompatibleRunResult> RunJsonAsync(
        OpenAiCompatibleRunRequest request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();

        try
        {
            var client = new ChatClient(
                request.Model,
                new ApiKeyCredential(request.ApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(request.BaseUrl)
                });

            var agent = client.AsAIAgent(
                instructions: request.SystemPrompt,
                name: "JournalJsonFormatter",
                description: "Formats morning journal raw inputs into JournalAiJson.");

            var options = new ChatClientAgentRunOptions(new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json,
                Temperature = (float)request.Temperature,
                MaxOutputTokens = request.MaxTokens
            });

            var response = await agent.RunAsync<Journal.Domain.Entries.JournalAiJson>(
                request.UserPrompt,
                session: null,
                serializerOptions: JsonOptions.Web,
                options: options,
                cancellationToken: cancellationToken);

            started.Stop();
            return OpenAiCompatibleRunResult.Success(
                response.Result,
                response.Text.Length > 240 ? response.Text[..240] : response.Text,
                started.Elapsed,
                200);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            started.Stop();
            return OpenAiCompatibleRunResult.Failure(
                "timeout",
                null,
                started.Elapsed,
                JournalAiSafeError.Create("provider-call", "timeout", "AI provider request timed out.", "request timed out"));
        }
        catch (ClientResultException exception)
        {
            started.Stop();
            var status = MapStatus(exception.Status);
            return OpenAiCompatibleRunResult.Failure(
                status,
                exception.Status,
                started.Elapsed,
                JournalAiSafeError.Create("provider-call", status, SafeMessage(status), exception.Message));
        }
        catch (Exception exception)
        {
            started.Stop();
            return OpenAiCompatibleRunResult.Failure(
                "provider_error",
                null,
                started.Elapsed,
                JournalAiSafeError.Create("provider-call", "provider_error", "AI provider call failed.", exception.Message));
        }
    }

    private static string MapStatus(int status) =>
        status switch
        {
            401 => "unauthorized",
            403 => "forbidden",
            404 => "model_not_found",
            408 => "timeout",
            429 => "rate_limited",
            >= 500 => "provider_error",
            _ => "provider_error"
        };

    private static string SafeMessage(string status) =>
        status switch
        {
            "unauthorized" => "AI provider rejected the API key.",
            "forbidden" => "AI provider denied access to this model or endpoint.",
            "model_not_found" => "AI model was not found.",
            "timeout" => "AI provider request timed out.",
            "rate_limited" => "AI provider rate limit was reached.",
            _ => "AI provider call failed."
        };
}
```

- [ ] **Step 7: Add OpenAI-compatible provider**

Create `src/Journal.Infrastructure/Ai/OpenAiCompatibleJournalAiProvider.cs`:

```csharp
namespace Journal.Infrastructure.Ai;

public sealed class OpenAiCompatibleJournalAiProvider : IJournalAiProvider
{
    private readonly IJournalAiAgentRuntime _runtime;

    public OpenAiCompatibleJournalAiProvider(IJournalAiAgentRuntime runtime)
    {
        _runtime = runtime;
    }

    public string ProviderId => "openai-compatible";

    public async Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = request.Settings.ToMetadata();
        var settingsError = ValidateSettings(request.Settings);
        if (settingsError is not null)
        {
            return JournalAiProviderResult.Failure(metadata, settingsError);
        }

        var runResult = await _runtime.RunJsonAsync(
            new OpenAiCompatibleRunRequest(
                request.Settings.Id,
                request.Settings.BaseUrl,
                request.Settings.Model,
                request.Settings.ApiKey,
                JournalAiPrompt.SystemInstructions,
                JournalAiPrompt.BuildUserPrompt(request.Date, request.RawInputs),
                "json_object",
                request.Settings.TimeoutSeconds,
                request.Settings.Temperature,
                request.Settings.MaxTokens),
            cancellationToken);

        return runResult.IsSuccess && runResult.AiJson is not null
            ? JournalAiProviderResult.Success(runResult.AiJson, metadata)
            : JournalAiProviderResult.Failure(metadata, runResult.Error ?? JournalAiSafeError.Create("provider-call", "provider_error", "AI provider call failed.", "empty provider result"));
    }

    public async Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken)
    {
        var settingsError = ValidateSettings(settings);
        if (settingsError is not null)
        {
            return JournalAiProviderHealthResult.Failure(settingsError.Code, null, null, settingsError);
        }

        var runResult = await _runtime.RunJsonAsync(
            new OpenAiCompatibleRunRequest(
                settings.Id,
                settings.BaseUrl,
                settings.Model,
                settings.ApiKey,
                "Return a JSON object only.",
                "Return { \"ok\": true }",
                "json_object",
                settings.TimeoutSeconds,
                settings.Temperature,
                settings.MaxTokens),
            cancellationToken);

        return runResult.IsSuccess
            ? JournalAiProviderHealthResult.Success(runResult.SafeResponseSnippet, runResult.Latency, runResult.HttpStatus)
            : JournalAiProviderHealthResult.Failure(runResult.Error?.Code ?? "provider_error", runResult.HttpStatus, runResult.Latency, runResult.Error!);
    }

    private static JournalAiSafeError? ValidateSettings(JournalAiProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return JournalAiSafeError.Create("settings", "missing_api_key", "API Key is required for this provider.", "api key missing");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            return JournalAiSafeError.Create("settings", "missing_model", "Model is required for this provider.", "model missing");
        }

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
        {
            return JournalAiSafeError.Create("settings", "invalid_base_url", "Base URL is invalid.", settings.BaseUrl);
        }

        return null;
    }
}
```

- [ ] **Step 8: Add settings reader interface and generation service**

Add this interface to `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`:

```csharp
public interface IJournalAiSettingsReader
{
    Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken);
}
```

Make `JournalAiSettingsService` implement `IJournalAiSettingsReader`.

Create `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`:

```csharp
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
        var providerId = string.IsNullOrWhiteSpace(providerIdOverride) ? settings.ActiveProviderId : providerIdOverride.Trim();
        var providerSettings = settings.Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase))
            ?? settings.Providers.First(provider => provider.Id == "mock");

        var provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
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

        return JournalAiProviderResult.Failure(
            result.Metadata,
            JournalAiSafeError.Create(
                "validation",
                "validation_failed",
                string.Join("; ", validation.Errors),
                string.Join(Environment.NewLine, validation.Errors)));
    }

    public async Task<JournalAiProviderHealthResult> CheckAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        var providerSettings = settings.Providers.First(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
        return providerSettings.IsMock
            ? await _mockProvider.CheckAsync(providerSettings, cancellationToken)
            : await _openAiCompatibleProvider.CheckAsync(providerSettings, cancellationToken);
    }
}
```

- [ ] **Step 9: Run provider and generation tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~OpenAiCompatibleJournalAiProviderTests|FullyQualifiedName~JournalAiGenerationServiceTests"
```

Expected: pass.

- [ ] **Step 10: Commit Task 3**

```powershell
git add src/Journal.Infrastructure/Ai tests/Journal.Tests/OpenAiCompatibleJournalAiProviderTests.cs tests/Journal.Tests/JournalAiGenerationServiceTests.cs
git commit -m "feat: add openai compatible ai runtime"
```

---

## Task 4: Integrate Generation Service into Today Workflow

**Files:**
- Modify: `src/Journal.Infrastructure/Today/TodayJournalService.cs`
- Modify: `tests/Journal.Tests/TodayJournalServiceTests.cs`

- [ ] **Step 1: Update service tests for async generation and regenerate**

Update helper `CreateService` in `tests/Journal.Tests/TodayJournalServiceTests.cs` to construct `JournalAiGenerationService` instead of injecting `IJournalAiProvider` directly. Add this test:

```csharp
[Fact]
public async Task RegenerateDraftAsync_UsesAllRawInputsAndDoesNotWriteEntry()
{
    using var workspace = TempWorkspace.Create();
    var paths = CreatePaths(workspace.Root);
    var service = CreateService(paths);
    await service.AddInputAsync("昨天完成配置设计 #Journal", "text", CancellationToken.None);
    await service.ConfirmDraftAsync(CancellationToken.None);
    await service.AddInputAsync("今天用真实模型重新整理 #Journal", "text", CancellationToken.None);

    var state = await service.RegenerateDraftAsync(providerIdOverride: "mock", CancellationToken.None);

    Assert.Equal(JournalStatus.Reviewing, state.Status);
    Assert.NotNull(state.Draft);
    Assert.Contains("昨天完成配置设计 #Journal", state.Draft.Markdown);
    Assert.Contains("今天用真实模型重新整理 #Journal", state.Draft.Markdown);
    Assert.NotNull(state.Entry);
    var entryText = await File.ReadAllTextAsync(paths.EntryPath(state.Date), CancellationToken.None);
    Assert.DoesNotContain("今天用真实模型重新整理 #Journal", entryText);
}
```

Add a fake failing settings/runtime test:

```csharp
[Fact]
public async Task AddInputAsync_WithRealProviderFailureCreatesAttentionDraftAndDoesNotFallbackToMock()
{
    using var workspace = TempWorkspace.Create();
    var paths = CreatePaths(workspace.Root);
    var service = CreateServiceWithFailingRealProvider(paths);

    var state = await service.AddInputAsync("今天真实 provider 会失败 #Journal", "text", CancellationToken.None);

    Assert.Equal(JournalStatus.Attention, state.Status);
    Assert.NotNull(state.Draft);
    Assert.Contains("AI provider failed", state.Draft.Markdown);
    Assert.Contains("unauthorized", string.Join(" ", state.Errors), StringComparison.Ordinal);
    Assert.DoesNotContain("整理今天的重点", state.Draft.Markdown, StringComparison.Ordinal);
    Assert.False(File.Exists(paths.EntryPath(state.Date)));
}
```

- [ ] **Step 2: Run Today service tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~TodayJournalServiceTests"
```

Expected: fail because `TodayJournalService` still expects `IJournalAiProvider` and no `RegenerateDraftAsync` exists.

- [ ] **Step 3: Change constructor dependencies**

In `src/Journal.Infrastructure/Today/TodayJournalService.cs`, replace:

```csharp
private readonly IJournalAiProvider _aiProvider;
```

with:

```csharp
private readonly JournalAiGenerationService _aiGenerationService;
```

Change the constructor parameter from `IJournalAiProvider aiProvider` to `JournalAiGenerationService aiGenerationService` and assign it.

- [ ] **Step 4: Replace draft generation inside AddInputAsync**

Replace the block that calls `_aiProvider.Generate(...)` with:

```csharp
var generation = await _aiGenerationService.GenerateAsync(
    date,
    inputs,
    now,
    providerIdOverride: null,
    cancellationToken);

if (!generation.IsSuccess || generation.AiJson is null)
{
    var errors = generation.Error is null
        ? ["AI provider failed."]
        : [generation.Error.Message, generation.Error.Code, generation.Error.TechnicalDetails];
    var attentionDraft = new JournalDraft(
        date,
        JournalStatus.Attention,
        RenderAttentionMarkdown("AI provider failed", errors),
        sourceRawInputIds,
        errors,
        now);

    await _draftStore.WriteAsync(attentionDraft, cancellationToken);
    return await BuildStateAsync(date, JournalStatus.Attention, cancellationToken);
}

var markdown = JmfMarkdownRenderer.Render(generation.AiJson, now, generation.Metadata);
```

- [ ] **Step 5: Add regenerate method**

Add this public method to `TodayJournalService`:

```csharp
public async Task<TodayJournalState> RegenerateDraftAsync(
    string? providerIdOverride,
    CancellationToken cancellationToken)
{
    var date = JournalDate.From(_clock.Today);
    var now = _clock.Now;
    var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
    var sourceRawInputIds = inputs.Select(rawInput => rawInput.Id).ToArray();
    var generation = await _aiGenerationService.GenerateAsync(date, inputs, now, providerIdOverride, cancellationToken);

    if (!generation.IsSuccess || generation.AiJson is null)
    {
        var errors = generation.Error is null
            ? ["AI provider failed."]
            : [generation.Error.Message, generation.Error.Code, generation.Error.TechnicalDetails];
        var attentionDraft = new JournalDraft(
            date,
            JournalStatus.Attention,
            RenderAttentionMarkdown("AI provider failed", errors),
            sourceRawInputIds,
            errors,
            now);

        await _draftStore.WriteAsync(attentionDraft, cancellationToken);
        return await BuildStateAsync(date, JournalStatus.Attention, cancellationToken);
    }

    var markdown = JmfMarkdownRenderer.Render(generation.AiJson, now, generation.Metadata);
    var draft = new JournalDraft(
        date,
        JournalStatus.Reviewing,
        markdown,
        sourceRawInputIds,
        Array.Empty<string>(),
        now);

    await _draftStore.WriteAsync(draft, cancellationToken);
    return await BuildStateAsync(date, JournalStatus.Reviewing, cancellationToken);
}
```

Change `RenderAttentionMarkdown` signature to:

```csharp
private static string RenderAttentionMarkdown(string title, IReadOnlyList<string> errors)
```

and first line:

```csharp
builder.Append("# ").AppendLine(title);
```

- [ ] **Step 6: Update test helpers**

In `TodayJournalServiceTests`, create helpers:

```csharp
private static TodayJournalService CreateService(LocalJournalPaths paths) =>
    new(
        new RawInputStore(paths),
        new DraftStore(paths),
        new EntryStore(paths),
        CreateGenerationService(JournalAiSettings.CreateDefault()),
        new FixedJournalClock(FixedDay, FixedNow));

private static TodayJournalService CreateServiceWithFailingRealProvider(LocalJournalPaths paths)
{
    var settings = JournalAiSettings.CreateDefault() with
    {
        ActiveProviderId = "openai",
        Providers =
        [
            new("openai", "openai-compatible", "OpenAI", "openai", "https://api.openai.com/v1", "gpt-5.4", "secret", true, 45, 0.2, 1200, "faithful")
        ]
    };

    return new TodayJournalService(
        new RawInputStore(paths),
        new DraftStore(paths),
        new EntryStore(paths),
        CreateGenerationService(settings, OpenAiCompatibleRunResult.Failure("unauthorized", 401, TimeSpan.Zero, JournalAiSafeError.Create("provider-call", "unauthorized", "AI provider rejected the API key.", "Authorization: Bearer [redacted]"))),
        new FixedJournalClock(FixedDay, FixedNow));
}

private static JournalAiGenerationService CreateGenerationService(
    JournalAiSettings settings,
    OpenAiCompatibleRunResult? runResult = null) =>
    new(
        new StaticSettingsReader(settings),
        new MockAiProvider(),
        new OpenAiCompatibleJournalAiProvider(new StaticRuntime(runResult)));
```

Add `StaticSettingsReader` and `StaticRuntime` nested classes matching the interfaces from Task 3.

- [ ] **Step 7: Run Today service tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~TodayJournalServiceTests"
```

Expected: pass.

- [ ] **Step 8: Commit Task 4**

```powershell
git add src/Journal.Infrastructure/Today/TodayJournalService.cs tests/Journal.Tests/TodayJournalServiceTests.cs
git commit -m "feat: use ai generation service for drafts"
```

---

## Task 5: Minimal API Endpoints and DI

**Files:**
- Modify: `src/Journal.Api/Program.cs`
- Modify: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Add tests to `tests/Journal.Tests/TodayJournalEndpointTests.cs`:

```csharp
[Fact]
public async Task GetSettingsAi_ReturnsSafeProviderView()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/settings/ai");
    response.EnsureSuccessStatusCode();

    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var root = document.RootElement;

    Assert.Equal("mock", root.GetProperty("activeProviderId").GetString());
    Assert.Contains(root.GetProperty("providers").EnumerateArray(), provider => provider.GetProperty("id").GetString() == "deepseek");
    Assert.DoesNotContain("apiKey", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task PutSettingsAi_SavesConfigurationWithoutReturningApiKey()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var response = await client.PutAsJsonAsync("/settings/ai", new
    {
        activeProviderId = "deepseek",
        providers = new[]
        {
            new
            {
                id = "deepseek",
                type = "openai-compatible",
                displayName = "DeepSeek",
                preset = "deepseek",
                baseUrl = "https://api.deepseek.com",
                model = "deepseek-v4-flash",
                apiKey = "secret-value",
                isEnabled = true,
                timeoutSeconds = 45,
                temperature = 0.2,
                maxTokens = 1200,
                stylePreset = "faithful"
            }
        }
    });
    response.EnsureSuccessStatusCode();

    var body = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("secret-value", body, StringComparison.Ordinal);
}

[Fact]
public async Task PostTodayDraftRegenerate_UsesMockOverrideAndDoesNotWriteEntry()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
    var date = JournalDate.From(FixedDay);

    using var inputResponse = await client.PostAsJsonAsync(
        "/journal/today/inputs",
        new { text = "今天先生成草稿 #Journal", source = "text" });
    inputResponse.EnsureSuccessStatusCode();

    using var confirmResponse = await client.PostAsync("/journal/today/draft/confirm", content: null);
    confirmResponse.EnsureSuccessStatusCode();

    using var regenerateResponse = await client.PostAsJsonAsync(
        "/journal/today/draft/regenerate",
        new { providerId = "mock" });
    regenerateResponse.EnsureSuccessStatusCode();

    using var document = await JsonDocument.ParseAsync(await regenerateResponse.Content.ReadAsStreamAsync());
    Assert.Equal("reviewing", document.RootElement.GetProperty("status").GetString());
    Assert.True(File.Exists(paths.EntryPath(date)));
}
```

- [ ] **Step 2: Run endpoint tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~TodayJournalEndpointTests"
```

Expected: fail because endpoints and DI services are not registered.

- [ ] **Step 3: Register AI services**

In `src/Journal.Api/Program.cs`, replace:

```csharp
builder.Services.AddSingleton<IJournalAiProvider, MockAiProvider>();
```

with:

```csharp
builder.Services.AddSingleton<IJournalAiEnvironment, SystemJournalAiEnvironment>();
builder.Services.AddSingleton<JournalAiSettingsStore>();
builder.Services.AddSingleton<IJournalAiSettingsReader>(services => services.GetRequiredService<JournalAiSettingsService>());
builder.Services.AddSingleton<JournalAiSettingsService>();
builder.Services.AddSingleton<MockAiProvider>();
builder.Services.AddSingleton<IJournalAiAgentRuntime, OpenAiCompatibleAgentRuntime>();
builder.Services.AddSingleton<OpenAiCompatibleJournalAiProvider>();
builder.Services.AddSingleton<JournalAiGenerationService>();
```

- [ ] **Step 4: Add settings and regenerate endpoints**

Add these endpoints before `app.Run()`:

```csharp
app.MapGet("/settings/ai", async (JournalAiSettingsService service, CancellationToken cancellationToken) =>
{
    var view = await service.ReadViewAsync(cancellationToken);
    return Results.Ok(view);
});

app.MapPut("/settings/ai", async (
    JournalAiSettingsSaveRequest request,
    JournalAiSettingsService service,
    CancellationToken cancellationToken) =>
{
    await service.SaveAsync(request, cancellationToken);
    var view = await service.ReadViewAsync(cancellationToken);
    return Results.Ok(view);
});

app.MapPost("/settings/ai/test", async (
    AiProviderTestRequest request,
    JournalAiGenerationService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ProviderId))
    {
        return Results.BadRequest(new { error = "providerId is required" });
    }

    var result = await service.CheckAsync(request.ProviderId, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/journal/today/draft/regenerate", async (
    RegenerateTodayDraftRequest? request,
    TodayJournalService service,
    CancellationToken cancellationToken) =>
{
    var state = await service.RegenerateDraftAsync(request?.ProviderId, cancellationToken);
    return Results.Ok(state);
});
```

Add request records at the bottom:

```csharp
public sealed record AiProviderTestRequest(string ProviderId);

public sealed record RegenerateTodayDraftRequest(string? ProviderId);
```

- [ ] **Step 5: Update endpoint test factory only when needed**

Keep the current `CreateFactory` override for `JournalStorageOptions` and `IJournalClock`. Do not replace AI services in endpoint tests; the default Mock path must work through the same production DI graph.

- [ ] **Step 6: Run endpoint tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~TodayJournalEndpointTests"
```

Expected: pass.

- [ ] **Step 7: Commit Task 5**

```powershell
git add src/Journal.Api/Program.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose ai settings endpoints"
```

---

## Task 6: Frontend API Client and LLM Settings Panel

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Create: `apps/desktop/src/LlmSettingsPanel.tsx`
- Modify: `apps/desktop/src/styles.css`
- Test: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add failing API client tests**

In `apps/desktop/src/App.test.tsx`, under `describe("editor API client", ...)`, add:

```tsx
test("getAiSettings calls settings endpoint", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(aiSettings));
  vi.stubGlobal("fetch", fetchMock);

  await getAiSettings();

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", undefined);
});

test("saveAiSettings sends provider settings", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(aiSettings));
  vi.stubGlobal("fetch", fetchMock);

  await saveAiSettings({
    activeProviderId: "deepseek",
    providers: [
      {
        id: "deepseek",
        type: "openai-compatible",
        displayName: "DeepSeek",
        preset: "deepseek",
        baseUrl: "https://api.deepseek.com",
        model: "deepseek-v4-flash",
        apiKey: "",
        isEnabled: true,
        timeoutSeconds: 45,
        temperature: 0.2,
        maxTokens: 1200,
        stylePreset: "faithful"
      }
    ]
  });

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      activeProviderId: "deepseek",
      providers: [
        {
          id: "deepseek",
          type: "openai-compatible",
          displayName: "DeepSeek",
          preset: "deepseek",
          baseUrl: "https://api.deepseek.com",
          model: "deepseek-v4-flash",
          apiKey: "",
          isEnabled: true,
          timeoutSeconds: 45,
          temperature: 0.2,
          maxTokens: 1200,
          stylePreset: "faithful"
        }
      ]
    })
  });
});

test("regenerateTodayDraft sends optional provider override", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse(reviewingToday));
  vi.stubGlobal("fetch", fetchMock);

  await regenerateTodayDraft("mock");

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: "mock" })
  });
});
```

Add imports for `getAiSettings`, `saveAiSettings`, and `regenerateTodayDraft` from `./api`.

- [ ] **Step 2: Run frontend tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- --runInBand
```

Expected: fail because API methods and `aiSettings` test data do not exist.

- [ ] **Step 3: Add API types and methods**

Modify `apps/desktop/src/api.ts`:

```ts
export type AiProviderView = {
  id: string;
  type: string;
  displayName: string;
  preset: string;
  baseUrl: string;
  model: string;
  isEnabled: boolean;
  isActive: boolean;
  hasApiKey: boolean;
  source: string;
  timeoutSeconds: number;
  temperature: number;
  maxTokens: number;
  stylePreset: string;
  lastTestStatus: string;
};

export type AiSettingsView = {
  activeProviderId: string;
  runtime: string;
  providers: AiProviderView[];
};

export type AiProviderSaveRequest = {
  id: string;
  type: string;
  displayName: string;
  preset: string;
  baseUrl: string;
  model: string;
  apiKey: string;
  isEnabled: boolean;
  timeoutSeconds: number;
  temperature: number;
  maxTokens: number;
  stylePreset: string;
};

export type AiSettingsSaveRequest = {
  activeProviderId: string;
  providers: AiProviderSaveRequest[];
};

export type AiProviderHealthResult = {
  isSuccess: boolean;
  status: string;
  safeResponseSnippet: string;
  httpStatus: number | null;
  latency: string | null;
  error: {
    stage: string;
    code: string;
    message: string;
    technicalDetails: string;
  } | null;
};
```

Add methods:

```ts
export function getAiSettings(): Promise<AiSettingsView> {
  return requestJson<AiSettingsView>("/settings/ai");
}

export function saveAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsView> {
  return requestJson<AiSettingsView>("/settings/ai", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });
}

export function testAiProvider(providerId: string): Promise<AiProviderHealthResult> {
  return requestJson<AiProviderHealthResult>("/settings/ai/test", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId })
  });
}

export function regenerateTodayDraft(providerId?: string): Promise<TodayJournalState> {
  return requestJson<TodayJournalState>("/journal/today/draft/regenerate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: providerId ?? null })
  });
}
```

- [ ] **Step 4: Create LLM settings panel component**

Create `apps/desktop/src/LlmSettingsPanel.tsx`:

```tsx
import { FormEvent, useMemo, useState } from "react";
import {
  type AiProviderHealthResult,
  type AiProviderSaveRequest,
  type AiSettingsView
} from "./api";

type LlmSettingsPanelProps = {
  settings: AiSettingsView;
  isBusy: boolean;
  onClose: () => void;
  onSave: (request: { activeProviderId: string; providers: AiProviderSaveRequest[] }) => Promise<void>;
  onTest: (providerId: string) => Promise<AiProviderHealthResult>;
  onRegenerate: (providerId?: string) => Promise<void>;
};

export function LlmSettingsPanel({
  settings,
  isBusy,
  onClose,
  onSave,
  onTest,
  onRegenerate
}: LlmSettingsPanelProps) {
  const [selectedId, setSelectedId] = useState(settings.activeProviderId);
  const [providers, setProviders] = useState<AiProviderSaveRequest[]>(
    settings.providers.map(provider => ({
      id: provider.id,
      type: provider.type,
      displayName: provider.displayName,
      preset: provider.preset,
      baseUrl: provider.baseUrl,
      model: provider.model,
      apiKey: "",
      isEnabled: provider.isEnabled,
      timeoutSeconds: provider.timeoutSeconds,
      temperature: provider.temperature,
      maxTokens: provider.maxTokens,
      stylePreset: provider.stylePreset
    }))
  );
  const [testResult, setTestResult] = useState<AiProviderHealthResult | null>(null);
  const [confirmRegenerate, setConfirmRegenerate] = useState(false);

  const selected = useMemo(
    () => providers.find(provider => provider.id === selectedId) ?? providers[0],
    [providers, selectedId]
  );
  const selectedView = settings.providers.find(provider => provider.id === selectedId);

  function updateSelected(patch: Partial<AiProviderSaveRequest>) {
    setProviders(current =>
      current.map(provider => provider.id === selected.id ? { ...provider, ...patch } : provider)
    );
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSave({ activeProviderId: selected.id, providers });
  }

  async function handleTest() {
    setTestResult(await onTest(selected.id));
  }

  async function handleRegenerate(providerId?: string) {
    if (!confirmRegenerate) {
      setConfirmRegenerate(true);
      return;
    }

    await onRegenerate(providerId);
    setConfirmRegenerate(false);
  }

  return (
    <section className="llm-settings-overlay" aria-label="LLM 配置面板">
      <header className="llm-settings-head">
        <div>
          <strong>LLM 配置</strong>
          <span>{settings.runtime}</span>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>关闭</button>
      </header>

      <div className="llm-settings-grid">
        <nav className="llm-provider-list" aria-label="Provider 列表">
          {settings.providers.map(provider => (
            <button
              key={provider.id}
              type="button"
              className={`llm-provider-card ${provider.id === selectedId ? "active" : ""}`}
              onClick={() => {
                setSelectedId(provider.id);
                setTestResult(null);
              }}
            >
              <strong>{provider.displayName}</strong>
              <span>{provider.isActive ? "active" : provider.source}</span>
              <small>{provider.hasApiKey ? "key ready" : "no key"}</small>
            </button>
          ))}
        </nav>

        <form className="llm-settings-main" onSubmit={handleSave}>
          <section className="llm-settings-card">
            <span className="rail-label">Selected provider</span>
            <h2>{selected.displayName}</h2>
            <p>{selectedView?.source === "environment" ? "当前配置来自环境变量，API Key 不会显示，也不会回写配置文件。" : "保存到本机 ai-providers.json。"}</p>
          </section>

          <section className="llm-settings-card">
            <span className="rail-label">Basic config</span>
            <label>
              显示名称
              <input value={selected.displayName} onChange={event => updateSelected({ displayName: event.target.value })} />
            </label>
            <label>
              模型
              <input value={selected.model} onChange={event => updateSelected({ model: event.target.value })} />
            </label>
            <label>
              API Key
              <input
                value={selected.apiKey}
                onChange={event => updateSelected({ apiKey: event.target.value })}
                aria-label={selectedView?.hasApiKey ? "API Key 已加载，值不显示" : "API Key 未配置"}
              />
            </label>
            <label>
              Base URL
              <input value={selected.baseUrl} onChange={event => updateSelected({ baseUrl: event.target.value })} />
            </label>
            <div className="llm-settings-actions">
              <button type="button" className="secondary-action" onClick={handleTest} disabled={isBusy}>测试已保存配置</button>
              <button type="submit" className="primary-action" disabled={isBusy}>启用 Provider</button>
            </div>
          </section>

          <section className="llm-settings-card">
            <span className="rail-label">Regenerate</span>
            <h2>重新整理今日草稿</h2>
            <p>{confirmRegenerate ? "这会覆盖当前草稿内容，但不会影响正式日记。" : "使用当前 Provider 重新生成 reviewing draft。"}</p>
            <div className="llm-settings-actions">
              <button type="button" className="secondary-action danger-action" onClick={() => handleRegenerate("mock")} disabled={isBusy}>用 Mock 生成一次</button>
              <button type="button" className="primary-action" onClick={() => handleRegenerate()} disabled={isBusy}>重新整理草稿</button>
            </div>
          </section>
        </form>

        <aside className="llm-settings-side">
          <section className="llm-settings-card">
            <span className="rail-label">Advanced</span>
            <label>
              JSON 模式
              <input value="json_object" readOnly />
            </label>
            <label>
              超时
              <input value={selected.timeoutSeconds} onChange={event => updateSelected({ timeoutSeconds: Number(event.target.value) })} />
            </label>
            <label>
              Temperature
              <input value={selected.temperature} onChange={event => updateSelected({ temperature: Number(event.target.value) })} />
            </label>
            <label>
              Max tokens
              <input value={selected.maxTokens} onChange={event => updateSelected({ maxTokens: Number(event.target.value) })} />
            </label>
          </section>

          <section className={`llm-settings-card ${testResult?.isSuccess === false ? "attention-panel" : ""}`}>
            <span className="rail-label">Connection test</span>
            <h2>{testResult ? testResult.status : "最小 JSON 请求"}</h2>
            <p>会使用已保存的 Provider 配置，不会测试当前未保存草稿。</p>
            {testResult?.error ? (
              <details>
                <summary>安全技术详情</summary>
                <pre>{testResult.error.technicalDetails}</pre>
              </details>
            ) : null}
          </section>
        </aside>
      </div>
    </section>
  );
}
```

- [ ] **Step 5: Add panel styles**

Append to `apps/desktop/src/styles.css`:

```css
.llm-settings-overlay {
  position: fixed;
  inset: 10px;
  z-index: 10;
  display: grid;
  grid-template-rows: 54px minmax(0, 1fr);
  border: 1px solid #bdb7aa;
  border-radius: 8px;
  background: rgba(243, 241, 235, 0.96);
  box-shadow: 0 28px 80px rgba(42, 36, 28, 0.24);
}

.llm-settings-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border-bottom: 1px solid #d8d4ca;
  padding: 0 16px;
  background: rgba(255, 253, 248, 0.88);
}

.llm-settings-head div {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.llm-settings-head span,
.llm-provider-card span,
.llm-provider-card small,
.llm-settings-card p {
  color: #655d53;
  font-size: 12px;
}

.llm-settings-grid {
  min-height: 0;
  display: grid;
  grid-template-columns: 260px minmax(420px, 1fr) 360px;
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.llm-provider-list,
.llm-settings-main,
.llm-settings-side {
  min-height: 0;
  display: grid;
  align-content: start;
  gap: 10px;
  overflow: auto;
}

.llm-provider-card,
.llm-settings-card {
  border: 1px solid #d8d4ca;
  border-radius: 8px;
  background: #fffdf8;
}

.llm-provider-card {
  min-height: 78px;
  padding: 12px;
  display: grid;
  gap: 6px;
  text-align: left;
}

.llm-provider-card.active {
  border-color: rgba(43, 104, 96, 0.38);
  background: #eef6f1;
}

.llm-settings-card {
  padding: 14px;
}

.llm-settings-card h2 {
  margin: 6px 0 10px;
  font-size: 16px;
}

.llm-settings-card label {
  display: grid;
  gap: 6px;
  margin-top: 10px;
  color: #2d2823;
  font-weight: 800;
}

.llm-settings-card input {
  min-height: 38px;
  border: 1px solid #c9c5bb;
  border-radius: 6px;
  background: #fffdf8;
  color: #252525;
  padding: 0 10px;
}

.llm-settings-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 12px;
}

.danger-action {
  border-color: rgba(168, 66, 53, 0.28);
  color: #8c332b;
}

.llm-settings-card pre {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  color: #655d53;
}

@media (max-width: 1080px) {
  .llm-settings-grid {
    grid-template-columns: 1fr;
    overflow: auto;
  }
}
```

- [ ] **Step 6: Run API client tests**

Run:

```powershell
npm test --prefix apps/desktop -- --runInBand
```

Expected: existing App tests may now fail because the app has not loaded settings yet. API client tests should pass once `aiSettings` fixture is added.

- [ ] **Step 7: Commit Task 6**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/LlmSettingsPanel.tsx apps/desktop/src/styles.css apps/desktop/src/App.test.tsx
git commit -m "feat: add llm settings panel shell"
```

---

## Task 7: Wire Frontend App to AI Settings and Regeneration

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add AI settings fixture**

In `apps/desktop/src/App.test.tsx`, add:

```tsx
const aiSettings = {
  activeProviderId: "mock",
  runtime: "OpenAI-compatible runtime · Agent Framework 1.5.0",
  providers: [
    {
      id: "mock",
      type: "mock",
      displayName: "Mock",
      preset: "mock",
      baseUrl: "local",
      model: "mock-journal",
      isEnabled: true,
      isActive: true,
      hasApiKey: true,
      source: "default",
      timeoutSeconds: 1,
      temperature: 0,
      maxTokens: 0,
      stylePreset: "faithful",
      lastTestStatus: "not-tested"
    },
    {
      id: "deepseek",
      type: "openai-compatible",
      displayName: "DeepSeek",
      preset: "deepseek",
      baseUrl: "https://api.deepseek.com",
      model: "deepseek-v4-flash",
      isEnabled: false,
      isActive: false,
      hasApiKey: false,
      source: "preset",
      timeoutSeconds: 45,
      temperature: 0.2,
      maxTokens: 1200,
      stylePreset: "faithful",
      lastTestStatus: "not-tested"
    }
  ]
};
```

- [ ] **Step 2: Update initial load tests**

Every test that currently mocks initial `health` and `getTodayEditor` calls must include `aiSettings` as the third initial response. For example:

```tsx
const fetchMock = mockFetchSequence([
  { body: healthResponse },
  { body: createEditorState() },
  { body: aiSettings }
]);
```

Update expected calls:

```tsx
expect(fetchMock).toHaveBeenNthCalledWith(3, "http://localhost:5057/settings/ai", undefined);
```

When a test triggers submit/confirm/save, shift later `NthCalledWith` indexes by one.

- [ ] **Step 3: Add failing UI tests**

Add these tests to the `describe("App", ...)` block:

```tsx
test("shows current LLM provider in top status strip", async () => {
  mockFetchSequence([
    { body: healthResponse },
    { body: createEditorState() },
    { body: aiSettings }
  ]);

  render(<App />);

  expect(await screen.findByRole("button", { name: "LLM Mock" })).toBeInTheDocument();
});

test("opens LLM settings panel from top status strip", async () => {
  mockFetchSequence([
    { body: healthResponse },
    { body: createEditorState() },
    { body: aiSettings }
  ]);

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));

  expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /DeepSeek/ })).toBeInTheDocument();
  expect(screen.getByText("最小 JSON 请求")).toBeInTheDocument();
});

test("tests provider and shows safe technical details", async () => {
  const fetchMock = mockFetchSequence([
    { body: healthResponse },
    { body: createEditorState() },
    { body: aiSettings },
    {
      body: {
        isSuccess: false,
        status: "unauthorized",
        safeResponseSnippet: "",
        httpStatus: 401,
        latency: "00:00:00.1200000",
        error: {
          stage: "provider-call",
          code: "unauthorized",
          message: "AI provider rejected the API key.",
          technicalDetails: "httpStatus: 401 authorization: [redacted]"
        }
      }
    }
  ]);

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
  fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
  fireEvent.click(screen.getByRole("button", { name: "测试已保存配置" }));

  expect(await screen.findByText("unauthorized")).toBeInTheDocument();
  expect(screen.getByText("安全技术详情")).toBeInTheDocument();
  expect(fetchMock).toHaveBeenNthCalledWith(4, "http://localhost:5057/settings/ai/test", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: "deepseek" })
  });
});

test("regenerates draft after confirmation prompt", async () => {
  const fetchMock = mockFetchSequence([
    { body: healthResponse },
    { body: createEditorState() },
    { body: aiSettings },
    { body: reviewingToday },
    { body: createEditorState() },
    { body: aiSettings }
  ]);

  render(<App />);

  fireEvent.click(await screen.findByRole("button", { name: "LLM Mock" }));
  fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));
  expect(screen.getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "重新整理草稿" }));

  await waitFor(() =>
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ providerId: null })
    })
  );
});
```

- [ ] **Step 4: Wire App initial load**

Modify imports in `apps/desktop/src/App.tsx`:

```tsx
import {
  addTodayInput,
  confirmTodayDraft,
  getAiSettings,
  getHealth,
  getTodayEditor,
  regenerateTodayDraft,
  saveAiSettings,
  saveBlockDraft,
  saveSourceDraft,
  testAiProvider,
  type AiProviderHealthResult,
  type AiSettingsView,
  type JournalBlockEditSection,
  type HealthResponse,
  type TodayEditorState
} from "./api";
import { LlmSettingsPanel } from "./LlmSettingsPanel";
```

Add state:

```tsx
const [aiSettings, setAiSettings] = useState<AiSettingsView | null>(null);
const [isLlmPanelOpen, setIsLlmPanelOpen] = useState(false);
```

Change initial load:

```tsx
const [healthResult, editorResult, aiSettingsResult] = await Promise.all([
  getHealth(),
  getTodayEditor(),
  getAiSettings()
]);
```

and set:

```tsx
setAiSettings(aiSettingsResult);
```

- [ ] **Step 5: Add LLM pill to top strip**

Inside `.status-strip`, after API pill:

```tsx
<button type="button" className="llm-status-pill" onClick={() => setIsLlmPanelOpen(true)}>
  LLM {aiSettings?.providers.find(provider => provider.isActive)?.displayName ?? "Mock"}
</button>
```

- [ ] **Step 6: Add panel handlers**

Add to `App.tsx`:

```tsx
async function refreshEditorAndAiSettings() {
  const [nextEditor, nextAiSettings] = await Promise.all([getTodayEditor(), getAiSettings()]);
  setEditor(nextEditor);
  setAiSettings(nextAiSettings);
}

async function handleSaveAiSettings(request: Parameters<typeof saveAiSettings>[0]) {
  const next = await saveAiSettings(request);
  setAiSettings(next);
}

async function handleTestAiProvider(providerId: string): Promise<AiProviderHealthResult> {
  return await testAiProvider(providerId);
}

async function handleRegenerateDraft(providerId?: string) {
  const requestId = requestIdRef.current + 1;
  requestIdRef.current = requestId;
  setValidationError("");
  setIsSubmitting(true);
  try {
    await regenerateTodayDraft(providerId);
    if (requestId === requestIdRef.current) {
      await refreshEditorAndAiSettings();
      setApiError("");
      setLoadState("ready");
    }
  } catch (caught) {
    if (requestId === requestIdRef.current) {
      setApiError(getErrorMessage(caught));
    }
  } finally {
    if (requestId === requestIdRef.current) {
      setIsSubmitting(false);
    }
  }
}
```

Render panel before closing `</main>`:

```tsx
{isLlmPanelOpen && aiSettings ? (
  <LlmSettingsPanel
    settings={aiSettings}
    isBusy={isBusy}
    onClose={() => setIsLlmPanelOpen(false)}
    onSave={handleSaveAiSettings}
    onTest={handleTestAiProvider}
    onRegenerate={handleRegenerateDraft}
  />
) : null}
```

- [ ] **Step 7: Add LLM pill style**

Add to `apps/desktop/src/styles.css`:

```css
.llm-status-pill {
  min-height: 32px;
  border: 1px solid #d3cfc5;
  border-radius: 6px;
  padding: 6px 10px;
  background: #fff8ed;
  color: #2b6860;
  font-size: 13px;
  font-weight: 800;
}
```

- [ ] **Step 8: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: pass.

- [ ] **Step 9: Run frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: pass.

- [ ] **Step 10: Commit Task 7**

```powershell
git add apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx apps/desktop/src/styles.css
git commit -m "feat: wire ai settings workflow"
```

---

## Task 8: Full Verification and Manual Smoke Notes

**Files:**
- Modify: `docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md`
- Optional create/update: `docs/superpowers/archives/2026-05/2026-05-10-ai-provider-integration-archives.md`

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: pass.

- [ ] **Step 2: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: pass.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: pass.

- [ ] **Step 4: Run a no-key local smoke check**

Run API:

```powershell
dotnet run --project src/Journal.Api
```

In another PowerShell:

```powershell
Invoke-RestMethod http://localhost:5057/settings/ai
```

Expected:

```text
activeProviderId = mock
providers include Mock, OpenAI, DeepSeek, 智谱 GLM, Custom
no apiKey property in response
```

- [ ] **Step 5: Run optional real-provider smoke check only when key exists**

Only run this if the current shell intentionally has a real key:

```powershell
$env:JOURNAL_AI_PROVIDER='deepseek'
$env:JOURNAL_AI_BASE_URL='https://api.deepseek.com'
$env:JOURNAL_AI_MODEL='deepseek-v4-flash'
$env:JOURNAL_AI_API_KEY='<real key in local shell only>'
dotnet run --project src/Journal.Api
```

Then:

```powershell
Invoke-RestMethod http://localhost:5057/settings/ai/test -Method Post -ContentType 'application/json' -Body '{"providerId":"deepseek"}'
```

Expected:

```text
isSuccess = true
status = success
safeResponseSnippet contains ok
response does not include the key value
```

- [ ] **Step 6: Search for secret leaks and template leftovers**

Run:

```powershell
rg -n "apiKey|api_key|Authorization|secret-value|raw_response|request_id" entries .journal src tests apps/desktop docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md
```

Expected: no generated journal Markdown or draft metadata contains secrets. Source code and tests may contain safe field names and fake values.

Run:

```powershell
$patterns = @('visual' + '-spec', 'Will ' + 'Specs', 'TO' + 'DO', 'T' + 'BD')
rg -n ($patterns -join '|') docs/superpowers/specs/2026-05-10-ai-provider-integration-prototype.html docs/superpowers/plans/2026-05-10-ai-provider-integration-implementation-plan.md
```

Expected: no matches.

- [ ] **Step 7: Update design status**

In `docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md`, change:

```markdown
> 状态：待用户复核
```

to:

```markdown
> 状态：已进入实现
```

- [ ] **Step 8: Route Superpowers asset compounding**

Run the asset-compounding gate before final close-out:

```powershell
rg -n "AI Provider|Agent Framework|OpenAI-compatible|ai-providers|JOURNAL_AI" docs/superpowers/archives docs/superpowers/problems docs/superpowers/inbox
```

Expected: decide whether to create a completed archive for Phase 5. If implementation is complete and accepted, create an archive asset under `docs/superpowers/archives/2026-05/`.

- [ ] **Step 9: Commit verification/docs**

```powershell
git add docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md docs/superpowers/archives docs/superpowers/problems docs/superpowers/inbox
git commit -m "docs: record ai provider integration delivery"
```

If no archive/problem/inbox file is created, commit only the design status change:

```powershell
git add docs/superpowers/specs/2026-05-10-ai-provider-integration-design.md
git commit -m "docs: mark ai provider implementation started"
```

---

## Self-Review

### Spec Coverage

- Real provider replaces Mock when configured: Task 3 and Task 4.
- Unconfigured path remains Mock: Task 2 and Task 3.
- Environment variables override config file: Task 2.
- Config file path and plain text storage: Task 2.
- API Key safe response boundary: Task 2, Task 3, Task 5, Task 8.
- OpenAI-compatible + Agent Framework 1.5.0: Task 1 and Task 3.
- JSON object mode: Task 3.
- Faithful prompt: Task 3.
- Dynamic provider/model/prompt front matter: Task 1 and Task 4.
- Settings API: Task 5.
- Regenerate draft API: Task 4 and Task 5.
- Huashu-style configuration UI: Task 6 and Task 7.
- Tests and verification: all tasks include focused commands; Task 8 does full verification.

### Important Implementation Notes

- Microsoft documentation for Agent Framework still shows some pages at older package identifiers, but NuGet stable versions were verified on 2026-05-10: `Microsoft.Agents.AI` 1.5.0, `Microsoft.Agents.AI.OpenAI` 1.5.0, and `OpenAI` 2.10.0.
- Use `ChatClient.AsAIAgent(...)` from `Microsoft.Agents.AI.OpenAI` and pass `ChatClientAgentRunOptions(new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = ..., MaxOutputTokens = ... })`.
- `AgentResponse<T>.Result` is the typed value to pass into `JournalAiJsonValidator`.
- Keep automatic tests on fake runtimes and Mock. Real provider calls belong only in the optional smoke check.
