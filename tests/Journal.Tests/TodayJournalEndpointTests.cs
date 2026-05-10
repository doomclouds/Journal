using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
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
    public async Task PutTodayEditorSource_WithInvalidMarkerReturnsAttentionWithValidationIssue()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天先建立 source editor baseline #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();
        const string markdown = """
            ---
            schema: journal-entry/v1
            date: "2026-05-08"
            ---

            <!-- /journal:section raw-inputs -->

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - source mode
            <!-- /journal:section raw-inputs -->

            <!-- journal:section today-focus -->
            ## 今日重点

            - keep editing
            <!-- /journal:section today-focus -->
            """;

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/source",
            new { markdown });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("attention", root.GetProperty("status").GetString());
        Assert.Equal("attention", root.GetProperty("today").GetProperty("status").GetString());
        Assert.False(root.GetProperty("validation").GetProperty("isValid").GetBoolean());
        Assert.Contains(
            root.GetProperty("validation").GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "unmatched-section-marker");
    }

    [Fact]
    public async Task PutTodayEditorSource_WithInvalidSectionIdReturnsAttentionWithUnknownSection()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天先建立 invalid section baseline #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();
        const string markdown = """
            ---
            schema: journal-entry/v1
            date: "2026-05-08"
            ---

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - source mode
            <!-- /journal:section raw-inputs -->

            <!-- journal:section yesterday-review -->
            ## 昨日回顾

            - keep review
            <!-- /journal:section yesterday-review -->

            <!-- journal:section today-focus -->
            ## 今日重点

            - keep editing
            <!-- /journal:section today-focus -->

            <!-- journal:section Custom -->
            ## Custom

            - invalid id shape
            <!-- /journal:section Custom -->
            """;

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/source",
            new { markdown });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("attention", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("validation").GetProperty("isValid").GetBoolean());
        Assert.Contains(
            root.GetProperty("validation").GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "unknown-section");
    }

    [Fact]
    public async Task PutTodayEditorSource_WithoutBaselineReturnsConflict()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/source",
            new { markdown = "# no baseline" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("editor baseline does not exist.", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PutTodayEditorSource_WithBlankMarkdownReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/journal/today/editor/source",
            new { markdown = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("markdown is required", document.RootElement.GetProperty("error").GetString());
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

    private static object CreateAiSettingsSaveRequest(string activeProviderId, string deepSeekApiKey) =>
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
                    baseUrl = "https://api.deepseek.com",
                    model = "deepseek-v4-flash",
                    apiKey = deepSeekApiKey,
                    isEnabled = true,
                    timeoutSeconds = 45,
                    temperature = 0.2,
                    maxTokens = 1200,
                    stylePreset = "faithful"
                }
            }
        };

    private static WebApplicationFactory<Program> CreateFactory(string root) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<JournalStorageOptions>();
                    services.RemoveAll<IJournalClock>();
                    services.RemoveAll<IJournalAiEnvironment>();
                    services.AddSingleton(new JournalStorageOptions(root));
                    services.AddSingleton<IJournalClock>(new FixedJournalClock(FixedDay, FixedNow));
                    services.AddSingleton<IJournalAiEnvironment>(new EmptyJournalAiEnvironment());
                });
            });

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class EmptyJournalAiEnvironment : IJournalAiEnvironment
    {
        public string? Get(string name) => null;
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
