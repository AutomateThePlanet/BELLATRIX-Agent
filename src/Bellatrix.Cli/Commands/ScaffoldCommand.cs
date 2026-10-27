using System.Text;
using Bellatrix.Cli.Output;
using Bellatrix.Cli.Scaffolding;
using Bellatrix.Cli.Scaffolding.Emitters;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// Scaffolds the BELLATRIX toolkit (desktop/web/mobile skills, locator-specialist and generator subagents, and
/// the instructions fragment) into a consumer repo in each selected AI agent's NATIVE format, obeying the
/// shared-namespace contract. Content is provider-neutral (<c>Templates/</c>); placement and frontmatter come
/// from one <see cref="AgentEmitter"/> per agent (<see cref="AgentSelection.All"/>):
/// <list type="bullet">
/// <item>R1/R2/R3 — the fragment lands in the agent's own auto-loaded file; where only a human-owned file
/// exists, exactly one <c>@import</c> line (Claude) or one marker block (Codex <c>AGENTS.md</c>, Gemini
/// <c>GEMINI.md</c>) is written, never anything else.</item>
/// <item>R4/R5 — prefixed skills and uniquely named subagents in the agent's native directories. A skill
/// directory several agents read is written once (see <see cref="PlanSkillDirs"/>).</item>
/// <item>R6/R8 — one manifest per agent (<c>.bellatrix/manifest.&lt;agent&gt;.json</c>); a file edited by the
/// developer is skipped unless <c>--regenerate</c>; a re-run for one agent never touches another agent's paths.</item>
/// <item>R7 — no MCP server is registered; only BELLATRIX's own stale keys are removed from the agent's MCP config.</item>
/// </list>
/// With <c>--template-source &lt;dir&gt; [--prefix &lt;p&gt;]</c> the caller supplies the template folder instead of the
/// built-in BELLATRIX templates (see <see cref="CustomTemplateSource"/>): same contract, rendered under the
/// caller's own namespace (fragment, manifests, marker block and skill/agent names all use the prefix), never
/// touching BELLATRIX-owned files, and leaving MCP configuration alone.
///
/// Options: <c>--agent a,b</c> (default: auto-detect; <c>--target</c> is a deprecated alias, <c>windsurf</c> →
/// <c>devin</c>), <c>--domains</c> (desktop,web,mobile), <c>--language</c>, <c>--out &lt;dir&gt;</c>,
/// <c>--regenerate</c>/<c>--force</c>, <c>--dry-run</c>, <c>--template-source &lt;dir&gt;</c>, <c>--prefix &lt;p&gt;</c>.
/// </summary>
internal static class ScaffoldCommand
{

    /// <summary>MCP server keys a previous <c>bellatrix init</c> (or the legacy servers) may have registered.</summary>
    internal static readonly string[] OwnedMcpKeys = { "bellatrix-desktop-mcp", "bellatrix-web-mcp", "bellatrix-mobile-mcp" };

    /// <summary>Files pre-2.2 <c>init</c> wrote MCP keys into for every target; always cleaned (never created).</summary>
    private static readonly string[] LegacyMcpConfigFiles = { ".mcp.json", ".vscode/mcp.json", ".claude/settings.json" };

    /// <summary>The scaffoldable BELLATRIX domains. <c>--domains</c> selects a subset (default: all).</summary>
    private static readonly string[] KnownDomains = { "desktop", "web", "mobile" };

