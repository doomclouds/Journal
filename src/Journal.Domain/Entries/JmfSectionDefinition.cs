namespace Journal.Domain.Entries;

public sealed record JmfSectionDefinition(
    string Id,
    string Title,
    int Order,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode);
