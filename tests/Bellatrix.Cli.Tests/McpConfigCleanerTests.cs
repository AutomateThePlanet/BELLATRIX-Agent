using System.Text.Json.Nodes;
using Bellatrix.Cli.Commands;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

public class McpConfigCleanerTests
{
    private static readonly string[] Owned = ScaffoldCommand.OwnedMcpKeys;

    [Fact]
    public void RemoveOwnedKeys_FromServers_PreservesForeignKeys()
    {
        using var repo = new TempRepo();
        repo.Write(".mcp.json",
            "{ \"servers\": { \"spectra\": { \"command\": \"spectra-mcp\", \"args\": [\".\"] }, " +
            "\"bellatrix-desktop-mcp\": { \"command\": \"bellatrix-desktop-mcp\", \"args\": [\"--mcp\"] }, " +
            "\"testimize\": { \"command\": \"testimize-mcp\" } }, \"inputs\": [] }");
        var path = repo.Path(".mcp.json");

        Assert.True(McpConfigCleaner.RemoveOwnedKeys(path, Owned));

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var servers = root["servers"]!.AsObject();
        Assert.False(servers.ContainsKey("bellatrix-desktop-mcp"));
        Assert.True(servers.ContainsKey("spectra"));
        Assert.True(servers.ContainsKey("testimize"));
        Assert.NotNull(root["inputs"]); // non-servers top-level key preserved
    }

    [Fact]
    public void RemoveOwnedKeys_FromMcpServers_RemovesAllOwnedKeys()
    {
        using var repo = new TempRepo();
        repo.Write(".claude/settings.json",
            "{ \"permissions\": { \"allow\": [] }, \"mcpServers\": { " +
            "\"bellatrix-web-mcp\": { \"command\": \"bellatrix-web-mcp\" }, " +
            "\"bellatrix-mobile-mcp\": { \"command\": \"bellatrix-mobile-mcp\" }, " +
            "\"spectra\": { \"command\": \"spectra-mcp\" } } }");
        var path = repo.Path(".claude/settings.json");

        Assert.True(McpConfigCleaner.RemoveOwnedKeys(path, Owned));

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var servers = root["mcpServers"]!.AsObject();
        Assert.Single(servers);
        Assert.True(servers.ContainsKey("spectra"));
        Assert.NotNull(root["permissions"]);
    }

    [Fact]
    public void RemoveOwnedKeys_LeavesEmptyContainer_AndNeverDeletesFile()
    {
        using var repo = new TempRepo();
        repo.Write(".mcp.json", "{ \"servers\": { \"bellatrix-desktop-mcp\": { \"command\": \"bellatrix-desktop-mcp\" } } }");
        var path = repo.Path(".mcp.json");

        Assert.True(McpConfigCleaner.RemoveOwnedKeys(path, Owned));

        Assert.True(File.Exists(path));
        Assert.Empty(JsonNode.Parse(File.ReadAllText(path))!["servers"]!.AsObject());
    }

    [Fact]
    public void RemoveOwnedKeys_AbsentFile_StaysAbsent()
    {
        using var repo = new TempRepo();
        var path = repo.Path(".mcp.json");

        Assert.False(McpConfigCleaner.RemoveOwnedKeys(path, Owned));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RemoveOwnedKeys_NothingOwned_LeavesBytesIdentical()
    {
        using var repo = new TempRepo();
        const string original = "{ \"servers\": { \"spectra\": { \"command\": \"spectra-mcp\" } } }";
        repo.Write(".mcp.json", original);
        var path = repo.Path(".mcp.json");

        Assert.False(McpConfigCleaner.RemoveOwnedKeys(path, Owned));
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void RemoveOwnedKeys_ParsesJsonWithCommentsAndTrailingCommas()
    {
        using var repo = new TempRepo();
        repo.Write(".vscode/mcp.json",
            "// VS Code MCP config\n{ \"servers\": { \"bellatrix-desktop-mcp\": { \"command\": \"x\" }, " +
            "\"spectra\": { \"command\": \"spectra-mcp\" }, }, }");
        var path = repo.Path(".vscode/mcp.json");

        Assert.True(McpConfigCleaner.RemoveOwnedKeys(path, Owned));

        var servers = JsonNode.Parse(File.ReadAllText(path))!["servers"]!.AsObject();
        Assert.Single(servers);
        Assert.True(servers.ContainsKey("spectra"));
    }
}
