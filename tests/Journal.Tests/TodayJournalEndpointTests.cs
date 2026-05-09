using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Journal.Domain.Entries;
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

    private static WebApplicationFactory<Program> CreateFactory(string root) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<JournalStorageOptions>();
                    services.RemoveAll<IJournalClock>();
                    services.AddSingleton(new JournalStorageOptions(root));
                    services.AddSingleton<IJournalClock>(new FixedJournalClock(FixedDay, FixedNow));
                });
            });

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
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
