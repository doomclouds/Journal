using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Journal.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsApplicationStatus()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("app", out _));
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("environment", out _));
        Assert.True(root.TryGetProperty("serverTime", out _));

        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Journal.Api", payload.App);
        Assert.Equal("ok", payload.Status);
        Assert.Equal("0.1.0", payload.Version);
        Assert.False(string.IsNullOrWhiteSpace(payload.Environment));
        Assert.True(payload.ServerTime > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task HealthEndpoint_AllowsDesktopDevelopmentOrigin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("http://localhost:5173", origins);
    }

    [Fact]
    public async Task HealthEndpoint_AllowsLoopbackDesktopDevelopmentOrigin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://127.0.0.1:5173");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("http://127.0.0.1:5173", origins);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("null")]
    public async Task HealthEndpoint_AllowsPackagedAndDevelopmentPreflightOrigins(string origin)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "X-Journal-Desktop-Token");

        using var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains(origin, origins);
    }

    [Fact]
    public async Task NullOriginRequests_RequireDesktopAccessToken()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/settings/ai/deepseek/api-key");
        request.Headers.Add("Origin", "null");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NullOriginRequests_WithDesktopAccessTokenReachEndpoint()
    {
        var previous = Environment.GetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", "test-desktop-token");
        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/settings/ai/deepseek/api-key");
            request.Headers.Add("Origin", "null");
            request.Headers.Add("X-Journal-Desktop-Token", "test-desktop-token");

            using var response = await client.SendAsync(request);

            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
            Assert.Contains("null", origins);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", previous);
        }
    }

    [Fact]
    public async Task ProductionDevOriginRequests_RequireDesktopAccessToken()
    {
        var previous = Environment.GetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", "test-desktop-token");
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/settings/ai/deepseek/api-key");
            request.Headers.Add("Origin", "http://localhost:5173");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.TryGetValues("Access-Control-Allow-Origin", out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", previous);
        }
    }

    [Fact]
    public async Task DesktopTokenConfiguredDevOriginRequests_RequireDesktopAccessTokenEvenInDevelopment()
    {
        var previous = Environment.GetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", "test-desktop-token");
        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/settings/ai/deepseek/api-key");
            request.Headers.Add("Origin", "http://localhost:5173");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", previous);
        }
    }

    [Fact]
    public async Task NullOriginHarnessEvents_AcceptDesktopAccessTokenQuery()
    {
        var previous = Environment.GetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", "test-desktop-token");
        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/journal/harness/runs/invalid/events?desktopAccessToken=test-desktop-token");
            request.Headers.Add("Origin", "null");

            using var response = await client.SendAsync(request);

            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
            Assert.Contains("null", origins);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOURNAL_DESKTOP_ACCESS_TOKEN", previous);
        }
    }

    [Fact]
    public async Task GetAppInfo_ReturnsVersionBuildAndStoragePaths()
    {
        using var workspace = TempWorkspace.Create();
        using var factory = TodayJournalEndpointTests.CreateFactory(workspace.Root);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/app/info");

        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal("Journal.Api", root.GetProperty("name").GetString());
        Assert.Equal("0.1.0", root.GetProperty("version").GetString());
        Assert.Equal("0.1.0", root.GetProperty("releaseVersion").GetString());
        Assert.Equal("dev", root.GetProperty("commit").GetString());
        Assert.Equal("local", root.GetProperty("buildTimeUtc").GetString());
        Assert.Equal("Development", root.GetProperty("environment").GetString());
        Assert.Equal(workspace.Root, root.GetProperty("dataRoot").GetString());
        Assert.EndsWith(Path.Combine(".journal", "index", "journal.db"), root.GetProperty("indexPath").GetString());
    }

    private sealed record HealthResponse(
        string App,
        string Status,
        string Version,
        string Environment,
        DateTimeOffset ServerTime);

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-health-endpoint-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            TestWorkspaceCleanup.DeleteDirectory(Root);
        }
    }
}
