using System.Text;

namespace Bellatrix.Cli.Scaffolding.Emitters;

/// <summary>
/// Claude Code: the shared fragment imported by one <c>@</c> line in <c>CLAUDE.md</c>; <c>.claude/skills</c>;
/// <c>.claude/agents</c>. This is the reference layout — its output is byte-identical to pre-2.2 <c>init</c>.
/// </summary>
internal sealed class ClaudeEmitter : AgentEmitter
{
    public override string Name => "claude";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".claude/skills" };

    public override bool ReadsClaudeAgents => true;

    public override string FragmentPath(string profile) => SharedFragmentPath(profile);

    public override HostRegistration Host(string profile)
        => new("CLAUDE.md", HostStyle.ToolInstructionsLine, "@" + SharedFragmentPath(profile));

    public override string AgentPath(string slug) => $".claude/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
        => string.IsNullOrEmpty(item.Tools)
            ? $"---\nname: {item.Name}\ndescription: {item.Description}\n---\n\n{item.Body}"
            : $"---\nname: {item.Name}\ndescription: {item.Description}\ntools: {item.Tools}\n---\n\n{item.Body}";

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".mcp.json", ".claude/settings.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".claude", "CLAUDE.md" };
}

/// <summary>
/// GitHub Copilot (VS Code, CLI, cloud agent): an always-applied <c>.github/instructions/*.instructions.md</c>
/// (<c>applyTo: "**"</c>, auto-loaded, so no line in <c>copilot-instructions.md</c>); <c>.github/skills</c>;
/// <c>.github/agents/*.agent.md</c> with Copilot tool aliases and an <c>agents:</c> allow-list on generators.
/// </summary>
internal sealed class CopilotEmitter : AgentEmitter
{
    public override string Name => "copilot";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".github/skills", ".claude/skills", ".agents/skills" };

    public override bool ReadsClaudeAgents => true; // VS Code + Copilot CLI (cloud agent: UNKNOWN)

    public override string FragmentPath(string profile) => $".github/instructions/{Stem(profile)}.instructions.md";

    public override string RenderFragment(string body, string profile)
        => "---\napplyTo: \"**\"\n---\n\n" + body;

    public override string AgentPath(string slug) => $".github/agents/{slug}.agent.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        var sb = new StringBuilder();
        sb.Append("---\nname: ").Append(item.Name).Append("\ndescription: ").Append(item.Description).Append('\n');

        var specialist = SpecialistFor(item, locatorSpecialists);
        var tools = IsLocatorSpecialist(item) && SourceTools(item).Count == 0
            ? new List<string> { "read", "search", "execute" }
            : MapTools(SourceTools(item));
        if (tools.Count > 0)
        {
            if (specialist is not null && !tools.Contains("agent"))
            {
                tools.Add("agent"); // VS Code: an agents: allow-list needs the agent tool
            }

            sb.Append("tools: [").Append(string.Join(", ", tools.Select(t => $"'{t}'"))).Append("]\n");
        }

        if (specialist is not null)
        {
            sb.Append("agents: ['").Append(specialist).Append("']\n");
        }

        return sb.Append("---\n\n").Append(item.Body).ToString();
    }

    /// <summary>Claude tool names → Copilot aliases (docs.github.com custom-agents-configuration).</summary>
    private static List<string> MapTools(IReadOnlyList<string> source)
    {
        var mapped = new List<string>();
        foreach (var tool in source)
        {
            var alias = tool switch
            {
                "Bash" => "execute",
                "Read" or "NotebookRead" => "read",
                "Edit" or "MultiEdit" or "Write" or "NotebookEdit" => "edit",
                "Grep" or "Glob" => "search",
                "Task" => "agent",
                "WebSearch" or "WebFetch" => "web",
                "TodoWrite" => "todo",
                _ => null,
            };
            if (alias is not null && !mapped.Contains(alias))
            {
                mapped.Add(alias);
            }
        }

        return mapped;
    }

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".vscode/mcp.json", ".github/mcp.json", ".mcp.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".github/copilot-instructions.md", ".github/agents" };
}

