using System.Globalization;
using Journal.Domain.Entries;
using Microsoft.Data.Sqlite;

namespace Journal.Infrastructure.Storage;

public sealed class JournalIndexStore
{
    private const int SchemaVersion = 1;
    private const int SearchHitsPerDateLimit = 5;
    private const int LikeSnippetMaxLength = 240;
    private readonly LocalJournalPaths _paths;

    public JournalIndexStore(LocalJournalPaths paths)
    {
        _paths = paths;
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyCoreAsync(DateTimeOffset.UtcNow, allowBackup: true, cancellationToken);
    }

    public async Task<string?> ReadMetaAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM journal_meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    public async Task SetMetaAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO journal_meta(key, value)
            VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertEntryAsync(
        JournalIndexedEntry entry,
        IReadOnlyList<JournalIndexedSection> sections,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var upsertEntry = connection.CreateCommand();
        upsertEntry.Transaction = (SqliteTransaction)transaction;
        upsertEntry.CommandText = """
            INSERT INTO entries(
                date,
                month_day,
                entry_path,
                status,
                mood,
                tags_json,
                topics_json,
                content_hash,
                last_write_time_utc,
                file_size,
                indexed_at_utc,
                attention_reason)
            VALUES(
                $date,
                $monthDay,
                $entryPath,
                $status,
                $mood,
                $tagsJson,
                $topicsJson,
                $contentHash,
                $lastWriteTimeUtc,
                $fileSize,
                $indexedAtUtc,
                $attentionReason)
            ON CONFLICT(date) DO UPDATE SET
                month_day = excluded.month_day,
                entry_path = excluded.entry_path,
                status = excluded.status,
                mood = excluded.mood,
                tags_json = excluded.tags_json,
                topics_json = excluded.topics_json,
                content_hash = excluded.content_hash,
                last_write_time_utc = excluded.last_write_time_utc,
                file_size = excluded.file_size,
                indexed_at_utc = excluded.indexed_at_utc,
                attention_reason = excluded.attention_reason;
            """;
        AddEntryParameters(upsertEntry, entry);
        await upsertEntry.ExecuteNonQueryAsync(cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            (SqliteTransaction)transaction,
            "DELETE FROM entry_sections WHERE date = $date;",
            command => command.Parameters.AddWithValue("$date", entry.Date.IsoDate),
            cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            (SqliteTransaction)transaction,
            "DELETE FROM section_fts WHERE date = $date;",
            command => command.Parameters.AddWithValue("$date", entry.Date.IsoDate),
            cancellationToken);

        foreach (var section in sections)
        {
            var upsertSection = connection.CreateCommand();
            upsertSection.Transaction = (SqliteTransaction)transaction;
            upsertSection.CommandText = """
                INSERT INTO entry_sections(date, section_id, title, display_order, content)
                VALUES($date, $sectionId, $title, $displayOrder, $content)
                ON CONFLICT(date, section_id) DO UPDATE SET
                    title = excluded.title,
                    display_order = excluded.display_order,
                    content = excluded.content;
                """;
            AddSectionParameters(upsertSection, section);
            await upsertSection.ExecuteNonQueryAsync(cancellationToken);

            var insertFts = connection.CreateCommand();
            insertFts.Transaction = (SqliteTransaction)transaction;
            insertFts.CommandText = """
                INSERT INTO section_fts(date, section_id, title, content, metadata)
                VALUES($date, $sectionId, $title, $content, $metadata);
                """;
            AddSectionParameters(insertFts, section);
            insertFts.Parameters.AddWithValue("$metadata", section.SectionId);
            await insertFts.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpsertRawInputAsync(JournalIndexedRawInput rawInput, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var upsertRaw = connection.CreateCommand();
        upsertRaw.Transaction = (SqliteTransaction)transaction;
        upsertRaw.CommandText = """
            INSERT INTO raw_inputs(id, date, created_at_utc, source, text)
            VALUES($id, $date, $createdAtUtc, $source, $text)
            ON CONFLICT(id) DO UPDATE SET
                date = excluded.date,
                created_at_utc = excluded.created_at_utc,
                source = excluded.source,
                text = excluded.text;
            """;
        AddRawInputParameters(upsertRaw, rawInput);
        await upsertRaw.ExecuteNonQueryAsync(cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            (SqliteTransaction)transaction,
            "DELETE FROM raw_input_fts WHERE raw_input_id = $id;",
            command => command.Parameters.AddWithValue("$id", rawInput.Id),
            cancellationToken);

        var insertFts = connection.CreateCommand();
        insertFts.Transaction = (SqliteTransaction)transaction;
        insertFts.CommandText = """
            INSERT INTO raw_input_fts(raw_input_id, date, source, text)
            VALUES($id, $date, $source, $text);
            """;
        AddRawInputParameters(insertFts, rawInput);
        await insertFts.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkEntryStatusAsync(
        JournalDate date,
        string status,
        string? attentionReason,
        DateTimeOffset indexedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE entries
            SET status = $status,
                attention_reason = $attentionReason,
                indexed_at_utc = $indexedAtUtc
            WHERE date = $date;
            """;
        command.Parameters.AddWithValue("$date", date.IsoDate);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$attentionReason", (object?)attentionReason ?? DBNull.Value);
        command.Parameters.AddWithValue("$indexedAtUtc", FormatDateTime(indexedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, JournalIndexedEntry>> ReadEntryIndexAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, entry_path, status, mood, tags_json, topics_json, content_hash,
                   last_write_time_utc, file_size, indexed_at_utc, attention_reason
            FROM entries
            ORDER BY date DESC;
            """;

        var entries = new Dictionary<string, JournalIndexedEntry>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var entry = ReadIndexedEntry(reader);
            entries[entry.Date.IsoDate] = entry;
        }

        return entries;
    }

    public async Task UpsertVersionAsync(JournalEntryVersion version, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entry_versions(id, date, version_path, created_at_utc, reason, content_hash, source_entry_path)
            VALUES($id, $date, $versionPath, $createdAtUtc, $reason, $contentHash, $sourceEntryPath)
            ON CONFLICT(id) DO UPDATE SET
                date = excluded.date,
                version_path = excluded.version_path,
                created_at_utc = excluded.created_at_utc,
                reason = excluded.reason,
                content_hash = excluded.content_hash,
                source_entry_path = excluded.source_entry_path;
            """;
        command.Parameters.AddWithValue("$id", version.Id);
        command.Parameters.AddWithValue("$date", version.Date.IsoDate);
        command.Parameters.AddWithValue("$versionPath", version.MarkdownPath);
        command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(version.CreatedAt));
        command.Parameters.AddWithValue("$reason", version.Reason);
        command.Parameters.AddWithValue("$contentHash", version.ContentHash);
        command.Parameters.AddWithValue("$sourceEntryPath", version.SourceEntryPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<JournalHistoryEntrySummary?> ReadSummaryAsync(JournalDate date, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.date,
                   e.status,
                   e.mood,
                   e.attention_reason,
                   (SELECT COUNT(*) FROM raw_inputs r WHERE r.date = e.date) AS raw_input_count,
                   (SELECT COUNT(*) FROM entry_versions v WHERE v.date = e.date) AS version_count
            FROM entries e
            WHERE e.date = $date;
            """;
        command.Parameters.AddWithValue("$date", date.IsoDate);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadSummary(reader, []);
    }

    public async Task<JournalHistorySearchResult> SearchAsync(JournalHistoryQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var normalizedQuery = string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim();
        var limit = NormalizeLimit(query.Limit);

        if (normalizedQuery is null)
        {
            return new JournalHistorySearchResult(await SearchEntriesAsync(connection, query, limit, cancellationToken));
        }

        if (normalizedQuery.Length < 3)
        {
            return new JournalHistorySearchResult(await SearchLikeAsync(connection, query, normalizedQuery, limit, cancellationToken));
        }

        return new JournalHistorySearchResult(await SearchFtsAsync(connection, query, normalizedQuery, limit, cancellationToken));
    }

    public async Task<JournalAnniversaryWheelResult> ReadAnniversaryAsync(
        string monthDay,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, cancellationToken);
        var normalizedLimit = NormalizeLimit(limit);
        var command = connection.CreateCommand();
        command.CommandText = """
            WITH selected_entries AS (
                SELECT e.date,
                       e.status,
                       e.mood,
                       e.attention_reason,
                       (SELECT COUNT(*) FROM raw_inputs r WHERE r.date = e.date) AS raw_input_count,
                       (SELECT COUNT(*) FROM entry_versions v WHERE v.date = e.date) AS version_count
                FROM entries e
                WHERE e.month_day = $monthDay
                ORDER BY e.date DESC
                LIMIT $limit
            ),
            candidate_hits AS (
                SELECT se.date,
                       se.status,
                       se.mood,
                       se.attention_reason,
                       'section' AS source_type,
                       s.section_id AS section_id,
                       NULL AS raw_input_id,
                       s.title AS title,
                       CASE
                           WHEN length(CASE WHEN substr(ltrim(s.content), 1, 2) = '- ' THEN substr(ltrim(s.content), 3) ELSE s.content END) > $snippetMaxLength
                           THEN substr(CASE WHEN substr(ltrim(s.content), 1, 2) = '- ' THEN substr(ltrim(s.content), 3) ELSE s.content END, 1, $snippetMaxLength - 3) || '...'
                           ELSE CASE WHEN substr(ltrim(s.content), 1, 2) = '- ' THEN substr(ltrim(s.content), 3) ELSE s.content END
                       END AS snippet,
                       se.raw_input_count,
                       se.version_count,
                       ROW_NUMBER() OVER (
                           PARTITION BY se.date
                           ORDER BY s.display_order, s.section_id
                       ) AS hit_rank
                FROM selected_entries se
                INNER JOIN entry_sections s ON s.date = se.date
                UNION ALL
                SELECT se.date,
                       se.status,
                       se.mood,
                       se.attention_reason,
                       'raw-input' AS source_type,
                       NULL AS section_id,
                       r.id AS raw_input_id,
                       r.source AS title,
                       CASE
                           WHEN length(r.text) > $snippetMaxLength
                           THEN substr(r.text, 1, $snippetMaxLength - 3) || '...'
                           ELSE r.text
                       END AS snippet,
                       se.raw_input_count,
                       se.version_count,
                       ROW_NUMBER() OVER (
                           PARTITION BY se.date
                           ORDER BY r.created_at_utc, r.id
                       ) AS hit_rank
                FROM selected_entries se
                INNER JOIN raw_inputs r ON r.date = se.date
            )
            SELECT se.date,
                   se.status,
                   se.mood,
                   se.attention_reason,
                   COALESCE(h.source_type, 'section') AS source_type,
                   h.section_id,
                   h.raw_input_id,
                   COALESCE(h.title, '日记') AS title,
                   COALESCE(h.snippet, '') AS snippet,
                   se.raw_input_count,
                   se.version_count
            FROM selected_entries se
            LEFT JOIN candidate_hits h ON h.date = se.date
                AND (
                    (h.source_type = 'section'
                        AND h.hit_rank <= CASE WHEN se.raw_input_count > 0 THEN $hitLimit - 1 ELSE $hitLimit END)
                    OR (h.source_type = 'raw-input' AND h.hit_rank <= 1)
                )
            ORDER BY se.date DESC,
                     CASE h.source_type WHEN 'section' THEN 0 WHEN 'raw-input' THEN 1 ELSE 2 END,
                     h.section_id,
                     h.raw_input_id;
            """;
        command.Parameters.AddWithValue("$monthDay", monthDay);
        command.Parameters.AddWithValue("$limit", normalizedLimit);
        command.Parameters.AddWithValue("$hitLimit", SearchHitsPerDateLimit);
        command.Parameters.AddWithValue("$snippetMaxLength", LikeSnippetMaxLength);

        return new JournalAnniversaryWheelResult(
            monthDay,
            await ReadAnniversaryGroupedHitsAsync(command, normalizedLimit, cancellationToken));
    }

    public async Task BackupAndResetAsync(DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.IndexBackupDirectory());
        var databasePath = _paths.IndexPath();
        if (File.Exists(databasePath) || File.Exists(databasePath + "-wal") || File.Exists(databasePath + "-shm"))
        {
            var backupPath = CreateUniqueBackupPath(now, reason);
            MoveIfExists(databasePath, backupPath);
            MoveIfExists(databasePath + "-wal", backupPath + "-wal");
            MoveIfExists(databasePath + "-shm", backupPath + "-shm");
        }

        await EnsureReadyCoreAsync(now, allowBackup: false, cancellationToken);
    }

