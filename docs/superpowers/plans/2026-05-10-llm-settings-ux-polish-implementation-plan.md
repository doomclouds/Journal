# LLM Settings UX Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the LLM settings experience so users can understand provider state, inspect file-backed keys safely, test current form values, activate only working configurations, and regenerate drafts from the today workflow instead of the settings panel.

**Architecture:** Keep the existing AI settings/runtime boundary, but add a candidate-settings path for tests and protected activation. `GET /settings/ai` remains safe and never returns `apiKey`; a narrow reveal endpoint reads file-backed keys only when the user clicks the eye button. The desktop panel becomes configuration-only, while draft regeneration moves back to the today page.

**Tech Stack:** .NET 10 minimal API, xUnit, React + TypeScript + Vite, Vitest + Testing Library, existing OpenAI-compatible provider runtime.

---

## Scope Check

This is one focused UX polish slice. It touches backend API shape and frontend interaction together because the UI contract depends on backend candidate testing and protected activation.

Do not include:

- New provider presets.
- Model list fetching.
- API key encryption.
- Diff preview for regenerated drafts.
- Prompt or validator changes.
- Production Electron process management.

Approved references:

- Spec: `docs/superpowers/specs/2026-05-10-llm-settings-ux-polish-design.md`
- Prototype: `docs/superpowers/specs/2026-05-10-llm-settings-ux-polish-prototype.html`

## File Structure

### Backend Modifies

- `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`
  - Add safe API-key preview fields to `JournalAiProviderView`.
  - Add `JournalAiProviderApiKeyView` for the explicit reveal endpoint.
  - Add `JournalAiSettingsActivationResult` for protected activation responses.
- `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`
  - Factor request normalization into reusable candidate creation.
  - Add candidate effective-settings builder.
  - Add file-backed key reveal.
  - Add safe masked key previews.
- `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`
  - Add `CheckAsync(providerId, candidateSettings, cancellationToken)` overload.
- `src/Journal.Api/Program.cs`
  - Extend `/settings/ai/test` with optional candidate payload.
  - Add `POST /settings/ai/activate`.
  - Add `GET /settings/ai/{providerId}/api-key`.
  - Keep existing `PUT /settings/ai` for compatibility.

### Backend Tests

- `tests/Journal.Tests/JournalAiSettingsTests.cs`
  - Candidate settings do not write files.
  - File-backed key reveal works only for config-file keys.
  - Safe view has masked previews and no full keys.
- `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`
  - Candidate check overload uses supplied settings instead of saved settings.
- `tests/Journal.Tests/TodayJournalEndpointTests.cs`
  - Candidate test does not write.
  - Protected activation succeeds for Mock.
  - Protected activation failure does not change saved config.
  - Reveal endpoint returns file key only and never env key.

### Frontend Modifies

- `apps/desktop/package.json`
  - Add `lucide-react` for polished, accessible icon buttons.
- `apps/desktop/package-lock.json`
  - Lock `lucide-react`.
- `apps/desktop/src/api.ts`
  - Add activation and key reveal DTOs/client functions.
  - Extend test client to accept candidate settings.
- `apps/desktop/src/App.tsx`
  - Replace settings save handler used by panel with protected activation.
  - Add key reveal handler.
  - Move regenerate action into today workflow UI.
- `apps/desktop/src/LlmSettingsPanel.tsx`
  - Rework panel around productized provider state, key field, current-form test, activation, diagnostics, and advanced summary.
  - Remove direct draft regeneration controls.
- `apps/desktop/src/styles.css`
  - Match approved prototype density and visual hierarchy.

### Frontend Tests

- `apps/desktop/src/App.test.tsx`
  - Update API client tests.
  - Update app workflow tests for today-page regeneration.
  - Replace old panel tests with API-key, dirty-state, candidate-test, activation, diagnostics, and no-regenerate tests.

---

## Task 1: Backend Safe Settings View and File-Backed Key Reveal

**Files:**
- Modify: `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`
- Modify: `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalAiSettingsTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing settings-service tests for safe previews and reveal**

Add these tests to `tests/Journal.Tests/JournalAiSettingsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run settings tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiSettingsTests"
```

Expected: fail because `CanRevealApiKey`, `ApiKeyPreview`, `JournalAiProviderApiKeyView`, and `ReadFileApiKeyAsync` do not exist.

- [ ] **Step 3: Extend settings DTOs**

In `src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs`, replace `JournalAiProviderView` with:

```csharp
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
```

Add these records below `JournalAiSettingsView`:

```csharp
public sealed record JournalAiProviderApiKeyView(
    string ProviderId,
    string Source,
    string ApiKey);

public sealed record JournalAiSettingsActivationResult(
    bool Saved,
    JournalAiSettingsView Settings,
    JournalAiProviderHealthResult TestResult);
