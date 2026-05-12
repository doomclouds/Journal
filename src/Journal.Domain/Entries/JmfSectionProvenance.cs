namespace Journal.Domain.Entries;

public sealed record JmfSectionProvenance(
    string Origin,
    string CreatedBy,
    string LastTouchedBy,
    string LastOperation,
    IReadOnlyList<string> BasedOnRawInputIds)
{
    public static JmfSectionProvenance Unknown { get; } =
        new("unknown", "unknown", "unknown", "unknown", Array.Empty<string>());

    public JmfSectionProvenance WithAiAppend(IReadOnlyList<string> rawInputIds) =>
        this with
        {
            Origin = string.Equals(Origin, "user", StringComparison.Ordinal)
                || string.Equals(Origin, "mixed", StringComparison.Ordinal)
                ? "mixed"
                : "ai",
            LastTouchedBy = "ai",
            LastOperation = "append",
            BasedOnRawInputIds = rawInputIds
        };

    public JmfSectionProvenance WithUserEdit() =>
        this with
        {
            Origin = string.Equals(Origin, "ai", StringComparison.Ordinal) ? "mixed" : Origin,
            LastTouchedBy = "user",
            LastOperation = "edit"
        };
}
