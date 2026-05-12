using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessRunStartResult(TodayJournalState Today, JournalHarnessAuditRun Run);

public sealed record JournalHarnessRunExecutionResult(TodayJournalState Today, JournalHarnessAuditRun Run);

public sealed class JournalHarnessService
{
    private readonly RawInputStore _rawInputStore;
    private readonly DraftStore _draftStore;
    private readonly EntryStore _entryStore;
    private readonly IJournalAiSettingsReader _settingsReader;
    private readonly JournalHarnessPlanner _planner;
    private readonly JournalHarnessAuditStore _auditStore;
    private readonly IJournalClock _clock;

    public JournalHarnessService(
        RawInputStore rawInputStore,
        DraftStore draftStore,
        EntryStore entryStore,
        IJournalAiSettingsReader settingsReader,
        JournalHarnessPlanner planner,
        JournalHarnessAuditStore auditStore,
        IJournalClock clock)
    {
        _rawInputStore = rawInputStore;
        _draftStore = draftStore;
        _entryStore = entryStore;
        _settingsReader = settingsReader;
        _planner = planner;
        _auditStore = auditStore;
        _clock = clock;
    }

    public async Task<JournalHarnessRunStartResult> StartTodayRunAsync(
        string text,
        string source,
        CancellationToken cancellationToken)
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
        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        var provider = ResolveProvider(settings);
        var run = new JournalHarnessAuditRun(
            $"run-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}",
            date,
            now,
            null,
            null,
            "queued",
            provider.Id,
            JournalHarnessPrompt.Version,
            input.Id,
            Array.Empty<JournalHarnessAuditToolCall>(),
            Array.Empty<string>(),
            "Harness run queued.");