```

- [ ] **Step 4: Add reusable candidate creation and key reveal**

In `src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs`, replace the body of `SaveAsync` with:

```csharp
public async Task SaveAsync(JournalAiSettingsSaveRequest request, CancellationToken cancellationToken)
{
    var settings = await CreateFileSettingsFromRequestAsync(request, cancellationToken);
    await _store.WriteAsync(settings, cancellationToken);
}
```

Add this public method:

```csharp
public async Task<JournalAiSettings> BuildEffectiveCandidateAsync(
    JournalAiSettingsSaveRequest request,
    CancellationToken cancellationToken)
{
    var fileCandidate = await CreateFileSettingsFromRequestAsync(request, cancellationToken);
    var overlay = ResolveEnvironmentOverlay(fileCandidate);
    return ApplyEnvironment(fileCandidate, overlay);
}
```

Add this public method:

```csharp
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

    return new JournalAiProviderApiKeyView(provider.Id, "file", provider.ApiKey);
}
```

Add this private helper below `SaveAsync`:

```csharp
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
```

- [ ] **Step 5: Add masked preview helpers**

Still in `JournalAiSettingsService.cs`, replace `ToView` with:

```csharp
private static JournalAiProviderView ToView(JournalAiProviderSettings provider, string activeProviderId, string source)
{
    var hasApiKey = provider.IsMock || !string.IsNullOrWhiteSpace(provider.ApiKey);
    var isFileBackedKey = string.Equals(source, "file", StringComparison.OrdinalIgnoreCase)
        && !provider.IsMock
        && !string.IsNullOrWhiteSpace(provider.ApiKey);

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
        isFileBackedKey ? MaskApiKey(provider.ApiKey) : string.Empty,
        isFileBackedKey,
        source,
        provider.TimeoutSeconds,
        provider.Temperature,
        provider.MaxTokens,
        provider.StylePreset,
        "not-tested");
}
```

Add:

```csharp
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
```

- [ ] **Step 6: Add reveal endpoint tests**

Add these tests to `tests/Journal.Tests/TodayJournalEndpointTests.cs` after `PutSettingsAi_SavesConfigurationWithoutReturningApiKey`:

```csharp
[Fact]
public async Task GetSettingsAiProviderApiKey_ReturnsFileBackedKeyOnly()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var saveResponse = await client.PutAsJsonAsync(
        "/settings/ai",
        CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: "secret-value"));
    saveResponse.EnsureSuccessStatusCode();

    using var response = await client.GetAsync("/settings/ai/deepseek/api-key");
    response.EnsureSuccessStatusCode();

    var body = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(body);

    Assert.Equal("deepseek", document.RootElement.GetProperty("providerId").GetString());
    Assert.Equal("file", document.RootElement.GetProperty("source").GetString());
    Assert.Equal("secret-value", document.RootElement.GetProperty("apiKey").GetString());
}

[Fact]
public async Task GetSettingsAiProviderApiKey_ForMissingFileKeyReturnsNotFound()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/settings/ai/openai/api-key");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

- [ ] **Step 7: Add reveal endpoint**

In `src/Journal.Api/Program.cs`, add this endpoint after `GET /settings/ai`:

```csharp
app.MapGet("/settings/ai/{providerId}/api-key", async (
    string providerId,
    JournalAiSettingsService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ReadFileApiKeyAsync(providerId, cancellationToken);
    return result is null
        ? Results.NotFound(new { error = "file-backed API key was not found" })
        : Results.Ok(result);
});
```

- [ ] **Step 8: Run backend settings and endpoint tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiSettingsTests|FullyQualifiedName~TodayJournalEndpointTests"
```

Expected: pass.

- [ ] **Step 9: Commit Task 1**

```powershell
git add src/Journal.Infrastructure/Ai/JournalAiProviderSettings.cs src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalAiSettingsTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose safe llm key state"
```

---

## Task 2: Backend Candidate Test and Protected Activation

**Files:**
- Modify: `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Write failing generation-service candidate check test**

Add this test to `tests/Journal.Tests/JournalAiGenerationServiceTests.cs`:

```csharp
[Fact]
public async Task CheckAsync_WithCandidateSettingsUsesCandidateInsteadOfSavedSettings()
{
    var savedSettings = JournalAiSettings.CreateDefault();
    var candidate = JournalAiSettings.CreateDefault();
    var deepSeek = candidate.Providers.Single(item => item.Id == "deepseek") with
    {
        ApiKey = "candidate-key",
        IsEnabled = true
    };
    candidate = candidate with
    {
        ActiveProviderId = "deepseek",
        Providers = candidate.Providers.Select(item => item.Id == "deepseek" ? deepSeek : item).ToArray()
    };
    var runtime = new StaticRuntime(OpenAiCompatibleRunResult.Success(null, """{"ok":true}""", TimeSpan.FromMilliseconds(12)));
    var service = new JournalAiGenerationService(
        new StaticSettingsService(savedSettings),
        new MockAiProvider(),
        new OpenAiCompatibleJournalAiProvider(runtime));

    var result = await service.CheckAsync("deepseek", candidate, CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.Equal("success", result.Status);
}
```

- [ ] **Step 2: Run candidate check test and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiGenerationServiceTests.CheckAsync_WithCandidateSettingsUsesCandidateInsteadOfSavedSettings"
```

Expected: fail because the overload does not exist.

- [ ] **Step 3: Add candidate check overload**

In `src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs`, replace existing `CheckAsync` with:

```csharp
public async Task<JournalAiProviderHealthResult> CheckAsync(
    string? providerId,
    CancellationToken cancellationToken)
{
    var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
    return await CheckAsync(providerId, settings, cancellationToken);
}

public async Task<JournalAiProviderHealthResult> CheckAsync(
    string? providerId,
    JournalAiSettings settings,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(settings);

    if (!TryResolveProvider(settings, providerId, out var providerSettings, out var error))
    {
        return JournalAiProviderHealthResult.Failure(error.Code, null, null, error);
    }

    IJournalAiProvider provider = providerSettings.IsMock ? _mockProvider : _openAiCompatibleProvider;
    return await provider.CheckAsync(providerSettings, cancellationToken);
}
```

- [ ] **Step 4: Write failing endpoint tests for candidate test and activation**

Add these tests to `tests/Journal.Tests/TodayJournalEndpointTests.cs` after `PostSettingsAiTest_WithMockProviderReturnsSuccess`:

```csharp
[Fact]
public async Task PostSettingsAiTest_WithCandidateDoesNotWriteSettingsFile()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();
    var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));

    using var response = await client.PostAsJsonAsync(
        "/settings/ai/test",
        new
        {
            providerId = "deepseek",
            candidate = CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: "")
        });
    response.EnsureSuccessStatusCode();

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.False(document.RootElement.GetProperty("isSuccess").GetBoolean());
    Assert.Equal("missing_api_key", document.RootElement.GetProperty("status").GetString());
    Assert.False(File.Exists(paths.AiSettingsPath()));
}

