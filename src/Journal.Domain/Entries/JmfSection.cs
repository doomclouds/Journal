namespace Journal.Domain.Entries;

public sealed record JmfSection(
    string Id,
    string Title,
    string Content,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode);
