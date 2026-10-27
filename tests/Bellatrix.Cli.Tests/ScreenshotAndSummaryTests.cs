using System.Runtime.CompilerServices;
using System.Text.Json;
using Bellatrix.Execution.Snapshots;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>take-screenshot</c> saves a file and returns <c>{path, width, height}</c> unless <c>--inline</c> asks
/// for base64, and the web snapshot summary counts interactive nodes with the web rules, not the desktop UIA list.
/// </summary>
public class ScreenshotAndSummaryTests
{
    [Fact]
    public void Web_TakeScreenshot_SavesUnderBellatrixScreenshots_AndReturnsSize()
    {
        using var server = new FakeSeleniumServer();
        using var repo = new TempRepo();
        Assert.Equal(0, Run(repo.Root, "web", "start-session", "--browser", "chrome", "--remote-url", server.BaseUrl, "--session", "w1").Exit);

        var shot = Run(repo.Root, "web", "take-screenshot", "--session", "w1");
        Assert.Equal(0, shot.Exit);
        using var doc = JsonDocument.Parse(shot.Output);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.StartsWith(Path.Combine(repo.Root, ".bellatrix", "screenshots"), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", path);
        Assert.True(File.Exists(path));
        Assert.Equal(1280, doc.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(720, doc.RootElement.GetProperty("height").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("screenshot", out _)); // no base64 by default

        Run(repo.Root, "web", "close-session", "--session", "w1");
    }

    [Fact]
    public void Web_TakeScreenshot_OutAndInline()
    {
        using var server = new FakeSeleniumServer();
        using var repo = new TempRepo();
        Assert.Equal(0, Run(repo.Root, "web", "start-session", "--browser", "chrome", "--remote-url", server.BaseUrl, "--session", "w1").Exit);

        var target = Path.Combine(repo.Root, "shots", "step1.png");
        var withOut = Run(repo.Root, "web", "take-screenshot", "--session", "w1", "--out", target);
        Assert.Equal(0, withOut.Exit);
        using (var doc = JsonDocument.Parse(withOut.Output))
        {
            Assert.Equal(target, doc.RootElement.GetProperty("path").GetString());
            Assert.Equal(1280, doc.RootElement.GetProperty("width").GetInt32());
        }

        Assert.True(File.Exists(target)); // the missing parent folder was created

        var inline = Run(repo.Root, "web", "take-screenshot", "--session", "w1", "--inline");
        Assert.Equal(0, inline.Exit);
        using (var doc = JsonDocument.Parse(inline.Output))
        {
            var bytes = Convert.FromBase64String(doc.RootElement.GetProperty("screenshot").GetString()!);
            Assert.Equal((1280, 720), ScreenshotStore.PngSize(bytes));
            Assert.False(doc.RootElement.TryGetProperty("path", out _));
        }

        Assert.False(Directory.Exists(Path.Combine(repo.Root, ".bellatrix", "screenshots"))); // nothing saved by default dir

        Run(repo.Root, "web", "close-session", "--session", "w1");
    }

    [Fact]
    public void PngSize_ReadsIhdr_AndRejectsNonPng()
    {
        Assert.Equal((3, 2), ScreenshotStore.PngSize(FakeSeleniumServer.PngHeader(3, 2)));
        Assert.Equal((0, 0), ScreenshotStore.PngSize(new byte[] { 1, 2, 3 }));
        Assert.Equal((0, 0), ScreenshotStore.PngSize(new byte[40]));
    }

    [Fact]
    public void WebSnapshotSummary_CountsWebInteractiveNodes_SameAsFindInteractiveOnly()
    {
        var nodes = new SnapshotStore(Path.GetTempPath()).Load(Path.Combine(FixtureDir(), "web-demo-shop.json"));
        var dir = Path.Combine(Path.GetTempPath(), $"bellatrix-summary-{Guid.NewGuid():N}");
        try
        {
            var reference = new SnapshotStore(dir).Save("w1", nodes, WebSnapshotQuery.IsInteractive);

            // 29 links with an href, 2 selects, 1 button, 1 search input. The desktop UIA list matched almost none of these.
            Assert.Equal(33, reference.Summary["interactive"]);
            Assert.True(new SnapshotStore(dir).Save("w1", nodes).Summary["interactive"] is int desktopCount && desktopCount < 33);

            var find = Run(Directory.GetCurrentDirectory(), "web", "find", "--snapshot", reference.Path, "--interactive-only");
            Assert.Equal(0, find.Exit);
            using var doc = JsonDocument.Parse(find.Output);
            Assert.Equal(33, doc.RootElement.GetProperty("count").GetInt32());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("a", "/cart", null, null, true)]
    [InlineData("a", null, null, null, false)]                // an anchor without href is not a link
    [InlineData("input", null, "text", null, true)]
    [InlineData("input", null, "hidden", null, false)]
    [InlineData("textarea", null, null, null, true)]
    [InlineData("option", null, null, null, false)]           // options are reached through their select
    [InlineData("div", null, null, "button", true)]
    [InlineData("li", null, null, "menuitem", true)]
    [InlineData("nav", null, null, "navigation", false)]
    public void WebIsInteractive_FollowsTheWebRules(string tag, string? href, string? type, string? role, bool expected)
    {
        var node = new Dictionary<string, object> { ["Tag"] = tag };
        if (href is not null) node["Href"] = href;
        if (type is not null) node["Type"] = type;
        if (role is not null) node["Role"] = role;
        Assert.Equal(expected, WebSnapshotQuery.IsInteractive(node));
    }

    private static string FixtureDir([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "fixtures", "snapshots"));

    private static (int Exit, string Output) Run(string cwd, params string[] args)
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var captured = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(cwd);
            Console.SetOut(captured);
            var exit = CliRunner.Run(args);
            return (exit, captured.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
