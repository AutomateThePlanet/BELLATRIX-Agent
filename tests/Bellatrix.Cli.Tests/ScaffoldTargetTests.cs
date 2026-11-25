using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Bellatrix.Cli;
using Bellatrix.Cli.Commands;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Native multi-agent scaffolding (<c>--agent</c>). Golden layouts per agent, coexistence of all nine
/// agents in one repo, R1 (human-owned instruction files untouched except one line/block), aliases,
/// auto-detect, dry-run, per-agent manifests and migration, domain-safe removal, and per-provider MCP cleanup.
/// </summary>
public class ScaffoldTargetTests
{
    private static readonly string[] AllAgents = AgentSelection.Names.ToArray();
    private static readonly IReadOnlySet<string> Desktop = ScaffoldCommand.ParseDomains("desktop");

    // ---------------------------------------------------------------- golden layouts

    /// <summary>
    /// Golden file per agent (desktop only): the sorted emitted paths, the host-file registration, and the full
    /// fragment + locator-specialist + generator + one skill. Regenerate with BELLATRIX_UPDATE_GOLDEN=1.
    /// </summary>
    [Theory]
    [InlineData("claude")]
    [InlineData("copilot")]
    [InlineData("cursor")]
    [InlineData("devin")]
    [InlineData("codex")]
    [InlineData("gemini")]
    [InlineData("antigravity")]
    [InlineData("kiro")]
    [InlineData("opencode")]
    public void Golden_PerAgentDesktopLayout(string agent)
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, agent, regenerate: false, domains: Desktop);

        var sb = new StringBuilder();
        sb.Append("# emitted\n");
        foreach (var path in result.emitted.OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(path).Append('\n');
        }

        var outcome = result.perAgent[agent];
        sb.Append("# registered: ").Append(outcome.registered ?? "(none)").Append('\n');
        if (outcome.registered is { } host)
        {
            sb.Append("# ").Append(host).Append('\n').Append(Normalize(repo.Read(host)));
        }

        foreach (var path in result.emitted
                     .Where(p => !p.Contains("/skills/", StringComparison.Ordinal)
                                 || p.Contains("bellatrix-desktop-components", StringComparison.Ordinal))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var content = Normalize(repo.Read(path));
            // Bodies are covered by TemplateCatalog tests; the golden pins placement + frontmatter + first lines.
            var head = string.Join("\n", content.Split('\n').Take(14));
            sb.Append("# file: ").Append(path).Append('\n').Append(head).Append("\n# sha256: ")
              .Append(Sha(content)).Append('\n');
        }

        AssertGolden($"{agent}-desktop.txt", sb.ToString());
    }

    /// <summary>
    /// Byte-equivalence pin: the full-content hash of every file <c>init --agent claude</c> emits for all
    /// domains. The core/lang template split must not change any of them except by intended, reviewed fixes.
    /// </summary>
    [Fact]
    public void Golden_ClaudeAllDomains_ContentHashes()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        var sb = new StringBuilder();
        foreach (var path in result.emitted.OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(Sha(repo.Read(path))).Append("  ").Append(path).Append('\n');
        }

        AssertGolden("claude-all-domains.sha.txt", sb.ToString());
    }

    /// <summary>The same content pin per non-C# language (TypeScript = web + the agnostic desktop, mobile parts).</summary>
    [Theory]
    [InlineData("java")]
    [InlineData("typescript")]
    public void Golden_ClaudeAllDomains_ContentHashes_PerLanguage(string language)
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, language: language);

        var sb = new StringBuilder();
        foreach (var path in result.emitted.OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(Sha(repo.Read(path))).Append("  ").Append(path).Append('\n');
        }

        AssertGolden($"claude-{language}-all-domains.sha.txt", sb.ToString());
    }

    [Fact]
    public void Claude_LayoutIsUnchangedFromPre22()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Equal(43, result.emitted.Length); // 36 skills + 6 agents + 1 fragment
        Assert.All(result.emitted, p => Assert.True(
            p == ".bellatrix/bellatrix.instructions.md" || p.StartsWith(".claude/", StringComparison.Ordinal), p));
        Assert.Equal("@.bellatrix/bellatrix.instructions.md",
            repo.Read("CLAUDE.md").Replace("\r\n", "\n").Split('\n').Single(l => l.StartsWith('@')));
        Assert.True(repo.Exists(".bellatrix/manifest.claude.json"));
        Assert.False(repo.Exists(".bellatrix/manifest.json"));
    }

    [Fact]
    public void Skills_DropCopilotOnlyToolsKey_ForEveryAgent()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, string.Join(",", AllAgents), regenerate: false, domains: Desktop);

        foreach (var skill in Directory.GetFiles(repo.Root, "SKILL.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(skill);
            Assert.DoesNotContain("runCommands", text);
            var folder = Path.GetFileName(Path.GetDirectoryName(skill)!);
            Assert.Contains($"name: {folder}\n", text); // Cursor/Gemini: name must equal the folder name
        }
    }

    [Fact]
    public void Copilot_WithoutClaude_EmitsNativeAgentsWithAliasesAndAllowList()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "copilot", regenerate: false, domains: Desktop);

        var specialist = repo.Read(".github/agents/bellatrix-desktop-locator-specialist.agent.md");
        Assert.Contains("tools: ['read', 'search', 'execute']", specialist);
        var generator = repo.Read(".github/agents/bellatrix-desktop-system-tests-generator.agent.md");
        Assert.Contains("agents: ['bellatrix-desktop-locator-specialist']", generator);
        Assert.StartsWith("---\napplyTo: \"**\"\n---\n", repo.Read(".github/instructions/bellatrix.instructions.md"));
        Assert.False(repo.Exists("CLAUDE.md"));
        Assert.True(repo.Exists(".github/skills/bellatrix-desktop-components/SKILL.md"));
    }

    [Fact]
    public void Codex_AgentIsValidTomlShape_AndAgentsMdHoldsOnePointerBlock()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "codex", regenerate: false, domains: Desktop);

        var toml = repo.Read(".codex/agents/bellatrix-desktop-locator-specialist.toml");
        Assert.StartsWith("name = \"bellatrix-desktop-locator-specialist\"\ndescription = \"", toml);
        Assert.Contains("developer_instructions = '''\n", toml);
        Assert.EndsWith("'''\n", toml);

        var agentsMd = repo.Read("AGENTS.md");
        Assert.Equal(1, Count(agentsMd, "<!-- >>> bellatrix >>> -->"));
        Assert.Contains("`.bellatrix/bellatrix.instructions.md`", agentsMd);
        Assert.True(repo.Exists(".agents/skills/bellatrix-desktop-components/SKILL.md"));
    }

    [Fact]
    public void Gemini_ImportsFragment_AndAgentsUseGeminiToolNames()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "gemini", regenerate: false, domains: Desktop);

        Assert.Contains("\n@./.bellatrix/bellatrix.instructions.md\n", repo.Read("GEMINI.md"));
        var specialist = repo.Read(".gemini/agents/bellatrix-desktop-locator-specialist.md");
        Assert.Contains("kind: local\ntools:\n  - read_file\n  - grep_search\n  - glob\n  - run_shell_command\n", specialist);
    }

    [Fact]
    public void Cursor_IsAlwaysNative_ReadonlySpecialist_EvenWithClaude()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude,cursor", regenerate: false, domains: Desktop);

        Assert.Contains("readonly: true", repo.Read(".cursor/agents/bellatrix-desktop-locator-specialist.md"));
        Assert.DoesNotContain("readonly", repo.Read(".cursor/agents/bellatrix-desktop-system-tests-generator.md"));
        Assert.Contains("alwaysApply: true", repo.Read(".cursor/rules/bellatrix.mdc"));
        Assert.False(Directory.Exists(repo.Path(".cursor/skills"))); // reuses .claude/skills
        Assert.Contains("skills: .claude/skills (for claude, cursor)", result.decisions);
    }

    // ---------------------------------------------------------------- coexistence

    [Fact]
    public void TheCliSupportsExactlyTheNineAgents_InReportOrder()
        => Assert.Equal(
            new[] { "claude", "copilot", "cursor", "devin", "codex", "gemini", "antigravity", "kiro", "opencode" },
            AgentSelection.Names);

    [Fact]
    public void AllNineAgents_Coexist_AndPerAgentRegenerateTouchesNoOtherAgent()
    {
        using var repo = new TempRepo();
        var first = ScaffoldCommand.Scaffold(repo.Root, string.Join(",", AllAgents), regenerate: false, domains: Desktop);

        Assert.Equal(AllAgents, first.agents);
        foreach (var agent in AllAgents)
        {
            Assert.True(repo.Exists($".bellatrix/manifest.{agent}.json"), agent);
        }

        // Skills land in exactly three dirs: .claude/skills (claude, copilot, cursor, devin, opencode),
        // .agents/skills (codex, gemini, antigravity) and .kiro/skills (kiro).
        var skillDirs = Directory.GetFiles(repo.Root, "SKILL.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(repo.Root, Path.GetDirectoryName(Path.GetDirectoryName(f))!).Replace('\\', '/'))
            .Distinct().OrderBy(d => d).ToArray();
        Assert.Equal(new[] { ".agents/skills", ".claude/skills", ".kiro/skills" }, skillDirs);
        foreach (var reader in new[] { "copilot", "cursor", "opencode" })
        {
            Assert.Contains(first.decisions, d => d.StartsWith($"warning: {reader} reads", StringComparison.Ordinal));
        }

        // One AGENTS.md block serves Codex and OpenCode; Antigravity and Kiro touch no human-owned file.
        Assert.Equal(1, CountOccurrences(repo.Read("AGENTS.md"), "<!-- >>> bellatrix >>> -->"));
        Assert.Equal("AGENTS.md", first.perAgent["opencode"].registered);
        Assert.Null(first.perAgent["antigravity"].registered);
        Assert.Null(first.perAgent["kiro"].registered);

        foreach (var agent in AllAgents)
        {
            var before = Snapshot(repo.Root, exclude: $".bellatrix/manifest.{agent}.json");
            var rerun = ScaffoldCommand.Scaffold(repo.Root, agent, regenerate: true, domains: Desktop);
            var after = Snapshot(repo.Root, exclude: $".bellatrix/manifest.{agent}.json");

            Assert.Empty(rerun.emitted);
            Assert.Empty(rerun.removed);
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public void SharedFragment_UserEdit_IsProtectedAcrossAgents()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude,codex", regenerate: false, domains: Desktop);
        File.AppendAllText(repo.Path(".bellatrix/bellatrix.instructions.md"), "\n# MY NOTE\n");

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "gemini", regenerate: false, domains: Desktop);

        Assert.Contains(".bellatrix/bellatrix.instructions.md", rerun.skipped);
        Assert.Contains("# MY NOTE", repo.Read(".bellatrix/bellatrix.instructions.md"));
    }

    [Fact]
    public void AddingClaude_RetiresCursorsNativeSkillCopies_ButNeverEditedOnes()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "cursor", regenerate: false, domains: Desktop);
        Assert.True(repo.Exists(".cursor/skills/bellatrix-desktop-components/SKILL.md"));
        File.AppendAllText(repo.Path(".cursor/skills/bellatrix-desktop-app-services/SKILL.md"), "edit");

        var result = ScaffoldCommand.Scaffold(repo.Root, "claude,cursor", regenerate: false, domains: Desktop);

        Assert.False(repo.Exists(".cursor/skills/bellatrix-desktop-components/SKILL.md"));
        Assert.Contains(".cursor/skills/bellatrix-desktop-components/SKILL.md", result.perAgent["cursor"].removed);
        Assert.True(repo.Exists(".cursor/skills/bellatrix-desktop-app-services/SKILL.md")); // user-edited: kept
        Assert.Contains(".cursor/skills/bellatrix-desktop-app-services/SKILL.md", result.skipped);
        Assert.True(repo.Exists(".claude/skills/bellatrix-desktop-components/SKILL.md"));
    }

    [Fact]
    public void NarrowerDomains_NeverRemoveOtherDomainsFiles()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "devin", regenerate: false);
        var result = ScaffoldCommand.Scaffold(repo.Root, "devin", regenerate: false, domains: ScaffoldCommand.ParseDomains("web"));

        Assert.Empty(result.removed);
        Assert.True(repo.Exists(".devin/skills/bellatrix-desktop-components/SKILL.md"));
        Assert.Contains(".devin/skills/bellatrix-desktop-components/SKILL.md", repo.Read(".bellatrix/manifest.devin.json"));
    }

    // ---------------------------------------------------------------- R1: human-owned files

    [Fact]
    public void R1_HumanOwnedInstructionFiles_ChangeOnlyByTheOneAllowedLineOrBlock()
    {
        using var repo = new TempRepo();
        const string human = "# Team rules\r\n\r\nUse tabs.\r\n";
        foreach (var file in new[] { "CLAUDE.md", "AGENTS.md", "GEMINI.md", ".github/copilot-instructions.md" })
        {
            repo.Write(file, human);
        }

        ScaffoldCommand.Scaffold(repo.Root, string.Join(",", AllAgents), regenerate: false, domains: Desktop);
        ScaffoldCommand.Scaffold(repo.Root, string.Join(",", AllAgents), regenerate: true, domains: Desktop); // idempotent

        Assert.Equal(human, repo.Read(".github/copilot-instructions.md")); // Copilot uses its own instructions file
        Assert.Equal(human + "\n# Tool instructions\n\n@.bellatrix/bellatrix.instructions.md\n", repo.Read("CLAUDE.md"));
        Assert.Equal(human + "\n" + MarkerBlockRegistrar.Block("bellatrix",
            "Before any UI automation, read `.bellatrix/bellatrix.instructions.md` and follow it (BELLATRIX CLI usage; never read a full snapshot into context)."),
            repo.Read("AGENTS.md"));
        Assert.Equal(human + "\n" + MarkerBlockRegistrar.Block("bellatrix", "@./.bellatrix/bellatrix.instructions.md"),
            repo.Read("GEMINI.md"));
    }

    [Fact]
    public void MarkerBlock_ChangedLine_IsReplacedInPlace_RestUntouched()
    {
        using var repo = new TempRepo();
        repo.Write("GEMINI.md", "top\n<!-- >>> bellatrix >>> -->\n@./old.md\n<!-- <<< bellatrix <<< -->\nbottom\n");

        MarkerBlockRegistrar.Register(repo.Path("GEMINI.md"), "bellatrix", "@./new.md");

        Assert.Equal("top\n<!-- >>> bellatrix >>> -->\n@./new.md\n<!-- <<< bellatrix <<< -->\nbottom\n", repo.Read("GEMINI.md"));
    }

    // ---------------------------------------------------------------- dry-run, manifests

    [Fact]
    public void DryRun_WritesNothing_ButReportsThePlan()
    {
        using var repo = new TempRepo();
        repo.Write(".mcp.json", "{\"mcpServers\":{\"bellatrix-desktop-mcp\":{}}}");

        var result = ScaffoldCommand.Scaffold(repo.Root, "claude,devin", regenerate: false, domains: Desktop, dryRun: true);

        Assert.True(result.dryRun);
        Assert.NotEmpty(result.perAgent["devin"].emitted);
        Assert.Contains(".mcp.json", result.mcpRemoved);
        Assert.Equal(new[] { ".mcp.json" }, Directory.GetFileSystemEntries(repo.Root).Select(Path.GetFileName));
        Assert.Contains("bellatrix-desktop-mcp", repo.Read(".mcp.json"));
    }

    [Fact]
    public void LegacyManifest_IsMigratedToClaudeManifest()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, domains: Desktop);
        File.Move(repo.Path(".bellatrix/manifest.claude.json"), repo.Path(".bellatrix/manifest.json"));

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false, domains: Desktop);

        Assert.Empty(rerun.emitted);
        Assert.False(repo.Exists(".bellatrix/manifest.json"));
        Assert.True(repo.Exists(".bellatrix/manifest.claude.json"));
    }

    // ---------------------------------------------------------------- MCP cleanup per provider

    [Fact]
    public void McpCleanup_PerProvider_RemovesOnlyOwnedKeys()
    {
        using var repo = new TempRepo();
        repo.Write(".cursor/mcp.json", "{\"mcpServers\":{\"bellatrix-web-mcp\":{},\"spectra\":{\"command\":\"x\"}}}");
        repo.Write(".gemini/settings.json", "{\"theme\":\"dark\",\"mcpServers\":{\"bellatrix-mobile-mcp\":{}}}");
        repo.Write(".codex/config.toml",
            "model = \"o\"\n\n[mcp_servers.bellatrix-desktop-mcp]\ncommand = \"b\"\n\n[mcp_servers.bellatrix-desktop-mcp.env]\nA = \"1\"\n\n[mcp_servers.spectra]\ncommand = \"s\"\n");

        var result = ScaffoldCommand.Scaffold(repo.Root, "cursor,gemini,codex", regenerate: false, domains: Desktop);

        Assert.Equal(new[] { ".cursor/mcp.json", ".codex/config.toml", ".gemini/settings.json" }.OrderBy(x => x),
            result.mcpRemoved.OrderBy(x => x));
        Assert.Contains("spectra", repo.Read(".cursor/mcp.json"));
        Assert.Contains("\"theme\": \"dark\"", repo.Read(".gemini/settings.json"));
        Assert.Equal("model = \"o\"\n\n[mcp_servers.spectra]\ncommand = \"s\"\n", repo.Read(".codex/config.toml"));
    }

    [Fact]
    public void McpCleanup_NewProviders_RemovesOnlyOwnedKeys()
    {
        using var repo = new TempRepo();
        repo.Write(".agents/mcp_config.json", "{\"mcpServers\":{\"bellatrix-desktop-mcp\":{},\"spectra\":{\"command\":\"x\"}}}");
        repo.Write(".kiro/settings/mcp.json", "{\"mcpServers\":{\"bellatrix-web-mcp\":{}}}");
        repo.Write("opencode.json", "{\"$schema\":\"https://opencode.ai/config.json\",\"mcp\":{\"bellatrix-mobile-mcp\":{\"type\":\"local\"},\"spectra\":{\"type\":\"local\"}}}");
        repo.Write("opencode.jsonc", "{\n  // keep me\n  \"theme\": \"dark\"\n}");

        var result = ScaffoldCommand.Scaffold(repo.Root, "antigravity,kiro,opencode", regenerate: false, domains: Desktop);

        Assert.Equal(new[] { ".agents/mcp_config.json", ".kiro/settings/mcp.json", "opencode.json" }.OrderBy(x => x),
            result.mcpRemoved.OrderBy(x => x));
        Assert.Contains("spectra", repo.Read(".agents/mcp_config.json"));
        Assert.DoesNotContain("bellatrix-web-mcp", repo.Read(".kiro/settings/mcp.json"));
        Assert.Contains("spectra", repo.Read("opencode.json"));
        Assert.Contains("https://opencode.ai/config.json", repo.Read("opencode.json"));
        Assert.Contains("// keep me", repo.Read("opencode.jsonc")); // nothing owned there: never rewritten
    }

    [Fact]
    public void Kiro_AgentsListTheSteeringFileAndSkills_AndSpecialistsAreReadOnly()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "kiro", regenerate: false, domains: Desktop);

        var steering = repo.Read(".kiro/steering/bellatrix.md");
        Assert.StartsWith("---\ninclusion: always\n---\n", steering.Replace("\r\n", "\n"));
        foreach (var agent in new[] { "bellatrix-desktop-locator-specialist", "bellatrix-desktop-system-tests-generator" })
        {
            var text = repo.Read($".kiro/agents/{agent}.md");
            Assert.Contains("  - file://.kiro/steering/bellatrix.md", text);
            Assert.Contains("  - skill://.kiro/skills/bellatrix-*/SKILL.md", text);
        }

        Assert.Contains("tools: [\"read\", \"shell\"]", repo.Read(".kiro/agents/bellatrix-desktop-locator-specialist.md"));
        Assert.Contains("tools: [\"*\"]", repo.Read(".kiro/agents/bellatrix-desktop-system-tests-generator.md"));
        Assert.True(repo.Exists(".kiro/skills/bellatrix-desktop-components/SKILL.md"));
        Assert.False(repo.Exists("AGENTS.md"));
    }

    [Fact]
    public void Antigravity_UsesAnAlwaysOnRule_FolderFormSubagents_AndDocumentedToolNamesOnly()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "agy", regenerate: false, domains: Desktop);

        Assert.Equal(new[] { "antigravity" }, result.agents);
        Assert.StartsWith("---\ntrigger: always_on\n---\n", repo.Read(".agents/rules/bellatrix.md").Replace("\r\n", "\n"));
        Assert.True(repo.Exists(".agents/skills/bellatrix-desktop-components/SKILL.md"));
        var specialist = repo.Read(".agents/agents/bellatrix-desktop-locator-specialist/agent.md");
        var generator = repo.Read(".agents/agents/bellatrix-desktop-system-tests-generator/agent.md");
        Assert.Contains("mainAgent: false", specialist);
        Assert.DoesNotContain("replace_file_content", specialist);
        Assert.Contains("mainAgent: true", generator);

        var documented = new[] { "view_file", "grep_search", "replace_file_content", "run_command" };
        foreach (var text in new[] { specialist, generator })
        {
            var tools = text.Replace("\r\n", "\n").Split('\n').SkipWhile(l => l != "tools:").Skip(1)
                .TakeWhile(l => l.StartsWith("  - ", StringComparison.Ordinal)).Select(l => l[4..]).ToArray();
            Assert.NotEmpty(tools);
            Assert.All(tools, t => Assert.Contains(t, documented));
        }

        Assert.Contains("antigravity subagents: native .agents/agents", result.decisions);
        Assert.False(repo.Exists("AGENTS.md"));
        Assert.False(repo.Exists("GEMINI.md"));
    }

    [Fact]
    public void OpenCode_SharesCodexsAgentsMdBlock_AndKeepsHumanContent()
    {
        using var repo = new TempRepo();
        repo.Write("AGENTS.md", "# Team rules\n\nUse tabs.\n");

        ScaffoldCommand.Scaffold(repo.Root, "opencode", regenerate: false, domains: Desktop);
        var afterOpenCode = repo.Read("AGENTS.md");
        ScaffoldCommand.Scaffold(repo.Root, "codex", regenerate: false, domains: Desktop);

        Assert.Equal(afterOpenCode, repo.Read("AGENTS.md")); // identical block: Codex adds nothing
        Assert.StartsWith("# Team rules\n\nUse tabs.\n", afterOpenCode.Replace("\r\n", "\n"));
        Assert.Equal(1, CountOccurrences(afterOpenCode, "<!-- >>> bellatrix >>> -->"));

        var specialist = repo.Read(".opencode/agents/bellatrix-desktop-locator-specialist.md");
        Assert.Contains("mode: subagent\npermission:\n  edit: deny\n  bash: allow\n", specialist.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\nname:", specialist); // OpenCode names an agent after its file
        Assert.Contains("mode: all", repo.Read(".opencode/agents/bellatrix-desktop-system-tests-generator.md"));
    }

    // ---------------------------------------------------------------- aliases + auto-detect via the CLI

    [Fact]
    public void Agy_IsAnAliasForAntigravity_WithoutNotice()
    {
        var notices = new StringWriter();
        Assert.Equal(new[] { "antigravity", "kiro" }, AgentSelection.Parse("kiro,agy", notices));
        Assert.Equal(string.Empty, notices.ToString());
        Assert.Equal(new[] { "antigravity" }, AgentSelection.Parse("agy,antigravity"));
    }

    [Fact]
    public void Windsurf_IsAnAliasForDevin()
    {
        var notices = new StringWriter();
        Assert.Equal(new[] { "devin" }, AgentSelection.Parse("windsurf", notices));
        Assert.Contains("Devin Desktop", notices.ToString());
    }

    [Fact]
    public void Init_UnknownAgent_ReturnsUsageError()
    {
        using var repo = new TempRepo();
        var (exit, output) = RunCli(repo.Root, "init", "--agent", "bogus");
        Assert.Equal(2, exit);
        Assert.Contains("Unknown --agent", output);
    }

    [Fact]
    public void Init_DeprecatedTarget_StillWorks()
    {
        using var repo = new TempRepo();
        var (exit, output) = RunCli(repo.Root, "init", "--target", "cursor", "--domains", "desktop");
        Assert.Equal(0, exit);
        Assert.Contains("\"agents\":[\"cursor\"]", output);
        Assert.True(repo.Exists(".cursor/rules/bellatrix.mdc"));
    }

    [Theory]
    [InlineData(".cursor", "cursor")]
    [InlineData("GEMINI.md", "gemini")]
    [InlineData(".windsurf", "devin")]
    [InlineData(".codex", "codex")]
    [InlineData(".github/agents", "copilot")]
    [InlineData("CLAUDE.md", "claude")]
    [InlineData(".agents/agents", "antigravity")]
    [InlineData(".agents/rules", "antigravity")]
    [InlineData(".agent", "antigravity")]
    [InlineData(".kiro", "kiro")]
    [InlineData("opencode.json", "opencode")]
    [InlineData("opencode.jsonc", "opencode")]
    [InlineData(".opencode", "opencode")]
    public void Init_AutoDetectsTheSingleAgentPresent(string marker, string agent)
    {
        using var repo = new TempRepo();
        if (marker.Contains('.') && !marker.StartsWith('.'))
        {
            repo.Write(marker, "x");
        }
        else
        {
            Directory.CreateDirectory(repo.Path(marker));
        }

        var (exit, output) = RunCli(repo.Root, "init", "--domains", "desktop", "--dry-run");
        Assert.Equal(0, exit);
        Assert.Contains($"\"agents\":[\"{agent}\"]", output);
    }

    [Fact]
    public void Init_AutoDetect_NoneOrSeveral_IsUsageError_ButExistingManifestsWin()
    {
        using var none = new TempRepo();
        var (exitNone, outNone) = RunCli(none.Root, "init");
        Assert.Equal(2, exitNone);
        Assert.Contains("No AI agent detected", outNone);

        using var several = new TempRepo();
        Directory.CreateDirectory(several.Path(".cursor"));
        Directory.CreateDirectory(several.Path(".gemini"));
        var (exitSeveral, outSeveral) = RunCli(several.Root, "init");
        Assert.Equal(2, exitSeveral);
        Assert.Contains("Several AI agents detected (cursor, gemini)", outSeveral);

        ScaffoldCommand.Scaffold(several.Root, "cursor,gemini", regenerate: false, domains: Desktop);
        var (exitRerun, outRerun) = RunCli(several.Root, "update-skills", "--domains", "desktop");
        Assert.Equal(0, exitRerun);
        Assert.Contains("\"agents\":[\"cursor\",\"gemini\"]", outRerun);
    }

    // ---------------------------------------------------------------- helpers

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static (int Exit, string Output) RunCli(string cwd, params string[] args)
    {
        var (exit, output, _) = CliHarness.Run(cwd, args);
        return (exit, output);
    }

    private static Dictionary<string, string> Snapshot(string root, string exclude)
        => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => p != exclude)
            .ToDictionary(p => p, p => Sha(File.ReadAllText(Path.Combine(root, p))));

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string Sha(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text))))[..16].ToLowerInvariant();

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    private static void AssertGolden(string name, string actual, [CallerFilePath] string thisFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden", name);
        if (Environment.GetEnvironmentVariable("BELLATRIX_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            if (Environment.GetEnvironmentVariable("BELLATRIX_UPDATE_GOLDEN") != "1")
            {
                Assert.Fail($"Golden file '{name}' did not exist; it was written. Review it and re-run.");
            }

            return;
        }

        Assert.Equal(Normalize(File.ReadAllText(path)), actual);
    }
}