/// <summary>
/// Cursor: an <c>alwaysApply</c> project rule <c>.cursor/rules/*.mdc</c> (must be <c>.mdc</c>);
/// <c>.cursor/skills</c>; <c>.cursor/agents</c> (wins a name clash with <c>.claude/agents</c>; <c>readonly</c>
/// instead of a <c>tools</c> list, which Cursor does not have).
/// </summary>
internal sealed class CursorEmitter : AgentEmitter
{
    public override string Name => "cursor";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".cursor/skills", ".agents/skills", ".claude/skills", ".codex/skills" };

    public override bool ReadsClaudeAgents => true;

    public override bool NativeAgentsShadowClaude => true;

    public override string FragmentPath(string profile) => $".cursor/rules/{Stem(profile)}.mdc";

    public override string RenderFragment(string body, string profile)
        => $"---\ndescription: {(CustomTemplateSource.IsCustom(profile) ? profile : "BELLATRIX")} UI automation through the model-free CLI (always applied).\nalwaysApply: true\n---\n\n" + body;

    public override string AgentPath(string slug) => $".cursor/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
        => $"---\nname: {item.Name}\ndescription: {item.Description}\n{(IsReadOnly(item) ? "readonly: true\n" : string.Empty)}---\n\n{item.Body}";

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".cursor/mcp.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".cursor" };
}

/// <summary>
/// Devin Desktop (formerly Windsurf) / Devin CLI: an <c>always_on</c> rule <c>.devin/rules/*.md</c>;
/// <c>.devin/skills</c>; <c>.devin/agents</c> (experimental; <c>.claude/agents</c> import is undocumented), with
/// <c>allowed-tools</c> where every name is documented.
/// </summary>
internal sealed class DevinEmitter : AgentEmitter
{
    public override string Name => "devin";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".devin/skills", ".claude/skills", ".github/skills" };

    public override string FragmentPath(string profile) => $".devin/rules/{Stem(profile)}.md";

    public override string RenderFragment(string body, string profile)
        => "---\ntrigger: always_on\n---\n\n" + body;

    public override string AgentPath(string slug) => $".devin/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        var tools = IsLocatorSpecialist(item) && SourceTools(item).Count == 0
            ? new[] { "read", "grep", "glob", "exec" }
            : MapTools(SourceTools(item));
        var allowed = tools is { Length: > 0 } ? $"allowed-tools: [{string.Join(", ", tools)}]\n" : string.Empty;
        return $"---\nname: {item.Name}\ndescription: {item.Description}\n{allowed}---\n\n{item.Body}";
    }

    /// <summary>Documented Devin tool names only; a source tool without one drops the whole restriction.</summary>
    private static string[]? MapTools(IReadOnlyList<string> source)
    {
        var mapped = new List<string>();
        foreach (var tool in source)
        {
            var name = tool switch
            {
                "Read" => "read",
                "Grep" => "grep",
                "Glob" => "glob",
                "Bash" => "exec",
                _ => null,
            };
            if (name is null)
            {
                return null; // e.g. Edit/Write: no documented Devin name — leave the agent unrestricted
            }

            if (!mapped.Contains(name))
            {
                mapped.Add(name);
            }
        }

        return mapped.ToArray();
    }

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".devin/mcp_config.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".devin", ".windsurf" };
}

