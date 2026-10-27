using Bellatrix.Cli.Scaffolding.Emitters;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// Resolves which AI agents a scaffold run targets: an explicit <c>--agent a,b</c> list (with the
/// <c>windsurf</c>→<c>devin</c> rename and the deprecated <c>--target</c> alias), else the agents already
/// scaffolded in the repo (their manifests), else exactly one agent detected from repo markers. It never
/// guesses: nothing detected, or several, is a <c>usage</c> error listing the choices.
/// </summary>
internal static class AgentSelection
{
    /// <summary>Every supported agent, in the order results are reported.</summary>
    public static readonly IReadOnlyList<AgentEmitter> All = new AgentEmitter[]
    {
        new ClaudeEmitter(), new CopilotEmitter(), new CursorEmitter(),
        new DevinEmitter(), new CodexEmitter(), new GeminiEmitter(),
        new AntigravityEmitter(), new KiroEmitter(), new OpenCodeEmitter(),
    };

    /// <summary>Accepted <c>--agent</c> spellings that map to a canonical name without a notice.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["agy"] = "antigravity",
    };

    public static IReadOnlyList<string> Names => All.Select(e => e.Name).ToArray();

    public static AgentEmitter Get(string name) => All.Single(e => e.Name == name);

    /// <summary>
    /// Parses a comma-separated <c>--agent</c> value. <c>windsurf</c> is accepted for <c>devin</c> (renamed to
    /// Devin Desktop on 2026-06-02) with a stderr notice, and <c>agy</c> for <c>antigravity</c>. Returns names in
    /// canonical order, de-duplicated.
    /// </summary>
    public static IReadOnlyList<string> Parse(string raw, TextWriter? notices = null)
    {
        var chosen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = token.ToLowerInvariant();
            if (name == "windsurf")
            {
                (notices ?? Console.Error).WriteLine(
                    "[bellatrix init] notice: 'windsurf' is now Devin Desktop — using --agent devin.");
                name = "devin";
            }
            else if (Aliases.TryGetValue(name, out var canonical))
            {
                name = canonical;
            }

            if (!Names.Contains(name))
            {
                throw new CliException("usage", exitCode: 2, message: $"Unknown --agent '{token}'. One of: {string.Join(", ", Names)}.");
            }

            chosen.Add(name);
        }

        if (chosen.Count == 0)
        {
            throw new CliException("usage", exitCode: 2, message: $"--agent needs at least one of: {string.Join(", ", Names)}.");
        }

        return Names.Where(chosen.Contains).ToArray();
    }

    /// <summary>
    /// Agents for a run with no <c>--agent</c>: those with a manifest already in <c>.bellatrix/</c> (a re-run
    /// refreshes what was scaffolded), else the single agent whose markers exist in <paramref name="root"/>.
    /// </summary>
    public static IReadOnlyList<string> Detect(string root, string profile)
    {
        var scaffolded = Names.Where(n => File.Exists(ManifestStore.PathFor(root, profile, n))).ToArray();
        if (scaffolded.Length > 0)
        {
            return scaffolded;
        }

        if (File.Exists(ManifestStore.LegacyPath(root, profile)))
        {
            return new[] { "claude" }; // a pre-2.2 manifest only ever described Claude's layout
        }

        var detected = All
            .Where(e => e.DetectionMarkers.Any(m =>
                File.Exists(Path.Combine(root, m)) || Directory.Exists(Path.Combine(root, m))))
            .Select(e => e.Name)
            .ToArray();

        return detected.Length switch
        {
            1 => detected,
            0 => throw new CliException("usage", exitCode: 2, message:
                $"No AI agent detected in '{root}'. Pass --agent <{string.Join("|", Names)}> (comma-separated for several)."),
            _ => throw new CliException("usage", exitCode: 2, message:
                $"Several AI agents detected ({string.Join(", ", detected)}). Pass --agent with the ones to scaffold, e.g. --agent {string.Join(",", detected)}."),
        };
    }
}