    public static async Task<int> Run(CommandContext ctx)
    {
        await Task.CompletedTask;
        var root = ctx.Opt("out") ?? Directory.GetCurrentDirectory();

        // A caller-supplied template folder renders under its own prefix (the profile); otherwise BELLATRIX's own.
        var profile = CustomTemplateSource.BuiltInProfile;
        string? templateSource = null;
        if (ctx.Opt("template-source") is { } rawSource)
        {
            templateSource = CustomTemplateSource.Validate(rawSource);
            profile = CustomTemplateSource.ResolvePrefix(templateSource, ctx.Opt("prefix"));
        }
        else if (ctx.Opt("prefix") is not null)
        {
            throw new CliException("usage", "--prefix requires --template-source <dir>.");
        }

        var agentOption = ctx.Opt("agent");
        if (agentOption is null && ctx.Opt("target") is { } target)
        {
            Console.Error.WriteLine("[bellatrix init] notice: --target is deprecated; use --agent (same values, comma-separated).");
            agentOption = target;
        }

        var agents = agentOption is null
            ? AgentSelection.Detect(root, profile)
            : AgentSelection.Parse(agentOption);

        var domains = ParseDomains(ctx.Opt("domains"));
        // A custom template folder is rendered verbatim and records no language, so it skips detection (and its
        // "using --language csharp" notice).
        var language = CustomTemplateSource.IsCustom(profile)
            ? LanguageSelection.Known[0]
            : ctx.Opt("language") is { } rawLanguage
                ? LanguageSelection.Parse(rawLanguage)
                : LanguageSelection.Detect(root, ManifestStore.Load(root, profile, AgentSelection.Names).RecordedLanguage(agents));
        var regenerate = ctx.Options.ContainsKey("regenerate") || ctx.Options.ContainsKey("force");
        var dryRun = ctx.Options.ContainsKey("dry-run");

        var result = Scaffold(root, string.Join(",", agents), regenerate, profile, templateSource, domains, dryRun, language);
        return JsonOutput.Success(result);
    }

    /// <summary>
    /// Parses the comma-separated <c>--domains</c> selector into a validated set. Null/blank → all known
    /// domains (back-compat: <c>bellatrix init</c> with no selector scaffolds everything available).
    /// </summary>
    internal static IReadOnlySet<string> ParseDomains(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HashSet<string>(KnownDomains, StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Array.IndexOf(KnownDomains, token.ToLowerInvariant()) < 0)
            {
                throw new CliException("usage", $"Unknown --domains entry '{token}'. One of: {string.Join(", ", KnownDomains)}.");
            }

            set.Add(token);
        }

