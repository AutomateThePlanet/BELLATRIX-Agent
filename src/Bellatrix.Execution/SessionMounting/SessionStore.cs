using Microsoft.Data.Sqlite;

namespace Bellatrix.Execution.SessionMounting;

/// <summary>
/// SQLite-backed session registry under <c>.bellatrix/sessions.db</c>, replacing
/// the former on-disk JSON registry. Maps a caller-supplied logical name to a live driver-server
/// session that a fresh, stateless CLI process re-mounts by id. Concurrency-safe
/// across parallel CLI processes via WAL journaling + <c>busy_timeout</c>; the logical name is an opaque
/// key and lookups fail loud rather than fabricating a session. Holds no secrets — only ids, a local
/// server URL, and cached capabilities. Provides <see cref="Prune"/> for daemon-less
/// idle cleanup.
///
/// Rows are keyed by <c>(domain, logical_name)</c>. A store constructed with a domain (<c>desktop</c>,
/// <c>web</c>, <c>mobile</c>) only ever sees, rejects duplicates against, touches, removes, and prunes its own
/// domain's rows, so the same logical name may exist in two domains and one domain's idle-prune never deletes
/// another's sessions. A store constructed without a domain is unscoped (admin <c>bellatrix sessions</c> and
/// tests): it reads every domain and writes <see cref="SessionRecord.Domain"/>. A pre-domain database is
/// migrated in place on open, with every existing row assigned to <c>desktop</c>.
/// </summary>
public sealed class SessionStore
{
    private const int BusyTimeoutMs = 5000;
    private const string Columns = "logical_name, session_id, driver_type, server_url, caps_json, " +
                                   "last_activity_utc, last_window_title, depends_on, priority, ordinal, domain";

    private readonly string _path;
    private readonly string _connectionString;
    private readonly string? _domain;

    /// <summary>Default store location: a working-directory-relative <c>.bellatrix/sessions.db</c>.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Directory.GetCurrentDirectory(), ".bellatrix", "sessions.db");

    public SessionStore(string? path = null, string? domain = null)
    {
        _path = path ?? DefaultPath;
        _domain = string.IsNullOrWhiteSpace(domain) ? null : domain.ToLowerInvariant();
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Disable pooling: each CLI invocation is a short-lived process, so pooling adds no value and
        // keeping a file handle open would block snapshot/db cleanup and concurrent processes.
        _connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
        EnsureSchema();
    }

    /// <summary>The backing database file path (useful for diagnostics and tests).</summary>
    public string Path => _path;

    /// <summary>The domain this store is scoped to, or null when unscoped (all domains).</summary>
    public string? Domain => _domain;

    /// <summary>
    /// Reads this store's records keyed by logical name (case-insensitive). On an unscoped store a name used
    /// in two domains collapses to one entry; use <see cref="ReadAllRecords"/> to see every row.
    /// </summary>
    public Dictionary<string, SessionRecord> ReadAll()
    {
        var map = new Dictionary<string, SessionRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in ReadAllRecords())
        {
            if (!string.IsNullOrWhiteSpace(record.LogicalName))
            {
                map[record.LogicalName] = record;
            }
        }

