using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class TodayJournalServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 8);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");

    [Fact]
    public async Task AddInputAsync_AppendsRawInputAndCreatesReviewingDraft()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var state = await service.AddInputAsync("昨天完成了存储骨架 #工程", "", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, state.Status);
        Assert.Single(state.RawInputs);
        Assert.StartsWith("raw-", state.RawInputs[0].Id);
        Assert.Equal(36, state.RawInputs[0].Id.Length);
        Assert.Equal("text", state.RawInputs[0].Source);
        Assert.Equal("昨天完成了存储骨架 #工程", state.RawInputs[0].Text);

        var rawLines = await File.ReadAllLinesAsync(paths.RawInputPath(state.Date), CancellationToken.None);
        Assert.Single(rawLines);
        using var rawJson = JsonDocument.Parse(rawLines[0]);
        Assert.Equal("昨天完成了存储骨架 #工程", rawJson.RootElement.GetProperty("text").GetString());

        var draft = await new DraftStore(paths).ReadAsync(state.Date, CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(JournalStatus.Reviewing, draft.Status);
        Assert.Equal([state.RawInputs[0].Id], draft.SourceRawInputIds);
        Assert.Empty(draft.Errors);
        Assert.Contains("<!-- journal:section raw-inputs -->", draft.Markdown);
        Assert.Contains("- 昨天完成了存储骨架 #工程", draft.Markdown);
    }

    [Fact]
    public async Task AddInputAsync_RegeneratesDraftFromAllRawInputsOnSameDay()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var first = await service.AddInputAsync("昨天完成了存储骨架 #工程", "text", CancellationToken.None);
        var second = await service.AddInputAsync("今天准备实现 TodayJournalService #Journal", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, second.Status);
        Assert.Equal(2, second.RawInputs.Count);
        Assert.Equal([first.RawInputs[0].Id, second.RawInputs[1].Id], second.Draft!.SourceRawInputIds);
        Assert.Contains("- 昨天完成了存储骨架 #工程", second.Draft.Markdown);
        Assert.Contains("- 今天准备实现 TodayJournalService #Journal", second.Draft.Markdown);

        var rawLines = await File.ReadAllLinesAsync(paths.RawInputPath(second.Date), CancellationToken.None);
        Assert.Equal(2, rawLines.Length);
    }

    [Fact]
    public async Task ConfirmDraftAsync_WritesEntryAndReturnsProcessedForFirstEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("今天准备实现确认流程 #Journal", "text", CancellationToken.None);

        var state = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, state.Status);
        Assert.NotNull(state.Entry);
        Assert.Contains("今天准备实现确认流程 #Journal", state.Entry.Markdown);
        Assert.True(File.Exists(paths.EntryPath(state.Date)));

        var draft = await new DraftStore(paths).ReadAsync(state.Date, CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(JournalStatus.Processed, draft.Status);
    }

    [Fact]
    public async Task ConfirmDraftAsync_ReturnsUpdatedAndOverwritesEntryWhenEntryAlreadyExists()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了第一版 #Journal", "text", CancellationToken.None);
        var processed = await service.ConfirmDraftAsync(CancellationToken.None);
        var firstMarkdown = processed.Entry!.Markdown;

        await service.AddInputAsync("今天补上覆盖正式 entry 的路径 #Journal", "text", CancellationToken.None);
        var updated = await service.ConfirmDraftAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Updated, updated.Status);
        Assert.NotNull(updated.Entry);
        Assert.NotEqual(firstMarkdown, updated.Entry.Markdown);
        Assert.Contains("昨天完成了第一版 #Journal", updated.Entry.Markdown);
        Assert.Contains("今天补上覆盖正式 entry 的路径 #Journal", updated.Entry.Markdown);

        var entryText = await File.ReadAllTextAsync(paths.EntryPath(updated.Date), CancellationToken.None);
        Assert.Equal(updated.Entry.Markdown, entryText);

        var versions = await new JournalVersionStore(paths).ReadByDateAsync(updated.Date, CancellationToken.None);
        var version = Assert.Single(versions);
        Assert.Equal("confirm-draft", version.Reason);
        Assert.Equal(firstMarkdown, (await new JournalVersionStore(paths).ReadAsync(updated.Date, version.Id, CancellationToken.None))!.Value.Markdown);

        var summary = await new JournalIndexStore(paths).ReadSummaryAsync(updated.Date, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("updated", summary.Status);
        Assert.Equal(1, summary.VersionCount);
    }

    [Fact]
    public async Task AddInputAsync_CreatesAttentionDraftAndDoesNotWriteEntryWhenAiJsonIsInvalid()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var settings = WithActiveProvider("openai");
        var service = CreateService(
            paths,
            CreateGenerationService(
                settings,
                OpenAiCompatibleRunResult.Success(CreateInvalidAiJson(), "{}", TimeSpan.Zero)));

        var state = await service.AddInputAsync("今天输入会触发 invalid AI JSON", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Attention, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Equal(JournalStatus.Attention, state.Draft.Status);
        Assert.Equal([state.RawInputs[0].Id], state.Draft.SourceRawInputIds);
        Assert.NotEmpty(state.Errors);
        Assert.Contains(state.Errors, error => error.Contains("schema must be journal-entry/v1", StringComparison.Ordinal));
        Assert.Contains("LLM generation failed", state.Draft.Markdown);
        Assert.Contains("schema must be journal-entry/v1", state.Draft.Markdown);
        Assert.False(File.Exists(paths.EntryPath(state.Date)));
    }

    [Fact]
    public async Task AddInputAsync_WithRealProviderOutputPreservesServerRawInputsInDraftMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var settings = WithActiveProvider("openai");
        var service = CreateService(
            paths,
            CreateGenerationService(
                settings,
                OpenAiCompatibleRunResult.Success(
                    new JournalAiJson(
                        "journal-entry/v1",
                        FixedDay.ToString("yyyy-MM-dd"),
                        "05-08",
                        "draft",
                        [],
                        [],
                        "专注",
                        ["模型改写过的 raw input"],
                        ["昨天完成了 provider 接线"],
                        ["今天验证原始输入保护"],
                        []),
                    "{}",
                    TimeSpan.Zero)));

        var state = await service.AddInputAsync("这段原文必须原样保留", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Contains("- 这段原文必须原样保留", state.Draft.Markdown);
        Assert.DoesNotContain("模型改写过的 raw input", state.Draft.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegenerateDraftAsync_UsesAllRawInputsAndDoesNotWriteEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        var processed = await service.AddInputAsync("昨天完成了确认流程 #Journal", "text", CancellationToken.None);
        await service.ConfirmDraftAsync(CancellationToken.None);
        var originalEntryMarkdown = await File.ReadAllTextAsync(paths.EntryPath(processed.Date), CancellationToken.None);
        var appendedRawInput = new RawInput(
            "raw-regenerate-extra",
            processed.Date,
            FixedNow.AddMinutes(5),
            "text",
            "确认后新增的原始输入也要参与重新生成 #追加");
        await new RawInputStore(paths).AppendAsync(appendedRawInput, CancellationToken.None);

        var state = await service.RegenerateDraftAsync(providerIdOverride: "mock", CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Equal(JournalStatus.Reviewing, state.Draft.Status);
        Assert.Equal([processed.RawInputs[0].Id, appendedRawInput.Id], state.Draft.SourceRawInputIds);
        Assert.Contains("昨天完成了确认流程 #Journal", state.Draft.Markdown);
        Assert.Contains("确认后新增的原始输入也要参与重新生成 #追加", state.Draft.Markdown);
        var entryMarkdown = await File.ReadAllTextAsync(paths.EntryPath(state.Date), CancellationToken.None);
        Assert.Equal(originalEntryMarkdown, entryMarkdown);
    }

    [Fact]
    public async Task RegenerateDraftAsync_WithoutRawInputsDoesNotCreateDraftOrEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        var date = JournalDate.From(FixedDay);

        var state = await service.RegenerateDraftAsync(providerIdOverride: "mock", CancellationToken.None);

        Assert.NotEqual(JournalStatus.Reviewing, state.Status);
        Assert.Null(state.Draft);
        Assert.Null(state.Entry);
        Assert.False(File.Exists(paths.DraftPath(date)));
        Assert.False(File.Exists(paths.DraftMetaPath(date)));
        Assert.False(File.Exists(paths.EntryPath(date)));
    }

    [Fact]
    public async Task AddInputAsync_WithRealProviderFailureCreatesAttentionDraftAndDoesNotFallbackToMock()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var settings = WithActiveProvider("openai");
        var service = CreateService(
            paths,
            CreateGenerationService(
                settings,
                OpenAiCompatibleRunResult.Failure(
                        JournalAiSafeError.Create(
                        "provider-call",
                        "unauthorized",
                        "LLM rejected the API key.",
                        "Authorization: Bearer sk-test-secret"),
                    TimeSpan.FromMilliseconds(12),
                    httpStatus: 401,
                    safeResponseSnippet: """{"error":"unauthorized"}""")));

        var state = await service.AddInputAsync("今天真实 provider 会失败 #Journal", "text", CancellationToken.None);

        Assert.Equal(JournalStatus.Attention, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Equal(JournalStatus.Attention, state.Draft.Status);
        Assert.Equal([state.RawInputs[0].Id], state.Draft.SourceRawInputIds);
        Assert.Contains(state.Errors, error => error.Contains("LLM rejected the API key.", StringComparison.Ordinal));
        Assert.Contains(state.Errors, error => error.Contains("unauthorized", StringComparison.Ordinal));
        Assert.Contains(state.Errors, error => error.Contains("[redacted-value]", StringComparison.Ordinal));
        Assert.Contains("# LLM generation failed", state.Draft.Markdown);
        Assert.DoesNotContain("provider: mock", state.Draft.Markdown, StringComparison.Ordinal);
        Assert.False(File.Exists(paths.EntryPath(state.Date)));
    }

    private static TodayJournalService CreateService(LocalJournalPaths paths, JournalAiGenerationService? generationService = null) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            CreateEntryWritePipeline(paths),
            generationService ?? CreateGenerationService(JournalAiSettings.CreateDefault()),
            new FixedJournalClock(FixedDay, FixedNow));

    private static EntryWritePipeline CreateEntryWritePipeline(LocalJournalPaths paths)
    {
        var indexStore = new JournalIndexStore(paths);
        return new EntryWritePipeline(
            new EntryStore(paths),
            new JournalVersionStore(paths),
            new JournalIndexingService(paths, indexStore),
            paths);
    }

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private static JournalAiGenerationService CreateGenerationService(
        JournalAiSettings settings,
        OpenAiCompatibleRunResult? runResult = null) =>
        new(
            new StaticSettingsReader(settings),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new StaticRuntime(
                runResult ?? OpenAiCompatibleRunResult.Failure(
                    JournalAiSafeError.Create("test", "unexpected_runtime_call", "Runtime should not be called.", "Runtime should not be called."),
                    TimeSpan.Zero))));

    private static JournalAiSettings WithActiveProvider(string providerId)
    {
        var settings = JournalAiSettings.CreateDefault();
        var providers = settings.Providers.Select(provider =>
            string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase)
                ? provider with { ApiKey = "test-api-key", IsEnabled = true }
                : provider).ToArray();

        return settings with { ActiveProviderId = providerId, Providers = providers };
    }

    private static JournalAiJson CreateInvalidAiJson() =>
        new(
            "invalid-schema",
            FixedDay.ToString("yyyy-MM-dd"),
            "05-08",
            "draft",
            [],
            [],
            "未标注",
            [],
            [],
            [],
            []);

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
    }

    private sealed class StaticSettingsReader(JournalAiSettings settings) : IJournalAiSettingsReader
    {
        public Task<JournalAiSettings> ReadEffectiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(settings);
    }

    private sealed class StaticRuntime(OpenAiCompatibleRunResult result) : IJournalAiAgentRuntime
    {
        public Task<OpenAiCompatibleRunResult> RunJsonAsync(
            OpenAiCompatibleRunRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
            JournalHarnessPlannerRuntimeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Harness planner runtime should not be called by today service tests.");
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-today-tests", Guid.NewGuid().ToString("N"));

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
