namespace Journal.Domain.Entries;

public sealed record JmfValidationIssue(
    string Code,
    string Message,
    string RepairHint);
