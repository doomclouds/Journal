namespace Journal.Domain.Entries;

public sealed record JournalHarnessAuditRun(
    string Id,
    JournalDate Date,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string Mode,
    string ProviderId,
    string PromptVersion,
    string? CurrentRawInputId,
    IReadOnlyList<JournalHarnessAuditToolCall> ToolCalls,
    IReadOnlyList<string> Errors,
    string Summary);

public sealed record JournalHarnessAuditToolCall(
    string Id,
    string Name,
    string OperationKind,
    string TargetSectionId,
    string Status,
    string Reason,
    string ResultSummary,
    string? RejectionReason);

public sealed record JournalHarnessRunEvent(
    string Type,
    string RunId,
    string Status,
    string Message);