[Fact]
public async Task PostSettingsAiActivate_WithMockSuccessSavesActiveProvider()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/settings/ai/activate",
        CreateAiSettingsSaveRequest("mock", deepSeekApiKey: ""));
    response.EnsureSuccessStatusCode();

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = document.RootElement;

    Assert.True(root.GetProperty("saved").GetBoolean());
    Assert.True(root.GetProperty("testResult").GetProperty("isSuccess").GetBoolean());
    Assert.Equal("mock", root.GetProperty("settings").GetProperty("activeProviderId").GetString());
}

[Fact]
public async Task PostSettingsAiActivate_WhenTestFailsDoesNotOverwriteExistingSettings()
{
    using var workspace = TempWorkspace.Create();
    using var factory = CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var saveResponse = await client.PutAsJsonAsync(
        "/settings/ai",
        CreateAiSettingsSaveRequest("mock", deepSeekApiKey: ""));
    saveResponse.EnsureSuccessStatusCode();

    using var response = await client.PostAsJsonAsync(
        "/settings/ai/activate",
        CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: ""));
    response.EnsureSuccessStatusCode();

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = document.RootElement;

    Assert.False(root.GetProperty("saved").GetBoolean());
    Assert.Equal("missing_api_key", root.GetProperty("testResult").GetProperty("status").GetString());
    Assert.Equal("mock", root.GetProperty("settings").GetProperty("activeProviderId").GetString());

    using var getResponse = await client.GetAsync("/settings/ai");
    getResponse.EnsureSuccessStatusCode();
    using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
    Assert.Equal("mock", getDocument.RootElement.GetProperty("activeProviderId").GetString());
}
```

- [ ] **Step 5: Run endpoint tests and verify failure**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~TodayJournalEndpointTests.PostSettingsAiTest_WithCandidateDoesNotWriteSettingsFile|FullyQualifiedName~TodayJournalEndpointTests.PostSettingsAiActivate"
```

Expected: fail because `/settings/ai/test` ignores `candidate` and `/settings/ai/activate` does not exist.

- [ ] **Step 6: Extend API request record**

In `src/Journal.Api/Program.cs`, replace:

```csharp
public sealed record AiProviderTestRequest(string ProviderId);
```

with:

```csharp
public sealed record AiProviderTestRequest(
    string ProviderId,
    JournalAiSettingsSaveRequest? Candidate = null);
```

- [ ] **Step 7: Extend `/settings/ai/test`**

Replace the current `app.MapPost("/settings/ai/test", ...)` block with:

```csharp
app.MapPost("/settings/ai/test", async (
    AiProviderTestRequest request,
    JournalAiSettingsService settingsService,
    JournalAiGenerationService generationService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ProviderId))
    {
        return Results.BadRequest(new { error = "providerId is required" });
    }

    try
    {
        var health = request.Candidate is null
            ? await generationService.CheckAsync(request.ProviderId, cancellationToken)
            : await generationService.CheckAsync(
                request.ProviderId,
                await settingsService.BuildEffectiveCandidateAsync(request.Candidate, cancellationToken),
                cancellationToken);

        return Results.Ok(health);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
```

- [ ] **Step 8: Add protected activation endpoint**

Add this endpoint after `/settings/ai/test`:

```csharp
app.MapPost("/settings/ai/activate", async (
    JournalAiSettingsSaveRequest request,
    JournalAiSettingsService settingsService,
    JournalAiGenerationService generationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var candidate = await settingsService.BuildEffectiveCandidateAsync(request, cancellationToken);
        var testResult = await generationService.CheckAsync(candidate.ActiveProviderId, candidate, cancellationToken);
        if (testResult.IsSuccess)
        {
            await settingsService.SaveAsync(request, cancellationToken);
        }

        var view = await settingsService.ReadViewAsync(cancellationToken);
        return Results.Ok(new JournalAiSettingsActivationResult(testResult.IsSuccess, view, testResult));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
```

- [ ] **Step 9: Run backend tests**

Run:

```powershell
dotnet test Journal.slnx --filter "FullyQualifiedName~JournalAiGenerationServiceTests|FullyQualifiedName~TodayJournalEndpointTests|FullyQualifiedName~JournalAiSettingsTests"
```

Expected: pass.

- [ ] **Step 10: Commit Task 2**

```powershell
git add src/Journal.Infrastructure/Ai/JournalAiGenerationService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalAiGenerationServiceTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: protect llm activation with candidate tests"
```

---

## Task 3: Frontend API Client and Today-Page Regenerate Flow

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Modify: `apps/desktop/src/App.tsx`
- Test: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Write failing API client tests**

In `apps/desktop/src/App.test.tsx`, add these imports if missing:

```ts
import {
  activateAiSettings,
  revealAiProviderApiKey,
  testAiProvider
} from "./api";
```

Add tests in `describe("editor API client", ...)`:

```ts
test("testAiProvider sends candidate settings when provided", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
    isSuccess: false,
    status: "missing_api_key",
    safeResponseSnippet: "",
    httpStatus: null,
    latency: null,
    error: null
  }));
  vi.stubGlobal("fetch", fetchMock);
  const candidate = {
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
  };

  await testAiProvider("deepseek", candidate);

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/test", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: "deepseek", candidate })
  });
});

test("activateAiSettings posts protected activation request", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
    saved: true,
    settings: aiSettings,
    testResult: {
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00.0100000",
      error: null
    }
  }));
  vi.stubGlobal("fetch", fetchMock);
  const request = { activeProviderId: "mock", providers: [] };

  await activateAiSettings(request);

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/activate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });
});

test("revealAiProviderApiKey reads file-backed key endpoint", async () => {
  const fetchMock = vi.fn().mockResolvedValue(mockJsonResponse({
    providerId: "deepseek",
    source: "file",
    apiKey: "secret-value"
  }));
  vi.stubGlobal("fetch", fetchMock);

  await revealAiProviderApiKey("deepseek");

  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/settings/ai/deepseek/api-key", undefined);
});
```

