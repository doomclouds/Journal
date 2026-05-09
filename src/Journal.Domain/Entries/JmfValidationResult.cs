namespace Journal.Domain.Entries;

public sealed record JmfValidationResult(
    bool IsValid,
    IReadOnlyList<JmfValidationIssue> Issues)
{
    public static JmfValidationResult Valid { get; } = new(true, Array.Empty<JmfValidationIssue>());
}
