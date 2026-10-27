using Bellatrix.Cli.Output;
using Bellatrix.Execution.SessionMounting;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// Top-level <c>bellatrix sessions</c> command: inspect and clean up the SQLite session store
/// without a daemon. <c>--list</c> reports the persisted sessions; <c>--prune</c> deletes rows whose
/// last activity is older than the keep-alive TTL and reports the pruned names. Idle
/// server sessions are otherwise reclaimed by the driver's own newCommandTimeout.
/// </summary>
internal static class SessionsCommand
{
    public static async Task<int> Run(CommandContext ctx)
    {
        await Task.CompletedTask;

        // Admin view spans every domain (desktop/web/mobile); domain verbs use their own scoped store.
        var store = new SessionStore();

        if (ctx.Options.ContainsKey("prune"))
        {
            var ttl = TimeSpan.FromMinutes(ctx.Settings.NewCommandTimeoutMinutes ?? 10);
            var now = DateTime.UtcNow;

            // Pruned web sessions can never be re-attached: stop their detached local driver servers first.
            foreach (var stale in store.ReadAllRecords().Where(r => r.Domain == "web" && now - r.LastActivityUtc > ttl))
            {
                Bellatrix.Execution.Web.WebDriverSessionManager.StopDriverServer(
                    Bellatrix.Execution.Web.WebDriverSessionManager.ReadDriverPid(stale.Caps));
            }

            var pruned = store.Prune(ttl, now);
            return JsonOutput.Success(new { pruned, count = pruned.Count });
        }

        // Default and --list both report the store.
        var sessions = store.ReadAllRecords()
            .OrderBy(r => r.Ordinal)
            .ThenByDescending(r => r.Priority)
            .Select(r => new
            {
                domain = r.Domain,
                logicalName = r.LogicalName,
                sessionId = r.SessionId,
                driverType = r.DriverType,
                serverUrl = r.ServerUrl,
                lastActivityUtc = r.LastActivityUtc,
            })
            .ToList();

        return JsonOutput.Success(new { sessions, count = sessions.Count });
    }
}
