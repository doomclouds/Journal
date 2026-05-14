namespace Journal.Domain.Entries;

public sealed record JournalDataExportManifest(
    string Format,
    DateTimeOffset CreatedAt,
    string AppVersion,
    string BackendVersion,
    string FrontendVersion,
    int EntryCount,
    int RawInputCount,
    int VersionCount,
    bool ContainsFullApiKeys);

public sealed record JournalDataExportResult(
    string ExportPath,
    JournalDataExportManifest Manifest);
