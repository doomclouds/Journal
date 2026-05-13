using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Infrastructure.Storage;

public sealed class JournalIndexingService
{
    private const string InvalidJmfReason = "invalid_jmf";
    private const string MissingEntryFileReason = "entry_file_missing";
    private readonly LocalJournalPaths _paths;
    private readonly JournalIndexStore _indexStore;

    public JournalIndexingService(LocalJournalPaths paths, JournalIndexStore indexStore)
    {
        _paths = paths;
        _indexStore = indexStore;
    }

    public async Task IndexEntryAsync(
        JournalDate date,
        string markdown,
        string entryPath,
        string status,
        DateTimeOffset indexedAtUtc,
        CancellationToken cancellationToken)
    {
        await _indexStore.EnsureReadyAsync(cancellationToken);

        var indexedAt = indexedAtUtc.ToUniversalTime();
        var parseResult = JmfMarkdownParser.Parse(markdown);
        var validation = JmfMarkdownValidator.Validate(parseResult.Document, parseResult.Issues);
        if (!validation.IsValid)
        {
            if (await _indexStore.ReadSummaryAsync(date, cancellationToken) is null)
            {
                await _indexStore.UpsertEntryAsync(
                    CreateMinimalAttentionEntry(date, markdown, entryPath, indexedAt),
                    [],
                    cancellationToken);
            }

            await _indexStore.MarkEntryStatusAsync(date, "attention", InvalidJmfReason, indexedAt, cancellationToken);
            return;
        }

        var entry = CreateIndexedEntry(date, markdown, entryPath, status, indexedAt, parseResult.Document);
        var sections = parseResult.Document.Sections
            .Select((section, index) => new JournalIndexedSection(
                date,
                section.Id,
                section.Title,
                index * 10,
                section.Content))
            .ToArray();

        await _indexStore.UpsertEntryAsync(entry, sections, cancellationToken);
    }

    public async Task ScanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _indexStore.EnsureReadyAsync(cancellationToken);

        var entryRoot = _paths.EntryRootDirectory();
        if (Directory.Exists(entryRoot))
        {
            foreach (var entryPath in Directory.EnumerateFiles(entryRoot, "*.md", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileNameWithoutExtension(entryPath);
                if (!JournalDate.TryParse(fileName, out var date))
                {
                    continue;
                }

                var markdown = await File.ReadAllTextAsync(entryPath, Encoding.UTF8, cancellationToken);
                await IndexEntryAsync(date, markdown, entryPath, "processed", now, cancellationToken);
            }
        }

        var index = await _indexStore.ReadEntryIndexAsync(cancellationToken);
        foreach (var entry in index.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(entry.EntryPath))
            {
                await _indexStore.MarkEntryStatusAsync(
                    entry.Date,
                    "missing",
                    MissingEntryFileReason,
                    now.ToUniversalTime(),
                    cancellationToken);
            }
        }
    }

    public async Task SyncRawInputsAsync(
        JournalDate date,
        IReadOnlyList<RawInput> rawInputs,
        CancellationToken cancellationToken)
    {
        await _indexStore.EnsureReadyAsync(cancellationToken);

        foreach (var rawInput in rawInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _indexStore.UpsertRawInputAsync(
                new JournalIndexedRawInput(
                    rawInput.Id,
                    date,
                    rawInput.CreatedAt.ToUniversalTime(),
                    rawInput.Source,
                    rawInput.Text),
                cancellationToken);
        }
    }

    public async Task SyncVersionAsync(JournalEntryVersion version, CancellationToken cancellationToken)
    {
        await _indexStore.EnsureReadyAsync(cancellationToken);
        await _indexStore.UpsertVersionAsync(version, cancellationToken);
    }

    public async Task RebuildAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _indexStore.BackupAndResetAsync(now, "rebuild", cancellationToken);
        await ScanAsync(now, cancellationToken);
    }

    private static JournalIndexedEntry CreateIndexedEntry(
        JournalDate date,
        string markdown,
        string entryPath,
        string status,
        DateTimeOffset indexedAtUtc,
        JmfDocument document)
    {
        var (lastWriteTimeUtc, fileSize) = ReadFileFacts(entryPath, markdown, indexedAtUtc);
        document.FrontMatter.TryGetValue("mood", out var mood);

        return new JournalIndexedEntry(
            date,
            entryPath,
            status,
            string.IsNullOrWhiteSpace(mood) ? null : mood,
            "[]",
            "[]",
            ComputeSha256(markdown),
            lastWriteTimeUtc,
            fileSize,
            indexedAtUtc,
            null);
    }

    private static JournalIndexedEntry CreateMinimalAttentionEntry(
        JournalDate date,
        string markdown,
        string entryPath,
        DateTimeOffset indexedAtUtc)
    {
        var (lastWriteTimeUtc, fileSize) = ReadFileFacts(entryPath, markdown, indexedAtUtc);

        return new JournalIndexedEntry(
            date,
            entryPath,
            "attention",
            null,
            "[]",
            "[]",
            ComputeSha256(markdown),
            lastWriteTimeUtc,
            fileSize,
            indexedAtUtc,
            InvalidJmfReason);
    }

    private static (DateTimeOffset LastWriteTimeUtc, long FileSize) ReadFileFacts(
        string entryPath,
        string markdown,
        DateTimeOffset indexedAtUtc)
    {
        var file = new FileInfo(entryPath);
        if (file.Exists)
        {
            return (new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), file.Length);
        }

        return (indexedAtUtc, Encoding.UTF8.GetByteCount(markdown));
    }

    private static string ComputeSha256(string markdown)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(markdown));
        return "sha256:" + Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }
}
