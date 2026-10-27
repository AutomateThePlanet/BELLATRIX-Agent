namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// The per-agent manifests of one profile (R6/R8): <c>.bellatrix/manifest.&lt;agent&gt;.json</c> for
/// <c>bellatrix init</c>, <c>.bellatrix/&lt;prefix&gt;-manifest.&lt;agent&gt;.json</c> for a custom template folder
/// (<c>init --template-source &lt;dir&gt; --prefix &lt;prefix&gt;</c>). A file
/// shared by several agents (the fragment, a skill directory several providers read) is listed in every
/// manifest that relies on it. A pre-2.2 single manifest (<c>manifest.json</c>) only ever described Claude's
/// layout, so it is read as Claude's and renamed to <c>manifest.claude.json</c> on the next write.
/// </summary>
internal sealed class ManifestStore
{
    private readonly string _root;
    private readonly string _profile;
    private readonly Dictionary<string, ScaffoldManifest> _byAgent;
    private readonly bool _hasLegacy;

    private ManifestStore(string root, string profile, Dictionary<string, ScaffoldManifest> byAgent, bool hasLegacy)
    {
        _root = root;
        _profile = profile;
        _byAgent = byAgent;
        _hasLegacy = hasLegacy;
    }

    public static string LegacyPath(string root, string profile)
        => Path.Combine(root, ".bellatrix", Stem(profile) + "json");

    public static string PathFor(string root, string profile, string agent)
        => Path.Combine(root, ".bellatrix", Stem(profile) + agent + ".json");

    /// <summary>Repo-relative path of an agent's manifest (for reporting).</summary>
    public static string RelativePathFor(string profile, string agent)
        => ".bellatrix/" + Stem(profile) + agent + ".json";

    /// <summary><c>manifest.</c> for BELLATRIX's own templates, <c>&lt;prefix&gt;-manifest.</c> for a custom template folder.</summary>
    private static string Stem(string profile)
        => CustomTemplateSource.IsCustom(profile) ? profile + "-manifest." : "manifest.";

    public static ManifestStore Load(string root, string profile, IEnumerable<string> agentNames)
    {
        var byAgent = new Dictionary<string, ScaffoldManifest>(StringComparer.Ordinal);
        foreach (var agent in agentNames)
        {
            var path = PathFor(root, profile, agent);
            if (File.Exists(path))
            {
                byAgent[agent] = ScaffoldManifest.Load(path);
            }
        }

        var legacy = LegacyPath(root, profile);
        var hasLegacy = File.Exists(legacy);
        if (hasLegacy && !byAgent.ContainsKey("claude"))
        {
            byAgent["claude"] = ScaffoldManifest.Load(legacy);
        }

        return new ManifestStore(root, profile, byAgent, hasLegacy);
    }

    /// <summary>True when the agent has been scaffolded into this repo before.</summary>
    public bool Has(string agent) => _byAgent.ContainsKey(agent);

    public ScaffoldManifest? Get(string agent) => _byAgent.TryGetValue(agent, out var m) ? m : null;

    /// <summary>The language recorded by the given agents' manifests (first one that has it), if any.</summary>
    public string? RecordedLanguage(IEnumerable<string> agents)
        => agents.Select(Get).Select(m => m?.Language).FirstOrDefault(l => !string.IsNullOrEmpty(l))
           ?? (agents.Any(Has) ? "csharp" : null); // scaffolded before 2.5 → C#

    /// <summary>Every hash any manifest recorded for a path (each is content BELLATRIX itself wrote).</summary>
    public IReadOnlyCollection<string> RecordedHashes(string relative)
        => _byAgent.Values.Select(m => m.HashFor(relative)).OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>Agents other than <paramref name="except"/> whose manifest lists the path.</summary>
    public IEnumerable<string> OtherOwners(string relative, IReadOnlyCollection<string> except)
        => _byAgent.Where(kv => !except.Contains(kv.Key) && kv.Value.HashFor(relative) is not null).Select(kv => kv.Key);

    /// <summary>Writes the given agents' manifests and retires the legacy single manifest.</summary>
    public void Save(IReadOnlyDictionary<string, ScaffoldManifest> manifests)
    {
        foreach (var (agent, manifest) in manifests)
        {
            manifest.Save(PathFor(_root, _profile, agent));
            _byAgent[agent] = manifest;
        }

        var legacy = LegacyPath(_root, _profile);
        if (_hasLegacy && File.Exists(legacy))
        {
            var claudePath = PathFor(_root, _profile, "claude");
            if (!File.Exists(claudePath))
            {
                File.Copy(legacy, claudePath);
            }

            File.Delete(legacy);
        }
    }
}
