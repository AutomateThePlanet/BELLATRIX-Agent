using Bellatrix.Execution.SessionMounting;

namespace Bellatrix.Cli.E2E;

/// <summary>Domain isolation of the shared session store, through real CLI processes.</summary>
public class CrossDomainE2ETests
{
    [E2EFact(Gates = "DESKTOP,WEB")]
    public void DesktopAndWebSessions_AliveAtOnce_ListSessionsShowsOnlyOwnDomain()
    {
        using var cli = new CliProcess("cross-live");

        var desktop = cli.Run("desktop", "start-session", "--app", "calculator", "--session", "x", "--json");
        CliProcess.SkipIfUnreachable(desktop, "WinAppDriver");
        var web = cli.Run("web", "start-session", "--browser", "chrome", "--session", "x", "--json");
        CliProcess.SkipIfUnreachable(web, "chromedriver");
        var desktopId = desktop.Ok.GetProperty("sessionId").GetString()!;
        var webId = web.Ok.GetProperty("sessionId").GetString()!;

        var desktopList = cli.Run("desktop", "list-sessions", "--json").Ok.GetRawText();
        var webList = cli.Run("web", "list-sessions", "--json").Ok.GetRawText();
        Assert.Contains(desktopId, desktopList);
        Assert.DoesNotContain(webId, desktopList);
        Assert.Contains(webId, webList);
        Assert.DoesNotContain(desktopId, webList);

        // The same logical name in both domains: each domain's verb still reaches its own session.
        _ = cli.Run("desktop", "get-title", "--session", "x", "--json").Ok;
        _ = cli.Run("web", "get-title", "--session", "x", "--json").Ok;

        var all = cli.Run("sessions", "--json").Ok;
        Assert.Equal(2, all.GetProperty("count").GetInt32());

        _ = cli.Run("web", "close-session", "--session", "x", "--json").Ok;
        _ = cli.Run("desktop", "close-session", "--session", "x", "--json").Ok;
    }

    /// <summary>No driver needed: one stale ghost row per domain; each domain's list-sessions prunes only its own.</summary>
    [E2EFact]
    public void Prune_NeverCrossesDomains()
    {
        using var cli = new CliProcess("cross-prune");
        var stale = DateTime.UtcNow.AddHours(-2);
        foreach (var domain in new[] { "desktop", "web", "mobile" })
        {
            cli.Store(domain).Upsert(new SessionRecord
            {
                Domain = domain,
                LogicalName = $"ghost-{domain}",
                SessionId = $"ghost-id-{domain}",
                LastActivityUtc = stale,
            });
        }

        _ = cli.Run("web", "list-sessions", "--json").Ok;
        Assert.Null(cli.Store("web").TryGet("ghost-web"));
        Assert.NotNull(cli.Store("desktop").TryGet("ghost-desktop"));
        Assert.NotNull(cli.Store("mobile").TryGet("ghost-mobile"));

        _ = cli.Run("mobile", "list-sessions", "--json").Ok;
        Assert.Null(cli.Store("mobile").TryGet("ghost-mobile"));
        Assert.NotNull(cli.Store("desktop").TryGet("ghost-desktop"));

        _ = cli.Run("desktop", "list-sessions", "--json").Ok;
        Assert.Null(cli.Store("desktop").TryGet("ghost-desktop"));
    }
}
