using System.Globalization;
using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;
using Journal.Infrastructure.Today;

namespace Journal.Tests;

public sealed class JournalAnniversaryServiceTests
{
    private static readonly DateOnly FixedDay = new(2026, 5, 16);
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-05-16T10:00:00+08:00");

    [Fact]
    public async Task SaveAsync_CreatesPinnedAnniversary()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);

        var item = await service.SaveAsync(new JournalAnniversarySaveRequest(
            "05-16",
            "Journal 阶段日",
            "project-milestone",
            "2024-05-16",
            "从记录习惯逐渐走向个人记忆核心。",
            true), CancellationToken.None);

        Assert.Equal("05-16", item.MonthDay);
        Assert.Equal("Journal 阶段日", item.Title);
        Assert.True(item.Pinned);
        Assert.StartsWith("anniv-", item.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_WithInvalidTypeThrowsArgumentException()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveAsync(
                new JournalAnniversarySaveRequest(
                    "05-16",
                    "纪念日",
                    "custom-type",
                    null,
                    "说明",
                    true),
                CancellationToken.None));

        Assert.Contains("type is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_WhenConcurrent_DoesNotLoseItems()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);
        var titles = Enumerable.Range(1, 16)
            .Select(index => $"纪念日 {index:00}")
            .ToArray();

        await Task.WhenAll(titles.Select(title => service.SaveAsync(
            new JournalAnniversarySaveRequest(
                "05-16",
                title,
                "self-reminder",
                null,
                "说明",
                true),
            CancellationToken.None)));

        var items = await service.ListAsync(CancellationToken.None);

        Assert.Equal(titles.Length, items.Count);
        Assert.Equal(
            titles.Order(StringComparer.Ordinal).ToArray(),
            items.Select(item => item.Title).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task AddNextYearNoteAsync_ForNormalDate_TargetsNextYear()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);

        var updated = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("明年回来看。"),
            CancellationToken.None);

        var note = Assert.Single(updated.NextYearNotes);
        Assert.Equal("2027-05-16", note.TargetDate);
        Assert.Equal(JournalNextYearNoteStatus.Pending, note.Status);
    }

    [Fact]
    public async Task AddNextYearNoteAsync_ForLeapDay_TargetsNextRealLeapDay()
    {
        using var workspace = TempWorkspace.Create();
        var service = CreateSubject(workspace.Root, new DateOnly(2026, 2, 28), DateTimeOffset.Parse("2026-02-28T10:00:00+08:00"));
        var item = await service.SaveAsync(CreateRequest("02-29"), CancellationToken.None);

        var updated = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("下一个闰日再看。"),
            CancellationToken.None);

        var note = Assert.Single(updated.NextYearNotes);
        Assert.Equal("2028-02-29", note.TargetDate);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WritesRawInputAndMarksAdopted()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("明年检查 Journal 是否进入日常。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var targetService = CreateSubject(workspace.Root, targetDate, TargetNow(targetDate));

        var result = await targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        Assert.Equal(JournalNextYearNoteStatus.Adopted, Assert.Single(result.Anniversary.NextYearNotes).Status);
        Assert.StartsWith("raw-", result.RawInput.Id, StringComparison.Ordinal);
        var rawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        Assert.Contains(rawInputs, raw => raw.Text.Contains("明年检查 Journal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_BeforeTargetDateThrowsAndKeepsNotePending()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("这句话只能明年再带回今天。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);

        var exception = await Assert.ThrowsAsync<JournalAnniversaryStateConflictException>(
            () => service.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None));

        Assert.Contains("target date has not arrived", exception.Message, StringComparison.Ordinal);
        var current = Assert.Single(await service.ListAsync(CancellationToken.None));
        Assert.Equal(JournalNextYearNoteStatus.Pending, Assert.Single(current.NextYearNotes).Status);
        var rawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(FixedDay), CancellationToken.None);
        Assert.Empty(rawInputs);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenCalledTwice_ReturnsAdoptedWithoutDuplicatingRawInput()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("明年只应该进入 raw 一次。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var targetService = CreateSubject(workspace.Root, targetDate, TargetNow(targetDate));

        var first = await targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);
        var second = await targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        Assert.Equal(JournalNextYearNoteStatus.Adopted, Assert.Single(second.Anniversary.NextYearNotes).Status);
        Assert.Equal(first.RawInput.Id, second.RawInput.Id);
        var rawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        Assert.Single(rawInputs, raw =>
            raw.Id == first.RawInput.Id
            && raw.Text == "明年只应该进入 raw 一次。");
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenAlreadyAdoptedOnNextDay_UsesOriginalAdoptedDate()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("第二天重复点击也不应跨天复制。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var targetService = CreateSubject(workspace.Root, targetDate, TargetNow(targetDate));
        var first = await targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);
        var secondDate = targetDate.AddDays(1);
        var nextDayService = CreateSubject(
            workspace.Root,
            secondDate,
            TargetNow(secondDate));

        var second = await nextDayService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        Assert.Equal(first.RawInput.Id, second.RawInput.Id);
        Assert.Equal(JournalDate.From(targetDate), second.RawInput.Date);
        var firstDayRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        var secondDayRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(secondDate), CancellationToken.None);
        Assert.Single(firstDayRawInputs, raw => raw.Id == first.RawInput.Id);
        Assert.Empty(secondDayRawInputs);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenPendingRawInputExistsFromYesterday_ReusesItWithoutWritingToday()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("昨天 raw 已落盘，今天只补状态。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var rawInputId = $"raw-anniversary-{note.Id}";
        await new RawInputStore(paths).AppendAsync(
            new RawInput(
                rawInputId,
                JournalDate.From(targetDate),
                TargetNow(targetDate),
                "anniversary-note",
                note.Text),
            CancellationToken.None);
        var secondDate = targetDate.AddDays(1);
        var nextDayService = CreateSubject(
            workspace.Root,
            secondDate,
            TargetNow(secondDate));

        var result = await nextDayService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        var adoptedNote = Assert.Single(result.Anniversary.NextYearNotes);
        Assert.Equal(JournalNextYearNoteStatus.Adopted, adoptedNote.Status);
        Assert.Equal(rawInputId, adoptedNote.RawInputId);
        Assert.Equal(JournalDate.From(targetDate), result.RawInput.Date);
        var firstDayRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        var secondDayRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(secondDate), CancellationToken.None);
        Assert.Single(firstDayRawInputs, raw => raw.Id == rawInputId);
        Assert.Empty(secondDayRawInputs);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenPendingRawInputExistsOutsideRecoveryWindow_WritesTodayAndDoesNotScanOldRaw()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("窗口外旧 raw 不应阻断今天采纳。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var rawInputId = $"raw-anniversary-{note.Id}";
        await new RawInputStore(paths).AppendAsync(
            new RawInput(
                rawInputId,
                JournalDate.From(targetDate),
                TargetNow(targetDate),
                "text",
                note.Text),
            CancellationToken.None);
        var today = targetDate.AddDays(8);
        var todayService = CreateSubject(
            workspace.Root,
            today,
            TargetNow(today));

        var result = await todayService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        Assert.Equal(JournalDate.From(today), result.RawInput.Date);
        var oldRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        var todayRawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(today), CancellationToken.None);
        Assert.Single(oldRawInputs, raw => raw.Id == rawInputId && raw.Source == "text");
        Assert.Single(todayRawInputs, raw =>
            raw.Id == rawInputId
            && raw.Source == "anniversary-note"
            && raw.Text == note.Text);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenDeterministicRawInputAlreadyExists_ReusesItBeforeMarkingAdopted()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("已先落盘的明年提醒。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var rawInputId = $"raw-anniversary-{note.Id}";
        await new RawInputStore(paths).AppendAsync(
            new RawInput(
                rawInputId,
                JournalDate.From(targetDate),
                TargetNow(targetDate),
                "anniversary-note",
                note.Text),
            CancellationToken.None);
        var targetService = CreateSubject(workspace.Root, targetDate, TargetNow(targetDate));

        var result = await targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None);

        var adoptedNote = Assert.Single(result.Anniversary.NextYearNotes);
        Assert.Equal(JournalNextYearNoteStatus.Adopted, adoptedNote.Status);
        Assert.Equal(rawInputId, adoptedNote.RawInputId);
        Assert.Equal(rawInputId, result.RawInput.Id);
        var rawInputs = await new RawInputStore(paths).ReadAsync(JournalDate.From(targetDate), CancellationToken.None);
        Assert.Single(rawInputs, raw => raw.Id == rawInputId && raw.Text == note.Text);
    }

    [Fact]
    public async Task AdoptNextYearNoteAsync_WhenDeterministicRawInputConflicts_ThrowsAndKeepsNotePending()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var service = CreateSubject(workspace.Root);
        var item = await service.SaveAsync(CreateRequest("05-16"), CancellationToken.None);
        var withNote = await service.AddNextYearNoteAsync(
            item.Id,
            new JournalNextYearNoteCreateRequest("应该保持一致的提醒。"),
            CancellationToken.None);
        var note = Assert.Single(withNote.NextYearNotes);
        var targetDate = TargetDate(note);
        var rawInputId = $"raw-anniversary-{note.Id}";
        await new RawInputStore(paths).AppendAsync(
            new RawInput(
                rawInputId,
                JournalDate.From(targetDate),
                TargetNow(targetDate),
                "text",
                note.Text),
            CancellationToken.None);
        var targetService = CreateSubject(workspace.Root, targetDate, TargetNow(targetDate));

        var exception = await Assert.ThrowsAsync<JournalAnniversaryStateConflictException>(
            () => targetService.AdoptNextYearNoteAsync(item.Id, note.Id, CancellationToken.None));

        Assert.Contains("anniversary raw input is inconsistent", exception.Message, StringComparison.Ordinal);
        var current = Assert.Single(await targetService.ListAsync(CancellationToken.None));
        Assert.Equal(JournalNextYearNoteStatus.Pending, Assert.Single(current.NextYearNotes).Status);
    }

    private static JournalAnniversarySaveRequest CreateRequest(string monthDay) =>
        new(monthDay, "纪念日", "self-reminder", null, "说明", true);

    private static DateOnly TargetDate(JournalNextYearNote note) =>
        DateOnly.ParseExact(note.TargetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTimeOffset TargetNow(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 10, 0, 0, TimeSpan.FromHours(8));

    private static JournalAnniversaryService CreateSubject(
        string root,
        DateOnly? today = null,
        DateTimeOffset? now = null)
    {
        var paths = new LocalJournalPaths(new JournalStorageOptions(root));
        return new JournalAnniversaryService(
            new JournalAnniversaryStore(paths),
            new RawInputStore(paths),
            new FixedJournalClock(today ?? FixedDay, now ?? FixedNow));
    }

    private sealed class FixedJournalClock(DateOnly today, DateTimeOffset now) : IJournalClock
    {
        public DateOnly Today => today;
        public DateTimeOffset Now => now;
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "journal-anniversary-service-tests", Guid.NewGuid().ToString("N"));

        public static TempWorkspace Create() => new();

        public void Dispose()
        {
            TestWorkspaceCleanup.DeleteDirectory(Root);
        }
    }
}
