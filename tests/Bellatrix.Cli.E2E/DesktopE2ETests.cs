namespace Bellatrix.Cli.E2E;

/// <summary>Desktop over WinAppDriver/NovaWindows on 4723: Windows Calculator, every step a separate process.</summary>
public class DesktopE2ETests
{
    [E2EFact(Gates = "DESKTOP")]
    public void Calculator_ListApps_Start_Snapshot_Find_Click_GetText_Close()
    {
        using var cli = new CliProcess("desktop-calc");

        var apps = cli.Run("desktop", "list-apps", "--json").Ok;
        Assert.Contains("calculator", apps.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var start = cli.Run("desktop", "start-session", "--app", "calculator", "--session", "calc", "--json");
        CliProcess.SkipIfUnreachable(start, "WinAppDriver");
        var sessionId = start.Ok.GetProperty("sessionId").GetString()!;
        Assert.False(string.IsNullOrEmpty(sessionId));

        var snap = cli.Run("desktop", "snapshot", "--session", "calc", "--json").Ok;
        Assert.True(snap.GetProperty("elementCount").GetInt32() > 0);
        var path = snap.GetProperty("path").GetString()!;
        Assert.True(File.Exists(path), $"snapshot file missing: {path}");

        var found = cli.Run("desktop", "find", "--session", "calc", "--snapshot", path, "--automation-id", "num1Button", "--json").Ok;
        Assert.Equal(1, found.GetProperty("count").GetInt32());

        for (var i = 0; i < 3; i++)
        {
            _ = cli.Run("desktop", "click", "--session", "calc", "--locator", "//*[@AutomationId='num1Button']", "--json").Ok;
        }

        var text = cli.Run("desktop", "get-text", "--session", "calc", "--locator", "//*[@AutomationId='CalculatorResults']", "--json").Ok;
        Assert.Contains("111", text.GetProperty("text").GetString());

        _ = cli.Run("desktop", "close-session", "--session", "calc", "--json").Ok;
        var after = cli.Run("desktop", "list-sessions", "--json").Ok;
        Assert.Null(cli.Store("desktop").TryGet("calc"));
        Assert.DoesNotContain(sessionId, after.GetRawText());
    }
}
