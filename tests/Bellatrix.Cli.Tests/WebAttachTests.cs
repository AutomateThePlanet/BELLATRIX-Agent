using Bellatrix.Execution.Web;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// A web session is reattachable across separate CLI invocations, the detached local
/// driver's PID is cached and only ever used to stop a real browser-driver process, and the unified
/// <c>--locator</c> / <c>--strategy</c> flags resolve per domain while the original flags keep working.
/// </summary>
public class WebAttachTests
{
    [Fact]
    public void Web_SeparateInvocations_ReattachToOneSession_ById()
    {
        using var server = new FakeSeleniumServer();
        using var repo = new TempRepo();

        var start = Run(repo.Root, "web", "start-session", "--browser", "chrome", "--remote-url", server.BaseUrl, "--session", "w1");
        Assert.Equal(0, start.Exit);
        Assert.Contains(FakeSeleniumServer.SessionId, start.Output);
        Assert.Contains("\"driverServer\":\"remote\"", start.Output);

        Assert.Equal(0, Run(repo.Root, "web", "navigate", "--session", "w1", "--url", "https://example.test/").Exit);
        var title = Run(repo.Root, "web", "get-title", "--session", "w1");
        Assert.Equal(0, title.Exit);
        Assert.Contains("Fake Shop", title.Output);
        Assert.Equal(0, Run(repo.Root, "web", "click", "--session", "w1", "--locator", "//button[normalize-space()='Buy']").Exit);
        Assert.Equal(0, Run(repo.Root, "web", "click", "--session", "w1", "--selector", "#buy").Exit); // legacy flag still works

        var close = Run(repo.Root, "web", "close-session", "--session", "w1");
        Assert.Equal(0, close.Exit);
        Assert.Contains("\"driverStopped\":false", close.Output); // --remote-url server is the user's: never stopped

        Assert.Equal(1, server.NewSessionRequestCount); // every later invocation re-attached, none re-created
        var requests = server.Requests.ToArray();
        Assert.Contains(requests, r => r.StartsWith($"POST /session/{FakeSeleniumServer.SessionId}/url", StringComparison.Ordinal));
        Assert.Contains(requests, r => r.StartsWith($"GET /session/{FakeSeleniumServer.SessionId}/title", StringComparison.Ordinal));
        Assert.Contains(requests, r => r.Contains("/element", StringComparison.Ordinal) && r.Contains("\"using\":\"xpath\"", StringComparison.Ordinal));
        Assert.Contains(requests, r => r.Contains("/element", StringComparison.Ordinal) && r.Contains("\"using\":\"css selector\"", StringComparison.Ordinal));
        Assert.Contains(requests, r => r == $"DELETE /session/{FakeSeleniumServer.SessionId}");

        var after = Run(repo.Root, "web", "get-title", "--session", "w1");
        Assert.NotEqual(0, after.Exit);
        Assert.Contains("session_not_found", after.Output);
    }

    [Fact]
    public void CapsJson_CarriesDriverPid_OnlyForLocalDriver()
    {
        Assert.Null(WebDriverSessionManager.ReadDriverPid(WebDriverSessionManager.BuildCapsJson("chrome")));
        Assert.Equal(4242, WebDriverSessionManager.ReadDriverPid(WebDriverSessionManager.BuildCapsJson("edge", 4242)));
        Assert.Contains("\"browserName\":\"MicrosoftEdge\"", WebDriverSessionManager.BuildCapsJson("edge", 4242));
        Assert.Null(WebDriverSessionManager.ReadDriverPid("not json"));
        Assert.Null(WebDriverSessionManager.ReadDriverPid(null));
    }

    [Fact]
    public void StopDriverServer_NeverKillsANonDriverProcess()
    {
        Assert.False(WebDriverSessionManager.StopDriverServer(null));
        Assert.False(WebDriverSessionManager.StopDriverServer(Environment.ProcessId)); // this test host: name check refuses
        Assert.False(WebDriverSessionManager.StopDriverServer(int.MaxValue));          // no such process
    }

    [Theory]
    [InlineData("//button", "xpath")]
    [InlineData("(//a)[2]", "xpath")]
    [InlineData("#buy", "css")]
    [InlineData("[data-testid='x']", "css")]
    public void Web_Strategy_AutoDetectsXPath_WhenNotGiven(string locator, string expected)
    {
        var ctx = new WebCommandContext(new Dictionary<string, string> { ["locator"] = locator });
        Assert.Equal(expected, ctx.Strategy);
        Assert.Equal(locator, ctx.Locator());
    }

    [Fact]
    public void Web_ExplicitStrategy_Wins()
    {
        var ctx = new WebCommandContext(new Dictionary<string, string> { ["locator"] = "submit", ["strategy"] = "id" });
        Assert.Equal("id", ctx.Strategy);
    }

    [Theory]
    [InlineData("Views", null, "accessibilityid")]
    [InlineData("//android.widget.TextView", null, "xpath")]
    [InlineData("com.app:id/login", "id", "id")]
    public void Mobile_UnifiedLocator_ResolvesStrategy(string locator, string? strategy, string expected)
    {
        var options = new Dictionary<string, string> { ["locator"] = locator };
        if (strategy is not null)
        {
            options["strategy"] = strategy;
        }

        var ctx = new MobileCommandContext(options);
        Assert.Equal(expected, ctx.LocatorType());
        Assert.Equal(locator, ctx.LocatorValue());
    }

    [Fact]
    public void Mobile_LegacyFlags_StillWork()
    {
        var ctx = new MobileCommandContext(new Dictionary<string, string>
        {
            ["locator-type"] = "classname", ["locator-value"] = "android.widget.Button",
        });
        Assert.Equal("classname", ctx.LocatorType());
        Assert.Equal("android.widget.Button", ctx.LocatorValue());
    }

    [Fact]
    public void Desktop_UnifiedLocator_IsXPathOnly()
    {
        Assert.Equal("//Button", new CommandContext(new Dictionary<string, string> { ["locator"] = "//Button" }).Locator());
        Assert.Equal("//Edit", new CommandContext(new Dictionary<string, string> { ["xpath"] = "//Edit" }).Locator());
        var ex = Assert.Throws<CliException>(() =>
            new CommandContext(new Dictionary<string, string> { ["locator"] = "x", ["strategy"] = "css" }).Locator());
        Assert.Equal("usage", ex.Code);
    }

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
