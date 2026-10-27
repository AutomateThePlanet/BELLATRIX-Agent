using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

public class ScaffoldEmissionTests
{
    [Fact]
    public void Scaffold_EmitsAllArtifactsToContractLocations()
    {
        using var repo = new TempRepo();

        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        // R2 fragment
        Assert.True(repo.Exists(".bellatrix/bellatrix.instructions.md"));
        Assert.Equal(".bellatrix/bellatrix.instructions.md", result.registered);
        Assert.True(result.mandatesDelegation);

        // R4 skills — 12 each for desktop + mobile + web
        var skillDir = repo.Path(".claude/skills");
        var skillFiles = Directory.GetFiles(skillDir, "SKILL.md", SearchOption.AllDirectories);
        Assert.Equal(36, skillFiles.Length);

        // R5 agents — generator (renamed, prefixed) + locator specialist
        Assert.True(repo.Exists(".claude/agents/bellatrix-desktop-system-tests-generator.md"));
        Assert.True(repo.Exists(".claude/agents/bellatrix-desktop-locator-specialist.md"));
    }

    [Fact]
    public void Scaffold_AgentFrontmatterUsesPrefixedKebabName()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        var agent = repo.Read(".claude/agents/bellatrix-desktop-system-tests-generator.md");
        Assert.Contains("name: bellatrix-desktop-system-tests-generator", agent);
        Assert.DoesNotContain("name: BELLATRIX Desktop System Tests Generator", agent);
    }

    [Fact]
    public void Scaffold_NoEmittedFileContainsDesktopUnderscoreTokens()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        foreach (var file in Directory.GetFiles(repo.Path(".claude"), "*.md", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(repo.Path(".bellatrix"), "*.md", SearchOption.AllDirectories)))
        {
            Assert.DoesNotContain("desktop_", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Scaffold_DoesNotWriteLegacyPerTargetFlatFiles()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.False(Directory.Exists(repo.Path(".bellatrix/claude")));
    }

    [Fact]
    public void Scaffold_WritesManifestForEveryEmittedFile()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.True(repo.Exists(".bellatrix/manifest.claude.json"));
        // 36 skills + 4 agents + 1 fragment all emitted on a fresh run.
        Assert.Equal(43, result.emitted.Length);
        Assert.Empty(result.skipped);
    }
}
