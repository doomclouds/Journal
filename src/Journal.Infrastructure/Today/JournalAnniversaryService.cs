using System.Globalization;
using System.Text.RegularExpressions;
using Journal.Domain.Entries;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed class JournalAnniversaryNotFoundException(string message) : InvalidOperationException(message);

public sealed class JournalAnniversaryStateConflictException(string message) : InvalidOperationException(message);

public sealed class JournalAnniversaryService
{
    private const int PendingRawInputRecoveryDays = 7;

    private static readonly Regex MonthDayRegex = new("^\\d{2}-\\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "project-milestone",
        "growth",
        "relationship",
        "gratitude",
        "self-reminder"
    };

    private readonly JournalAnniversaryStore _anniversaryStore;
    private readonly RawInputStore _rawInputStore;
    private readonly IJournalClock _clock;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public JournalAnniversaryService(
        JournalAnniversaryStore anniversaryStore,
        RawInputStore rawInputStore,
        IJournalClock clock)
    {
        _anniversaryStore = anniversaryStore;
        _rawInputStore = rawInputStore;
        _clock = clock;
    }

    public async Task<IReadOnlyList<JournalAnniversaryItem>> ListAsync(CancellationToken cancellationToken)
    {
        var document = await _anniversaryStore.ReadAsync(cancellationToken);
        return Sort(document.Items).ToArray();
    }

    public async Task<IReadOnlyList<JournalAnniversaryItem>> ListByMonthDayAsync(
        string monthDay,
        CancellationToken cancellationToken)
    {
        var normalizedMonthDay = NormalizeMonthDay(monthDay);
        var document = await _anniversaryStore.ReadAsync(cancellationToken);
        return Sort(document.Items)
            .Where(item => string.Equals(item.MonthDay, normalizedMonthDay, StringComparison.Ordinal))
            .ToArray();
    }