        return set;
    }

    /// <summary>The domain a template (or an emitted path) belongs to; null = shared (always emitted).</summary>
    internal static string? DomainOf(string slug)
    {
        foreach (var domain in KnownDomains)
        {
            if (slug.Contains($"-{domain}", StringComparison.OrdinalIgnoreCase))
            {
                return domain;
            }
        }

        return null;
    }

    /// <summary>What one agent got from a run. Paths are repo-relative, forward-slash.</summary>
    internal sealed record AgentOutcome(
        string[] emitted,
        string[] skipped,
        string[] removed,
        string[] unchanged,
        string? registered,
        string manifest);

    /// <summary>
    /// Outcome of a scaffold run; the success JSON shape is the consumer contract. <c>agents</c>, <c>domains</c>,
    /// <c>perAgent</c>, <c>decisions</c>, <c>removed</c> and <c>dryRun</c> are the 2.2 surface; <c>target</c>
    /// (first agent), <c>emitted</c>/<c>skipped</c> (distinct paths across agents), <c>registered</c> (first agent's
    /// fragment), <c>mcpMerged</c> (always empty), <c>mandatesDelegation</c> and <c>mcpRemoved</c> keep the
    /// pre-2.2 shape.
    /// </summary>
    internal sealed record ScaffoldResult(
        string target,
        string[] emitted,
        string[] skipped,
        string registered,
        string[] mcpMerged,
        bool mandatesDelegation,
        string[] mcpRemoved,
        string[] agents,
        string[] domains,
        bool dryRun,
        string[] removed,
        IReadOnlyDictionary<string, AgentOutcome> perAgent,
        string[] decisions,
        string language,
        string[] unsupported,
        string[] warnings);

    /// <summary>One planned file: its content and the agents that rely on it.</summary>
    private sealed class PlannedFile
    {
        public required string Content { get; init; }
        public List<string> Owners { get; } = new();
    }

    /// <summary>
    /// Testable core. <paramref name="agents"/> is a comma-separated <c>--agent</c> value. Pure filesystem work —
    /// no driver, no model, no credentials; with <paramref name="dryRun"/> nothing is written at all.
    /// <paramref name="templateSource"/> is required for a custom <paramref name="profile"/> (= its prefix) and
    /// ignored for the built-in BELLATRIX profile.
    /// </summary>
    internal static ScaffoldResult Scaffold(
        string root, string agents, bool regenerate, string profile = CustomTemplateSource.BuiltInProfile,
        string? templateSource = null, IReadOnlySet<string>? domains = null,
        bool dryRun = false, string language = TemplateCatalog.DefaultLanguage)
    {
        var selected = AgentSelection.Parse(agents);
        var emitters = selected.Select(AgentSelection.Get).ToArray();
        var activeDomains = domains ?? new HashSet<string>(KnownDomains, StringComparer.OrdinalIgnoreCase);
        var store = ManifestStore.Load(root, profile, AgentSelection.Names);
        var decisions = new List<string>();

        var custom = CustomTemplateSource.IsCustom(profile);
        var resolvedSource = custom
            ? CustomTemplateSource.Validate(templateSource ?? throw new CliException("usage", "--template-source <dir> is required."))
            : null;

        var (loaded, gaps) = TemplateCatalog.LoadWithGaps(profile, resolvedSource, language);
        var items = loaded
            .Where(i => DomainOf(i.Slug) is not { } d || activeDomains.Contains(d))
            .ToArray();
        var unsupported = gaps.Where(g => DomainOf(g) is not { } d || activeDomains.Contains(d)).ToArray();
        var warnings = new List<string>();
        foreach (var domain in KnownDomains.Where(activeDomains.Contains))
        {
            if (LanguageSelection.Limits.TryGetValue((language, domain), out var limit))
            {
                warnings.Add(limit);
            }
        }

        if (unsupported.Length > 0)
        {
            warnings.Add($"No {language} version of: {string.Join(", ", unsupported)} — skipped.");
        }

        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"[bellatrix init] warning: {warning}");
        }
        var specialists = items
            .Where(i => i.Kind == TemplateKind.Agent && i.Slug.EndsWith("-locator-specialist", StringComparison.Ordinal))
            .Select(i => i.Slug)
            .ToArray();

        // ---- plan: path → content + owning agents (ordered for stable output) ----
        var plan = new Dictionary<string, PlannedFile>(StringComparer.Ordinal);
        var order = new List<string>();
        void Add(string path, string content, string owner)
        {
            if (!plan.TryGetValue(path, out var file))
            {
                file = new PlannedFile { Content = content };
                plan[path] = file;
                order.Add(path);
            }
            else if (file.Content != content)
            {
                throw new InvalidOperationException($"Two agents planned different content for '{path}'.");
            }

            if (!file.Owners.Contains(owner))
            {
                file.Owners.Add(owner);
            }
        }

        var fragment = items.FirstOrDefault(i => i.Kind == TemplateKind.Fragment);
        if (fragment is not null)
        {
            var body = TemplateCatalog.FilterDomainBlocks(fragment.Body, activeDomains);
            foreach (var emitter in emitters)
            {
                Add(emitter.FragmentPath(profile), emitter.RenderFragment(body, profile), emitter.Name);
            }
        }

        var claudeActive = selected.Contains("claude") || store.Has("claude");
        var skillDirs = PlanSkillDirs(emitters, claudeActive, decisions);
        foreach (var skill in items.Where(i => i.Kind == TemplateKind.Skill))
        {
            var content = AgentEmitter.RenderSkill(skill);
            foreach (var emitter in emitters)
            {
                Add($"{skillDirs[emitter.Name]}/{skill.Slug}/SKILL.md", content, emitter.Name);
            }
        }

        var claude = AgentSelection.Get("claude");
        foreach (var emitter in emitters)
        {
            var reuseClaude = emitter.Name != "claude" && claudeActive
                && emitter.ReadsClaudeAgents && !emitter.NativeAgentsShadowClaude;
            if (emitter.Name != "claude")
            {
                decisions.Add(reuseClaude
                    ? $"{emitter.Name} subagents: reuse .claude/agents (read natively; no duplicate copy)"
                    : $"{emitter.Name} subagents: native {AgentsDir(emitter)}");
            }

            foreach (var agent in items.Where(i => i.Kind == TemplateKind.Agent))
            {
                var source = reuseClaude ? claude : emitter;
                Add(source.AgentPath(agent.Slug), source.RenderAgent(agent, specialists), emitter.Name);
            }
        }

        // ---- write through the hash gate (R6) ----
        var perAgentFiles = selected.ToDictionary(a => a, _ => new List<ManifestEntry>(), StringComparer.Ordinal);
        var outcome = selected.ToDictionary(a => a, _ => (Emitted: new List<string>(), Skipped: new List<string>(),
            Removed: new List<string>(), Unchanged: new List<string>()), StringComparer.Ordinal);
        var emitted = new List<string>();
        var skipped = new List<string>();

        foreach (var relative in order)
        {
            var file = plan[relative];
            var full = FullPath(root, relative);
            var bytes = Encoding.UTF8.GetBytes(file.Content);
            var newHash = ScaffoldManifest.Sha256Hex(bytes);
            var recordHash = newHash;
            string bucket;

            if (File.Exists(full))
            {
                var diskHash = ScaffoldManifest.Sha256HexOfFile(full);
                var recorded = store.RecordedHashes(relative);
                var userEdited = recorded.Count > 0 && !recorded.Contains(diskHash);

                if (!regenerate && userEdited)
                {
                    bucket = "skipped";
                    recordHash = diskHash; // keep tracking the user's bytes (pre-2.2 behavior)
                    Console.Error.WriteLine($"[bellatrix init] skipped (user-edited): {relative}");
                }
                else if (diskHash == newHash)
                {
                    bucket = "unchanged"; // idempotent: identical content leaves bytes untouched
                }
                else
                {
                    bucket = "emitted";
                }
            }
            else
            {
                bucket = "emitted";
            }

            if (bucket == "emitted" && !dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, bytes);
            }

            if (bucket == "emitted") emitted.Add(relative);
            if (bucket == "skipped") skipped.Add(relative);

            foreach (var owner in file.Owners)
            {
                perAgentFiles[owner].Add(new ManifestEntry { Path = relative, Sha256 = recordHash });
                var o = outcome[owner];
                (bucket switch { "emitted" => o.Emitted, "skipped" => o.Skipped, _ => o.Unchanged }).Add(relative);
            }
        }

        // ---- retire files an agent owned before but no longer plans ----
        var removed = new List<string>();
        foreach (var agent in selected)
        {
            var previous = store.Get(agent);
            if (previous is null)
            {
                continue;
            }

            foreach (var entry in previous.Files)
            {
                if (plan.TryGetValue(entry.Path, out var planned) && planned.Owners.Contains(agent))
                {
                    continue;
                }

                // Another selected domain's run owns it; leave it and keep tracking it.
                if (DomainOf(entry.Path) is { } d && !activeDomains.Contains(d))
                {
                    perAgentFiles[agent].Add(entry);
                    continue;
                }

                // Still relied on by another agent (this run or an earlier one): just drop our claim.
                if (plan.ContainsKey(entry.Path) || store.OtherOwners(entry.Path, selected).Any())
                {
                    continue;
                }

                var full = FullPath(root, entry.Path);
                if (!File.Exists(full))
                {
                    continue;
                }

                if (!regenerate && ScaffoldManifest.Sha256HexOfFile(full) != entry.Sha256)
                {
                    outcome[agent].Skipped.Add(entry.Path); // user-edited: never deleted
                    skipped.Add(entry.Path);
                    continue;
                }

                if (!dryRun)
                {
                    File.Delete(full);
                    DeleteEmptyParents(root, Path.GetDirectoryName(full)!);
                }

                outcome[agent].Removed.Add(entry.Path);
                if (!removed.Contains(entry.Path))
                {
                    removed.Add(entry.Path);
                }
            }
        }

        if (!dryRun)
        {
            store.Save(perAgentFiles.ToDictionary(
                kv => kv.Key,
                kv => new ScaffoldManifest { Version = "2.5", Target = kv.Key, Language = custom ? null : language, Files = kv.Value },
                StringComparer.Ordinal));
        }

        // ---- R1/R3: one line or one marker block in the agent's own instructions file ----
        var registeredHost = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var emitter in emitters)
        {
            var host = emitter.Host(profile);
            registeredHost[emitter.Name] = host?.Path;
            if (host is null)
            {
                continue;
            }

            var hostPath = FullPath(root, host.Path);
            _ = host.Style == HostStyle.ToolInstructionsLine
                ? ClaudeMdRegistrar.Register(hostPath, host.Line, dryRun)
                : MarkerBlockRegistrar.Register(hostPath, profile, host.Line, dryRun);
        }

        // ---- R7 (no MCP servers): remove only BELLATRIX's own stale keys; a custom template folder leaves MCP alone ----
        var mcpRemoved = new List<string>();
        if (!custom)
        {
            var configs = LegacyMcpConfigFiles.Concat(emitters.SelectMany(e => e.McpConfigFiles)).Distinct(StringComparer.Ordinal);
            foreach (var relativeConfig in configs)
            {
                var configPath = FullPath(root, relativeConfig);
                var cleaned = relativeConfig.EndsWith(".toml", StringComparison.Ordinal)
                    ? TomlMcpConfigCleaner.RemoveOwnedKeys(configPath, OwnedMcpKeys, dryRun)
                    : McpConfigCleaner.RemoveOwnedKeys(configPath, OwnedMcpKeys, dryRun);
                if (cleaned)
                {
                    mcpRemoved.Add(relativeConfig);
                }
            }
        }

        var perAgent = selected.ToDictionary(
            a => a,
            a => new AgentOutcome(
                outcome[a].Emitted.ToArray(),
                outcome[a].Skipped.ToArray(),
                outcome[a].Removed.ToArray(),
                outcome[a].Unchanged.ToArray(),
                registeredHost[a],
                ManifestStore.RelativePathFor(profile, a)),
            StringComparer.Ordinal);

        return new ScaffoldResult(
            target: selected[0],
            emitted: emitted.ToArray(),
            skipped: skipped.Distinct().ToArray(),
            registered: emitters[0].FragmentPath(profile),
            mcpMerged: Array.Empty<string>(), // kept for JSON-shape stability; nothing is ever merged
            mandatesDelegation: true,
            mcpRemoved: mcpRemoved.ToArray(),
            agents: selected.ToArray(),
            domains: KnownDomains.Where(activeDomains.Contains).ToArray(),
            dryRun: dryRun,
            removed: removed.ToArray(),
            perAgent: perAgent,
            decisions: decisions.ToArray(),
            language: custom ? "none" : language,
            unsupported: unsupported,
            warnings: warnings.ToArray());
    }

    /// <summary>The folder an agent's subagents live in (Antigravity nests each one in its own folder).</summary>
    private static string AgentsDir(AgentEmitter emitter)
    {
        var path = emitter.AgentPath("x");
        return path[..path.IndexOf("/x", StringComparison.Ordinal)];
    }

    /// <summary>
    /// Chooses the fewest skill directories that reach every selected agent: <c>.claude/skills</c> first
    /// when Claude is selected or already scaffolded, then greedily the directory read by the most still-uncovered
    /// agents (ties → the first uncovered agent's native order). Returns agent → the directory written for it, and
    /// records a decision per directory plus a warning for any agent that will read two copies.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> PlanSkillDirs(
        IReadOnlyList<AgentEmitter> emitters, bool claudeActive, List<string>? decisions = null)
    {
        var chosen = new List<string>();
        if (claudeActive)
        {
            chosen.Add(".claude/skills");
        }

        var uncovered = emitters.Where(e => !e.SkillDirs.Any(chosen.Contains)).ToList();
        while (uncovered.Count > 0)
        {
            var candidates = uncovered.SelectMany(e => e.SkillDirs).Distinct(StringComparer.Ordinal).ToList();
            var best = candidates
                .Select((dir, index) => (dir, index, reach: uncovered.Count(e => e.SkillDirs.Contains(dir))))
                .OrderByDescending(c => c.reach)
                .ThenBy(c => c.index)
                .First().dir;
            chosen.Add(best);
            uncovered.RemoveAll(e => e.SkillDirs.Contains(best));
        }

        var assignment = emitters.ToDictionary(
            e => e.Name, e => chosen.First(e.SkillDirs.Contains), StringComparer.Ordinal);

        if (decisions is not null)
        {
            foreach (var dir in chosen)
            {
                var readers = emitters.Where(e => assignment[e.Name] == dir).Select(e => e.Name).ToArray();
                if (readers.Length > 0)
                {
                    decisions.Add($"skills: {dir} (for {string.Join(", ", readers)})");
                }
            }

            foreach (var emitter in emitters)
            {
                var seen = chosen.Where(emitter.SkillDirs.Contains).ToArray();
                if (seen.Length > 1)
                {
                    decisions.Add($"warning: {emitter.Name} reads {string.Join(" and ", seen)} — it may list each bellatrix skill twice (no single-copy layout exists for this agent set)");
                }
            }
        }

        return assignment;
    }

    private static string FullPath(string root, string relative)
        => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Removes now-empty directories up to (not including) the repo root, e.g. a retired skill folder.</summary>
    private static void DeleteEmptyParents(string root, string directory)
    {
        var stop = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var current = Path.GetFullPath(directory);
        while (current.Length > stop.Length && Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
        {
            Directory.Delete(current);
            current = Path.GetDirectoryName(current)!;
        }
    }
}
