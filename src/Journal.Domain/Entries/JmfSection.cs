namespace Journal.Domain.Entries;

public sealed record JmfSection(
    string Id,
    string Title,
    string Content,
    JmfSectionKind Kind,
    bool IsEditableInBlockMode,
    JmfSectionProvenance Provenance)
{
    public JmfSection(
        string id,
        string title,
        string content,
        JmfSectionKind kind,
        bool isEditableInBlockMode)
        : this(id, title, content, kind, isEditableInBlockMode, JmfSectionProvenance.Unknown)
    {
    }
}
