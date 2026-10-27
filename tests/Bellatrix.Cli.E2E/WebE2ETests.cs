using System.Diagnostics;
using Bellatrix.Execution.Web;

namespace Bellatrix.Cli.E2E;

/// <summary>Web over Selenium: the local detached driver (no server) and a remote Selenium server.</summary>
public class WebE2ETests
{
    private const string Site = "https://demos.bellatrix.solutions/";

    [E2EFact(Gates = "WEB")]
    public void ChromeLocal_FullFlow_DriverProcessGoneAfterClose()
    {
        using var cli = new CliProcess("web-local");

        var start = cli.Run("web", "start-session", "--browser", "chrome", "--session", "w1", "--json");
        CliProcess.SkipIfUnreachable(start, "chromedriver");
        Assert.Equal("local", start.Ok.GetProperty("driverServer").GetString());

        var pid = WebDriverSessionManager.ReadDriverPid(cli.Store("web").TryGet("w1")?.Caps);
        Assert.NotNull(pid);

        RunFlow(cli, "w1");

        var close = cli.Run("web", "close-session", "--session", "w1", "--json").Ok;
        Assert.True(close.GetProperty("driverStopped").GetBoolean());
        Assert.True(ProcessGone(pid!.Value), $"driver process {pid} still running after close-session");
    }

    [E2EFact(Gates = "WEB_REMOTE")]
    public void ChromeRemote_FullFlow_ServerKeepsRunning()
    {
        using var cli = new CliProcess("web-remote");
        var url = Environment.GetEnvironmentVariable("BELLATRIX_E2E_SELENIUM_URL") is { Length: > 0 } u ? u : "http://localhost:4444";

        var start = cli.Run("web", "start-session", "--browser", "chrome", "--remote-url", url, "--session", "w3", "--json");
        CliProcess.SkipIfUnreachable(start, $"Selenium server {url}");
        Assert.Equal("remote", start.Ok.GetProperty("driverServer").GetString());

        RunFlow(cli, "w3");

        var close = cli.Run("web", "close-session", "--session", "w3", "--json").Ok;
        Assert.False(close.GetProperty("driverStopped").GetBoolean());
    }

    private static void RunFlow(CliProcess cli, string session)
    {
        _ = cli.Run("web", "navigate", "--session", session, "--url", Site, "--json").Ok;

        var snap = cli.Run("web", "snapshot", "--session", session, "--json").Ok;
        Assert.True(snap.GetProperty("elementCount").GetInt32() > 0);
        var path = snap.GetProperty("path").GetString()!;

        var found = cli.Run("web", "find", "--session", session, "--snapshot", path, "--text", "Falcon 9", "--json").Ok;
        Assert.True(found.GetProperty("count").GetInt32() >= 1);

        _ = cli.Run("web", "click", "--session", session, "--locator", "//h2[contains(.,'Falcon 9')]", "--json").Ok;

        var title = cli.Run("web", "get-title", "--session", session, "--json").Ok;
        Assert.False(string.IsNullOrWhiteSpace(title.GetProperty("title").GetString()));
    }

    internal static bool ProcessGone(int pid)
    {
        for (var i = 0; i < 20; i++)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.HasExited) return true;
            }
            catch (ArgumentException)
            {
                return true; // no such process
            }

            Thread.Sleep(250);
        }

        return false;
    }
}