- [ ] **Step 2: Run API client tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "testAiProvider sends candidate|activateAiSettings|revealAiProviderApiKey"
```

Expected: fail because the functions/types do not exist or `testAiProvider` does not accept a candidate.

- [ ] **Step 3: Extend frontend API types and functions**

In `apps/desktop/src/api.ts`, update `AiProviderView`:

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
  apiKeyPreview: string;
  canRevealApiKey: boolean;
  source: string;
  timeoutSeconds: number;
  temperature: number;
  maxTokens: number;
  stylePreset: string;
  lastTestStatus: string;
};
```

Add:

```ts
export type AiProviderApiKeyView = {
  providerId: string;
  source: string;
  apiKey: string;
};

export type AiSettingsActivationResult = {
  saved: boolean;
  settings: AiSettingsView;
  testResult: AiProviderHealthResult;
};
```

Replace `testAiProvider` with:

```ts
export function testAiProvider(
  providerId: string,
  candidate?: AiSettingsSaveRequest
): Promise<AiProviderHealthResult> {
  return requestJson<AiProviderHealthResult>("/settings/ai/test", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(candidate ? { providerId, candidate } : { providerId })
  });
}
```

Add:

```ts
export function activateAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsActivationResult> {
  return requestJson<AiSettingsActivationResult>("/settings/ai/activate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });
}

export function revealAiProviderApiKey(providerId: string): Promise<AiProviderApiKeyView> {
  return requestJson<AiProviderApiKeyView>(`/settings/ai/${encodeURIComponent(providerId)}/api-key`);
}
```

- [ ] **Step 4: Write failing today-page regenerate tests**

Replace the two old `LlmSettingsPanel` regenerate tests with app-level tests:

```ts
test("shows regenerate draft action on today page instead of LLM settings panel", async () => {
  const fetchMock = createInitialFetchMock();
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);

  expect(await screen.findByRole("button", { name: "重新整理今日草稿" })).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: /LLM/ }));

  expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
  expect(within(screen.getByRole("region", { name: "LLM 配置面板" })).queryByRole("button", { name: "重新整理草稿" })).not.toBeInTheDocument();
});

test("regenerates draft from today page after confirmation", async () => {
  const fetchMock = createInitialFetchMock()
    .mockResolvedValueOnce(mockJsonResponse(reviewingToday))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState()))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings));
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);

  const button = await screen.findByRole("button", { name: "重新整理今日草稿" });
  fireEvent.click(button);
  expect(screen.getByText("这会覆盖当前草稿内容，但不会影响正式日记。")).toBeInTheDocument();

  fireEvent.click(button);

  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/journal/today/draft/regenerate", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ providerId: null })
  }));
});
```

- [ ] **Step 5: Run regenerate tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "regenerate draft action|regenerates draft from today page"
```

Expected: fail because the action still lives in `LlmSettingsPanel`.

- [ ] **Step 6: Wire App handlers**

In `apps/desktop/src/App.tsx`, import:

```ts
import {
  activateAiSettings,
  revealAiProviderApiKey,
  testAiProvider,
  type AiSettingsActivationResult,
  type AiSettingsSaveRequest
} from "./api";
```

Replace `handleSaveAiSettings` with:

```ts
async function handleActivateAiSettings(request: AiSettingsSaveRequest): Promise<AiSettingsActivationResult> {
  const settingsRequestId = settingsRequestIdRef.current + 1;
  settingsRequestIdRef.current = settingsRequestId;
  setIsSettingsSubmitting(true);
  try {
    const result = await activateAiSettings(request);
    if (settingsRequestId === settingsRequestIdRef.current) {
      setAiSettings(result.settings);
      setApiError("");
    }
    return result;
  } catch (caught) {
    if (settingsRequestId === settingsRequestIdRef.current) {
      setApiError(getErrorMessage(caught));
    }
    throw caught;
  } finally {
    if (settingsRequestId === settingsRequestIdRef.current) {
      setIsSettingsSubmitting(false);
    }
  }
}
```

Replace `handleTestAiProvider` with:

```ts
async function handleTestAiProvider(
  providerId: string,
  candidate?: AiSettingsSaveRequest
): Promise<AiProviderHealthResult> {
  return await testAiProvider(providerId, candidate);
}
```

Add:

```ts
async function handleRevealAiProviderKey(providerId: string) {
  return await revealAiProviderApiKey(providerId);
}
```

- [ ] **Step 7: Move regenerate action into today page**

In `App.tsx`, add state near other UI state:

```ts
const [pendingRegenerateDraft, setPendingRegenerateDraft] = useState(false);
```

Add this helper:

```ts
async function handleRegenerateCurrentDraft() {
  if (!pendingRegenerateDraft) {
    setPendingRegenerateDraft(true);
    return;
  }

  setPendingRegenerateDraft(false);
  await handleRegenerateDraft();
}
```

In the input dock, before `canConfirm` panel, add:

```tsx
{hasEditableJournal ? (
  <section className="dock-block regenerate-panel" aria-label="重新整理">
    <div className="section-head">
      <h2>重新整理</h2>
      <span>{activeProviderName}</span>
    </div>
    <p>
      {pendingRegenerateDraft
        ? "这会覆盖当前草稿内容，但不会影响正式日记。"
        : "使用当前 LLM 重新整理 reviewing draft。"}
    </p>
    <button type="button" className="secondary-action" onClick={handleRegenerateCurrentDraft} disabled={isBusy}>
      重新整理今日草稿
    </button>
  </section>
) : null}
```

Update `LlmSettingsPanel` props:

```tsx
<LlmSettingsPanel
  settings={aiSettings}
  isBusy={isBusy || isSettingsSubmitting}
  onClose={() => setIsLlmPanelOpen(false)}
  onActivate={handleActivateAiSettings}
  onTest={handleTestAiProvider}
  onRevealApiKey={handleRevealAiProviderKey}