/// <summary>
/// OpenAI Codex CLI: no import syntax and no tool-owned auto-loaded file, so <c>AGENTS.md</c> gets one marker
/// block pointing at the shared fragment; <c>.agents/skills</c>; TOML subagents in <c>.codex/agents</c>
/// (<c>developer_instructions</c> carries the body; no per-agent read-only setting exists).
/// </summary>
internal sealed class CodexEmitter : AgentEmitter
{
    public override string Name => "codex";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".agents/skills" };

    public override string FragmentPath(string profile) => SharedFragmentPath(profile);

    public override HostRegistration Host(string profile) => AgentsMdPointer(profile);

    /// <summary>The <c>AGENTS.md</c> pointer block; every agent that registers through it writes this same block.</summary>
    public static HostRegistration AgentsMdPointer(string profile)
        => new("AGENTS.md", HostStyle.MarkerBlock,
            $"Before any UI automation, read `{SharedFragmentPath(profile)}` and follow it (BELLATRIX CLI usage; never read a full snapshot into context).");

    public override string AgentPath(string slug) => $".codex/agents/{slug}.toml";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
        => $"name = {TomlString(item.Name)}\ndescription = {TomlString(item.Description)}\ndeveloper_instructions = {TomlMultiline(item.Body)}\n";

    private static string TomlString(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>A literal multi-line string (no escapes) unless the body contains its delimiter.</summary>
    private static string TomlMultiline(string body)
        => body.Contains("'''", StringComparison.Ordinal)
            ? "\"\"\"\n" + body.Replace("\\", "\\\\").Replace("\"\"\"", "\"\"\\\"") + "\"\"\""
            : "'''\n" + body + "'''";

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".codex/config.toml" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".codex" };
}

/// <summary>
/// Gemini CLI: one marker block in <c>GEMINI.md</c> holding a real <c>@./</c> import of the shared fragment;
/// <c>.agents/skills</c> (Gemini's highest-precedence skills dir, shared with Codex); <c>.gemini/agents</c>
/// (isolated, <c>@name</c> delegation) with Gemini tool names.
/// </summary>
internal sealed class GeminiEmitter : AgentEmitter
{
    public override string Name => "gemini";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".agents/skills", ".gemini/skills" };

    public override string FragmentPath(string profile) => SharedFragmentPath(profile);

    public override HostRegistration Host(string profile)
        => new("GEMINI.md", HostStyle.MarkerBlock, "@./" + SharedFragmentPath(profile));

    public override string AgentPath(string slug) => $".gemini/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        var tools = IsLocatorSpecialist(item) && SourceTools(item).Count == 0
            ? new List<string> { "read_file", "grep_search", "glob", "run_shell_command" }
            : MapTools(SourceTools(item));
        var toolsBlock = tools.Count > 0
            ? "tools:\n" + string.Concat(tools.Select(t => $"  - {t}\n"))
            : string.Empty;
        return $"---\nname: {item.Name}\ndescription: {item.Description}\nkind: local\n{toolsBlock}---\n\n{item.Body}";
    }

    /// <summary>Claude tool names → Gemini CLI built-in tool names (gemini-cli docs/reference/tools.md).</summary>
    private static List<string> MapTools(IReadOnlyList<string> source)
    {
        var mapped = new List<string>();
        foreach (var tool in source)
        {
            var names = tool switch
            {
                "Read" => new[] { "read_file" },
                "Grep" => new[] { "grep_search" },
                "Glob" => new[] { "glob" },
                "Bash" => new[] { "run_shell_command" },
                "Edit" or "MultiEdit" => new[] { "replace" },
                "Write" => new[] { "write_file" },
                "WebFetch" => new[] { "web_fetch" },
                "WebSearch" => new[] { "google_web_search" },
                _ => Array.Empty<string>(),
            };
            mapped.AddRange(names.Where(n => !mapped.Contains(n)));
        }

        return mapped;
    }

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".gemini/settings.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".gemini", "GEMINI.md" };
}

