using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Journal.Tests;

public sealed class TodayJournalEndpointTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 8);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-08T08:05:00+08:00");

    [Fact]
    public async Task GetToday_ReturnsEmptyStateAndIsoDate()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/journal/today");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("empty", root.GetProperty("status").GetString());
        Assert.Equal("2026-05-08", root.GetProperty("date").GetProperty("isoDate").GetString());
    }

    [Fact]
    public async Task PostTodayInputs_CreatesReviewingDraftWithSectionMarker()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "昨天完成了 API 暴露 #Journal", source = "text" });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("reviewing", root.GetProperty("status").GetString());
        var draft = root.GetProperty("draft");
        Assert.Equal("reviewing", draft.GetProperty("status").GetString());
        Assert.Contains(
            "<!-- journal:section raw-inputs -->",
            draft.GetProperty("markdown").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostTodayInputs_WithBlankText_ReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "   ", source = "text" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("text is required", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostTodayDraftConfirm_WritesEntryAndReturnsProcessed()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天确认 draft 并写入 entry #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var response = await client.PostAsync("/journal/today/draft/confirm", content: null);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal("processed", document.RootElement.GetProperty("status").GetString());
        Assert.True(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task PostTodayDraftConfirm_WithoutDraft_ReturnsConflict()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/journal/today/draft/confirm", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("draft does not exist", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTodayEditor_ReturnsEditorStateWithNestedToday()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "昨天完成了 API，今天准备编辑 JMF #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/journal/today/editor");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("reviewing", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("canConfirm").GetBoolean());
        Assert.True(root.GetProperty("validation").GetProperty("isValid").GetBoolean());
        Assert.Equal("reviewing", root.GetProperty("today").GetProperty("status").GetString());
        Assert.Contains(
            root.GetProperty("sections").EnumerateArray(),
            section => section.GetProperty("id").GetString() == "today-focus");
        Assert.True(root.GetProperty("availableOptionalSections").ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task PutTodayEditorBlocks_UpdatesTodayFocusAndReturnsReviewingEditorState()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "昨天完成了草稿，今天准备 block save #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/blocks",
            new { sections = new[] { new { id = "today-focus", content = "- updated" } } });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("reviewing", root.GetProperty("status").GetString());
        Assert.Equal("reviewing", root.GetProperty("today").GetProperty("status").GetString());
        Assert.True(root.GetProperty("validation").GetProperty("isValid").GetBoolean());
        Assert.Contains(
            root.GetProperty("sections").EnumerateArray(),
            section =>
                section.GetProperty("id").GetString() == "today-focus"
                && section.GetProperty("content").GetString() == "- updated");
    }

    [Fact]
    public async Task PutTodayEditorBlocks_WithRawInputsReturnsAttentionAndDoesNotOverwriteEntry()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天确认正式 entry，再验证 raw inputs 安全 #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var confirmResponse = await client.PostAsync("/journal/today/draft/confirm", content: null);
        confirmResponse.EnsureSuccessStatusCode();

        var entryPath = paths.EntryPath(date);
        var originalEntryText = await File.ReadAllTextAsync(entryPath, Encoding.UTF8);

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/blocks",
            new { sections = new[] { new { id = "raw-inputs", content = "- unsafe overwrite" } } });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var currentEntryText = await File.ReadAllTextAsync(entryPath, Encoding.UTF8);

        Assert.Equal("attention", root.GetProperty("status").GetString());
        Assert.Equal("attention", root.GetProperty("today").GetProperty("status").GetString());
        Assert.Contains(
            root.GetProperty("today").GetProperty("errors").EnumerateArray(),
            error => error.GetString()!.Contains("Raw inputs cannot be edited", StringComparison.Ordinal));
        Assert.Equal(originalEntryText, currentEntryText);
    }

    [Fact]
    public async Task PutTodayEditorBlocks_WithMalformedShapeReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天验证 malformed block request #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var response = await client.PutAsync(
            "/journal/today/editor/blocks",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Contains("sections is required", document.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Services_ResolveJournalAiSettingsServiceConcreteAndReaderAsSameInstance()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);

        var concrete = factory.Services.GetRequiredService<JournalAiSettingsService>();
        var reader = factory.Services.GetRequiredService<IJournalAiSettingsReader>();

        Assert.Same(concrete, reader);
    }

    [Fact]
    public async Task GetSettingsAi_ReturnsSafeProviderView()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/settings/ai");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("mock", root.GetProperty("activeProviderId").GetString());
        Assert.Contains(
            root.GetProperty("providers").EnumerateArray(),
            provider => provider.GetProperty("id").GetString() == "deepseek");
        Assert.DoesNotContain("\"apiKey\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutSettingsAi_SavesConfigurationWithoutReturningApiKey()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/settings/ai",
            CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: "secret-value"));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var deepSeek = root.GetProperty("providers").EnumerateArray()
            .Single(provider => provider.GetProperty("id").GetString() == "deepseek");

        Assert.DoesNotContain("secret-value", body, StringComparison.Ordinal);
        Assert.True(deepSeek.GetProperty("hasApiKey").GetBoolean());

        using var getResponse = await client.GetAsync("/settings/ai");
        getResponse.EnsureSuccessStatusCode();

        using var getDocument = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        Assert.Equal("deepseek", getDocument.RootElement.GetProperty("activeProviderId").GetString());
    }

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

    [Fact]
    public async Task GetSettingsAiProviderApiKey_WhenEnvironmentOverridesFileKeyReturnsNotFound()
    {
        using var workspace = TempWorkspace.Create();
        using var fileFactory = CreateFactory(workspace.Root);
        using var fileClient = fileFactory.CreateClient();

        using var saveResponse = await fileClient.PutAsJsonAsync(
            "/settings/ai",
            CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: "file-secret"));
        saveResponse.EnsureSuccessStatusCode();

        using var envFactory = CreateFactory(workspace.Root, new Dictionary<string, string?>
        {
            ["DEEPSEEK_API_KEY"] = "env-secret"
        });
        using var envClient = envFactory.CreateClient();

        using var getSettingsResponse = await envClient.GetAsync("/settings/ai");
        getSettingsResponse.EnsureSuccessStatusCode();
        using var settingsDocument = await JsonDocument.ParseAsync(await getSettingsResponse.Content.ReadAsStreamAsync());
        var deepSeek = settingsDocument.RootElement.GetProperty("providers").EnumerateArray()
            .Single(provider => provider.GetProperty("id").GetString() == "deepseek");

        using var response = await envClient.GetAsync("/settings/ai/deepseek/api-key");

        Assert.Equal("environment", deepSeek.GetProperty("source").GetString());
        Assert.False(deepSeek.GetProperty("canRevealApiKey").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutSettingsAi_WithMalformedRequestReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/settings/ai",
            new { activeProviderId = "deepseek", providers = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutSettingsAi_WithUnknownActiveProviderReturnsBadRequestAndPreservesExistingConfiguration()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var validResponse = await client.PutAsJsonAsync(
            "/settings/ai",
            CreateAiSettingsSaveRequest("deepseek", deepSeekApiKey: "secret-value"));
        validResponse.EnsureSuccessStatusCode();

        using var response = await client.PutAsJsonAsync(
            "/settings/ai",
            CreateAiSettingsSaveRequest("openai", deepSeekApiKey: "replacement-secret"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var getResponse = await client.GetAsync("/settings/ai");
        getResponse.EnsureSuccessStatusCode();

        using var getDocument = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        Assert.Equal("deepseek", getDocument.RootElement.GetProperty("activeProviderId").GetString());
    }

    [Fact]
    public async Task PostSettingsAiTest_WithBlankProviderReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/settings/ai/test",
            new { providerId = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("providerId is required", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostSettingsAiTest_WithMockProviderReturnsSuccess()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/settings/ai/test",
            new { providerId = "mock" });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("success", root.GetProperty("status").GetString());
    }

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
    public async Task PostSettingsAiActivate_WithMockSuccessPersistsCandidateSettings()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        const string persistedSecret = "persisted-secret";
        const string persistedModel = "deepseek-persisted-model";
        const string persistedBaseUrl = "https://persisted.deepseek.example/v1";

        using var response = await client.PostAsJsonAsync(
            "/settings/ai/activate",
            CreateAiSettingsSaveRequest(
                "mock",
                deepSeekApiKey: persistedSecret,
                deepSeekModel: persistedModel,
                deepSeekBaseUrl: persistedBaseUrl));
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.GetProperty("saved").GetBoolean());
        Assert.True(root.GetProperty("testResult").GetProperty("isSuccess").GetBoolean());
        Assert.Equal("mock", root.GetProperty("settings").GetProperty("activeProviderId").GetString());
        Assert.True(File.Exists(paths.AiSettingsPath()));

        using var getResponse = await client.GetAsync("/settings/ai");
        getResponse.EnsureSuccessStatusCode();

        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var deepSeek = getDocument.RootElement.GetProperty("providers").EnumerateArray()
            .Single(provider => provider.GetProperty("id").GetString() == "deepseek");

        Assert.Equal("mock", getDocument.RootElement.GetProperty("activeProviderId").GetString());
        Assert.True(deepSeek.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("file", deepSeek.GetProperty("source").GetString());
        Assert.Equal(persistedModel, deepSeek.GetProperty("model").GetString());
        Assert.Equal(persistedBaseUrl, deepSeek.GetProperty("baseUrl").GetString());
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
            new { text = "今天先确认正式 entry #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var confirmResponse = await client.PostAsync("/journal/today/draft/confirm", content: null);
        confirmResponse.EnsureSuccessStatusCode();

        var entryPath = paths.EntryPath(date);
        var originalEntryText = await File.ReadAllTextAsync(entryPath, Encoding.UTF8);

        using var secondInputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "确认后新增 raw input，只应该进入 draft #Journal", source = "text" });
        secondInputResponse.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync(
            "/journal/today/draft/regenerate",
            new { providerId = "mock" });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var currentEntryText = await File.ReadAllTextAsync(entryPath, Encoding.UTF8);

        Assert.Equal("reviewing", document.RootElement.GetProperty("status").GetString());
        Assert.True(File.Exists(entryPath));
        Assert.Equal(originalEntryText, currentEntryText);
        Assert.DoesNotContain("确认后新增 raw input", currentEntryText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostTodayDraftRegenerate_WithEmptyBodyReturnsOk()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天验证空 body regenerate #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();

        using var response = await client.PostAsync("/journal/today/draft/regenerate", content: null);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("reviewing", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostTodayDraftRegenerate_WithMalformedBodyReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/journal/today/draft/regenerate",
            new StringContent("{", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("invalid request body", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostTodayHarnessRun_AppendsInputAndReturnsQueuedRunWithId()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "今天把 harness API 接起来", source = "text" });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var run = root.GetProperty("run");

        Assert.Equal("empty", root.GetProperty("today").GetProperty("status").GetString());
        Assert.Single(root.GetProperty("today").GetProperty("rawInputs").EnumerateArray());
        Assert.StartsWith("run-2026-05-08-", run.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("queued", run.GetProperty("status").GetString());
        Assert.Equal("mock", run.GetProperty("providerId").GetString());
        Assert.NotEmpty(run.GetProperty("currentRawInputId").GetString()!);
    }

    [Fact]
    public async Task PostTodayHarnessRun_WithBlankTextReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "   ", source = "text" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("text is required", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetAuditByDate_ReturnsDailyHarnessRuns()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "第一条 harness 输入", source = "text" });
        firstResponse.EnsureSuccessStatusCode();
        using var secondResponse = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "第二条 harness 输入", source = "text" });
        secondResponse.EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/journal/audit?date=2026-05-08");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var runs = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, runs.Length);
        Assert.All(runs, run => Assert.StartsWith("run-2026-05-08-", run.GetProperty("id").GetString(), StringComparison.Ordinal));
        Assert.All(runs, run => Assert.Equal("queued", run.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task GetAuditByDate_WithInvalidDateReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/journal/audit?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("date must use yyyy-MM-dd", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetHarnessRunById_ReturnsRunFromEmbeddedRunDate()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var startResponse = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "按 runId 查询 harness run", source = "text" });
        startResponse.EnsureSuccessStatusCode();
        using var startDocument = await JsonDocument.ParseAsync(await startResponse.Content.ReadAsStreamAsync());
        var runId = startDocument.RootElement.GetProperty("run").GetProperty("id").GetString();

        using var response = await client.GetAsync($"/journal/harness/runs/{runId}");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(runId, document.RootElement.GetProperty("id").GetString());
        Assert.Equal("queued", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("2026-05-08", document.RootElement.GetProperty("date").GetProperty("isoDate").GetString());
    }

    [Fact]
    public async Task GetHarnessRunById_WithInvalidRunIdReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/journal/harness/runs/not-a-run");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("runId is invalid", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetHarnessRunEvents_ReturnsServerSentEventStream()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var startResponse = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "验证 harness SSE stream", source = "text" });
        startResponse.EnsureSuccessStatusCode();
        using var startDocument = await JsonDocument.ParseAsync(await startResponse.Content.ReadAsStreamAsync());
        var runId = startDocument.RootElement.GetProperty("run").GetProperty("id").GetString();

        using var response = await client.GetAsync(
            $"/journal/harness/runs/{runId}/events",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetHarnessRunEvents_WithMissingRunReturnsNotFound()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/journal/harness/runs/run-2026-05-08-missing/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object CreateAiSettingsSaveRequest(
        string activeProviderId,
        string deepSeekApiKey,
        string deepSeekModel = "deepseek-v4-flash",
        string deepSeekBaseUrl = "https://api.deepseek.com") =>
        new
        {
            activeProviderId,
            providers = new object[]
            {
                new
                {
                    id = "mock",
                    type = "mock",
                    displayName = "Mock",
                    preset = "mock",
                    baseUrl = "local",
                    model = "mock-journal",
                    apiKey = "",
                    isEnabled = true,
                    timeoutSeconds = 1,
                    temperature = 0.0,
                    maxTokens = 0,
                    stylePreset = "faithful"
                },
                new
                {
                    id = "deepseek",
                    type = "openai-compatible",
                    displayName = "DeepSeek",
                    preset = "deepseek",
                    baseUrl = deepSeekBaseUrl,
                    model = deepSeekModel,
                    apiKey = deepSeekApiKey,
                    isEnabled = true,
                    timeoutSeconds = 45,
                    temperature = 0.2,
                    maxTokens = 1200,
                    stylePreset = "faithful"
                }
            }
        };

    private static WebApplicationFactory<Program> CreateFactory(
        string root,
        IReadOnlyDictionary<string, string?>? env = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<JournalStorageOptions>();
                    services.RemoveAll<IJournalClock>();
                    services.RemoveAll<IJournalAiEnvironment>();
                    services.RemoveAll<IJournalAiAgentRuntime>();
                    services.AddSingleton(new JournalStorageOptions(root));
                    services.AddSingleton<IJournalClock>(new FixedJournalClock(FixedDay, FixedNow));
                    services.AddSingleton<IJournalAiEnvironment>(new DictionaryJournalAiEnvironment(env));
                    services.AddSingleton<IJournalAiAgentRuntime>(new EndpointHarnessRuntime());
                });
            });

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class DictionaryJournalAiEnvironment(IReadOnlyDictionary<string, string?>? values) : IJournalAiEnvironment
    {
        private readonly IReadOnlyDictionary<string, string?> _values = values ?? new Dictionary<string, string?>();

        public string? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class EndpointHarnessRuntime : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "wrong_path", "Wrong runtime path.", "RunJsonAsync was called."),
                TimeSpan.Zero));

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("Endpoint SSE test completed without draft changes.")],
                "no-op",
                TimeSpan.Zero,
                200));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-today-endpoint-tests", Guid.NewGuid().ToString("N"));

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
