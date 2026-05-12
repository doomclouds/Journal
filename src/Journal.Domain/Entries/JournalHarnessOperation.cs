namespace Journal.Domain.Entries;

public sealed record JournalHarnessOperation(
    string Kind,
    string TargetSectionId,
    string Content,
    IReadOnlyList<string> BasedOnRawInputIds,
    string Reason)
{
    public static JournalHarnessOperation Append(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("append", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation Upsert(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("upsert", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation ReviseAiGeneratedSection(
        string targetSectionId,
        string content,
        IReadOnlyList<string> basedOnRawInputIds,
        string reason) =>
        new("revise-ai-generated-section", targetSectionId, content, basedOnRawInputIds, reason);

    public static JournalHarnessOperation NoOp(string reason) =>
        new("no-op", string.Empty, string.Empty, Array.Empty<string>(), reason);
}
