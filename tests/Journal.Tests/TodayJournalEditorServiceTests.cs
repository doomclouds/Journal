using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class TodayJournalEditorServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 8);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-08T09:30:00+08:00");

    [Fact]
    public async Task GetTodayEditorAsync_ReturnsDraftSectionsWhenReviewingDraftExists()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        await service.AddInputAsync("昨天完成了存储骨架，今天准备实现 editor workflow #Journal", "text", CancellationToken.None);

        var editor = await service.GetTodayEditorAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, editor.Status);
        Assert.True(editor.CanConfirm);
        Assert.True(editor.Validation.IsValid);
        Assert.NotNull(editor.Today.Draft);
        Assert.Contains(editor.Sections, section => section.Id == "raw-inputs" && section.Content.Contains("editor workflow", StringComparison.Ordinal));
        Assert.Contains(editor.Sections, section => section.Id == "today-focus");
        Assert.DoesNotContain(editor.AvailableOptionalSections, section => editor.Sections.Any(existing => existing.Id == section.Id));
    }

    [Fact]
    public async Task GetTodayEditorAsync_FallsBackToEntryWhenNoDraftExists()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了正式 entry，今天只读正式文件 #Journal", "text", CancellationToken.None);
        var confirmed = await service.ConfirmDraftAsync(CancellationToken.None);
        File.Delete(paths.DraftPath(confirmed.Date));
        File.Delete(paths.DraftMetaPath(confirmed.Date));

        var editor = await service.GetTodayEditorAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Processed, editor.Status);
        Assert.False(editor.CanConfirm);
        Assert.Equal(confirmed.Entry!.Markdown, editor.Markdown);
        Assert.True(editor.Validation.IsValid);
        Assert.Null(editor.Today.Draft);
        Assert.NotNull(editor.Today.Entry);
        Assert.Contains(editor.Sections, section => section.Id == "raw-inputs");
    }

    [Fact]
    public async Task SaveBlockDraftAsync_PreservesRawInputsFromBaselineAndUpdatesTodayFocus()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了存储骨架，今天准备做编辑器 #Journal", "text", CancellationToken.None);

        var editor = await service.SaveBlockDraftAsync(
            new JournalBlockEditRequest([new("today-focus", "- 改成通过 block editor 保存")]),
            CancellationToken.None);

        Assert.Equal(JournalStatus.Reviewing, editor.Status);
        Assert.True(editor.Validation.IsValid);
        var rawInputs = GetSection(editor.Markdown, "raw-inputs");
        var todayFocus = GetSection(editor.Markdown, "today-focus");
        Assert.Contains("昨天完成了存储骨架，今天准备做编辑器 #Journal", rawInputs.Content, StringComparison.Ordinal);
        Assert.Equal("- 改成通过 block editor 保存", todayFocus.Content);
    }

    [Fact]
    public async Task SaveBlockDraftAsync_MarksEditedSectionAsUserTouched()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("今天验证 provenance #Journal", "text", CancellationToken.None);

        var editor = await service.SaveBlockDraftAsync(
            new JournalBlockEditRequest([new("today-focus", "- 用户手动编辑")]),
            CancellationToken.None);

        var section = GetSection(editor.Markdown, "today-focus");
        Assert.Equal("user", section.Provenance.LastTouchedBy);
        Assert.Equal("edit", section.Provenance.LastOperation);
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ReturnsAttentionWhenRequestContainsRawInputs()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("今天准备验证 raw inputs 只读 #Journal", "text", CancellationToken.None);
        var originalDraft = (await new DraftStore(paths).ReadAsync(JournalDate.From(FixedDay), CancellationToken.None))!;

        var editor = await service.SaveBlockDraftAsync(
            new JournalBlockEditRequest([new("raw-inputs", "- unsafe overwrite")]),
            CancellationToken.None);

        var storedDraft = (await new DraftStore(paths).ReadAsync(editor.Date, CancellationToken.None))!;
        Assert.Equal(JournalStatus.Attention, editor.Status);
        Assert.Equal(JournalStatus.Attention, storedDraft.Status);
        Assert.Equal(originalDraft.Markdown, storedDraft.Markdown);
        Assert.Contains(storedDraft.Errors, error => error.Contains("Raw inputs cannot be edited", StringComparison.Ordinal));
        Assert.Contains("今天准备验证 raw inputs 只读 #Journal", GetSection(storedDraft.Markdown, "raw-inputs").Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ComposesOptionalBlocksInFixedOrder()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了 renderer，今天准备补 editor service #Journal", "text", CancellationToken.None);

        var editor = await service.SaveBlockDraftAsync(
            new JournalBlockEditRequest(
            [
                new("gratitude", "- 感谢测试先行"),
                new("work", "- 推进 JMF editor"),
                new("mood", "平静"),
                new("today-focus", "- 保存 block draft")
            ]),
            CancellationToken.None);

        var sectionIds = JmfMarkdownParser.Parse(editor.Markdown).Document.Sections.Select(section => section.Id).ToArray();

        Assert.Equal(
            ["raw-inputs", "mood", "yesterday-review", "today-focus", "work", "inspiration", "gratitude"],
            sectionIds);
    }

    [Fact]
    public async Task GetTodayEditorAsync_ReturnsEmptyInvalidStateWhenBaselineDoesNotExist()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var editor = await service.GetTodayEditorAsync(CancellationToken.None);

        Assert.Equal(JournalStatus.Empty, editor.Status);
        Assert.False(editor.CanConfirm);
        Assert.Equal(string.Empty, editor.Markdown);
        Assert.Empty(editor.Sections);
        Assert.False(editor.Validation.IsValid);
        Assert.Contains(editor.Validation.Issues, issue => issue.Code == "missing-front-matter");
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ThrowsWhenBaselineDoesNotExist()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveBlockDraftAsync(
                new JournalBlockEditRequest([new("today-focus", "- cannot save without raw inputs")]),
                CancellationToken.None));

        Assert.Equal("editor baseline does not exist.", exception.Message);
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ThrowsArgumentExceptionWhenSectionsIsNull()
    {
        await AssertBlockRequestShapeThrowsAsync(
            new JournalBlockEditRequest(null!),
            "sections is required");
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ThrowsArgumentExceptionWhenSectionItemIsNull()
    {
        await AssertBlockRequestShapeThrowsAsync(
            new JournalBlockEditRequest([null!]),
            "section is required");
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ThrowsArgumentExceptionWhenSectionIdIsBlank()
    {
        await AssertBlockRequestShapeThrowsAsync(
            new JournalBlockEditRequest([new(" ", "- content")]),
            "section id is required");
    }

    [Fact]
    public async Task SaveBlockDraftAsync_ThrowsArgumentExceptionWhenSectionContentIsNull()
    {
        await AssertBlockRequestShapeThrowsAsync(
            new JournalBlockEditRequest([new("today-focus", null!)]),
            "section content is required");
    }

    private static JmfSection GetSection(string markdown, string sectionId)
    {
        var parseResult = JmfMarkdownParser.Parse(markdown);
        return parseResult.Document.Sections.Single(section => section.Id == sectionId);
    }

    private static async Task AssertBlockRequestShapeThrowsAsync(
        JournalBlockEditRequest request,
        string expectedMessage)
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("今天准备验证 block request shape #Journal", "text", CancellationToken.None);
        var date = JournalDate.From(FixedDay);
        var originalDraft = (await new DraftStore(paths).ReadAsync(date, CancellationToken.None))!;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveBlockDraftAsync(request, CancellationToken.None));

        var storedDraft = (await new DraftStore(paths).ReadAsync(date, CancellationToken.None))!;
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalDraft.Status, storedDraft.Status);
        Assert.Equal(originalDraft.Markdown, storedDraft.Markdown);
        Assert.Equal(originalDraft.SourceRawInputIds, storedDraft.SourceRawInputIds);
        Assert.Equal(originalDraft.Errors, storedDraft.Errors);
    }

    private static TodayJournalService CreateService(LocalJournalPaths paths) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            CreateGenerationService(),
            new FixedJournalClock(FixedDay, FixedNow));

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private static JournalAiGenerationService CreateGenerationService() =>
        new(
            new StaticSettingsReader(JournalAiSettings.CreateDefault()),
            new MockAiProvider(),
            new OpenAiCompatibleJournalAiProvider(new StaticRuntime(
                OpenAiCompatibleRunResult.Failure(
                    JournalAiSafeError.Create("test", "unexpected_runtime_call", "Runtime should not be called.", "Runtime should not be called."),
                    TimeSpan.Zero))));

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
            throw new NotSupportedException("Harness planner runtime should not be called by editor service tests.");
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-editor-tests", Guid.NewGuid().ToString("N"));

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
