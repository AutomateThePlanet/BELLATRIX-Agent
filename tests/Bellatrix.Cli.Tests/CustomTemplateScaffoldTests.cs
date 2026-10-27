using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Bellatrix.Cli;
using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>bellatrix init --template-source &lt;dir&gt; [--prefix &lt;p&gt;]</c>: a caller-supplied template folder rendered
/// with the same contract as the built-in BELLATRIX templates (R1–R8, per-agent manifests), under the caller's own
/// namespace — fragment <c>.bellatrix/&lt;p&gt;.instructions.md</c>, manifests <c>.bellatrix/&lt;p&gt;-manifest.&lt;agent&gt;.json</c>,
/// skills/agents named <c>&lt;p&gt;-…</c>. Fixture: the neutral <c>tests/fixtures/custom-templates/</c> (prefix <c>acme</c>).
/// </summary>
public class CustomTemplateScaffoldTests
{
    private const string P = CustomTemplateFixture.Prefix;
    private const string Fragment = ".bellatrix/acme.instructions.md";
    private const string BellatrixFragment = ".bellatrix/bellatrix.instructions.md";
    private const string Skill = ".claude/skills/acme-desktop-components/SKILL.md";

    private static ScaffoldCommand.ScaffoldResult Init(TempRepo repo, CustomTemplateFixture source, bool regenerate = false, string agents = "claude")
        => ScaffoldCommand.Scaffold(repo.Root, agents, regenerate, profile: P, templateSource: source.Root);

