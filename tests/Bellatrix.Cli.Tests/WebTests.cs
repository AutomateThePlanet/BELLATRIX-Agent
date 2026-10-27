using Bellatrix.Cli;
using Bellatrix.Cli.Commands;
using Bellatrix.Execution.Snapshots;
using Bellatrix.Execution.Web;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Web domain tests: dispatcher grammar (W2), the pure DOM-snapshot parser (W1), and the web narrow
/// query with its own data-testid → id → role → text ladder (W4). No live browser/Selenium server.
/// </summary>
public class WebTests
{
    // ===== W2: dispatch grammar =====

    [Fact]
    public void Run_WebUnknownVerb_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "web", "frobnicate" }));

    [Fact]
    public void Run_WebMissingVerb_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "web" }));

    [Fact]
    public void Run_WebClick_MissingSelector_ReturnsUsageError()
    {
        // No session named 'default' and no --selector → fails before any Selenium access.
        Assert.Equal(1, CliRunner.Run(new[] { "web", "click", "--session", "nope-" + Guid.NewGuid().ToString("N") }));
    }

    [Fact]
    public void KnownVerbs_CoverTheWebSurface()
    {
        foreach (var verb in new[]
                 {
                     "start-session", "close-session", "list-sessions", "navigate", "back", "forward",
                     "refresh", "get-title", "get-url", "get-page-source", "snapshot", "find", "click",
                     "type-text", "clear", "select", "get-text", "get-attribute", "is-visible",
                     "is-enabled", "wait-for-element", "wait-for-page-ready", "execute-js", "take-screenshot",
                 })
        {
            Assert.Contains(verb, WebCommands.KnownVerbs);
        }
    }

    [Fact]
    public void Web_HasNo_XpathGeneratePrompt_Verb()
        => Assert.DoesNotContain("xpath-generate-prompt", WebCommands.KnownVerbs);

    // ===== W1: DOM snapshot parse =====

    private const string SampleJson = """
    [
      {"tag":"button","id":"loginBtn","text":"Log in","role":null,"ariaLabel":null,"dataTestid":"login","name":null,"class":"btn primary"},
      {"tag":"input","id":"","text":"","type":"text","name":"username","ariaLabel":"Username","dataTestid":null,"role":"textbox"},
      {"tag":"div","id":"","text":"","name":null,"role":null,"ariaLabel":null,"dataTestid":null,"class":"layout-row"}
    ]
    """;

    [Fact]
    public void ParseSnapshotJson_KeepsIdentityNodes_DropsAnonymousLayout()
    {
        var nodes = WebDriverService.ParseSnapshotJson(SampleJson);

        Assert.Equal(2, nodes.Count); // button + input kept; anonymous div dropped
        Assert.Equal("login", nodes[0]["DataTestid"].ToString());
        Assert.Equal("Username", nodes[1]["AriaLabel"].ToString());
        Assert.False(nodes[1].ContainsKey("Id")); // empty id not added
    }

    [Fact]
    public void ParseSnapshotJson_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(WebDriverService.ParseSnapshotJson("[]"));
        Assert.Empty(WebDriverService.ParseSnapshotJson(""));
    }

    // ===== W4: web narrow query (own ladder) =====

    private static List<Dictionary<string, object>> Nodes() => WebDriverService.ParseSnapshotJson(SampleJson);

    [Fact]
    public void Query_ByDataTestid_Matches()
    {
        var results = WebSnapshotQuery.Run(Nodes(), WebSnapshotQueryMode.DataTestid, "login");
        Assert.Single(results);
        Assert.Equal("loginBtn", results[0]["Id"].ToString());
    }

    [Fact]
    public void Query_ByRole_MatchesExact()
    {
        var results = WebSnapshotQuery.Run(Nodes(), WebSnapshotQueryMode.Role, "textbox");
        Assert.Single(results);
        Assert.Equal("username", results[0]["Name"].ToString());
    }

    [Fact]
    public void Query_InteractiveOnly_KeepsButtonAndInput()
    {
        var results = WebSnapshotQuery.Run(Nodes(), WebSnapshotQueryMode.Interactive, null);
        Assert.Equal(2, results.Count); // button + input (the div was already dropped at parse)
    }

    [Fact]
    public void BuildCapsJson_CarriesBrowserName()
    {
        Assert.Contains("chrome", WebDriverSessionManager.BuildCapsJson("chrome"));
        Assert.Contains("MicrosoftEdge", WebDriverSessionManager.BuildCapsJson("edge"));
        Assert.Contains("firefox", WebDriverSessionManager.BuildCapsJson("firefox"));
    }

    // ===== scaffolding: --domains web emits web templates, no MCP server =====

    [Fact]
    public void Scaffold_WebOnly_EmitsWeb_SkipsDesktop_AndRegistersNoMcp()
    {
        using var repo = new TempRepo();
        var domains = ScaffoldCommand.ParseDomains("web");
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, domains: domains);

        Assert.True(repo.Exists(".claude/skills/bellatrix-web-locators-and-waits/SKILL.md"));
        Assert.True(repo.Exists(".claude/agents/bellatrix-web-locator-specialist.md"));
        Assert.False(repo.Exists(".claude/skills/bellatrix-desktop-components/SKILL.md"));
        Assert.Empty(result.mcpMerged); // web has no MCP server
    }
}
