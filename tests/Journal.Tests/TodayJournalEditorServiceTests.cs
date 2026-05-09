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
    public async Task SaveSourceDraftAsync_WritesReviewingDraftForValidMarkdown()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了 parser，今天准备验证 source editor #Journal", "text", CancellationToken.None);
        var originalDraft = (await new DraftStore(paths).ReadAsync(JournalDate.From(FixedDay), CancellationToken.None))!;
        var sourceMarkdown = originalDraft.Markdown.Replace("今天准备验证 source editor #Journal", "source mode updated focus", StringComparison.Ordinal);

        var editor = await service.SaveSourceDraftAsync(new JournalSourceEditRequest(sourceMarkdown), CancellationToken.None);

        var storedDraft = (await new DraftStore(paths).ReadAsync(editor.Date, CancellationToken.None))!;
        Assert.Equal(JournalStatus.Reviewing, editor.Status);
        Assert.True(editor.Validation.IsValid);
        Assert.True(editor.CanConfirm);
        Assert.Equal(JournalStatus.Reviewing, storedDraft.Status);
        Assert.Equal(sourceMarkdown, storedDraft.Markdown);
        Assert.Empty(storedDraft.Errors);
    }

    [Fact]
    public async Task SaveSourceDraftAsync_WritesAttentionDraftForInvalidMarkdownAndDoesNotOverwriteEntry()
    {
        using var workspace = TempWorkspace.Create();
        var paths = CreatePaths(workspace.Root);
        var service = CreateService(paths);
        await service.AddInputAsync("昨天完成了确认，今天验证 invalid source 不覆盖 entry #Journal", "text", CancellationToken.None);
        var confirmed = await service.ConfirmDraftAsync(CancellationToken.None);
        var originalEntryMarkdown = confirmed.Entry!.Markdown;
        const string invalidMarkdown = """
            ---
            schema: journal-entry/v1
            date: "2026-05-08"
            ---

            <!-- journal:section raw-inputs -->
            ## 原始输入

            - 保留 provenance
            <!-- /journal:section raw-inputs -->
            """;

        var editor = await service.SaveSourceDraftAsync(new JournalSourceEditRequest(invalidMarkdown), CancellationToken.None);

        var storedDraft = (await new DraftStore(paths).ReadAsync(editor.Date, CancellationToken.None))!;
        var entryMarkdown = await File.ReadAllTextAsync(paths.EntryPath(editor.Date), CancellationToken.None);
        Assert.Equal(JournalStatus.Attention, editor.Status);
        Assert.False(editor.Validation.IsValid);
        Assert.False(editor.CanConfirm);
        Assert.Equal(JournalStatus.Attention, storedDraft.Status);
        Assert.Equal(invalidMarkdown, storedDraft.Markdown);
        Assert.Contains(storedDraft.Errors, error => error.Contains("Required section 'today-focus' is missing", StringComparison.Ordinal));
        Assert.Equal(originalEntryMarkdown, entryMarkdown);
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

    private static TodayJournalService CreateService(LocalJournalPaths paths, IJournalAiProvider? aiProvider = null) =>
        new(
            new RawInputStore(paths),
            new DraftStore(paths),
            new EntryStore(paths),
            aiProvider ?? new MockAiProvider(),
            new FixedJournalClock(FixedDay, FixedNow));

    private static LocalJournalPaths CreatePaths(string root) =>
        new(new JournalStorageOptions(root));

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;

        public DateTimeOffset Now => now;
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
