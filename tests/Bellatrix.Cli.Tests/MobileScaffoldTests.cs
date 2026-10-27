using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Mobile scaffolding tests (M4): the <c>--domains</c> selector filters which templates emit, the mobile
/// locator-specialist + skills ship, and mobile has no MCP server to register.
/// </summary>
public class MobileScaffoldTests
{
    private const string MobileSkill = ".claude/skills/bellatrix-mobile-locators-and-waits/SKILL.md";
    private const string MobileAgent = ".claude/agents/bellatrix-mobile-locator-specialist.md";
    private const string DesktopSkill = ".claude/skills/bellatrix-desktop-components/SKILL.md";

    [Fact]
    public void Scaffold_AllDomains_EmitsBothDesktopAndMobile()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.True(repo.Exists(DesktopSkill));
        Assert.True(repo.Exists(MobileSkill));
        Assert.True(repo.Exists(MobileAgent));
    }

    [Fact]
    public void Scaffold_MobileOnly_EmitsMobile_SkipsDesktop_AndRegistersNoMcp()
    {
        using var repo = new TempRepo();
        var domains = ScaffoldCommand.ParseDomains("mobile");
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, domains: domains);

        Assert.True(repo.Exists(MobileSkill));
        Assert.True(repo.Exists(MobileAgent));
        Assert.False(repo.Exists(DesktopSkill));
        Assert.Empty(result.mcpMerged); // mobile has no MCP server
    }

    [Fact]
    public void Scaffold_DesktopOnly_EmitsDesktop_AndRegistersNoMcp()
    {
        using var repo = new TempRepo();
        var domains = ScaffoldCommand.ParseDomains("desktop");
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, domains: domains);

        Assert.True(repo.Exists(DesktopSkill));
        Assert.False(repo.Exists(MobileSkill));
        // No MCP servers anywhere: desktop no longer merges the MCP shim key, and no config file is created.
        Assert.Empty(result.mcpMerged);
        Assert.Empty(result.mcpRemoved);
        Assert.False(repo.Exists(".mcp.json"));
    }

    [Fact]
    public void Scaffold_RemovesStaleOwnedMcpKeys_PreservesForeignKeys()
    {
        using var repo = new TempRepo();
        repo.Write(".mcp.json",
            "{ \"servers\": { \"bellatrix-desktop-mcp\": { \"command\": \"bellatrix-desktop-mcp\", \"args\": [\"--mcp\"] }, " +
            "\"spectra\": { \"command\": \"spectra-mcp\" } } }");

        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Equal(new[] { ".mcp.json" }, result.mcpRemoved);
        var mcp = repo.Read(".mcp.json");
        Assert.DoesNotContain("bellatrix-desktop-mcp", mcp);
        Assert.Contains("spectra", mcp);
    }

    [Fact]
    public void ParseDomains_Default_IsAllKnown()
    {
        var all = ScaffoldCommand.ParseDomains(null);
        Assert.Contains("desktop", all);
        Assert.Contains("web", all);
        Assert.Contains("mobile", all);
    }

    [Fact]
    public void ParseDomains_Unknown_Throws()
        => Assert.Throws<CliException>(() => ScaffoldCommand.ParseDomains("banana"));

    [Fact]
    public void DomainOf_DerivesFromSlug()
    {
        Assert.Equal("desktop", ScaffoldCommand.DomainOf("bellatrix-desktop-components"));
        Assert.Equal("mobile", ScaffoldCommand.DomainOf("bellatrix-mobile-locators-and-waits"));
        Assert.Null(ScaffoldCommand.DomainOf("bellatrix-instructions"));
    }
}