/>
```

- [ ] **Step 8: Run frontend API/App tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
```

Expected at this point: API and App tests for new client/regenerate pass; panel tests fail until Task 4 updates `LlmSettingsPanel`.

- [ ] **Step 9: Commit Task 3**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/App.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: move llm draft regeneration to today page"
```

---

## Task 4: LLM Settings Panel Productized UX

**Files:**
- Modify: `apps/desktop/package.json`
- Modify: `apps/desktop/package-lock.json`
- Modify: `apps/desktop/src/LlmSettingsPanel.tsx`
- Modify: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add lucide-react**

Run:

```powershell
npm install lucide-react --prefix apps/desktop
```

Expected: `apps/desktop/package.json` includes `lucide-react` under `dependencies`, and `apps/desktop/package-lock.json` is updated.

- [ ] **Step 2: Update panel prop types**

In `apps/desktop/src/LlmSettingsPanel.tsx`, replace props with:

```ts
type LlmSettingsPanelProps = {
  settings: AiSettingsView;
  isBusy: boolean;
  onClose: () => void;
  onActivate: (request: AiSettingsSaveRequest) => Promise<AiSettingsActivationResult>;
  onTest: (providerId: string, candidate?: AiSettingsSaveRequest) => Promise<AiProviderHealthResult>;
  onRevealApiKey: (providerId: string) => Promise<AiProviderApiKeyView>;
};
```

Update imports from `./api`:

```ts
import {
  type AiProviderApiKeyView,
  type AiProviderHealthResult,
  type AiProviderSaveRequest,
  type AiSettingsActivationResult,
  type AiSettingsSaveRequest,
  type AiSettingsView
} from "./api";
```

Add icon imports:

```ts
import { Eye, EyeOff, LockKeyhole } from "lucide-react";
```

- [ ] **Step 3: Replace old panel tests with productized UX tests**

In `apps/desktop/src/App.test.tsx`, replace the current `describe("LlmSettingsPanel", ...)` block with tests that cover these exact expectations:

```ts
test("shows productized provider state and key status", () => {
  render(
    <LlmSettingsPanel
      settings={aiSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={vi.fn()}
      onTest={vi.fn()}
      onRevealApiKey={vi.fn()}
    />
  );

  expect(screen.getByRole("region", { name: "LLM 配置面板" })).toBeInTheDocument();
  expect(screen.getByText("本地备用")).toBeInTheDocument();
  expect(screen.getByText("默认预设")).toBeInTheDocument();
  expect(screen.getByText("无需 API Key")).toBeInTheDocument();
  expect(screen.queryByText("key ready")).not.toBeInTheDocument();
  expect(screen.queryByText("no key")).not.toBeInTheDocument();
});

test("shows file-backed key as masked preview and reveals on eye click", async () => {
  const fileSettings = {
    ...aiSettings,
    activeProviderId: "deepseek",
    providers: aiSettings.providers.map(provider =>
      provider.id === "deepseek"
        ? {
            ...provider,
            isActive: true,
            hasApiKey: true,
            source: "file",
            apiKeyPreview: "sk-••••••••••••••••4A7C",
            canRevealApiKey: true
          }
        : { ...provider, isActive: false }
    )
  };
  const onRevealApiKey = vi.fn().mockResolvedValue({
    providerId: "deepseek",
    source: "file",
    apiKey: "sk-file-backed-secret-4A7C"
  });

  render(
    <LlmSettingsPanel
      settings={fileSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={vi.fn()}
      onTest={vi.fn()}
      onRevealApiKey={onRevealApiKey}
    />
  );

  expect(screen.getByDisplayValue("sk-••••••••••••••••4A7C")).toBeInTheDocument();

  fireEvent.click(screen.getByRole("button", { name: "查看 API Key" }));

  expect(await screen.findByDisplayValue("sk-file-backed-secret-4A7C")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "隐藏 API Key" })).toBeInTheDocument();
  expect(onRevealApiKey).toHaveBeenCalledWith("deepseek");
});

test("shows environment key as loaded and not revealable", () => {
  const envSettings = {
    ...aiSettings,
    activeProviderId: "openai",
    providers: aiSettings.providers.map(provider =>
      provider.id === "openai"
        ? {
            ...provider,
            isActive: true,
            hasApiKey: true,
            source: "environment",
            apiKeyPreview: "",
            canRevealApiKey: false
          }
        : { ...provider, isActive: false }
    )
  };

  render(
    <LlmSettingsPanel
      settings={envSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={vi.fn()}
      onTest={vi.fn()}
      onRevealApiKey={vi.fn()}
    />
  );

  expect(screen.getByText("已从环境变量加载，不在界面显示")).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "查看 API Key" })).not.toBeInTheDocument();
  expect(screen.getByTestId("environment-key-lock")).toBeInTheDocument();
});