    private async Task EnsureReadyCoreAsync(DateTimeOffset now, bool allowBackup, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.IndexDirectory());
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await ConfigureConnectionAsync(connection, cancellationToken);
            await CreateMetaTableAsync(connection, cancellationToken);
            var schemaVersion = await ReadMetaAsync(connection, "schema_version", cancellationToken);
            if (schemaVersion is not null
                && schemaVersion != SchemaVersion.ToString(CultureInfo.InvariantCulture)
                && allowBackup)
            {
                await connection.CloseAsync();
                await BackupAndResetAsync(now, "schema", cancellationToken);
                return;
            }

            await CreateSchemaAsync(connection, cancellationToken);
            if (!await ValidateSchemaAsync(connection, cancellationToken))
            {
                if (!allowBackup)
                {
                    throw new InvalidOperationException("Journal index schema validation failed after reset.");
                }

                await connection.CloseAsync();
                await BackupAndResetAsync(now, "schema", cancellationToken);
                return;
            }

            await SetMetaAsync(connection, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }
        catch (SqliteException) when (allowBackup)
        {
            await BackupAndResetAsync(now, "corrupt", cancellationToken);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.IndexDirectory());
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.IndexPath(),
            DefaultTimeout = 5,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, null, "PRAGMA journal_mode=WAL;", null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, "PRAGMA busy_timeout=5000;", null, cancellationToken);
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await CreateMetaTableAsync(connection, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS entries(
                date TEXT PRIMARY KEY,
                month_day TEXT NOT NULL,
                entry_path TEXT NOT NULL,
                status TEXT NOT NULL,
                mood TEXT NULL,
                tags_json TEXT NOT NULL,
                topics_json TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                last_write_time_utc TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                indexed_at_utc TEXT NOT NULL,
                attention_reason TEXT NULL
            );
            """, null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS entry_sections(
                date TEXT NOT NULL,
                section_id TEXT NOT NULL,
                title TEXT NOT NULL,
                display_order INTEGER NOT NULL,
                content TEXT NOT NULL,
                PRIMARY KEY(date, section_id)
            );
            """, null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS entry_versions(
                id TEXT PRIMARY KEY,
                date TEXT NOT NULL,
                version_path TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                reason TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                source_entry_path TEXT NOT NULL
            );
            """, null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS raw_inputs(
                id TEXT PRIMARY KEY,
                date TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                source TEXT NOT NULL,
                text TEXT NOT NULL
            );
            """, null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE VIRTUAL TABLE IF NOT EXISTS section_fts
            USING fts5(
                date UNINDEXED,
                section_id UNINDEXED,
                title,
                content,
                metadata,
                tokenize = 'trigram'
            );
            """, null, cancellationToken);
        await ExecuteNonQueryAsync(connection, null, """
            CREATE VIRTUAL TABLE IF NOT EXISTS raw_input_fts
            USING fts5(
                raw_input_id UNINDEXED,
                date UNINDEXED,
                source UNINDEXED,
                text,
                tokenize = 'trigram'
            );
            """, null, cancellationToken);
    }

    private static async Task CreateMetaTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS journal_meta(
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """, null, cancellationToken);
    }

    private static async Task<bool> ValidateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await IntegrityCheckPassesAsync(connection, cancellationToken))
        {
            return false;
        }

        return await HasColumnsAsync(connection, "journal_meta", ["key", "value"], cancellationToken)
            && await HasColumnsAsync(
                connection,
                "entries",
                [
                    "date",
                    "month_day",
                    "entry_path",
                    "status",
                    "mood",
                    "tags_json",
                    "topics_json",
                    "content_hash",
                    "last_write_time_utc",
                    "file_size",
                    "indexed_at_utc",
                    "attention_reason"
                ],
                cancellationToken)
            && await HasColumnsAsync(
                connection,
                "entry_sections",
                ["date", "section_id", "title", "display_order", "content"],
                cancellationToken)
            && await HasColumnsAsync(
                connection,
                "entry_versions",
                ["id", "date", "version_path", "created_at_utc", "reason", "content_hash", "source_entry_path"],
                cancellationToken)
            && await HasColumnsAsync(
                connection,
                "raw_inputs",
                ["id", "date", "created_at_utc", "source", "text"],
                cancellationToken)
            && await HasColumnsAsync(
                connection,
                "section_fts",
                ["date", "section_id", "title", "content", "metadata"],
                cancellationToken)
            && await IsFts5TrigramVirtualTableAsync(connection, "section_fts", cancellationToken)
            && await HasColumnsAsync(
                connection,
                "raw_input_fts",
                ["raw_input_id", "date", "source", "text"],
                cancellationToken)
            && await IsFts5TrigramVirtualTableAsync(connection, "raw_input_fts", cancellationToken);
    }

    private static async Task<bool> IntegrityCheckPassesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return string.Equals(result as string, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasColumnsAsync(
        SqliteConnection connection,
        string tableName,
        IReadOnlyCollection<string> requiredColumns,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
        var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            actualColumns.Add(reader.GetString(1));
        }

        return requiredColumns.All(actualColumns.Contains);
    }

    private static async Task<bool> IsFts5TrigramVirtualTableAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);
        var sql = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var normalizedSql = NormalizeSql(sql);
        return normalizedSql.Contains("createvirtualtable", StringComparison.Ordinal)
            && normalizedSql.Contains("usingfts5", StringComparison.Ordinal)
            && (normalizedSql.Contains("tokenize='trigram'", StringComparison.Ordinal)
                || normalizedSql.Contains("tokenize=\"trigram\"", StringComparison.Ordinal)
                || normalizedSql.Contains("tokenize=trigram", StringComparison.Ordinal));
    }

    private static string NormalizeSql(string sql)
    {
        return string.Concat(sql.Where(static character => !char.IsWhiteSpace(character))).ToLowerInvariant();
    }

    private static async Task<IReadOnlyList<JournalHistoryEntrySummary>> SearchEntriesAsync(
        SqliteConnection connection,
        JournalHistoryQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.date,
                   e.status,
                   e.mood,
                   e.attention_reason,
                   (SELECT COUNT(*) FROM raw_inputs r WHERE r.date = e.date) AS raw_input_count,
                   (SELECT COUNT(*) FROM entry_versions v WHERE v.date = e.date) AS version_count
            FROM entries e
            WHERE ($status IS NULL OR e.status = $status)
              AND ($from IS NULL OR e.date >= $from)
              AND ($to IS NULL OR e.date <= $to)
              AND ($cursor IS NULL OR e.date < $cursor)
            ORDER BY e.date DESC
            LIMIT $limit;
            """;
        AddSearchParameters(command, query, limit);

        var summaries = new List<JournalHistoryEntrySummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            summaries.Add(ReadSummary(reader, []));
        }

        return summaries;
    }

    private static async Task<IReadOnlyList<JournalHistoryEntrySummary>> SearchFtsAsync(
        SqliteConnection connection,
        JournalHistoryQuery query,
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            WITH hits AS (
                SELECT e.date,
                       e.status,
                       e.mood,
                       e.attention_reason,
                       'section' AS source_type,
                       section_fts.section_id AS section_id,
                       NULL AS raw_input_id,
                       section_fts.title AS title,
                       snippet(section_fts, 3, '[', ']', '...', 12) AS snippet
                FROM section_fts
                INNER JOIN entries e ON e.date = section_fts.date
                WHERE section_fts MATCH $query
                  AND ($status IS NULL OR e.status = $status)
                  AND ($from IS NULL OR e.date >= $from)
                  AND ($to IS NULL OR e.date <= $to)
                  AND ($cursor IS NULL OR e.date < $cursor)
                UNION ALL
                SELECT e.date,
                       e.status,
                       e.mood,
                       e.attention_reason,
                       'raw-input' AS source_type,
                       NULL AS section_id,
                       raw_input_fts.raw_input_id AS raw_input_id,
                       raw_input_fts.source AS title,
                       snippet(raw_input_fts, 3, '[', ']', '...', 12) AS snippet
                FROM raw_input_fts
                INNER JOIN entries e ON e.date = raw_input_fts.date
                WHERE raw_input_fts MATCH $query
                  AND ($status IS NULL OR e.status = $status)
                  AND ($from IS NULL OR e.date >= $from)
                  AND ($to IS NULL OR e.date <= $to)
                  AND ($cursor IS NULL OR e.date < $cursor)
            ),
            ranked_hits AS (
                SELECT h.*,
                       ROW_NUMBER() OVER (
                           PARTITION BY h.date
                           ORDER BY h.source_type, h.section_id, h.raw_input_id
                       ) AS hit_rank
                FROM hits h
            )
            SELECT h.date,
                   h.status,
                   h.mood,
                   h.attention_reason,
                   h.source_type,
                   h.section_id,
                   h.raw_input_id,
                   h.title,
                   h.snippet,
                   (SELECT COUNT(*) FROM raw_inputs r WHERE r.date = h.date) AS raw_input_count,
                   (SELECT COUNT(*) FROM entry_versions v WHERE v.date = h.date) AS version_count
            FROM ranked_hits h
            WHERE h.hit_rank <= $hitLimit
            ORDER BY h.date DESC, h.source_type, h.section_id, h.raw_input_id;
            """;
        AddSearchParameters(command, query, limit);
        command.Parameters.AddWithValue("$query", BuildFtsLiteralQuery(normalizedQuery));
        command.Parameters.AddWithValue("$hitLimit", SearchHitsPerDateLimit);

        return await ReadGroupedHitsAsync(command, limit, cancellationToken);
    }

    private static async Task<IReadOnlyList<JournalHistoryEntrySummary>> SearchLikeAsync(
        SqliteConnection connection,
        JournalHistoryQuery query,
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            WITH hits AS (
                SELECT e.date,
                       e.status,
                       e.mood,
                       e.attention_reason,
                       'section' AS source_type,
                       s.section_id AS section_id,
                       NULL AS raw_input_id,
                       s.title AS title,
                       CASE
                           WHEN length(s.content) > $snippetMaxLength
                           THEN substr(s.content, 1, $snippetMaxLength - 3) || '...'
                           ELSE s.content
                       END AS snippet
                FROM entry_sections s
                INNER JOIN entries e ON e.date = s.date
                WHERE (s.title LIKE $like ESCAPE '\' OR s.content LIKE $like ESCAPE '\')
                  AND ($status IS NULL OR e.status = $status)
                  AND ($from IS NULL OR e.date >= $from)
                  AND ($to IS NULL OR e.date <= $to)
                  AND ($cursor IS NULL OR e.date < $cursor)
                UNION ALL
                SELECT e.date,
                       e.status,
                       e.mood,
                       e.attention_reason,
                       'raw-input' AS source_type,
                       NULL AS section_id,
                       r.id AS raw_input_id,
                       r.source AS title,
                       CASE
                           WHEN length(r.text) > $snippetMaxLength
                           THEN substr(r.text, 1, $snippetMaxLength - 3) || '...'
                           ELSE r.text
                       END AS snippet
                FROM raw_inputs r
                INNER JOIN entries e ON e.date = r.date
                WHERE r.text LIKE $like ESCAPE '\'
                  AND ($status IS NULL OR e.status = $status)
                  AND ($from IS NULL OR e.date >= $from)
                  AND ($to IS NULL OR e.date <= $to)
                  AND ($cursor IS NULL OR e.date < $cursor)
            )
            SELECT h.date,
                   h.status,
                   h.mood,
                   h.attention_reason,
                   h.source_type,
                   h.section_id,
                   h.raw_input_id,
                   h.title,
                   h.snippet,
                   (SELECT COUNT(*) FROM raw_inputs ri WHERE ri.date = h.date) AS raw_input_count,
                   (SELECT COUNT(*) FROM entry_versions v WHERE v.date = h.date) AS version_count
            FROM hits h
            ORDER BY h.date DESC, h.source_type, h.section_id, h.raw_input_id;
            """;
        AddSearchParameters(command, query, limit);
        command.Parameters.AddWithValue("$like", $"%{EscapeLike(normalizedQuery)}%");
        command.Parameters.AddWithValue("$snippetMaxLength", LikeSnippetMaxLength);

        return await ReadGroupedHitsAsync(command, limit, cancellationToken);
    }

    private static async Task<IReadOnlyList<JournalHistoryEntrySummary>> ReadGroupedHitsAsync(
        SqliteCommand command,
        int limit,
        CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<string, MutableSummary>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = reader.GetString(0);
            if (!grouped.TryGetValue(date, out var summary))
            {
                if (grouped.Count >= limit)
                {
                    break;
                }

                summary = new MutableSummary(
                    JournalDate.Parse(date),
                    reader.GetString(1),
                    GetNullableString(reader, 2),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    GetNullableString(reader, 3));
                grouped.Add(date, summary);
            }

            if (summary.Hits.Count >= SearchHitsPerDateLimit)
            {
                continue;
            }

            summary.Hits.Add(new JournalHistoryHit(
                reader.GetString(4),
                GetNullableString(reader, 5),
                GetNullableString(reader, 6),
                reader.GetString(7),
                reader.GetString(8)));
        }

        return grouped.Values
            .Select(summary => summary.ToImmutable())
            .ToArray();
    }

    private static async Task<IReadOnlyList<JournalHistoryEntrySummary>> ReadAnniversaryGroupedHitsAsync(
        SqliteCommand command,
        int limit,
        CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<string, MutableSummary>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = reader.GetString(0);
            if (!grouped.TryGetValue(date, out var summary))
            {
                if (grouped.Count >= limit)
                {
                    break;
                }

                summary = new MutableSummary(
                    JournalDate.Parse(date),
                    reader.GetString(1),
                    GetNullableString(reader, 2),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    GetNullableString(reader, 3));
                grouped.Add(date, summary);
            }

            if (summary.Hits.Count >= SearchHitsPerDateLimit)
            {
                continue;
            }

            var snippet = reader.GetString(8);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                continue;
            }

            summary.Hits.Add(new JournalHistoryHit(
                reader.GetString(4),
                GetNullableString(reader, 5),
                GetNullableString(reader, 6),
                reader.GetString(7),
                snippet));
        }

        return grouped.Values
            .Select(summary => summary.ToImmutable())
            .ToArray();
    }

    private static JournalHistoryEntrySummary ReadSummary(SqliteDataReader reader, IReadOnlyList<JournalHistoryHit> hits) =>
        new(
            JournalDate.Parse(reader.GetString(0)),
            reader.GetString(1),
            GetNullableString(reader, 2),
            reader.GetInt32(4),
            reader.GetInt32(5),
            hits,
            GetNullableString(reader, 3));

    private static JournalIndexedEntry ReadIndexedEntry(SqliteDataReader reader) =>
        new(
            JournalDate.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            GetNullableString(reader, 3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.GetInt64(8),
            DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            GetNullableString(reader, 10));

    private static async Task<string?> ReadMetaAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM journal_meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    private static async Task SetMetaAsync(SqliteConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO journal_meta(key, value)
            VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        Action<SqliteCommand>? configure,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddEntryParameters(SqliteCommand command, JournalIndexedEntry entry)
    {
        command.Parameters.AddWithValue("$date", entry.Date.IsoDate);
        command.Parameters.AddWithValue("$monthDay", entry.Date.MonthDay);
        command.Parameters.AddWithValue("$entryPath", entry.EntryPath);
        command.Parameters.AddWithValue("$status", entry.Status);
        command.Parameters.AddWithValue("$mood", (object?)entry.Mood ?? DBNull.Value);
        command.Parameters.AddWithValue("$tagsJson", entry.TagsJson);
        command.Parameters.AddWithValue("$topicsJson", entry.TopicsJson);
        command.Parameters.AddWithValue("$contentHash", entry.ContentHash);
        command.Parameters.AddWithValue("$lastWriteTimeUtc", FormatDateTime(entry.LastWriteTimeUtc));
        command.Parameters.AddWithValue("$fileSize", entry.FileSize);
        command.Parameters.AddWithValue("$indexedAtUtc", FormatDateTime(entry.IndexedAtUtc));
        command.Parameters.AddWithValue("$attentionReason", (object?)entry.AttentionReason ?? DBNull.Value);
    }

    private static void AddSectionParameters(SqliteCommand command, JournalIndexedSection section)
    {
        command.Parameters.AddWithValue("$date", section.Date.IsoDate);
        command.Parameters.AddWithValue("$sectionId", section.SectionId);
        command.Parameters.AddWithValue("$title", section.Title);
        command.Parameters.AddWithValue("$displayOrder", section.DisplayOrder);
        command.Parameters.AddWithValue("$content", section.Content);
    }

    private static void AddRawInputParameters(SqliteCommand command, JournalIndexedRawInput rawInput)
    {
        command.Parameters.AddWithValue("$id", rawInput.Id);
        command.Parameters.AddWithValue("$date", rawInput.Date.IsoDate);
        command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(rawInput.CreatedAt));
        command.Parameters.AddWithValue("$source", rawInput.Source);
        command.Parameters.AddWithValue("$text", rawInput.Text);
    }

    private static void AddSearchParameters(SqliteCommand command, JournalHistoryQuery query, int limit)
    {
        command.Parameters.AddWithValue("$status", (object?)NormalizeFilter(query.Status) ?? DBNull.Value);
        command.Parameters.AddWithValue("$from", (object?)query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$cursor", (object?)NormalizeFilter(query.Cursor) ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", limit);
    }

    private static string FormatDateTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static int NormalizeLimit(int limit) =>
        limit <= 0 ? 50 : Math.Clamp(limit, 1, 100);

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private static string BuildFtsLiteralQuery(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private string CreateUniqueBackupPath(DateTimeOffset now, string reason)
    {
        var backupPath = Path.Combine(
            _paths.IndexBackupDirectory(),
            $"journal-{now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{SanitizeReason(reason)}.db");
        if (!BackupGroupExists(backupPath))
        {
            return backupPath;
        }

        backupPath = Path.Combine(
            _paths.IndexBackupDirectory(),
            $"journal-{now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{SanitizeReason(reason)}-{Guid.NewGuid():N}.db");
        return backupPath;
    }

    private static bool BackupGroupExists(string backupPath) =>
        File.Exists(backupPath)
        || File.Exists(backupPath + "-wal")
        || File.Exists(backupPath + "-shm");

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string SanitizeReason(string reason)
    {
        var normalized = string.IsNullOrWhiteSpace(reason) ? "reset" : reason.Trim();
        return string.Concat(normalized.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-'));
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void MoveIfExists(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private sealed class MutableSummary
    {
        public MutableSummary(
            JournalDate date,
            string status,
            string? mood,
            int rawInputCount,
            int versionCount,
            string? attentionReason)
        {
            Date = date;
            Status = status;
            Mood = mood;
            RawInputCount = rawInputCount;
            VersionCount = versionCount;
            AttentionReason = attentionReason;
        }

        public JournalDate Date { get; }

        public string Status { get; }

        public string? Mood { get; }

        public int RawInputCount { get; }

        public int VersionCount { get; }

        public string? AttentionReason { get; }

        public List<JournalHistoryHit> Hits { get; } = [];

        public JournalHistoryEntrySummary ToImmutable() =>
            new(Date, Status, Mood, RawInputCount, VersionCount, Hits.ToArray(), AttentionReason);
    }
}
