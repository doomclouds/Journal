using System.Net;
using System.Net.Http.Json;
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
