namespace Bellatrix.Cli.E2E;

/// <summary>Mobile over Appium 2 (uiautomator2) on 4723: an Android emulator with ApiDemos.</summary>
public class MobileE2ETests
{
    [E2EFact(Gates = "MOBILE")]
    public void Android_ApiDemos_Start_Snapshot_Find_Click_GetText_Close()
    {
        using var cli = new CliProcess("mobile-android");
        var apk = Environment.GetEnvironmentVariable("BELLATRIX_E2E_APK") is { Length: > 0 } a ? a : "ApiDemos-debug.apk";
        Skip.IfNot(File.Exists(apk), $"APK not found: {apk} (set BELLATRIX_E2E_APK)");

        var start = cli.Run("mobile", "start-session", "--platform", "android", "--app", apk, "--session", "m1", "--json");
        CliProcess.SkipIfUnreachable(start, "Appium");
        Assert.False(string.IsNullOrEmpty(start.Ok.GetProperty("sessionId").GetString()));

        var snap = cli.Run("mobile", "snapshot", "--session", "m1", "--json").Ok;
        Assert.True(snap.GetProperty("elementCount").GetInt32() > 0);
        var path = snap.GetProperty("path").GetString()!;

        var found = cli.Run("mobile", "find", "--session", "m1", "--snapshot", path, "--text", "Views", "--json").Ok;
        Assert.True(found.GetProperty("count").GetInt32() >= 1);

        _ = cli.Run("mobile", "click", "--session", "m1", "--locator", "Views", "--json").Ok;

        var text = cli.Run("mobile", "get-text", "--session", "m1", "--locator", "//android.widget.TextView[@text='Animation']", "--json").Ok;
        Assert.Equal("Animation", text.GetProperty("text").GetString());

        _ = cli.Run("mobile", "close-session", "--session", "m1", "--json").Ok;
        Assert.Null(cli.Store("mobile").TryGet("m1"));
    }
}
