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
    private readonly EntryWritePipeline _entryWritePipeline;
    private readonly JournalAiGenerationService _aiGenerationService;
    private readonly IJournalClock _clock;

    public TodayJournalService(
        RawInputStore rawInputStore,
        DraftStore draftStore,
        EntryStore entryStore,
        EntryWritePipeline entryWritePipeline,
        JournalAiGenerationService aiGenerationService,
        IJournalClock clock)
    {
        _rawInputStore = rawInputStore;
        _draftStore = draftStore;
        _entryStore = entryStore;
        _entryWritePipeline = entryWritePipeline;
        _aiGenerationService = aiGenerationService;
        _clock = clock;
    }

    public Task<TodayJournalState> GetTodayAsync(CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        return BuildStateAsync(date, statusOverride: null, cancellationToken);
    }

    public async Task<TodayEditorState> GetTodayEditorAsync(CancellationToken cancellationToken)
    {
        var baseline = await ReadEditorBaselineAsync(cancellationToken);
        var parseResult = JmfMarkdownParser.Parse(baseline.Markdown);
        var validation = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        var today = await BuildStateAsync(baseline.Date, statusOverride: null, cancellationToken);
        var availableOptionalSections = JmfSectionCatalog.GetAvailableOptionalSections(
            parseResult.Document.Sections.Select(section => section.Id));
        var canConfirm = today.Status == JournalStatus.Reviewing && validation.IsValid;

        return new TodayEditorState(
            baseline.Date,
            today.Status,
            baseline.Markdown,
            parseResult.Document.Sections,
            availableOptionalSections,
            validation,
            canConfirm,
            today);
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
        return await GenerateDraftAsync(date, inputs, now, providerIdOverride: null, cancellationToken);
    }

    public async Task<TodayJournalState> RegenerateDraftAsync(
        string? providerIdOverride,
        CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var now = _clock.Now;
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        if (inputs.Count == 0)
        {
            return await BuildStateAsync(date, statusOverride: null, cancellationToken);
        }

        return await GenerateDraftAsync(date, inputs, now, providerIdOverride, cancellationToken);
    }

    private async Task<TodayJournalState> GenerateDraftAsync(
        JournalDate date,
        IReadOnlyList<RawInput> inputs,
        DateTimeOffset now,
        string? providerIdOverride,
        CancellationToken cancellationToken)
    {
        var sourceRawInputIds = inputs.Select(rawInput => rawInput.Id).ToArray();

        var generation = await _aiGenerationService.GenerateAsync(
            date,
            inputs,
            now,
            providerIdOverride,
            cancellationToken);
        if (!generation.IsSuccess || generation.AiJson is null)
        {
            var errors = ToAiErrorMessages(generation.Error);
            var attentionDraft = new JournalDraft(
                date,
                JournalStatus.Attention,
                RenderAttentionMarkdown("LLM generation failed", errors),
                sourceRawInputIds,
                errors,
                now);

            await _draftStore.WriteAsync(attentionDraft, cancellationToken);
            return await BuildStateAsync(date, JournalStatus.Attention, cancellationToken);
        }

        var markdown = JmfMarkdownRenderer.Render(generation.AiJson, now, generation.Metadata);
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
        var result = await _entryWritePipeline.WriteFormalEntryAsync(
            date,
            draft.Markdown,
            now,
            "confirm-draft",
            cancellationToken);

        var errors = string.IsNullOrWhiteSpace(result.IndexWarning)
            ? draft.Errors
            : draft.Errors.Concat([$"Index warning: {result.IndexWarning.Trim()}"]).ToArray();

        await _draftStore.WriteAsync(
            draft with { Status = result.Status, Errors = errors, UpdatedAt = now },
            cancellationToken);

        return await BuildStateAsync(date, result.Status, cancellationToken);
    }

    public async Task<TodayEditorState> SaveBlockDraftAsync(
        JournalBlockEditRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBlockDraftRequestShape(request);

        var baseline = await ReadEditorBaselineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseline.Markdown))
        {
            throw new InvalidOperationException("editor baseline does not exist.");
        }

        var sourceRawInputIds = await GetDraftSourceRawInputIdsAsync(baseline.Date, baseline.Draft, cancellationToken);
        var requestValidation = JmfMarkdownValidator.ValidateBlockEditRequest(request);
        if (!requestValidation.IsValid)
        {
            await WriteEditorDraftAsync(
                baseline.Date,
                JournalStatus.Attention,
                baseline.Markdown,
                sourceRawInputIds,
                ToMessages(requestValidation.Issues),
                cancellationToken);

            return await GetTodayEditorAsync(cancellationToken);
        }

        var parseResult = JmfMarkdownParser.Parse(baseline.Markdown);
        _ = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        var mergedSections = MergeBlockSections(parseResult.Document.Sections, request.Sections);
        var composedMarkdown = JmfMarkdownComposer.Compose(parseResult.Document with { Sections = mergedSections });
        var composedParseResult = JmfMarkdownParser.Parse(composedMarkdown);
        var composedValidation = JmfMarkdownValidator.Validate(composedParseResult.Document, composedParseResult.Issues);
        var status = composedValidation.IsValid ? JournalStatus.Reviewing : JournalStatus.Attention;
        var errors = composedValidation.IsValid ? Array.Empty<string>() : ToMessages(composedValidation.Issues);

        await WriteEditorDraftAsync(
            baseline.Date,
            status,
            composedMarkdown,
            sourceRawInputIds,
            errors,
            cancellationToken);

        return await GetTodayEditorAsync(cancellationToken);
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

    private async Task<(JournalDate Date, string Markdown, JournalDraft? Draft, JournalEntry? Entry)> ReadEditorBaselineAsync(
        CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var draft = await _draftStore.ReadAsync(date, cancellationToken);
        var entry = await _entryStore.ReadAsync(date, cancellationToken);
        var markdown = draft?.Markdown ?? entry?.Markdown ?? string.Empty;

        return (date, markdown, draft, entry);
    }

    private async Task<IReadOnlyList<string>> GetDraftSourceRawInputIdsAsync(
        JournalDate date,
        JournalDraft? draft,
        CancellationToken cancellationToken)
    {
        if (draft is not null)
        {
            return draft.SourceRawInputIds;
        }

        var rawInputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        return rawInputs.Select(rawInput => rawInput.Id).ToArray();
    }

    private async Task WriteEditorDraftAsync(
        JournalDate date,
        JournalStatus status,
        string markdown,
        IReadOnlyList<string> sourceRawInputIds,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        var draft = new JournalDraft(
            date,
            status,
            markdown,
            sourceRawInputIds,
            errors,
            _clock.Now);

        await _draftStore.WriteAsync(draft, cancellationToken);
    }

    private static IReadOnlyList<JmfSection> MergeBlockSections(
        IReadOnlyList<JmfSection> baselineSections,
        IReadOnlyList<JournalBlockEditSection> requestSections)
    {
        var merged = baselineSections.ToList();
        var indexes = merged
            .Select((section, index) => new { section.Id, Index = index })
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.Ordinal);

        foreach (var requestSection in requestSections)
        {
            if (!JmfSectionCatalog.TryGet(requestSection.Id, out var definition)
                || !definition.IsEditableInBlockMode
                || definition.Kind == JmfSectionKind.System)
            {
                continue;
            }

            var existing = indexes.TryGetValue(definition.Id, out var existingIndex)
                ? merged[existingIndex]
                : null;

            var section = new JmfSection(
                definition.Id,
                definition.Title,
                requestSection.Content,
                definition.Kind,
                definition.IsEditableInBlockMode,
                (existing?.Provenance ?? JmfSectionProvenance.Unknown).WithUserEdit());

            if (existing is not null)
            {
                merged[existingIndex] = section;
            }
            else
            {
                indexes[definition.Id] = merged.Count;
                merged.Add(section);
            }
        }

        return merged;
    }

    private static IReadOnlyList<string> ToMessages(IReadOnlyList<JmfValidationIssue> issues) =>
        issues.Select(issue => issue.Message).ToArray();

    private static void ValidateBlockDraftRequestShape(JournalBlockEditRequest request)
    {
        if (request.Sections is null)
        {
            throw new ArgumentException("sections is required", nameof(request));
        }

        foreach (var section in request.Sections)
        {
            if (section is null)
            {
                throw new ArgumentException("section is required", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(section.Id))
            {
                throw new ArgumentException("section id is required", nameof(request));
            }

            if (section.Content is null)
            {
                throw new ArgumentException("section content is required", nameof(request));
            }
        }
    }

    private static IReadOnlyList<string> ToAiErrorMessages(JournalAiSafeError? error)
    {
        if (error is null)
        {
            return ["LLM generation failed."];
        }

        var messages = new List<string>();
        if (!string.IsNullOrWhiteSpace(error.Message))
        {
            messages.Add(error.Message);
        }

        if (!string.IsNullOrWhiteSpace(error.Code))
        {
            messages.Add($"Code: {error.Code}");
        }

        if (!string.IsNullOrWhiteSpace(error.TechnicalDetails))
        {
            messages.Add($"Technical details: {error.TechnicalDetails}");
        }

        return messages.Count > 0 ? messages : ["LLM generation failed."];
    }

    private static string RenderAttentionMarkdown(string title, IReadOnlyList<string> errors)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(title);
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
