namespace Journal.Domain.Entries;

public sealed record JmfParseResult(
    JmfDocument Document,
    IReadOnlyList<JmfValidationIssue> Issues);