test("tests current form with candidate settings and marks old result stale after edits", async () => {
  const onTest = vi.fn().mockResolvedValue({
    isSuccess: true,
    status: "success",
    safeResponseSnippet: "{\"ok\":true}",
    httpStatus: 200,
    latency: "00:00:00.1200000",
    error: null
  });

  render(
    <LlmSettingsPanel
      settings={aiSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={vi.fn()}
      onTest={onTest}
      onRevealApiKey={vi.fn()}
    />
  );

  fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
  fireEvent.change(screen.getByLabelText("模型"), { target: { value: "deepseek-next" } });
  fireEvent.click(screen.getByRole("button", { name: "测试当前表单" }));

  await waitFor(() => expect(onTest).toHaveBeenCalled());
  expect(onTest.mock.calls[0][0]).toBe("deepseek");
  expect(onTest.mock.calls[0][1].providers.find((provider: AiProviderSaveRequest) => provider.id === "deepseek").model).toBe("deepseek-next");
  expect(await screen.findByText("连接测试通过")).toBeInTheDocument();

  fireEvent.change(screen.getByLabelText("模型"), { target: { value: "deepseek-latest" } });

  expect(screen.getByText("测试结果已过期")).toBeInTheDocument();
});

test("save and activate keeps panel open and does not switch provider on failed activation", async () => {
  const onActivate = vi.fn().mockResolvedValue({
    saved: false,
    settings: aiSettings,
    testResult: {
      isSuccess: false,
      status: "missing_api_key",
      safeResponseSnippet: "",
      httpStatus: null,
      latency: null,
      error: {
        stage: "configuration",
        code: "missing_api_key",
        message: "LLM API key is required.",
        technicalDetails: "Provider key is empty."
      }
    }
  });

  render(
    <LlmSettingsPanel
      settings={aiSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={onActivate}
      onTest={vi.fn()}
      onRevealApiKey={vi.fn()}
    />
  );

  fireEvent.click(screen.getByRole("button", { name: /DeepSeek/ }));
  fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

  expect(await screen.findByText("测试失败，配置没有保存")).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "DeepSeek" })).toBeInTheDocument();
});

test("save and activate success shows today-page next step", async () => {
  const activatedSettings = {
    ...aiSettings,
    activeProviderId: "deepseek",
    providers: aiSettings.providers.map(provider =>
      provider.id === "deepseek"
        ? { ...provider, isActive: true, hasApiKey: true, source: "file", apiKeyPreview: "sk-••••••••••••••••4A7C", canRevealApiKey: true }
        : { ...provider, isActive: false }
    )
  };
  const onActivate = vi.fn().mockResolvedValue({
    saved: true,
    settings: activatedSettings,
    testResult: {
      isSuccess: true,
      status: "success",
      safeResponseSnippet: "{\"ok\":true}",
      httpStatus: 200,
      latency: "00:00:00.1200000",
      error: null
    }
  });

  render(
    <LlmSettingsPanel
      settings={aiSettings}
      isBusy={false}
      onClose={vi.fn()}
      onActivate={onActivate}
      onTest={vi.fn()}
      onRevealApiKey={vi.fn()}
    />
  );

  fireEvent.click(screen.getByRole("button", { name: "保存并启用" }));

  expect(await screen.findByText("可以回到今日页重新整理")).toBeInTheDocument();
});
```

- [ ] **Step 4: Run panel tests and verify failure**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "productized provider state|file-backed key|environment key|current form|failed activation|today-page next step"
```

Expected: fail because the panel still uses old labels, props, and regenerate controls.

- [ ] **Step 5: Implement local panel state**

In `LlmSettingsPanel.tsx`, use these state values:

```ts
const [selectedId, setSelectedId] = useState(settings.activeProviderId);
const selectedIdRef = useRef(settings.activeProviderId);
const [providers, setProviders] = useState<AiProviderSaveRequest[]>(() => toSaveRequests(settings));
const [dirtyProviderIds, setDirtyProviderIds] = useState<Set<string>>(() => new Set());
const [revealedKeyProviderId, setRevealedKeyProviderId] = useState<string | null>(null);
const [revealedKey, setRevealedKey] = useState("");
const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);
const [testResult, setTestResult] = useState<AiProviderHealthResult | null>(null);
const [testResultIsStale, setTestResultIsStale] = useState(false);
const [activationResult, setActivationResult] = useState<AiSettingsActivationResult | null>(null);
```

Add helpers:

```ts
function toSaveRequests(view: AiSettingsView): AiProviderSaveRequest[] {
  return view.providers.map(provider => ({
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
  }));
}

function createCandidate(activeProviderId: string, draftProviders: AiProviderSaveRequest[]): AiSettingsSaveRequest {
  return {
    activeProviderId,
    providers: draftProviders.map(provider => ({
      ...provider,
      isEnabled: provider.id === activeProviderId
    }))
  };
}
```

Add provider display helpers:

```ts
function providerSourceLabel(source: string) {
  switch (source) {
    case "environment":
      return "环境变量";
    case "file":
      return "本机配置文件";
    case "default":
    case "preset":
      return "默认预设";
    default:
      return source;
  }
}

function providerStatusLabel(provider: AiProviderView, selectedTestResult: AiProviderHealthResult | null) {
  if (selectedTestResult && !selectedTestResult.isSuccess) {
    return "测试失败";
  }

  if (provider.isActive) {
    return "已启用";
  }

  if (provider.id === "mock" || provider.hasApiKey) {
    return "已配置";
  }

  return "需要配置";
}
```

- [ ] **Step 6: Implement key field behavior**

Render API key state with these rules:

```tsx
{selectedView?.source === "environment" ? (
  <div className="llm-key-display" aria-label="API Key 已从环境变量加载">
    <LockKeyhole size={16} strokeWidth={1.8} aria-hidden="true" data-testid="environment-key-lock" />
    已从环境变量加载，不在界面显示
  </div>
) : selectedView?.id === "mock" ? (
  <div className="llm-key-display" aria-label="Mock 无需 API Key">
    无需 API Key
  </div>
) : (
  <div className="llm-key-row">
    <input
      aria-label="API Key"
      value={
        revealedKeyProviderId === selected.id
          ? revealedKey
          : selected.apiKey || selectedView?.apiKeyPreview || ""
      }
      placeholder={selectedView?.hasApiKey ? "API Key 已配置" : "未填写 API Key"}
      onChange={event => updateSelected({ apiKey: event.target.value })}
    />
    {selectedView?.canRevealApiKey && !selected.apiKey ? (
      <button
        type="button"
        className="icon-action"
        aria-label={revealedKeyProviderId === selected.id ? "隐藏 API Key" : "查看 API Key"}
        onClick={handleToggleApiKeyReveal}
      >
        {revealedKeyProviderId === selected.id ? (
          <EyeOff size={17} strokeWidth={1.8} aria-hidden="true" />
        ) : (
          <Eye size={17} strokeWidth={1.8} aria-hidden="true" />
        )}
      </button>
    ) : null}
  </div>
)}
```