    [Fact]
    public void BellatrixInit_EmitsOnlyBellatrixAssets()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Equal(BellatrixFragment, result.registered);
        Assert.Equal(43, result.emitted.Length); // 3 domains x (12 skills + 2 agents) + 1 fragment
        Assert.False(repo.Exists(Fragment));
        Assert.False(repo.Exists(".bellatrix/acme-manifest.claude.json"));
    }

    [Fact]
    public void TemplateSource_EmitsRenderedAssetsToContractLocations()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();

        var result = Init(repo, source);

        Assert.Equal(Fragment, result.registered);
        Assert.True(repo.Exists(Fragment));
        Assert.Equal(3, Directory.GetDirectories(repo.Path(".claude/skills"), "acme-*").Length);
        Assert.True(repo.Exists(".claude/agents/acme-desktop-system-tests-generator.md"));
        Assert.True(repo.Exists(".claude/agents/acme-desktop-locator-specialist.md"));
        Assert.True(repo.Exists(".bellatrix/acme-manifest.claude.json"));
        Assert.False(repo.Exists(".bellatrix/manifest.claude.json")); // BELLATRIX's own init never ran here
        Assert.Contains("@" + Fragment, repo.Read("CLAUDE.md"));
        Assert.Empty(result.mcpMerged);
        Assert.Empty(result.mcpRemoved); // a custom template folder leaves MCP configuration alone
        Assert.Equal("none", result.language);
        Assert.Equal(6, result.emitted.Length); // 3 skills + 2 agents + 1 fragment
        Assert.Empty(result.skipped);
    }

    [Fact]
    public void TemplateSource_OtherAgents_UseThePrefixForMarkerBlocksAndNativeDirs()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();

        Init(repo, source, agents: "codex,gemini");

        Assert.Contains("<!-- >>> acme >>> -->", repo.Read("AGENTS.md"));
        Assert.Contains("<!-- >>> acme >>> -->", repo.Read("GEMINI.md"));
        Assert.True(repo.Exists(".agents/skills/acme-desktop-components/SKILL.md"));
        Assert.True(repo.Exists(".bellatrix/acme-manifest.codex.json"));
        Assert.True(repo.Exists(".bellatrix/acme-manifest.gemini.json"));
    }

    [Fact]
    public void TemplateSource_PhasePAgents_UseThePrefixEverywhere()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();

        var result = Init(repo, source, agents: "antigravity,kiro,opencode");

        Assert.Equal(new[] { "antigravity", "kiro", "opencode" }, result.agents);
        Assert.True(repo.Exists(".agents/rules/acme.md"));
        Assert.True(repo.Exists(".agents/agents/acme-desktop-locator-specialist/agent.md"));
        Assert.True(repo.Exists(".kiro/steering/acme.md"));
        var kiroAgent = repo.Read(".kiro/agents/acme-desktop-locator-specialist.md");
        Assert.Contains("file://.kiro/steering/acme.md", kiroAgent);
        Assert.Contains("skill://.kiro/skills/acme-*/SKILL.md", kiroAgent);
        Assert.True(repo.Exists(".opencode/agents/acme-desktop-locator-specialist.md"));
        Assert.Contains("<!-- >>> acme >>> -->", repo.Read("AGENTS.md"));
        Assert.Contains("`.bellatrix/acme.instructions.md`", repo.Read("AGENTS.md"));
        foreach (var agent in result.agents)
        {
            Assert.True(repo.Exists($".bellatrix/acme-manifest.{agent}.json"), agent);
        }

        Assert.Empty(result.mcpRemoved);
    }

    [Fact]
    public void Prefix_IsInferred_OrExplicit_AndNamespacesEveryName()
    {
        using var source = new CustomTemplateFixture();
        Assert.Equal("acme", Bellatrix.Cli.Scaffolding.CustomTemplateSource.ResolvePrefix(source.Root, null));

        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", false, profile: "contoso", templateSource: source.Root);

        Assert.True(repo.Exists(".claude/skills/contoso-acme-desktop-components/SKILL.md"));
        Assert.True(repo.Exists(".claude/agents/contoso-acme-desktop-locator-specialist.md"));
        Assert.True(repo.Exists(".bellatrix/contoso.instructions.md"));
        Assert.True(repo.Exists(".bellatrix/contoso-manifest.claude.json"));
    }

    [Theory]
    [InlineData("bellatrix", "reserved")]
    [InlineData("Acme", "kebab-case")]
    [InlineData("acme_qa", "kebab-case")]
    public void Prefix_RejectsReservedAndInvalidNames(string prefix, string message)
    {
        using var source = new CustomTemplateFixture();

        var ex = Assert.Throws<CliException>(() => Bellatrix.Cli.Scaffolding.CustomTemplateSource.ResolvePrefix(source.Root, prefix));

        Assert.Equal("usage", ex.Code);
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Prefix_CannotBeInferred_FromMixedNames()
    {
        using var source = new CustomTemplateFixture();
        Directory.CreateDirectory(Path.Combine(source.Root, "skills", "other-skill"));
        File.WriteAllText(Path.Combine(source.Root, "skills", "other-skill", "SKILL.md"), "---\ndescription: x\n---\n\nx\n");

        var ex = Assert.Throws<CliException>(() => Bellatrix.Cli.Scaffolding.CustomTemplateSource.ResolvePrefix(source.Root, null));

        Assert.Contains("--prefix", ex.Message);
    }

    [Fact]
    public void TemplateSource_InvalidFolder_IsAClearError()
    {
        using var repo = new TempRepo(); // has neither skills/ nor agents/

        var ex = Assert.Throws<CliException>(() =>
            ScaffoldCommand.Scaffold(repo.Root, "claude", false, profile: P, templateSource: repo.Root));

        Assert.Equal("template_source_invalid", ex.Code);
    }

    [Fact]
    public void CleanRerun_IsIdempotent()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        Init(repo, source);
        var before = repo.Read(Skill);

        var second = Init(repo, source);

        Assert.Empty(second.emitted);
        Assert.Empty(second.skipped);
        Assert.Equal(before, repo.Read(Skill));
    }

    [Fact]
    public void SourceEdit_PropagatesToGeneratedFile()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        Init(repo, source);

        File.WriteAllText(Path.Combine(source.Root, "skills", "acme-desktop-components", "SKILL.md"),
            "---\ndescription: Reference skill acme-desktop-components.\n---\n\n# acme-desktop-components\n\nUPDATED body content.\n");
        var result = Init(repo, source);

        Assert.Contains(Skill, result.emitted);
        Assert.Contains("UPDATED body content", repo.Read(Skill));
    }

    [Fact]
    public void UserEditedFile_IsSkipped_UnlessRegenerate()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        Init(repo, source);
        repo.Write(Skill, repo.Read(Skill) + "\n# LOCAL EDIT\n");

        var rerun = Init(repo, source);
        Assert.Contains(Skill, rerun.skipped);
        Assert.Contains("# LOCAL EDIT", repo.Read(Skill));

        var regenerated = Init(repo, source, regenerate: true);
        Assert.DoesNotContain(Skill, regenerated.skipped);
        Assert.DoesNotContain("# LOCAL EDIT", repo.Read(Skill));
    }

    [Fact]
    public void BellatrixInit_DoesNotTouchCustomOwnedFiles_AndViceVersa()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        Init(repo, source);
        var customSkill = repo.Read(Skill);
        var customManifest = repo.Read(".bellatrix/acme-manifest.claude.json");

        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
        Assert.Equal(customSkill, repo.Read(Skill));
        Assert.Equal(customManifest, repo.Read(".bellatrix/acme-manifest.claude.json"));
        Assert.True(repo.Exists(".claude/skills/bellatrix-desktop-components/SKILL.md"));

        var bellatrixFragment = repo.Read(BellatrixFragment);
        var bellatrixManifest = repo.Read(".bellatrix/manifest.claude.json");
        Init(repo, source, regenerate: true);
        Assert.Equal(bellatrixFragment, repo.Read(BellatrixFragment));
        Assert.Equal(bellatrixManifest, repo.Read(".bellatrix/manifest.claude.json"));
    }

    [Fact]
    public void BothProfiles_ClaudeMdImportsCoexistExactlyOnce()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        for (var i = 0; i < 2; i++)
        {
            ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
            Init(repo, source);
        }

        var content = repo.Read("CLAUDE.md");
        Assert.Equal(1, content.Split("@" + BellatrixFragment).Length - 1);
        Assert.Equal(1, content.Split("@" + Fragment).Length - 1);
        Assert.Equal(1, content.Split("# Tool instructions").Length - 1);
    }

    [Fact]
    public void Agents_KeepSourceToolsRestriction_SkillsHaveNone()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        Init(repo, source);

        Assert.Contains("tools: [Read, Grep, Glob, Bash]", repo.Read(".claude/agents/acme-desktop-locator-specialist.md"));
        Assert.Contains("tools: [Read, Grep, Glob, Bash, Edit, Write]", repo.Read(".claude/agents/acme-desktop-system-tests-generator.md"));
        foreach (var dir in Directory.GetDirectories(repo.Path(".claude/skills"), "acme-*"))
        {
            Assert.DoesNotContain("tools:", File.ReadAllText(Path.Combine(dir, "SKILL.md")));
        }
    }

    [Fact]
    public void BellatrixInit_AgentsStillHaveNoToolsLine()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.DoesNotContain("tools:", repo.Read(".claude/agents/bellatrix-desktop-locator-specialist.md"));
        Assert.DoesNotContain("tools:", repo.Read(".claude/agents/bellatrix-desktop-system-tests-generator.md"));
    }

    [Fact]
    public void CliRunner_InitTemplateSource_EmitsAssets_WithoutLanguageNotice()
    {
        using var source = new CustomTemplateFixture();
        using var repo = new TempRepo();
        var (exit, _, stderr) = Run("init", "--agent", "claude", "--out", repo.Root, "--template-source", source.Root);

        Assert.Equal(0, exit);
        Assert.True(repo.Exists(Fragment));
        Assert.True(repo.Exists(".claude/agents/acme-desktop-locator-specialist.md"));
        Assert.DoesNotContain("--language", stderr);
    }

    [Fact]
    public void CliRunner_PrefixWithoutTemplateSource_IsAUsageError()
    {
        using var repo = new TempRepo();
        var (exit, stdout, _) = Run("init", "--agent", "claude", "--out", repo.Root, "--prefix", "acme");

        Assert.Equal(1, exit);
        Assert.Contains("--template-source", stdout);
    }

    /// <summary>
    /// v.4 regression pin (generic mechanism): the content hash of every file rendered from the neutral fixture, plus
    /// CLAUDE.md after registration. Regenerate with <c>BELLATRIX_UPDATE_GOLDEN=1</c>.
    /// </summary>
    [Fact]
    public void Golden_TemplateSource_ContentHashes()
    {
        using var repo = new TempRepo();
        using var source = new CustomTemplateFixture();
        repo.Write("CLAUDE.md", "# Project\n\nExisting consumer instructions.\n");

        var result = Init(repo, source);

        var sb = new StringBuilder();
        foreach (var path in result.emitted.Append("CLAUDE.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(Sha(repo.Read(path))).Append("  ").Append(path).Append('\n');
        }

        AssertGolden("custom-templates-claude.sha.txt", sb.ToString());
    }

    internal static (int exit, string stdout, string stderr) Run(params string[] args)
    {
        var (originalOut, originalErr) = (Console.Out, Console.Error);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            return (CliRunner.Run(args), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string Sha(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"))))[..16].ToLowerInvariant();

    private static void AssertGolden(string name, string actual, [CallerFilePath] string thisFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden", name);
        if (Environment.GetEnvironmentVariable("BELLATRIX_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, actual);
            Assert.True(Environment.GetEnvironmentVariable("BELLATRIX_UPDATE_GOLDEN") == "1", $"Golden '{name}' was written; review it and re-run.");
            return;
        }

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }
}
