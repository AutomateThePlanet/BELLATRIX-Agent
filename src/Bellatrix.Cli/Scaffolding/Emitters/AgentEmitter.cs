namespace Bellatrix.Cli.Scaffolding.Emitters;

/// <summary>
/// A file in an AI agent's own instructions file (e.g. <c>CLAUDE.md</c>, <c>AGENTS.md</c>) that BELLATRIX
/// registers itself in with one line or one marker block (R1/R3). Everything else in that file is the
/// human's and is never rewritten.
/// </summary>
/// <param name="Path">Repo-relative path of the human-owned host file.</param>
/// <param name="Style">How the registration is written.</param>
/// <param name="Line">The single line to register (an <c>@import</c> or a pointer sentence).</param>
internal sealed record HostRegistration(string Path, HostStyle Style, string Line);

internal enum HostStyle
{
    /// <summary>One idempotent line under a shared <c># Tool instructions</c> heading (Claude Code).</summary>
    ToolInstructionsLine,

    /// <summary>One <c>&lt;!-- &gt;&gt;&gt; bellatrix &gt;&gt;&gt; --&gt;</c> … block holding only the line.</summary>
    MarkerBlock,
}

/// <summary>
/// Placement and frontmatter for one AI agent provider. Content is provider-neutral (the embedded
/// <c>Templates/</c>); an emitter only decides <b>where</b> each piece lands and <b>which frontmatter</b> the
/// provider's loader expects. Every path and field here was verified against the provider's official docs
/// (see <c>docs/architecture.md</c>, "Provider emitters").
/// </summary>
internal abstract class AgentEmitter
{
    /// <summary>The <c>--agent</c> value.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Skill directories this provider loads from the repo, its native one first. The planner picks the
    /// fewest directories that reach every selected provider, so one skill is not loaded twice.
    /// </summary>
    public abstract IReadOnlyList<string> SkillDirs { get; }

    /// <summary>True when the provider loads <c>.claude/agents/*.md</c>, so Claude's copy reaches it.</summary>
    public virtual bool ReadsClaudeAgents => false;

    /// <summary>
    /// True when the provider's native subagent wins a name clash with <c>.claude/agents</c>, so emitting a
    /// native copy next to Claude's never produces a duplicate.
    /// </summary>
    public virtual bool NativeAgentsShadowClaude => false;

    /// <summary>Repo-relative path of the provider's auto-loaded copy of the fragment.</summary>
    public abstract string FragmentPath(string profile);

    /// <summary>The fragment as this provider loads it (frontmatter, if any, + the shared body).</summary>
    public virtual string RenderFragment(string body, string profile) => body;

    /// <summary>The line or block written into the provider's human-owned instructions file, if any.</summary>
    public virtual HostRegistration? Host(string profile) => null;

    /// <summary>Repo-relative path of a subagent.</summary>
    public abstract string AgentPath(string slug);

    /// <summary>A subagent file in the provider's native format.</summary>
    public abstract string RenderAgent(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists);

    /// <summary>MCP config files this provider reads; BELLATRIX only ever removes its own stale keys there.</summary>
    public virtual IReadOnlyList<string> McpConfigFiles => Array.Empty<string>();

    /// <summary>Markers in a repo root that show this provider is in use (auto-detect).</summary>
    public abstract IReadOnlyList<string> DetectionMarkers { get; }

    /// <summary>Agent Skills (agentskills.io) frontmatter: <c>name</c> must equal the folder name.</summary>
    public static string RenderSkill(TemplateItem item)
        => $"---\nname: {item.Name}\ndescription: {item.Description}\n---\n\n{item.Body}";

    /// <summary>The fragment file name stem for a profile: <c>bellatrix</c>, or the custom template prefix.</summary>
    protected static string Stem(string profile) => profile;

    /// <summary>The shared, provider-neutral fragment location (Claude imports it; others may point at it).</summary>
    public static string SharedFragmentPath(string profile) => $".bellatrix/{Stem(profile)}.instructions.md";

    /// <summary>True for an isolated locator-resolution subagent (read-only + shell).</summary>
    protected static bool IsLocatorSpecialist(TemplateItem item)
        => item.Slug.EndsWith("-locator-specialist", StringComparison.Ordinal);

    /// <summary>The domain-matched locator specialist a generator agent may delegate to.</summary>
    protected static string? SpecialistFor(TemplateItem item, IReadOnlyCollection<string> locatorSpecialists)
    {
        if (IsLocatorSpecialist(item))
        {
            return null;
        }

        var domain = Commands.ScaffoldCommand.DomainOf(item.Slug);
        return locatorSpecialists.FirstOrDefault(s =>
            domain is not null && Commands.ScaffoldCommand.DomainOf(s) == domain
            && s.StartsWith(item.Slug.Split('-')[0] + "-", StringComparison.Ordinal));
    }

    /// <summary>
    /// The Claude-style tool names (<c>Read</c>, <c>Grep</c>, <c>Glob</c>, <c>Bash</c>, <c>Edit</c>, <c>Write</c>)
    /// a template declared, parsed from <c>[A, B]</c> or <c>A, B</c>; empty when it declared none.
    /// </summary>
    protected static IReadOnlyList<string> SourceTools(TemplateItem item)
        => string.IsNullOrWhiteSpace(item.Tools)
            ? Array.Empty<string>()
            : item.Tools.Trim().TrimStart('[').TrimEnd(']')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Trim('\'', '"'))
                .ToArray();

    /// <summary>True when the declared tools grant no file writes (a read-only agent).</summary>
    protected static bool IsReadOnly(TemplateItem item)
    {
        var tools = SourceTools(item);
        return IsLocatorSpecialist(item)
            || (tools.Count > 0 && !tools.Any(t => t is "Edit" or "Write" or "MultiEdit" or "NotebookEdit"));
    }
}