Add toggle handler:

```ts
async function handleToggleApiKeyReveal() {
  if (!selected) {
    return;
  }

  if (revealedKeyProviderId === selected.id) {
    setRevealedKeyProviderId(null);
    setRevealedKey("");
    return;
  }

  const result = await onRevealApiKey(selected.id);
  if (selectedIdRef.current === selected.id) {
    setRevealedKeyProviderId(selected.id);
    setRevealedKey(result.apiKey);
  }
}
```

In provider switching and `onClose`, reset `revealedKeyProviderId` and `revealedKey`.

- [ ] **Step 7: Implement current-form test and protected activation**

Add:

```ts
async function handleTestCurrentForm() {
  if (!selected) {
    return;
  }

  const providerId = selected.id;
  const candidate = createCandidate(providerId, providers);
  setTestResultIsStale(false);
  setActivationResult(null);

  let result: AiProviderHealthResult;
  try {
    result = await onTest(providerId, candidate);
  } catch (caught) {
    result = createClientFailure(caught, "The current LLM form test failed before a result was returned.");
  }

  if (selectedIdRef.current === providerId) {
    setTestResult(result);
  }
}

async function handleActivate(event: FormEvent<HTMLFormElement>) {
  event.preventDefault();
  if (!selected) {
    return;
  }

  const providerId = selected.id;
  const candidate = createCandidate(providerId, providers);
  setTestResultIsStale(false);

  let result: AiSettingsActivationResult;
  try {
    result = await onActivate(candidate);
  } catch (caught) {
    result = {
      saved: false,
      settings,
      testResult: createClientFailure(caught, "The LLM activation request failed before a result was returned.")
    };
  }

  if (selectedIdRef.current === providerId) {
    setActivationResult(result);
    setTestResult(result.testResult);
    if (result.saved) {
      setDirtyProviderIds(new Set());
      setRevealedKeyProviderId(null);
      setRevealedKey("");
    }
  }
}
```

Add:

```ts
function createClientFailure(caught: unknown, technicalDetails: string): AiProviderHealthResult {
  return {
    isSuccess: false,
    status: "request_failed",
    safeResponseSnippet: "",
    httpStatus: null,
    latency: null,
    error: {
      stage: "client",
      code: "request_failed",
      message: caught instanceof Error ? caught.message : "LLM 请求失败。",
      technicalDetails
    }
  };
}
```

In `updateSelected`, mark dirty and stale:

```ts
setDirtyProviderIds(current => new Set(current).add(selected.id));
setTestResultIsStale(Boolean(testResult));
setActivationResult(null);
```

- [ ] **Step 8: Render advanced summary and diagnostics**

Render advanced summary:

```tsx
<section className="llm-settings-card">
  <button type="button" className="llm-advanced-summary" onClick={() => setIsAdvancedOpen(current => !current)}>
    <span>
      高级参数：temperature {selected.temperature} · max tokens {selected.maxTokens} · timeout {selected.timeoutSeconds}s · JSON 模式开启
    </span>
    <strong>{isAdvancedOpen ? "收起" : "展开"}</strong>
  </button>
  {isAdvancedOpen ? (
    <div className="llm-advanced-fields">
      <!-- Keep current number inputs here. -->
    </div>
  ) : null}
</section>
```

Render diagnostics with these required texts:

```tsx
<section className={`llm-settings-card ${testResult?.isSuccess === false ? "attention-panel" : ""}`}>
  <span className="rail-label">诊断与下一步</span>
  {testResultIsStale ? <p>测试结果已过期</p> : null}
  {testResult?.isSuccess ? <h2>连接测试通过</h2> : null}
  {testResult && !testResult.isSuccess ? <h2>测试失败，配置没有保存</h2> : null}
  {activationResult?.saved ? <p>可以回到今日页重新整理</p> : null}
  {testResult ? (
    <dl className="llm-diagnostics-list">
      <dt>HTTP</dt>
      <dd>{testResult.httpStatus ?? "未返回"}</dd>
      <dt>Status</dt>
      <dd>{testResult.status}</dd>
      <dt>Provider</dt>
      <dd>{selected.displayName}</dd>
      <dt>Model</dt>
      <dd>{selected.model || "未填写"}</dd>
      <dt>Base URL</dt>
      <dd>{selected.baseUrl}</dd>
    </dl>
  ) : (
    <p>测试会向当前 LLM 发送一次最小请求，可能产生少量 token 消耗。</p>
  )}
  {testResult?.error?.message ? <p>{testResult.error.message}</p> : null}
  {testResult?.error ? (
    <details open>
      <summary>技术详情</summary>
      <pre>{testResult.error.technicalDetails}</pre>
    </details>
  ) : null}
</section>
```

- [ ] **Step 9: Remove regenerate UI from panel**

Delete:

- `pendingRegenerate` state.
- `handleRegenerate`.
- The entire `Regenerate` settings card.
- `onRegenerate` prop usage.

- [ ] **Step 10: Run panel tests**

Run:

```powershell
npm test --prefix apps/desktop -- App.test.tsx -t "LlmSettingsPanel"
```

Expected: pass.

- [ ] **Step 11: Commit Task 4**

```powershell
git add apps/desktop/package.json apps/desktop/package-lock.json apps/desktop/src/LlmSettingsPanel.tsx apps/desktop/src/App.test.tsx
git commit -m "feat: polish llm settings panel ux"
```

---

## Task 5: Styling, Full Verification, and Docs Alignment

