using System.Text;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Today;

public sealed class TodayJournalService
{
    private readonly RawInputStore _rawInputStore;
    private readonly DraftStore _draftStore;
    private readonly EntryStore _entryStore;
    private readonly IJournalAiProvider _aiProvider;
    private readonly IJournalClock _clock;

    public TodayJournalService(
        RawInputStore rawInputStore,
        DraftStore draftStore,
        EntryStore entryStore,
        IJournalAiProvider aiProvider,
        IJournalClock clock)
    {
        _rawInputStore = rawInputStore;
        _draftStore = draftStore;
        _entryStore = entryStore;
        _aiProvider = aiProvider;
        _clock = clock;
    }

    public Task<TodayJournalState> GetTodayAsync(CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        return BuildStateAsync(date, statusOverride: null, cancellationToken);
    }

    public async Task<TodayJournalState> AddInputAsync(string text, string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text is required", nameof(text));
        }

        var date = JournalDate.From(_clock.Today);
        var now = _clock.Now;
        var input = new RawInput(
            $"raw-{Guid.NewGuid():N}",
            date,
            now,
            string.IsNullOrWhiteSpace(source) ? "text" : source.Trim(),
            text);

        await _rawInputStore.AppendAsync(input, cancellationToken);
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var sourceRawInputIds = inputs.Select(rawInput => rawInput.Id).ToArray();

        var aiJson = _aiProvider.Generate(date, inputs, now);
        var validation = JournalAiJsonValidator.Validate(aiJson);
        if (!validation.IsValid)
        {
            var attentionDraft = new JournalDraft(
                date,
                JournalStatus.Attention,
                RenderAttentionMarkdown(validation.Errors),
                sourceRawInputIds,
                validation.Errors,
                now);

            await _draftStore.WriteAsync(attentionDraft, cancellationToken);
            return await BuildStateAsync(date, JournalStatus.Attention, cancellationToken);
        }

        var markdown = JmfMarkdownRenderer.Render(aiJson, now);
        var draft = new JournalDraft(
            date,
            JournalStatus.Reviewing,
            markdown,
            sourceRawInputIds,
            Array.Empty<string>(),
            now);

        await _draftStore.WriteAsync(draft, cancellationToken);
        return await BuildStateAsync(date, JournalStatus.Reviewing, cancellationToken);
    }

    public async Task<TodayJournalState> ConfirmDraftAsync(CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var draft = await _draftStore.ReadAsync(date, cancellationToken)
            ?? throw new InvalidOperationException("draft does not exist.");

        if (draft.Status != JournalStatus.Reviewing)
        {
            throw new InvalidOperationException("draft is not ready for confirmation.");
        }

        var now = _clock.Now;
        var status = _entryStore.Exists(date) ? JournalStatus.Updated : JournalStatus.Processed;

        await _entryStore.WriteAsync(date, draft.Markdown, now, cancellationToken);
        await _draftStore.WriteAsync(draft with { Status = status, UpdatedAt = now }, cancellationToken);

        return await BuildStateAsync(date, status, cancellationToken);
    }

    private async Task<TodayJournalState> BuildStateAsync(
        JournalDate date,
        JournalStatus? statusOverride,
        CancellationToken cancellationToken)
    {
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var draft = await _draftStore.ReadAsync(date, cancellationToken);
        var entry = await _entryStore.ReadAsync(date, cancellationToken);
        var status = statusOverride
            ?? draft?.Status
            ?? (entry is not null ? JournalStatus.Processed : JournalStatus.Empty);
        var errors = draft?.Errors ?? Array.Empty<string>();

        return new TodayJournalState(date, status, inputs, draft, entry, errors);
    }

    private static string RenderAttentionMarkdown(IReadOnlyList<string> errors)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AI JSON validation failed");
        builder.AppendLine();
        builder.AppendLine("## Errors");
        builder.AppendLine();

        foreach (var error in errors)
        {
            builder.Append("- ").AppendLine(error);
        }

        return builder.ToString();
    }
}
