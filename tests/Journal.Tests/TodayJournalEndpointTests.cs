using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Harness;
using Journal.Infrastructure.Jmf;
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
        var summary = await new JournalIndexStore(paths).ReadSummaryAsync(date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("processed", summary.Status);
    }

    [Fact]
    public async Task PostTodayDraftConfirm_WhenIndexFails_ReturnsProcessedAndVisibleWarning()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);

        using var inputResponse = await client.PostAsJsonAsync(
            "/journal/today/inputs",
            new { text = "今天确认 draft，索引失败也要让 API 看见 #Journal", source = "text" });
        inputResponse.EnsureSuccessStatusCode();
        Directory.CreateDirectory(paths.IndexPath());

        using var response = await client.PostAsync("/journal/today/draft/confirm", content: null);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("processed", root.GetProperty("status").GetString());
        Assert.True(File.Exists(paths.EntryPath(date)));
        Assert.Contains(
            root.GetProperty("errors").EnumerateArray(),
            error => error.GetString()!.StartsWith("Index warning:", StringComparison.Ordinal));
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
    public void Services_ResolveEntryWritePipelineDependencies()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);

        Assert.IsType<JournalVersionStore>(factory.Services.GetRequiredService<IJournalVersionStore>());
        Assert.NotNull(factory.Services.GetRequiredService<JournalIndexStore>());
        Assert.NotNull(factory.Services.GetRequiredService<JournalIndexingService>());
        Assert.NotNull(factory.Services.GetRequiredService<EntryWritePipeline>());
    }

    [Fact]
    public async Task GetJournalHistory_ReturnsSearchResults()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "历史搜索 API"));

        using var response = await client.GetAsync("/journal/history?query=历史搜索&limit=20");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("date").GetProperty("isoDate").GetString() == "2026-05-08");
    }

    [Theory]
    [InlineData("/journal/history?from=not-a-date", "from must use yyyy-MM-dd")]
    [InlineData("/journal/history?to=2026-99-99", "to must use yyyy-MM-dd")]
    public async Task GetJournalHistory_WithInvalidDateFilterReturnsBadRequest(string url, string expectedError)
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(expectedError, document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetJournalHistoryDate_ReturnsEntryDetail()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "历史详情 API"));

        using var response = await client.GetAsync("/journal/history/2026-05-08");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("2026-05-08", root.GetProperty("date").GetProperty("isoDate").GetString());
        Assert.Contains("历史详情 API", root.GetProperty("markdown").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            root.GetProperty("sections").EnumerateArray(),
            section => section.GetProperty("id").GetString() == "today-focus");
    }

    [Fact]
    public async Task GetJournalHistoryVersions_ReturnsVersionList()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "current formal entry"));
        var version = await CreateVersionAsync(paths, date, "历史版本 API");

        using var response = await client.GetAsync("/journal/history/2026-05-08/versions");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var versions = document.RootElement.EnumerateArray().ToArray();

        Assert.Contains(versions, item => item.GetProperty("id").GetString() == version.Id);
    }

    [Fact]
    public async Task GetJournalHistoryVersion_ReturnsVersionAndMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "current formal entry"));
        var version = await CreateVersionAsync(paths, date, "历史版本详情 API");

        using var response = await client.GetAsync($"/journal/history/2026-05-08/versions/{version.Id}");
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal(version.Id, root.GetProperty("version").GetProperty("id").GetString());
        Assert.Equal("2026-05-08", root.GetProperty("version").GetProperty("date").GetProperty("isoDate").GetString());
        Assert.Contains("历史版本详情 API", root.GetProperty("markdown").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostJournalHistoryVersionRestoreDraft_WritesDraftAndReturnsTodayEditor()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "current formal entry"));
        var version = await CreateVersionAsync(paths, date, "restored version API");

        using var response = await client.PostAsync($"/journal/history/2026-05-08/versions/{version.Id}/restore-draft", content: null);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal("reviewing", document.RootElement.GetProperty("status").GetString());
        Assert.Contains("restored version API", document.RootElement.GetProperty("markdown").GetString(), StringComparison.Ordinal);
        Assert.True(File.Exists(paths.DraftPath(date)));
        Assert.DoesNotContain(
            "restored version API",
            await File.ReadAllTextAsync(paths.EntryPath(date), Encoding.UTF8),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostJournalHistoryVersionRestoreDraft_WhenVersionDateIsNotTodayReturnsConflict()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var otherDate = JournalDate.From(FixedDay.AddDays(-1));
        await WriteEntryAsync(paths, otherDate, CreateMarkdown(otherDate, "older formal entry"));
        var version = await CreateVersionAsync(paths, otherDate, "older restored version API");

        using var response = await client.PostAsync(
            $"/journal/history/2026-05-07/versions/{version.Id}/restore-draft",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Contains(
            "Only today's journal versions can be restored",
            document.RootElement.GetProperty("error").GetString(),
            StringComparison.Ordinal);
        Assert.False(File.Exists(paths.DraftPath(otherDate)));
        Assert.False(File.Exists(paths.DraftPath(JournalDate.From(FixedDay))));
    }

    [Fact]
    public async Task GetJournalHistoryDate_WithInvalidDateReturnsBadRequest()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/journal/history/not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("date must use yyyy-MM-dd", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostJournalHistoryVersionRestoreDraft_WithMissingVersionReturnsNotFound()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/journal/history/2026-05-08/versions/missing/restore-draft", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostJournalIndexScan_MakesNewEntrySearchable()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "索引扫描 endpoint 可搜索"));

        using var response = await client.PostAsync("/journal/index/scan", content: null);
        response.EnsureSuccessStatusCode();

        var result = await new JournalIndexStore(paths).SearchAsync(
            new JournalHistoryQuery("索引扫描", null, null, null, null, 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(date, item.Date);
        Assert.Contains(item.Hits, hit => hit.SourceType == "section");
    }

    [Fact]
    public async Task PostJournalIndexRebuild_RestoresSearchAndVersionCountFromFiles()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = CreateFactory(workspace.Root);
        using var client = factory.CreateClient();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var date = JournalDate.From(FixedDay);
        await WriteEntryAsync(paths, date, CreateMarkdown(date, "索引重建 markdown 可搜索"));
        await new RawInputStore(paths).AppendAsync(
            new RawInput("raw-1", date, FixedNow, "text", "索引重建 raw input 可搜索"),
            CancellationToken.None);
        await CreateVersionAsync(paths, date, "索引重建 version file");

        await new JournalIndexStore(paths).EnsureReadyAsync(CancellationToken.None);
        Directory.Delete(paths.IndexDirectory(), recursive: true);
        using var response = await client.PostAsync("/journal/index/rebuild", content: null);
        response.EnsureSuccessStatusCode();

        var store = new JournalIndexStore(paths);
        var markdownResult = await store.SearchAsync(
            new JournalHistoryQuery("markdown 可搜索", null, null, null, null, 20),
            CancellationToken.None);
        var rawResult = await store.SearchAsync(
            new JournalHistoryQuery("raw input", null, null, null, null, 20),
            CancellationToken.None);
        var summary = await store.ReadSummaryAsync(date, CancellationToken.None);

        Assert.Contains(markdownResult.Items, item => item.Date == date);
        Assert.Contains(
            rawResult.Items,
            item => item.Date == date && item.Hits.Any(hit => hit.SourceType == "raw-input"));
        Assert.NotNull(summary);
        Assert.Equal(1, summary.VersionCount);
        Assert.Equal(1, summary.RawInputCount);
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
        var runtime = new EndpointHarnessRuntime();
        using var factory = CreateFactory(workspace.Root, runtime: runtime);
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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var body = await response.Content.ReadAsStringAsync(timeout.Token);

        Assert.Contains("event: run-started", body, StringComparison.Ordinal);
        Assert.Contains("event: run-completed", body, StringComparison.Ordinal);
        Assert.Equal(1, runtime.HarnessPlannerCallCount);
    }

    [Fact]
    public async Task GetHarnessRunEvents_WhenRunAlreadyCompleted_ReturnsTerminalEventWithoutExecutingAgain()
    {
        using var workspace = TempWorkspace.Create();
        var runtime = new EndpointHarnessRuntime();
        using var factory = CreateFactory(workspace.Root, runtime: runtime);
        using var client = factory.CreateClient();

        using var startResponse = await client.PostAsJsonAsync(
            "/journal/today/harness/runs",
            new { text = "验证 harness SSE reconnect 不重复执行", source = "text" });
        startResponse.EnsureSuccessStatusCode();
        using var startDocument = await JsonDocument.ParseAsync(await startResponse.Content.ReadAsStreamAsync());
        var runId = startDocument.RootElement.GetProperty("run").GetProperty("id").GetString();

        using var firstResponse = await client.GetAsync(
            $"/journal/harness/runs/{runId}/events",
            HttpCompletionOption.ResponseHeadersRead);
        firstResponse.EnsureSuccessStatusCode();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var firstBody = await firstResponse.Content.ReadAsStringAsync(timeout.Token);

        using var secondResponse = await client.GetAsync(
            $"/journal/harness/runs/{runId}/events",
            HttpCompletionOption.ResponseHeadersRead);
        secondResponse.EnsureSuccessStatusCode();
        var secondBody = await secondResponse.Content.ReadAsStringAsync(timeout.Token);

        using var auditResponse = await client.GetAsync($"/journal/harness/runs/{runId}");
        auditResponse.EnsureSuccessStatusCode();
        using var auditDocument = await JsonDocument.ParseAsync(await auditResponse.Content.ReadAsStreamAsync());

        Assert.Contains("event: run-completed", firstBody, StringComparison.Ordinal);
        Assert.Contains("event: run-already-completed", secondBody, StringComparison.Ordinal);
        Assert.Equal(1, runtime.HarnessPlannerCallCount);
        Assert.Single(auditDocument.RootElement.GetProperty("toolCalls").EnumerateArray());
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

    private static async Task WriteEntryAsync(LocalJournalPaths paths, JournalDate date, string markdown)
    {
        LocalJournalPaths.EnsureParentDirectory(paths.EntryPath(date));
        await File.WriteAllTextAsync(paths.EntryPath(date), markdown, Encoding.UTF8);
    }

    private static async Task<JournalEntryVersion> CreateVersionAsync(
        LocalJournalPaths paths,
        JournalDate date,
        string todayFocus) =>
        await new JournalVersionStore(paths).CreateSnapshotAsync(
            date,
            CreateMarkdown(date, todayFocus),
            paths.EntryPath(date),
            "test",
            FixedNow.AddMinutes(1),
            CancellationToken.None);

    private static string CreateMarkdown(JournalDate date, string todayFocus)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "reviewing",
            ["#Journal"],
            ["历史"],
            "平静",
            ["今天记录历史 API 测试"],
            ["昨天完成索引基础"],
            [todayFocus],
            ["继续验证"]);

        return JmfMarkdownRenderer.Render(aiJson, DateTimeOffset.Parse($"{date.IsoDate}T09:00:00+08:00"));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string root,
        IReadOnlyDictionary<string, string?>? env = null,
        EndpointHarnessRuntime? runtime = null) =>
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
                    services.AddSingleton<IJournalAiAgentRuntime>(runtime ?? new EndpointHarnessRuntime());
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
        public int HarnessPlannerCallCount { get; private set; }

        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create("test", "wrong_path", "Wrong runtime path.", "RunJsonAsync was called."),
                TimeSpan.Zero));

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            HarnessPlannerCallCount++;
            return Task.FromResult(JournalHarnessPlannerRuntimeResult.Success(
                [JournalHarnessOperation.NoOp("Endpoint SSE test completed without draft changes.")],
                "no-op",
                TimeSpan.Zero,
                200));
        }
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
