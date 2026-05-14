using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Threading.Channels;
using Journal.Domain.Entries;
using Journal.Infrastructure.Ai;
using Journal.Infrastructure.Clock;
using Journal.Infrastructure.Jmf;
using Journal.Infrastructure.Storage;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessRunStartResult(TodayJournalState Today, JournalHarnessAuditRun Run);

public sealed record JournalHarnessRunExecutionResult(TodayJournalState Today, JournalHarnessAuditRun Run);

public sealed record JournalHarnessRunStartRequest(string Mode, string? Text, string Source)
{
    public static JournalHarnessRunStartRequest AppendInput(string text, string source) =>
        new(JournalHarnessPrompt.AppendInputMode, text, source);

    public static JournalHarnessRunStartRequest ReorganizeExisting() =>
        new(JournalHarnessPrompt.ReorganizeExistingMode, null, string.Empty);
}

public sealed class JournalHarnessService
{
    private readonly RawInputStore _rawInputStore;
    private readonly DraftStore _draftStore;
    private readonly EntryStore _entryStore;
    private readonly IJournalAiSettingsReader _settingsReader;
    private readonly JournalHarnessPlanner _planner;
    private readonly JournalHarnessAuditStore _auditStore;
    private readonly IJournalClock _clock;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _runGates = new(StringComparer.Ordinal);

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
        CancellationToken cancellationToken) =>
        await StartTodayRunAsync(JournalHarnessRunStartRequest.AppendInput(text, source), cancellationToken);

    public async Task<JournalHarnessRunStartResult> StartTodayRunAsync(
        JournalHarnessRunStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.Equals(request.Mode, JournalHarnessPrompt.AppendInputMode, StringComparison.Ordinal))
        {
            return await StartAppendInputRunAsync(request, cancellationToken);
        }

        if (string.Equals(request.Mode, JournalHarnessPrompt.ReorganizeExistingMode, StringComparison.Ordinal))
        {
            return await StartReorganizeExistingRunAsync(cancellationToken);
        }

        throw new ArgumentException("run mode is invalid", nameof(request));
    }

    private async Task<JournalHarnessRunStartResult> StartAppendInputRunAsync(
        JournalHarnessRunStartRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("text is required", nameof(request));
        }

        var date = JournalDate.From(_clock.Today);
        var now = _clock.Now;
        var input = new RawInput(
            $"raw-{Guid.NewGuid():N}",
            date,
            now,
            string.IsNullOrWhiteSpace(request.Source) ? "text" : request.Source.Trim(),
            request.Text);

        await _rawInputStore.AppendAsync(input, cancellationToken);
        var run = await CreateQueuedRunAsync(
            date,
            now,
            JournalHarnessPrompt.AppendInputMode,
            input.Id,
            cancellationToken);

        await _auditStore.WriteAsync(run, cancellationToken);
        return new JournalHarnessRunStartResult(await BuildStateAsync(date, null, cancellationToken), run);
    }

    private async Task<JournalHarnessRunStartResult> StartReorganizeExistingRunAsync(
        CancellationToken cancellationToken)
    {
        var date = JournalDate.From(_clock.Today);
        var now = _clock.Now;
        var run = await CreateQueuedRunAsync(
            date,
            now,
            JournalHarnessPrompt.ReorganizeExistingMode,
            currentRawInputId: null,
            cancellationToken);

        await _auditStore.WriteAsync(run, cancellationToken);
        return new JournalHarnessRunStartResult(await BuildStateAsync(date, null, cancellationToken), run);
    }

    private async Task<JournalHarnessAuditRun> CreateQueuedRunAsync(
        JournalDate date,
        DateTimeOffset now,
        string mode,
        string? currentRawInputId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
        var provider = ResolveProvider(settings);
        return new JournalHarnessAuditRun(
            $"run-{date.IsoDate}-{Guid.NewGuid():N}",
            date,
            now,
            null,
            null,
            "queued",
            mode,
            provider.Id,
            JournalHarnessPrompt.Version,
            currentRawInputId,
            Array.Empty<JournalHarnessAuditToolCall>(),
            Array.Empty<string>(),
            "Harness run queued.");
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
            CancellationToken.None);

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

        var date = ParseDateFromRunId(runId);
        var gate = _runGates.GetOrAdd(runId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            var inProgressRun = await _auditStore.ReadAsync(date, runId, cancellationToken)
                ?? throw new InvalidOperationException("harness run does not exist.");
            if (string.Equals(inProgressRun.Status, "queued", StringComparison.Ordinal))
            {
                inProgressRun = inProgressRun with
                {
                    Status = "running",
                    Summary = "Harness run is already running."
                };
            }

            Emit(emit, "run-status", inProgressRun, $"Harness run is {inProgressRun.Status}.");
            return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, null, cancellationToken), inProgressRun);
        }

        try
        {
            var run = await _auditStore.ReadAsync(date, runId, cancellationToken)
                ?? throw new InvalidOperationException("harness run does not exist.");
            if (!string.Equals(run.Status, "queued", StringComparison.Ordinal))
            {
                var eventType = IsTerminalRunStatus(run.Status)
                    ? "run-already-completed"
                    : "run-status";
                Emit(emit, eventType, run, $"Harness run is {run.Status}.");
                return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, null, cancellationToken), run);
            }

            var now = _clock.Now;
            run = run with { StartedAt = now, Status = "running", Summary = "Harness run started." };
            await _auditStore.WriteAsync(run, cancellationToken);
            Emit(emit, "run-started", run, "Harness run started.");

            try
            {
                var settings = await _settingsReader.ReadEffectiveAsync(cancellationToken);
                var provider = ResolveProvider(settings);
                var allInputs = await _rawInputStore.ReadAsync(date, cancellationToken);
                var draft = await _draftStore.ReadAsync(date, cancellationToken);
                var entry = await _entryStore.ReadAsync(date, cancellationToken);
                var isReorganizeExistingRun = IsReorganizeExistingRun(run);
                var baselineMarkdown = isReorganizeExistingRun
                    ? CreateEmptyDraftMarkdown(date, allInputs, now)
                    : draft?.Markdown ?? entry?.Markdown ?? CreateEmptyDraftMarkdown(date, allInputs, now);
                var baselineDocument = BuildBaselineDocumentWithServerRawInputs(baselineMarkdown, allInputs);
                var authoritativeBaselineMarkdown = JmfMarkdownComposer.Compose(baselineDocument);
                var promptContextInputs = GetPromptContextInputs(run, allInputs);
                var promptBaselineDocument = BuildBaselineDocumentWithServerRawInputs(baselineMarkdown, promptContextInputs);
                var promptBaselineMarkdown = JmfMarkdownComposer.Compose(promptBaselineDocument);
                var prompt = BuildPromptForRun(
                    run,
                    date,
                    allInputs,
                    promptBaselineMarkdown,
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
                        allInputs,
                        errors,
                        "Planner failed.",
                        cancellationToken);
                    Emit(emit, "run-completed", run, run.Summary);
                    return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, JournalStatus.Attention, cancellationToken), run);
                }

                var allowedRawInputIds = allInputs.Select(input => input.Id).ToArray();
                var execution = JournalHarnessOperationExecutor.Apply(baselineDocument, plan.Operations, allowedRawInputIds);
                var toolCalls = CreateToolCalls(plan.Operations, execution.Issues);
                var sourceRawInputIds = allInputs.Select(input => input.Id).ToArray();
                if (execution.Validation.IsValid)
                {
                    var status = plan.Operations.Any(operation => !string.Equals(operation.Kind, "no-op", StringComparison.Ordinal))
                        ? "reviewing"
                        : "no-change";
                    if (status == "reviewing")
                    {
                        var markdown = JmfMarkdownComposer.Compose(execution.Document);
                        await _draftStore.WriteAsync(
                            new JournalDraft(date, JournalStatus.Reviewing, markdown, sourceRawInputIds, Array.Empty<string>(), _clock.Now),
                            cancellationToken);
                    }

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
                    var statusOverride = status == "reviewing" ? JournalStatus.Reviewing : (JournalStatus?)null;
                    return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, statusOverride, cancellationToken), run);
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
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var failed = await CompleteWithFailureAsync(run, date, exception, cancellationToken);
                Emit(emit, "run-failed", failed, failed.Summary);
                return new JournalHarnessRunExecutionResult(await BuildStateAsync(date, JournalStatus.Attention, cancellationToken), failed);
            }
        }
        finally
        {
            gate.Release();
        }
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

    private static JournalHarnessPromptRequest BuildPromptForRun(
        JournalHarnessAuditRun run,
        JournalDate date,
        IReadOnlyList<RawInput> inputs,
        string authoritativeBaselineMarkdown,
        string confirmedEntryMarkdown)
    {
        if (IsReorganizeExistingRun(run))
        {
            return JournalHarnessPrompt.BuildForReorganizeExisting(
                date,
                inputs,
                string.Empty,
                string.Empty);
        }

        if (!string.Equals(run.Mode, JournalHarnessPrompt.AppendInputMode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("harness run mode is invalid.");
        }

        if (string.IsNullOrWhiteSpace(run.CurrentRawInputId))
        {
            throw new InvalidOperationException("current raw input does not exist.");
        }

        var currentInput = inputs.FirstOrDefault(input => string.Equals(input.Id, run.CurrentRawInputId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("current raw input does not exist.");

        return JournalHarnessPrompt.BuildForAppendInput(
            date,
            inputs.Where(input => !string.Equals(input.Id, currentInput.Id, StringComparison.Ordinal)).ToArray(),
            currentInput,
            authoritativeBaselineMarkdown,
            confirmedEntryMarkdown);
    }

    private static IReadOnlyList<RawInput> GetPromptContextInputs(
        JournalHarnessAuditRun run,
        IReadOnlyList<RawInput> inputs)
    {
        if (!string.Equals(run.Mode, JournalHarnessPrompt.AppendInputMode, StringComparison.Ordinal))
        {
            return inputs;
        }

        if (string.IsNullOrWhiteSpace(run.CurrentRawInputId))
        {
            throw new InvalidOperationException("current raw input does not exist.");
        }

        return inputs
            .Where(input => !string.Equals(input.Id, run.CurrentRawInputId, StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsReorganizeExistingRun(JournalHarnessAuditRun run) =>
        string.Equals(run.Mode, JournalHarnessPrompt.ReorganizeExistingMode, StringComparison.Ordinal);

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

    private static JournalDate ParseDateFromRunId(string runId)
    {
        if (!LocalJournalPaths.IsValidHarnessRunId(runId)
            || runId.Length < 16
            || !runId.StartsWith("run-", StringComparison.Ordinal)
            || runId[14] != '-'
            || !DateOnly.TryParseExact(runId.AsSpan(4, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ArgumentException("runId is invalid.", nameof(runId));
        }

        return JournalDate.From(date);
    }

    private async Task<JournalHarnessAuditRun> CompleteWithFailureAsync(
        JournalHarnessAuditRun run,
        JournalDate date,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var errors = new[] { ToSafeErrorMessage(exception) };

        try
        {
            var inputs = await _rawInputStore.ReadAsync(date, CancellationToken.None);
            var draft = await _draftStore.ReadAsync(date, CancellationToken.None);
            var entry = await _entryStore.ReadAsync(date, CancellationToken.None);
            var baselineMarkdown = draft?.Markdown ?? entry?.Markdown ?? CreateEmptyDraftMarkdown(date, inputs, _clock.Now);
            var baselineDocument = BuildBaselineDocumentWithServerRawInputs(baselineMarkdown, inputs);
            await _draftStore.WriteAsync(
                new JournalDraft(
                    date,
                    JournalStatus.Attention,
                    JmfMarkdownComposer.Compose(baselineDocument),
                    inputs.Select(input => input.Id).ToArray(),
                    errors,
                    _clock.Now),
                CancellationToken.None);
        }
        catch (Exception draftException) when (draftException is not OperationCanceledException)
        {
            errors = [errors[0], $"Failed to write attention draft: {ToSafeErrorMessage(draftException)}"];
        }

        var failed = run with
        {
            CompletedAt = _clock.Now,
            Status = "failed",
            Errors = errors,
            Summary = "Harness run failed before completion."
        };
        await _auditStore.WriteAsync(failed, CancellationToken.None);
        return failed;
    }

    private static string ToSafeErrorMessage(Exception exception) =>
        exception.GetType().Name;

    private static bool IsTerminalRunStatus(string status) =>
        string.Equals(status, "reviewing", StringComparison.Ordinal)
        || string.Equals(status, "no-change", StringComparison.Ordinal)
        || string.Equals(status, "attention", StringComparison.Ordinal)
        || string.Equals(status, "failed", StringComparison.Ordinal);

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
        var document = RestoreLegacyGeneratedSectionProvenance(parseResult.Document);
        var rawContent = RenderRawInputsSectionContent(inputs);
        var sections = document.Sections.ToList();
        var rawIndex = sections.FindIndex(section => string.Equals(section.Id, "raw-inputs", StringComparison.Ordinal));
        if (rawIndex >= 0)
        {
            sections[rawIndex] = sections[rawIndex] with { Content = rawContent };
            return document with { Sections = sections };
        }

        var definition = JmfSectionCatalog.Require("raw-inputs");
        sections.Insert(0, new JmfSection(
            definition.Id,
            definition.Title,
            rawContent,
            definition.Kind,
            definition.IsEditableInBlockMode));

        return document with { Sections = sections };
    }

    private static JmfDocument RestoreLegacyGeneratedSectionProvenance(JmfDocument document)
    {
        if (!LooksLikeGeneratedDocument(document))
        {
            return document;
        }

        var changed = false;
        var sections = document.Sections
            .Select(section =>
            {
                if (!CanRestoreLegacyAiProvenance(section))
                {
                    return section;
                }

                changed = true;
                return section with
                {
                    Provenance = new JmfSectionProvenance(
                        "ai",
                        "ai",
                        "ai",
                        "create",
                        Array.Empty<string>())
                };
            })
            .ToArray();

        return changed ? document with { Sections = sections } : document;
    }

    private static bool LooksLikeGeneratedDocument(JmfDocument document) =>
        HasFrontMatterValue(document, "provider")
        && HasFrontMatterValue(document, "model")
        && HasFrontMatterValue(document, "prompt_version")
        && HasFrontMatterValue(document, "generated_at");

    private static bool HasFrontMatterValue(JmfDocument document, string key) =>
        document.FrontMatter.TryGetValue(key, out var value)
        && !string.IsNullOrWhiteSpace(value);

    private static bool CanRestoreLegacyAiProvenance(JmfSection section) =>
        IsUnknownProvenance(section.Provenance)
        && JmfSectionCatalog.TryGet(section.Id, out var definition)
        && definition.IsEditableInBlockMode
        && definition.Kind != JmfSectionKind.System
        && !string.Equals(definition.Id, "raw-inputs", StringComparison.Ordinal);

    private static bool IsUnknownProvenance(JmfSectionProvenance provenance) =>
        string.Equals(provenance.Origin, "unknown", StringComparison.Ordinal)
        && string.Equals(provenance.CreatedBy, "unknown", StringComparison.Ordinal)
        && string.Equals(provenance.LastTouchedBy, "unknown", StringComparison.Ordinal)
        && string.Equals(provenance.LastOperation, "unknown", StringComparison.Ordinal)
        && provenance.BasedOnRawInputIds.Count == 0;

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