        return map;
    }

    /// <summary>Reads every record visible to this store (all domains when unscoped), one per row.</summary>
    public IReadOnlyList<SessionRecord> ReadAllRecords()
    {
        var records = new List<SessionRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM sessions WHERE 1=1{ScopeClause()};";
        BindScope(cmd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(Read(reader));
        }

        return records;
    }

    /// <summary>Returns the record for a logical name, or null if absent.</summary>
    public SessionRecord? TryGet(string logicalName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM sessions WHERE logical_name = $name COLLATE NOCASE{ScopeClause()};";
        cmd.Parameters.AddWithValue("$name", logicalName);
        BindScope(cmd);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>
    /// Inserts or updates a record by logical name. Throws <see cref="InvalidOperationException"/> when
    /// <paramref name="rejectDuplicate"/> is set and a different live session already owns the name
    /// (no silent aliasing).
    /// </summary>
    public void Upsert(SessionRecord record, bool rejectDuplicate = false)
    {
        var domain = _domain ?? (string.IsNullOrWhiteSpace(record.Domain) ? "desktop" : record.Domain.ToLowerInvariant());
        record.Domain = domain;

        using var conn = Open();
        if (rejectDuplicate)
        {
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT session_id FROM sessions WHERE logical_name = $name COLLATE NOCASE AND domain = $domain;";
            check.Parameters.AddWithValue("$name", record.LogicalName);
            check.Parameters.AddWithValue("$domain", domain);
            var existing = check.ExecuteScalar() as string;
            if (existing is not null && !string.Equals(existing, record.SessionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"A session named '{record.LogicalName}' already exists (sessionId {existing}). Close it first or use a different --session name.");
            }
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO sessions ({Columns}) " +
            "VALUES ($name, $sid, $driver, $url, $caps, $activity, $title, $deps, $priority, $ordinal, $domain) " +
            "ON CONFLICT(domain, logical_name) DO UPDATE SET session_id=$sid, driver_type=$driver, server_url=$url, " +
            "caps_json=$caps, last_activity_utc=$activity, last_window_title=$title, depends_on=$deps, " +
            "priority=$priority, ordinal=$ordinal;";
        Bind(cmd, record);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes a record by logical name within this store's domain (no-op if absent). On an unscoped store the
    /// name is removed from every domain.
    /// </summary>
    public void Remove(string logicalName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM sessions WHERE logical_name = $name COLLATE NOCASE{ScopeClause()};";
        cmd.Parameters.AddWithValue("$name", logicalName);
        BindScope(cmd);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Refreshes the activity timestamp (and optionally the window title) for a name.</summary>
    public void Touch(string logicalName, DateTime utcNow, string? windowTitle = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = windowTitle is null
            ? $"UPDATE sessions SET last_activity_utc = $activity WHERE logical_name = $name COLLATE NOCASE{ScopeClause()};"
            : $"UPDATE sessions SET last_activity_utc = $activity, last_window_title = $title WHERE logical_name = $name COLLATE NOCASE{ScopeClause()};";
        cmd.Parameters.AddWithValue("$activity", utcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$name", logicalName);
        BindScope(cmd);
        if (windowTitle is not null)
        {
            cmd.Parameters.AddWithValue("$title", windowTitle);
        }

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes rows whose last activity is older than <paramref name="ttl"/> and returns the pruned
    /// logical names — daemon-less idle cleanup. The caller is responsible for any
    /// best-effort server-side session DELETE.
    /// </summary>
    public IReadOnlyList<string> Prune(TimeSpan ttl, DateTime utcNow)
    {
        var cutoff = utcNow - ttl;
        var pruned = new List<string>();
        foreach (var record in ReadAllRecords())
        {
            if (record.LastActivityUtc < cutoff)
            {
                RemoveExact(record.Domain, record.LogicalName);
                pruned.Add(record.LogicalName);
            }
        }

        return pruned;
    }

    /// <summary>Deletes exactly one row by its <c>(domain, logical_name)</c> key.</summary>
    private void RemoveExact(string domain, string logicalName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE logical_name = $name AND domain = $domain;";
        cmd.Parameters.AddWithValue("$name", logicalName);
        cmd.Parameters.AddWithValue("$domain", domain);
        cmd.ExecuteNonQuery();
    }

    private string ScopeClause() => _domain is null ? string.Empty : " AND domain = $domain";

    private void BindScope(SqliteCommand cmd)
    {
        if (_domain is not null)
        {
            cmd.Parameters.AddWithValue("$domain", _domain);
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyTimeoutMs};";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private static string CreateTableSql(string table) =>
        $"CREATE TABLE IF NOT EXISTS {table} (" +
        "domain TEXT NOT NULL DEFAULT 'desktop', logical_name TEXT NOT NULL, session_id TEXT NOT NULL, " +
        "driver_type TEXT NOT NULL, server_url TEXT NOT NULL, caps_json TEXT, last_activity_utc TEXT, " +
        "last_window_title TEXT, depends_on TEXT, priority INTEGER DEFAULT 0, ordinal INTEGER DEFAULT 0, " +
        "PRIMARY KEY (domain, logical_name));";

    private void EnsureSchema()
    {
        using var conn = Open();
        if (HasLegacySchema(conn))
        {
            MigrateLegacySchema(conn);
        }

        Exec(conn, CreateTableSql("sessions"));
    }

    /// <summary>True when a pre-domain <c>sessions</c> table (keyed by logical_name only) exists.</summary>
    private static bool HasLegacySchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('sessions');";
        using var reader = cmd.ExecuteReader();
        var hasTable = false;
        while (reader.Read())
        {
            hasTable = true;
            if (string.Equals(reader.GetString(0), "domain", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return hasTable;
    }

    /// <summary>
    /// Rebuilds a pre-domain table under the composite <c>(domain, logical_name)</c> key, assigning every
    /// existing row to <c>desktop</c>. Runs under <c>BEGIN IMMEDIATE</c> and re-checks inside the write lock
    /// so concurrent CLI processes opening the same database migrate it exactly once.
    /// </summary>
    private static void MigrateLegacySchema(SqliteConnection conn)
    {
        Exec(conn, "BEGIN IMMEDIATE;");
        try
        {
            if (HasLegacySchema(conn))
            {
                Exec(conn, "DROP TABLE IF EXISTS sessions_v2;");
                Exec(conn, CreateTableSql("sessions_v2"));
                Exec(conn,
                    $"INSERT INTO sessions_v2 ({Columns}) " +
                    "SELECT logical_name, session_id, driver_type, server_url, caps_json, last_activity_utc, " +
                    "last_window_title, depends_on, priority, ordinal, 'desktop' FROM sessions;");
                Exec(conn, "DROP TABLE sessions;");
                Exec(conn, "ALTER TABLE sessions_v2 RENAME TO sessions;");
            }

            Exec(conn, "COMMIT;");
        }
        catch
        {
            Exec(conn, "ROLLBACK;");
            throw;
        }
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, SessionRecord record)
    {
        cmd.Parameters.AddWithValue("$name", record.LogicalName);
        cmd.Parameters.AddWithValue("$sid", record.SessionId);
        cmd.Parameters.AddWithValue("$driver", record.DriverType);
        cmd.Parameters.AddWithValue("$url", record.ServerUrl);
        cmd.Parameters.AddWithValue("$caps", (object?)record.Caps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$activity", record.LastActivityUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$title", (object?)record.LastWindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$deps", (object?)record.DependsOn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$priority", record.Priority);
        cmd.Parameters.AddWithValue("$ordinal", record.Ordinal);
        cmd.Parameters.AddWithValue("$domain", record.Domain);
    }

    private static SessionRecord Read(SqliteDataReader reader) => new()
    {
        LogicalName = reader.GetString(0),
        SessionId = reader.GetString(1),
        DriverType = reader.GetString(2),
        ServerUrl = reader.GetString(3),
        Caps = reader.IsDBNull(4) ? null : reader.GetString(4),
        LastActivityUtc = reader.IsDBNull(5) ? default : DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
        LastWindowTitle = reader.IsDBNull(6) ? null : reader.GetString(6),
        DependsOn = reader.IsDBNull(7) ? null : reader.GetString(7),
        Priority = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
        Ordinal = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
        Domain = reader.IsDBNull(10) ? "desktop" : reader.GetString(10),
    };
}