/// <summary>
/// Google Antigravity CLI (<c>agy</c>): an <c>always_on</c> workspace rule <c>.agents/rules/*.md</c> (rules are
/// cumulative with <c>AGENTS.md</c>/<c>GEMINI.md</c>, so no human-owned file is touched); <c>.agents/skills</c>
/// (shared with Codex and Gemini); subagents in the folder form <c>.agents/agents/&lt;name&gt;/agent.md</c>, the only
/// form both the subagents page and the <c>/agents</c> command page accept. <c>tools</c> defaults to none and an
/// unknown tool name can hang the subagent, so only documented names are listed.
/// </summary>
internal sealed class AntigravityEmitter : AgentEmitter
{
    public override string Name => "antigravity";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".agents/skills" };

    public override string FragmentPath(string profile) => $".agents/rules/{Stem(profile)}.md";

    public override string RenderFragment(string body, string profile)
        => "---\ntrigger: always_on\n---\n\n" + body;

    public override string AgentPath(string slug) => $".agents/agents/{slug}/agent.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        var tools = IsReadOnly(item)
            ? new[] { "view_file", "grep_search", "run_command" }
            : new[] { "view_file", "grep_search", "replace_file_content", "run_command" };
        var main = IsLocatorSpecialist(item) ? "false" : "true";
        return $"---\nname: {item.Name}\ndescription: {item.Description}\ntools:\n{string.Concat(tools.Select(t => $"  - {t}\n"))}"
            + $"subagent: true\nmainAgent: {main}\n---\n\n{item.Body}";
    }

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".agents/mcp_config.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } =
        new[] { ".agents/agents", ".agents/rules", ".agents/mcp_config.json", ".agent" };
}

/// <summary>
/// Kiro CLI (v3 format): an always-included steering file <c>.kiro/steering/*.md</c>; <c>.kiro/skills</c>;
/// Markdown custom agents in <c>.kiro/agents</c>. Custom agents load neither steering nor skills unless their
/// <c>resources</c> list them, so every agent lists the BELLATRIX steering file and skills.
/// </summary>
internal sealed class KiroEmitter : AgentEmitter
{
    public override string Name => "kiro";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".kiro/skills" };

    public override string FragmentPath(string profile) => $".kiro/steering/{Stem(profile)}.md";

    public override string RenderFragment(string body, string profile)
        => "---\ninclusion: always\n---\n\n" + body;

    public override string AgentPath(string slug) => $".kiro/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        var prefix = item.Slug.Split('-')[0];
        var tools = IsReadOnly(item) ? "[\"read\", \"shell\"]" : "[\"*\"]";
        return $"---\nname: {item.Name}\ndescription: {item.Description}\ntools: {tools}\nresources:\n"
            + $"  - file://.kiro/steering/{prefix}.md\n  - skill://.kiro/skills/{prefix}-*/SKILL.md\n---\n\n{item.Body}";
    }

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { ".kiro/settings/mcp.json" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { ".kiro" };
}

/// <summary>
/// OpenCode: no tool-owned auto-loaded instructions file (it reads <c>AGENTS.md</c>, else <c>CLAUDE.md</c>, and does
/// not follow <c>@</c> imports), so <c>AGENTS.md</c> gets the same pointer block as Codex; <c>.opencode/skills</c>
/// (it also reads <c>.agents/skills</c> and <c>.claude/skills</c>); Markdown agents in <c>.opencode/agents</c>
/// (the file name is the agent name) with <c>mode</c> and <c>permission</c>.
/// </summary>
internal sealed class OpenCodeEmitter : AgentEmitter
{
    public override string Name => "opencode";

    public override IReadOnlyList<string> SkillDirs { get; } = new[] { ".opencode/skills", ".agents/skills", ".claude/skills" };

    public override string FragmentPath(string profile) => SharedFragmentPath(profile);

    public override HostRegistration Host(string profile) => CodexEmitter.AgentsMdPointer(profile);

    public override string AgentPath(string slug) => $".opencode/agents/{slug}.md";

    public override string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
        => IsReadOnly(item)
            ? $"---\ndescription: {item.Description}\nmode: subagent\npermission:\n  edit: deny\n  bash: allow\n---\n\n{item.Body}"
            : $"---\ndescription: {item.Description}\nmode: all\n---\n\n{item.Body}";

    public override IReadOnlyList<string> McpConfigFiles { get; } = new[] { "opencode.json", "opencode.jsonc" };

    public override IReadOnlyList<string> DetectionMarkers { get; } = new[] { "opencode.json", "opencode.jsonc", ".opencode" };
}
