using Bellatrix.Execution.SessionMounting;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Unit tests for the SQLite-backed <see cref="SessionStore"/>. No live driver is
/// required: the store is pure persistence. Covers the cross-process
/// round-trip, fail-loud absence, reject-duplicate, the extra routing columns, and the
/// daemon-less TTL prune.
/// </summary>
public class SessionStoreTests
{
    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"bellatrix-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void Upsert_Get_And_Remove_RoundTrip_AcrossInstances()
    {
        var path = TempDbPath();
        try
        {
            var store = new SessionStore(path);
            Assert.Null(store.TryGet("pos1")); // fail-loud absence, never fabricate

            store.Upsert(new SessionRecord
            {
                LogicalName = "pos1",
                SessionId = "abc-123",
                DriverType = "NovaWindows",
                ServerUrl = "http://127.0.0.1:4723",
                Caps = "{\"appium:automationName\":\"NovaWindows\"}",
                DependsOn = "central",
                Priority = 5,
                Ordinal = 2,
                LastActivityUtc = DateTime.UtcNow,
            });

            // A fresh instance must read the persisted db — the cross-process contract.
            var reopened = new SessionStore(path);
            var got = reopened.TryGet("pos1");
            Assert.NotNull(got);
            Assert.Equal("abc-123", got!.SessionId);
            Assert.Equal("NovaWindows", got.DriverType);
            Assert.Equal("central", got.DependsOn);
            Assert.Equal(5, got.Priority);
            Assert.Equal(2, got.Ordinal);

            reopened.Remove("pos1");
            Assert.Null(new SessionStore(path).TryGet("pos1"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void RejectsDuplicateLogicalName_ButAllowsIdempotentRefresh()
    {
        var path = TempDbPath();
        try
        {
            var store = new SessionStore(path);
            store.Upsert(new SessionRecord { LogicalName = "pos1", SessionId = "id-1", DriverType = "WinAppDriver", ServerUrl = "http://127.0.0.1:4723" });

            // A different session under the same name is rejected (no silent aliasing).
            Assert.Throws<InvalidOperationException>(() =>
                store.Upsert(new SessionRecord { LogicalName = "pos1", SessionId = "id-2", DriverType = "WinAppDriver", ServerUrl = "http://127.0.0.1:4723" }, rejectDuplicate: true));

            // The same session id is an allowed refresh.
            store.Upsert(new SessionRecord { LogicalName = "pos1", SessionId = "id-1", DriverType = "WinAppDriver", ServerUrl = "http://127.0.0.1:4723" }, rejectDuplicate: true);
            Assert.Equal("id-1", store.TryGet("pos1")!.SessionId);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Prune_RemovesOnlyExpiredRows_AndReportsThem()
    {
        var path = TempDbPath();
        try
        {
            var store = new SessionStore(path);
            var now = DateTime.UtcNow;
            store.Upsert(new SessionRecord { LogicalName = "fresh", SessionId = "s-fresh", DriverType = "WinAppDriver", ServerUrl = "http://127.0.0.1:4723", LastActivityUtc = now });
            store.Upsert(new SessionRecord { LogicalName = "stale", SessionId = "s-stale", DriverType = "WinAppDriver", ServerUrl = "http://127.0.0.1:4723", LastActivityUtc = now.AddMinutes(-30) });

            var pruned = store.Prune(TimeSpan.FromMinutes(10), now);

            Assert.Equal(new[] { "stale" }, pruned);
            Assert.NotNull(store.TryGet("fresh"));
            Assert.Null(store.TryGet("stale"));
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static void Cleanup(string path)
    {
        foreach (var p in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
