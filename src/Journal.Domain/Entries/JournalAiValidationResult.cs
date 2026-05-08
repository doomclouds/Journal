namespace Journal.Domain.Entries;

public sealed record JournalAiValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static JournalAiValidationResult Valid { get; } = new(true, Array.Empty<string>());

    public static JournalAiValidationResult Invalid(params string[] errors) => new(false, errors);
}
