namespace Journal.Domain.Entries;

public sealed record TodayEditorState(
    JournalDate Date,
    JournalStatus Status,
    string Markdown,
    IReadOnlyList<JmfSection> Sections,
    IReadOnlyList<JmfSectionDefinition> AvailableOptionalSections,
    JmfValidationResult Validation,
    bool CanConfirm,
    TodayJournalState Today);