        await _auditStore.WriteAsync(run, cancellationToken);
        return new JournalHarnessRunStartResult(await BuildStateAsync(date, null, cancellationToken), run);
    }

    public Task<JournalHarnessRunExecutionResult> ExecuteRunAsync(
        string runId,
        CancellationToken cancellationToken) =>
        ExecuteRunCoreAsync(runId, emit: null, cancellationToken);

    public async IAsyncEnumerable<JournalHarnessRunEvent> ExecuteRunAsStreamAsync(
        string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<JournalHarnessRunEvent>();
        var executeTask = ExecuteRunCoreAsync(
            runId,
            runEvent => channel.Writer.TryWrite(runEvent),
            cancellationToken);

        _ = executeTask.ContinueWith(
            task =>
            {
                if (task.Exception is not null)
                {
                    channel.Writer.TryComplete(task.Exception);
                    return;
                }

                channel.Writer.TryComplete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var runEvent))
            {
                yield return runEvent;
            }
        }

        await executeTask;
    }

    private async Task<JournalHarnessRunExecutionResult> ExecuteRunCoreAsync(
        string runId,
        Action<JournalHarnessRunEvent>? emit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("runId is required", nameof(runId));
        }

        var date = JournalDate.From(_clock.Today);
        var run = await _auditStore.ReadAsync(date, runId, cancellationToken)
            ?? throw new InvalidOperationException("harness run does not exist.");
        var now = _clock.Now;
        run = run with { StartedAt = now, Status = "running", Summary = "Harness run started." };
        await _auditStore.WriteAsync(run, cancellationToken);
        Emit(emit, "run-started", run, "Harness run started.");

        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        var provider = ResolveProvider(settings);
        var inputs = await _rawInputStore.ReadAsync(date, cancellationToken);
        var currentInput = inputs.FirstOrDefault(input => string.Equals(input.Id, run.CurrentRawInputId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("current raw input does not exist.");
        var draft = await _draftStore.ReadAsync(date, cancellationToken);
        var entry = await _entryStore.ReadAsync(date, cancellationToken);
        var baselineMarkdown = draft?.Markdown ?? entry?.Markdown ?? CreateEmptyDraftMarkdown(date, inputs, now);
        var baselineDocument = BuildBaselineDocumentWithServerRawInputs(baselineMarkdown, inputs);
        var authoritativeBaselineMarkdown = JmfMarkdownComposer.Compose(baselineDocument);
        var prompt = JournalHarnessPrompt.Build(
            date,
            inputs.Where(input => !string.Equals(input.Id, currentInput.Id, StringComparison.Ordinal)).ToArray(),
            currentInput,
            authoritativeBaselineMarkdown,
            entry?.Markdown ?? string.Empty);

        Emit(emit, "planner-started", run, "Planner started.");
        var plan = await _planner.PlanAsync(provider, prompt, cancellationToken);
        if (!plan.IsSuccess)
        {
            var errors = ToPlanErrors(plan);
            run = await CompleteWithAttentionAsync(
                run,
                date,
                authoritativeBaselineMarkdown,
                inputs,
                errors,
                "Planner failed.",
                cancellationToken);
            Emit(emit, "run-completed", run, run.Summary);
            return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, JournalStatus.Attention, cancellationToken), run);
        }

        var execution = JournalHarnessOperationExecutor.Apply(baselineDocument, plan.Operations);
        var toolCalls = CreateToolCalls(plan.Operations, execution.Issues);
        var sourceRawInputIds = inputs.Select(input => input.Id).ToArray();
        if (execution.Validation.IsValid)
        {
            var markdown = JmfMarkdownComposer.Compose(execution.Document);
            var status = plan.Operations.Any(operation => !string.Equals(operation.Kind, "no-op", StringComparison.Ordinal))
                ? "reviewing"
                : "no-change";
            await _draftStore.WriteAsync(
                new JournalDraft(date, JournalStatus.Reviewing, markdown, sourceRawInputIds, Array.Empty<string>(), _clock.Now),
                cancellationToken);

            run = run with
            {
                CompletedAt = _clock.Now,
                Status = status,
                ProviderId = provider.Id,
                ToolCalls = toolCalls,
                Errors = Array.Empty<string>(),
                Summary = status == "reviewing"
                    ? "Harness run completed and wrote a reviewing draft."
                    : "Harness run completed with no draft changes."
            };
            await _auditStore.WriteAsync(run, cancellationToken);
            Emit(emit, "run-completed", run, run.Summary);
            return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, JournalStatus.Reviewing, cancellationToken), run);
        }

        var validationErrors = execution.Issues.Select(issue => issue.Message).ToArray();
        var attentionMarkdown = JmfMarkdownComposer.Compose(baselineDocument);
        await _draftStore.WriteAsync(
            new JournalDraft(date, JournalStatus.Attention, attentionMarkdown, sourceRawInputIds, validationErrors, _clock.Now),
            cancellationToken);

        run = run with
        {
            CompletedAt = _clock.Now,
            Status = "attention",
            ProviderId = provider.Id,
            ToolCalls = toolCalls,
            Errors = validationErrors,
            Summary = "Harness run completed with validation issues."
        };
        await _auditStore.WriteAsync(run, cancellationToken);
        Emit(emit, "run-completed", run, run.Summary);
        return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, JournalStatus.Attention, cancellationToken), run);
    }

    private async Task<JournalHarnessAuditRun> CompleteWithAttentionAsync(
        JournalHarnessAuditRun run,
        JournalDate date,
        string baselineMarkdown,
        IReadOnlyList<RawInput> inputs,
        IReadOnlyList<string> errors,
        string summary,
        CancellationToken cancellationToken)
    {
        await _draftStore.WriteAsync(
            new JournalDraft(
                date,
                JournalStatus.Attention,
                baselineMarkdown,
                inputs.Select(input => input.Id).ToArray(),
                errors,
                _clock.Now),
            cancellationToken);

        var completed = run with
        {
            CompletedAt = _clock.Now,
            Status = "attention",
            Errors = errors,
            Summary = summary
        };
        await _auditStore.WriteAsync(completed, cancellationToken);
        return completed;
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

    private static JournalAiProviderSettings ResolveProvider(JournalAiSettings settings)
    {
        var active = settings.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase));
        if (active is not null)
        {
            return active;
        }

        return settings.Providers.FirstOrDefault(provider => provider.IsEnabled)
            ?? settings.Providers.FirstOrDefault()
            ?? throw new InvalidOperationException("No AI provider is configured.");
    }

    private static string CreateEmptyDraftMarkdown(
        JournalDate date,
        IReadOnlyList<RawInput> inputs,
        DateTimeOffset now)
    {
        var aiJson = new JournalAiJson(
            "journal-entry/v1",
            date.IsoDate,
            date.MonthDay,
            "draft",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "未标注",
            inputs.Select(input => input.Text).ToArray(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var metadata = new JournalAiMetadata(
            "harness",
            "local-baseline",
            JournalHarnessPrompt.Version);

        return JmfMarkdownRenderer.Render(aiJson, now, metadata);
    }

    private static JmfDocument BuildBaselineDocumentWithServerRawInputs(
        string baselineMarkdown,
        IReadOnlyList<RawInput> inputs)
    {
        var parseResult = JmfMarkdownParser.Parse(baselineMarkdown);
        var rawContent = RenderRawInputsSectionContent(inputs);
        var sections = parseResult.Document.Sections.ToList();
        var rawIndex = sections.FindIndex(section => string.Equals(section.Id, "raw-inputs", StringComparison.Ordinal));
        if (rawIndex >= 0)
        {
            sections[rawIndex] = sections[rawIndex] with { Content = rawContent };
            return parseResult.Document with { Sections = sections };
        }

        var definition = JmfSectionCatalog.Require("raw-inputs");
        sections.Insert(0, new JmfSection(
            definition.Id,
            definition.Title,
            rawContent,
            definition.Kind,
            definition.IsEditableInBlockMode));

        return parseResult.Document with { Sections = sections };
    }

    private static string RenderRawInputsSectionContent(IReadOnlyList<RawInput> inputs) =>
        string.Join(
            Environment.NewLine,
            inputs
                .Where(input => !string.IsNullOrWhiteSpace(input.Text))
                .Select(input => $"- {SanitizeBullet(input.Text)}"));

    private static string SanitizeBullet(string value) =>
        value
            .Trim()
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("<!--", "&lt;!--", StringComparison.Ordinal)
            .Replace("-->", "--&gt;", StringComparison.Ordinal);

    private static IReadOnlyList<JournalHarnessAuditToolCall> CreateToolCalls(
        IReadOnlyList<JournalHarnessOperation> operations,
        IReadOnlyList<JmfValidationIssue> issues)
    {
        var hasIssues = issues.Count > 0;
        return operations.Select((operation, index) => new JournalHarnessAuditToolCall(
            $"tool-{index + 1}",
            ToToolName(operation.Kind),
            operation.Kind,
            operation.TargetSectionId,
            hasIssues ? "rejected" : "applied",
            operation.Reason,
            hasIssues ? "Operation was rejected by JMF validation." : ToResultSummary(operation),
            hasIssues ? string.Join("; ", issues.Select(issue => issue.Message)) : null)).ToArray();
    }

    private static string ToToolName(string kind) =>
        kind switch
        {
            "append" => "appendJournalSection",
            "upsert" => "upsertJournalSection",
            "revise-ai-generated-section" => "reviseAiGeneratedSection",
            "no-op" => "noOp",
            _ => kind
        };

    private static string ToResultSummary(JournalHarnessOperation operation) =>
        string.Equals(operation.Kind, "no-op", StringComparison.Ordinal)
            ? "No draft change requested."
            : $"Applied {operation.Kind} to {operation.TargetSectionId}.";

    private static IReadOnlyList<string> ToPlanErrors(JournalHarnessPlanResult plan)
    {
        if (plan.Error is null)
        {
            return ["Planner failed."];
        }

        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.Error.Message))
        {
            errors.Add(plan.Error.Message);
        }

        if (!string.IsNullOrWhiteSpace(plan.Error.Code))
        {
            errors.Add($"Code: {plan.Error.Code}");
        }

        if (!string.IsNullOrWhiteSpace(plan.Error.TechnicalDetails))
        {
            errors.Add($"Technical details: {plan.Error.TechnicalDetails}");
        }

        return errors.Count > 0 ? errors : ["Planner failed."];
    }

    private static void Emit(
        Action<JournalHarnessRunEvent>? emit,
        string type,
        JournalHarnessAuditRun run,
        string message) =>
        emit?.Invoke(new JournalHarnessRunEvent(type, run.Id, run.Status, message));
}
