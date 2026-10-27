using System.Text.Json;
using Bellatrix.Cli;
using Bellatrix.Execution.Snapshots;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// End-to-end tests through the public <see cref="CliRunner"/> for verbs that need no live driver:
/// scaffold, query-snapshot, and list-apps (env-configured).
/// </summary>
public class CliIntegrationTests
{
    private static string Capture(out int exitCode, params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            exitCode = CliRunner.Run(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    [Fact]
    public void Scaffold_Claude_WritesDelegationMandatingArtifacts()
    {
        var outDir = Path.Combine(Path.GetTempPath(), $"bellatrix-scaffold-{Guid.NewGuid():N}");
        try
        {
            var output = Capture(out var exit, "desktop", "scaffold", "--target", "claude", "--out", outDir);
            Assert.Equal(0, exit);

            // Contract locations (R2/R5): the instructions fragment + the isolated locator specialist.
            var fragment = Path.Combine(outDir, ".bellatrix", "bellatrix.instructions.md");
            var specialist = Path.Combine(outDir, ".claude", "agents", "bellatrix-desktop-locator-specialist.md");
            Assert.True(File.Exists(fragment));
            Assert.True(File.Exists(specialist));

            // The scaffolded guidance must MANDATE delegation, not merely suggest it.
            Assert.Contains("MANDATORY", File.ReadAllText(fragment));
            Assert.Contains("snapshotPath", File.ReadAllText(specialist));
            Assert.Contains("\"mandatesDelegation\":true", output);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Scaffold_UnknownTarget_ReturnsError()
    {
        Capture(out var exit, "desktop", "scaffold", "--target", "emacs");
        Assert.Equal(2, exit); // agent-selection usage errors exit 2
    }

    [Fact]
    public void QuerySnapshot_OverPersistedArtifact_ReturnsFilteredNodes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bellatrix-q-{Guid.NewGuid():N}");
        try
        {
            var store = new SnapshotStore(dir);
            var reference = store.Save("s1", new List<Dictionary<string, object>>
            {
                new() { ["Tag"] = "Button", ["ControlType"] = "Button", ["Name"] = "OK", ["AutomationId"] = "ok" },
                new() { ["Tag"] = "Text", ["ControlType"] = "Text", ["Name"] = "hello" },
            });

            var output = Capture(out var exit, "desktop", "find", "--snapshot", reference.Path, "--interactive-only");
            Assert.Equal(0, exit);

            using var doc = JsonDocument.Parse(output);
            Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("ok", doc.RootElement.GetProperty("nodes")[0].GetProperty("AutomationId").GetString());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ListApps_FromEnvironment_ReturnsConfiguredApps()
    {
        Environment.SetEnvironmentVariable("McpSettings__Apps__0__Name", "notepad");
        Environment.SetEnvironmentVariable("McpSettings__Apps__0__Path", "notepad.exe");
        Environment.SetEnvironmentVariable("McpSettings__Apps__0__IsDefault", "true");
        try
        {
            var output = Capture(out var exit, "desktop", "list-apps");
            Assert.Equal(0, exit);

            using var doc = JsonDocument.Parse(output);
            Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("notepad", doc.RootElement.GetProperty("apps")[0].GetProperty("name").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("McpSettings__Apps__0__Name", null);
            Environment.SetEnvironmentVariable("McpSettings__Apps__0__Path", null);
            Environment.SetEnvironmentVariable("McpSettings__Apps__0__IsDefault", null);
        }
    }
}