**Files:**
- Modify: `apps/desktop/src/styles.css`
- Optional modify: `docs/superpowers/specs/2026-05-10-llm-settings-ux-polish-design.md` only if implementation reveals a necessary contract clarification.

- [ ] **Step 1: Update settings panel styles**

In `apps/desktop/src/styles.css`, replace the existing `.llm-settings-*` block with styles aligned to the prototype:

```css
.llm-settings-overlay {
  position: fixed;
  inset: 10px;
  z-index: 10;
  display: grid;
  grid-template-rows: 54px minmax(0, 1fr);
  border: 1px solid #bdb7aa;
  border-radius: 8px;
  background: rgba(243, 241, 235, 0.97);
  box-shadow: 0 28px 80px rgba(42, 36, 28, 0.24);
}

.llm-settings-grid {
  min-height: 0;
  display: grid;
  grid-template-columns: 250px minmax(380px, 1fr) 390px;
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.llm-provider-card {
  min-height: 86px;
  padding: 12px;
  display: grid;
  gap: 7px;
  text-align: left;
}

.llm-provider-card.active {
  border-color: rgba(43, 104, 96, 0.42);
  background: #e8f3ef;
}

.llm-provider-status {
  width: fit-content;
  border-radius: 999px;
  padding: 3px 8px;
  background: #eee7dd;
  color: #655d53;
  font-size: 11px;
  font-weight: 800;
}

.llm-provider-status.active,
.llm-provider-status.ready {
  background: #dceee8;
  color: #2d746a;
}

.llm-provider-status.failed {
  background: #f8e3df;
  color: #963c33;
}

.llm-key-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 38px;
  gap: 8px;
}

.llm-key-display,
.llm-advanced-summary {
  min-height: 38px;
  border: 1px solid #c9c5bb;
  border-radius: 6px;
  background: #fffdf8;
  color: #655d53;
  padding: 9px 10px;
}

.llm-key-display {
  display: flex;
  align-items: center;
  gap: 8px;
}

.icon-action {
  width: 38px;
  min-height: 38px;
  display: grid;
  place-items: center;
  border: 1px solid #c9c5bb;
  border-radius: 6px;
  background: #faf7f0;
  color: #655d53;
}

.icon-action:hover {
  border-color: #9fc9be;
  color: #2d746a;
}

.icon-action:focus-visible {
  outline: 2px solid rgba(45, 116, 106, 0.35);
  outline-offset: 2px;
}

.icon-action:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.llm-advanced-summary {
  width: 100%;
  display: flex;
  justify-content: space-between;
  gap: 10px;
  text-align: left;
}

.llm-diagnostics-list {
  display: grid;
  grid-template-columns: 82px minmax(0, 1fr);
  gap: 6px 10px;
  margin: 10px 0;
  font-size: 12px;
}

.llm-diagnostics-list dt {
  color: #655d53;
  font-weight: 800;
}

.llm-diagnostics-list dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.regenerate-panel {
  border-color: #c7d6e8;
  background: #eef5fc;
}
```

Keep existing shared button and responsive styles. If selectors already exist, merge carefully instead of duplicating conflicting blocks.

- [ ] **Step 2: Run frontend tests**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: all Vitest tests pass.

- [ ] **Step 3: Run backend tests**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: all xUnit tests pass.

- [ ] **Step 4: Run frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: Vite/TypeScript build succeeds.

- [ ] **Step 5: Search for secret leakage and old UX labels**

Run:

```powershell
rg -n "key ready|no key|测试已保存配置，不会测试当前未保存草稿|重新整理草稿\"|apiKey.:|Authorization\\s*[:=]\\s*Bearer" apps/desktop/src src tests docs/superpowers/specs
```

Expected:

- No `key ready` or `no key` in frontend UI.
- No old saved-config warning string.
- `apiKey` appears only in request DTOs, tests, and explicit reveal endpoint code.
- No raw Authorization bearer values.

- [ ] **Step 6: Optional manual visual check**

Start the app:

```powershell
dotnet run --project src/Journal.Api
```

In another shell:

```powershell
npm run desktop --prefix apps/desktop
```

Manual checks:

- Open LLM settings.
- File-backed key appears masked.
- Environment key appears as loaded and cannot be revealed.
- Editing model makes test result stale.
- Testing current form uses unsaved model value.
- Failed activation leaves old active Provider in the top status.
- Successful activation shows “可以回到今日页重新整理”.
- Today page has “重新整理今日草稿”.
- Settings panel no longer has draft regeneration buttons.

- [ ] **Step 7: Commit Task 5**

```powershell
git add apps/desktop/src/styles.css
git commit -m "style: refine llm settings ux"
```

If Task 5 included small test or doc fixes, include those files in the same commit.

---

## Final Verification Before Merge

Run all three commands from repository root:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected:

- `dotnet test Journal.slnx` exits 0.
- `npm test --prefix apps/desktop` exits 0.
- `npm run build --prefix apps/desktop` exits 0.

Then run:

```powershell
git status --short
```

Expected: no uncommitted changes.

## Self-Review Notes

Spec coverage:

- API Key visibility: Task 1 and Task 4.
- Environment vs file-backed Key behavior: Task 1 and Task 4.
- Candidate form test without saving: Task 2, Task 3, Task 4.
- Protected save-and-enable: Task 2, Task 3, Task 4.
- Productized provider statuses: Task 4 and Task 5.
- Advanced collapsed summary: Task 4 and Task 5.
- Technical diagnostics priority: Task 4.
- Regenerate moved to today page: Task 3 and Task 4.
- Full verification: Task 5.

Important implementation note:

- The plan adds `GET /settings/ai/{providerId}/api-key` as a narrow implementation detail for the spec requirement “配置文件 Key 可通过眼睛临时查看完整值”. This preserves the stronger invariant that `GET /settings/ai` never returns `apiKey`, and it never reveals environment variable keys.
