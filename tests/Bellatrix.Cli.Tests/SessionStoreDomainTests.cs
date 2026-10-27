using Bellatrix.Execution.SessionMounting;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Domain scoping of the shared <see cref="SessionStore"/>: desktop, web and mobile share one
/// <c>.bellatrix/sessions.db</c>, so each domain's store must only see, prune and reject duplicates against its
/// own rows, and a pre-domain database must migrate in place with its rows assigned to desktop.
/// </summary>
public class SessionStoreDomainTests
{
    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"bellatrix-domain-{Guid.NewGuid():N}.db");

    private static SessionRecord Record(string name, string sessionId, DateTime lastActivityUtc) => new()
    {
        LogicalName = name,
        SessionId = sessionId,
        DriverType = "Appium",
        ServerUrl = "http://127.0.0.1:4723",
        LastActivityUtc = lastActivityUtc,
    };

    private static void Cleanup(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void WebPrune_NeverDeletesDesktopRow()
    {
        var path = TempDbPath();
        try
        {
            var stale = DateTime.UtcNow.AddHours(-1);
            new SessionStore(path, "desktop").Upsert(Record("calc", "desk-1", stale));
            new SessionStore(path, "web").Upsert(Record("w1", "web-1", stale));

            var pruned = new SessionStore(path, "web").Prune(TimeSpan.FromMinutes(10), DateTime.UtcNow);

            Assert.Equal(new[] { "w1" }, pruned);
            Assert.NotNull(new SessionStore(path, "desktop").TryGet("calc"));
            Assert.Null(new SessionStore(path, "web").TryGet("w1"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void SameLogicalName_AllowedAcrossDomains_AndIsolated()
    {
        var path = TempDbPath();
        try
        {
            var now = DateTime.UtcNow;
            new SessionStore(path, "desktop").Upsert(Record("s1", "desk-1", now), rejectDuplicate: true);
            new SessionStore(path, "web").Upsert(Record("s1", "web-1", now), rejectDuplicate: true);

            Assert.Equal("desk-1", new SessionStore(path, "desktop").TryGet("s1")!.SessionId);
            Assert.Equal("web-1", new SessionStore(path, "web").TryGet("s1")!.SessionId);
            Assert.Null(new SessionStore(path, "mobile").TryGet("s1"));

            new SessionStore(path, "web").Remove("s1");
            Assert.NotNull(new SessionStore(path, "desktop").TryGet("s1")); // web close never touches desktop
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void DuplicateWithinOneDomain_IsStillRejected()
    {
        var path = TempDbPath();
        try
        {
            var store = new SessionStore(path, "mobile");
            store.Upsert(Record("m1", "mob-1", DateTime.UtcNow), rejectDuplicate: true);

            Assert.Throws<InvalidOperationException>(
                () => store.Upsert(Record("m1", "mob-2", DateTime.UtcNow), rejectDuplicate: true));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void UnscopedStore_ListsEveryDomain()
    {
        var path = TempDbPath();
        try
        {
            var now = DateTime.UtcNow;
            new SessionStore(path, "desktop").Upsert(Record("s1", "desk-1", now));
            new SessionStore(path, "web").Upsert(Record("s1", "web-1", now));
            new SessionStore(path, "mobile").Upsert(Record("m1", "mob-1", now));

            var all = new SessionStore(path).ReadAllRecords();

            Assert.Equal(3, all.Count);
            Assert.Equal(new[] { "desktop", "mobile", "web" }, all.Select(r => r.Domain).OrderBy(d => d).ToArray());
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LegacySchema_MigratesInPlace_RowsBecomeDesktop()
    {
        var path = TempDbPath();
        try
        {
            // The pre-domain schema, exactly as shipped through 2.1.0 (logical_name is the sole key).
            using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "CREATE TABLE sessions (logical_name TEXT PRIMARY KEY, session_id TEXT NOT NULL, " +
                    "driver_type TEXT NOT NULL, server_url TEXT NOT NULL, caps_json TEXT, last_activity_utc TEXT, " +
                    "last_window_title TEXT, depends_on TEXT, priority INTEGER DEFAULT 0, ordinal INTEGER DEFAULT 0);" +
                    "INSERT INTO sessions (logical_name, session_id, driver_type, server_url, caps_json, last_activity_utc, " +
                    "last_window_title, priority, ordinal) VALUES ('pos1', 'legacy-1', 'NovaWindows', " +
                    "'http://127.0.0.1:4723', '{\"a\":1}', '2026-10-26T10:00:00.0000000Z', 'Calculator', 3, 1);";
                cmd.ExecuteNonQuery();
            }

            var migrated = new SessionStore(path, "desktop").TryGet("pos1");

            Assert.NotNull(migrated);
            Assert.Equal("desktop", migrated!.Domain);
            Assert.Equal("legacy-1", migrated.SessionId);
            Assert.Equal("Calculator", migrated.LastWindowTitle);
            Assert.Equal("{\"a\":1}", migrated.Caps);
            Assert.Equal(3, migrated.Priority);
            Assert.Null(new SessionStore(path, "web").TryGet("pos1"));

            // Re-opening an already-migrated database is a no-op.
            Assert.Single(new SessionStore(path).ReadAllRecords());
        }
        finally
        {
            Cleanup(path);
        }
    }
}
