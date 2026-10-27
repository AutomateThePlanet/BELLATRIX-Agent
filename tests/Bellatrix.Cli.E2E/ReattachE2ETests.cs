using System.Text.Json;

namespace Bellatrix.Cli.E2E;

/// <summary>
/// Stateless re-attach (no daemon): a session survives N separate invocations with exactly one NewSession.
/// Evidence: the trace has exactly one start-session line, and the store's real session id never changes.
/// </summary>
public class ReattachE2ETests
{
    private const int N = 5;

    [E2EFact(Gates = "WEB")]
    public void Web_SessionSurvivesSeparateInvocations_WithOneNewSession()
    {
        using var cli = new CliProcess("reattach-web");
        var start = cli.Run("web", "start-session", "--browser", "chrome", "--session", "r", "--json");
        CliProcess.SkipIfUnreachable(start, "chromedriver");

        AssertSurvives(cli, "web", start.Ok.GetProperty("sessionId").GetString()!,
            new[] { "get-title", "get-url", "refresh", "get-title", "get-url" });
    }

    [E2EFact(Gates = "DESKTOP")]
    public void Desktop_SessionSurvivesSeparateInvocations_WithOneNewSession()
    {
        using var cli = new CliProcess("reattach-desktop");
        var start = cli.Run("desktop", "start-session", "--app", "calculator", "--session", "r", "--json");
        CliProcess.SkipIfUnreachable(start, "WinAppDriver");

        AssertSurvives(cli, "desktop", start.Ok.GetProperty("sessionId").GetString()!,
            new[] { "get-title", "get-title", "get-title", "get-title", "get-title" });
    }

    private static void AssertSurvives(CliProcess cli, string domain, string sessionId, string[] verbs)
    {
        Assert.Equal(N, verbs.Length);
        foreach (var verb in verbs)
        {
            _ = cli.Run(domain, verb, "--session", "r", "--json").Ok;
            Assert.Equal(sessionId, cli.Store(domain).TryGet("r")?.SessionId);
        }

        _ = cli.Run(domain, "close-session", "--session", "r", "--json").Ok;

        IReadOnlyList<JsonElement> trace = cli.Trace();
        Assert.Equal(N + 2, trace.Count);
        Assert.Single(trace, l => l.GetProperty("verb").GetString() == "start-session");
        Assert.All(trace, l => Assert.Equal(0, l.GetProperty("exitCode").GetInt32()));
        Assert.All(trace, l => Assert.Equal(domain, l.GetProperty("domain").GetString()));
    }
}