    public async Task<JournalAnniversaryItem> SaveAsync(
        JournalAnniversarySaveRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var now = _clock.Now;
            var item = new JournalAnniversaryItem(
                $"anniv-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
                normalized.MonthDay,
                normalized.Title,
                normalized.Type,
                normalized.OriginDate,
                normalized.Description,
                normalized.Pinned,
                now,
                now,
                []);

            var document = await _anniversaryStore.ReadAsync(cancellationToken);
            await _anniversaryStore.WriteAsync(
                document with { Items = document.Items.Concat([item]).ToArray() },
                cancellationToken);

            return item;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<JournalAnniversaryItem> UpdateAsync(
        string id,
        JournalAnniversarySaveRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeId(id);
        var normalized = NormalizeRequest(request);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var document = await _anniversaryStore.ReadAsync(cancellationToken);
            var items = document.Items.ToList();
            var index = items.FindIndex(item => string.Equals(item.Id, normalizedId, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new JournalAnniversaryNotFoundException("anniversary was not found.");
            }

            var existing = items[index];
            var updated = existing with
            {
                MonthDay = normalized.MonthDay,
                Title = normalized.Title,
                Type = normalized.Type,
                OriginDate = normalized.OriginDate,
                Description = normalized.Description,
                Pinned = normalized.Pinned,
                UpdatedAt = _clock.Now,
                NextYearNotes = existing.NextYearNotes
            };

            items[index] = updated;
            await _anniversaryStore.WriteAsync(document with { Items = items.ToArray() }, cancellationToken);
            return updated;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<JournalAnniversaryItem> AddNextYearNoteAsync(
        string id,
        JournalNextYearNoteCreateRequest request,
        CancellationToken cancellationToken)
    {
        var text = NormalizeText(request?.Text, "text is required");
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var document = await _anniversaryStore.ReadAsync(cancellationToken);
            var (items, index, item) = FindItem(document, id);
            var now = _clock.Now;
            var note = new JournalNextYearNote(
                $"note-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
                ResolveNextTargetDate(item.MonthDay),
                text,
                JournalNextYearNoteStatus.Pending,
                now,
                null,
                null);

            var updated = item with
            {
                UpdatedAt = now,
                NextYearNotes = item.NextYearNotes.Concat([note]).ToArray()
            };

            items[index] = updated;
            await _anniversaryStore.WriteAsync(document with { Items = items.ToArray() }, cancellationToken);
            return updated;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<JournalAnniversaryAdoptResult> AdoptNextYearNoteAsync(
        string id,
        string noteId,
        CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var document = await _anniversaryStore.ReadAsync(cancellationToken);
            var (items, itemIndex, item) = FindItem(document, id);
            var notes = item.NextYearNotes.ToList();
            var noteIndex = FindNoteIndex(notes, noteId);
            var note = notes[noteIndex];
            if (string.Equals(note.Status, JournalNextYearNoteStatus.Adopted, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(note.RawInputId))
            {
                var adoptedDate = JournalDate.From(DateOnly.FromDateTime((note.AdoptedAt ?? _clock.Now).Date));
                var adoptedRawInput = await EnsureRawInputAsync(
                    note.RawInputId,
                    note.Text,
                    note.AdoptedAt ?? _clock.Now,
                    adoptedDate,
                    cancellationToken);
                return new JournalAnniversaryAdoptResult(item, adoptedRawInput);
            }

            if (!string.Equals(note.Status, JournalNextYearNoteStatus.Pending, StringComparison.Ordinal))
            {
                throw new JournalAnniversaryStateConflictException("next-year note is not pending.");
            }

            var targetDate = ParseTargetDate(note.TargetDate);
            if (targetDate > _clock.Today)
            {
                throw new JournalAnniversaryStateConflictException("next-year note target date has not arrived.");
            }

            var now = _clock.Now;
            var rawInputId = CreateAnniversaryRawInputId(note.Id);
            var rawInput = await FindExistingRawInputAsync(
                    rawInputId,
                    note.Text,
                    _clock.Today.AddDays(-PendingRawInputRecoveryDays),
                    _clock.Today,
                    cancellationToken)
                ?? await EnsureRawInputAsync(
                    rawInputId,
                    note.Text,
                    now,
                    JournalDate.From(_clock.Today),
                    cancellationToken);

            notes[noteIndex] = note with
            {
                Status = JournalNextYearNoteStatus.Adopted,
                AdoptedAt = rawInput.CreatedAt,
                RawInputId = rawInput.Id
            };

            var updated = item with { UpdatedAt = now, NextYearNotes = notes.ToArray() };
            items[itemIndex] = updated;
            await _anniversaryStore.WriteAsync(document with { Items = items.ToArray() }, cancellationToken);

            return new JournalAnniversaryAdoptResult(updated, rawInput);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<JournalAnniversaryItem> DismissNextYearNoteAsync(
        string id,
        string noteId,
        CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var document = await _anniversaryStore.ReadAsync(cancellationToken);
            var (items, itemIndex, item) = FindItem(document, id);
            var notes = item.NextYearNotes.ToList();
            var noteIndex = FindNoteIndex(notes, noteId);
            var note = notes[noteIndex];
            if (!string.Equals(note.Status, JournalNextYearNoteStatus.Pending, StringComparison.Ordinal))
            {
                throw new JournalAnniversaryStateConflictException("next-year note is not pending.");
            }

            notes[noteIndex] = note with { Status = JournalNextYearNoteStatus.Dismissed };
            var updated = item with { UpdatedAt = _clock.Now, NextYearNotes = notes.ToArray() };
            items[itemIndex] = updated;
            await _anniversaryStore.WriteAsync(document with { Items = items.ToArray() }, cancellationToken);
            return updated;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private static IOrderedEnumerable<JournalAnniversaryItem> Sort(IReadOnlyList<JournalAnniversaryItem> items) =>
        items.OrderByDescending(item => item.Pinned)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.MonthDay, StringComparer.Ordinal);

    private static (List<JournalAnniversaryItem> Items, int Index, JournalAnniversaryItem Item) FindItem(
        JournalAnniversaryDocument document,
        string id)
    {
        var normalizedId = NormalizeId(id);
        var items = document.Items.ToList();
        var index = items.FindIndex(item => string.Equals(item.Id, normalizedId, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new JournalAnniversaryNotFoundException("anniversary was not found.");
        }

        return (items, index, items[index]);
    }

    private static int FindNoteIndex(IReadOnlyList<JournalNextYearNote> notes, string noteId)
    {
        var normalizedNoteId = NormalizeId(noteId);
        for (var index = 0; index < notes.Count; index++)
        {
            if (string.Equals(notes[index].Id, normalizedNoteId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new JournalAnniversaryNotFoundException("next-year note was not found.");
    }

    private async Task<RawInput> EnsureRawInputAsync(
        string rawInputId,
        string text,
        DateTimeOffset createdAt,
        JournalDate date,
        CancellationToken cancellationToken)
    {
        var rawInputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var existing = rawInputs.FirstOrDefault(rawInput =>
            string.Equals(rawInput.Id, rawInputId, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing.Date != date
                || !string.Equals(existing.Source, "anniversary-note", StringComparison.Ordinal)
                || !string.Equals(existing.Text, text, StringComparison.Ordinal))
            {
                throw new JournalAnniversaryStateConflictException("anniversary raw input is inconsistent.");
            }

            return existing;
        }

        var rawInput = new RawInput(
            rawInputId,
            date,
            createdAt,
            "anniversary-note",
            text);

        await _rawInputStore.AppendAsync(rawInput, cancellationToken);
        return rawInput;
    }

    private async Task<RawInput?> FindExistingRawInputAsync(
        string rawInputId,
        string text,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var from = start <= end ? start : end;
        var to = start <= end ? end : start;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var journalDate = JournalDate.From(date);
            var rawInputs = await _rawInputStore.ReadAsync(journalDate, cancellationToken);
            var existing = rawInputs.FirstOrDefault(rawInput =>
                string.Equals(rawInput.Id, rawInputId, StringComparison.Ordinal));
            if (existing is null)
            {
                continue;
            }

            if (existing.Date != journalDate
                || !string.Equals(existing.Source, "anniversary-note", StringComparison.Ordinal)
                || !string.Equals(existing.Text, text, StringComparison.Ordinal))
            {
                throw new JournalAnniversaryStateConflictException("anniversary raw input is inconsistent.");
            }

            return existing;
        }

        return null;
    }

    private static string CreateAnniversaryRawInputId(string noteId) =>
        $"raw-anniversary-{noteId}";

    private static DateOnly ParseTargetDate(string targetDate)
    {
        if (!DateOnly.TryParseExact(
            targetDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            throw new JournalAnniversaryStateConflictException("next-year note target date is invalid.");
        }

        return parsed;
    }

    private string ResolveNextTargetDate(string monthDay)
    {
        var month = int.Parse(monthDay[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(monthDay[3..], CultureInfo.InvariantCulture);
        var year = _clock.Today.Year + 1;
        while (!IsRealDate(year, month, day))
        {
            year++;
        }

        return new DateOnly(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static NormalizedAnniversaryRequest NormalizeRequest(JournalAnniversarySaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var monthDay = NormalizeMonthDay(request.MonthDay);
        var title = NormalizeText(request.Title, "title is required");
        var type = NormalizeText(request.Type, "type is required");
        if (!AllowedTypes.Contains(type))
        {
            throw new ArgumentException("type is invalid", nameof(request));
        }

        var description = request.Description?.Trim() ?? string.Empty;
        var originDate = NormalizeOriginDate(request.OriginDate);

        return new NormalizedAnniversaryRequest(monthDay, title, type, originDate, description, request.Pinned);
    }

    private static string NormalizeMonthDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MonthDayRegex.IsMatch(value.Trim()))
        {
            throw new ArgumentException("monthDay must use MM-dd", nameof(value));
        }

        var normalized = value.Trim();
        var month = int.Parse(normalized[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(normalized[3..], CultureInfo.InvariantCulture);
        if (month is < 1 or > 12 || !IsRealMonthDay(month, day))
        {
            throw new ArgumentException("monthDay is invalid", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, nameof(value));
        }

        return value.Trim();
    }

    private static string NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("id is required", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Any(character => !IsAsciiIdCharacter(character)))
        {
            throw new ArgumentException("id is invalid", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOriginDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!DateOnly.TryParseExact(
            trimmed,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            throw new ArgumentException("originDate must use yyyy-MM-dd", nameof(value));
        }

        return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static bool IsRealMonthDay(int month, int day) =>
        day >= 1 && day <= (month == 2 ? 29 : DateTime.DaysInMonth(2000, month));

    private static bool IsRealDate(int year, int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);

    private static bool IsAsciiIdCharacter(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-';

    private sealed record NormalizedAnniversaryRequest(
        string MonthDay,
        string Title,
        string Type,
        string? OriginDate,
        string Description,
        bool Pinned);
}
