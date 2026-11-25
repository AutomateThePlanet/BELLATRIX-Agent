using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Config;

/// <summary>Asks the user one question and returns the answer (null = end of input).</summary>
internal interface IPrompter
{
    string? Ask(string question);
}

/// <summary>Prompts on stderr (stdout carries the JSON result) and reads stdin.</summary>
internal sealed class ConsolePrompter : IPrompter
{
    public string? Ask(string question)
    {
        Console.Error.Write(question);
        return Console.In.ReadLine();
    }
}

/// <summary>What <c>bellatrix init</c> did with <c>.bellatrix/config.json</c> (the <c>config</c> field of its JSON).</summary>
internal sealed record ConfigOutcome(string path, string status, JsonObject? values, bool commit, string[] warnings);

/// <summary>
/// The <c>.bellatrix/config.json</c> part of <c>bellatrix init</c> (R9). Runs before any file is scaffolded so an
/// ambiguous project fails the run without side effects:
/// <list type="number">
/// <item>An existing, valid file is the base and is never overwritten without <c>--regenerate</c>: only missing values
/// (a domain, a project, a test-case folder) are merged in, and a flag that disagrees with a recorded value is
/// reported, not applied. An invalid file is left untouched (a warning; the skills stop and ask).</item>
/// <item>A domain without a project gets one from <c>--project</c>, else from <see cref="ProjectDiscovery"/>: one
/// candidate (or exactly one whose name has the domain word) is proposed; in a terminal the user confirms, picks or
/// types a path; otherwise several candidates are a usage error (exit 2) and none leaves the domain empty with a
/// warning. Nothing defaults to a sample project name.</item>
/// <item>The test-case folder: <c>--test-cases-dir</c>, else SPECTRA's <c>spectra.config.json</c> <c>tests.dir</c>, else
/// <c>test-cases</c>.</item>
/// </list>
/// </summary>
internal static class InitConfig
{
    /// <summary>Test hook: replaces the console prompter.</summary>
    internal static IPrompter? PrompterOverride { get; set; }

    /// <summary>Test hook: forces interactive (true) or non-interactive (false) mode.</summary>
    internal static bool? InteractiveOverride { get; set; }

    public const string CommitNote = "Commit .bellatrix/config.json: it tells your AI agent (and your team) which test project and test-case folder to use.";

    private static readonly Regex PerDomain = new(@"^\s*(desktop|web|mobile)\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string[]> DomainWords = new(StringComparer.Ordinal)
    {
        ["desktop"] = new[] { "desktop" },
        ["web"] = new[] { "web" },
        ["mobile"] = new[] { "mobile", "android", "ios" },
    };

    /// <summary>True when init may ask questions: a real terminal on all three streams, no CI, no opt-out.</summary>
    public static bool IsInteractive(bool noInteraction, bool dryRun)
    {
        if (InteractiveOverride is { } forced)
        {
            return forced && !noInteraction && !dryRun;
        }

        return !noInteraction && !dryRun
               && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
               && !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;
    }

    /// <summary>
    /// Parses a per-domain flag: a plain value (key <c>""</c>: applies to every domain) or
    /// <c>desktop=&lt;v&gt;,web=&lt;v&gt;</c>. Domains outside <paramref name="domains"/> are a usage error.
    /// </summary>
    internal static Dictionary<string, string> ParsePerDomain(string? raw, string flag, IReadOnlyCollection<string> domains)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw) || raw == "true")
        {
            if (raw == "true")
            {
                throw new CliException("usage", $"--{flag} needs a value: --{flag} <path> or --{flag} desktop=<path>,web=<path>.", exitCode: 2);
            }

            return result;
        }

        if (!PerDomain.IsMatch(raw))
        {
            result[""] = raw.Trim();
            return result;
        }

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            var domain = eq > 0 ? part[..eq].Trim().ToLowerInvariant() : "";
            var value = eq > 0 ? part[(eq + 1)..].Trim() : "";
            if (!DomainWords.ContainsKey(domain) || value.Length == 0)
            {
                throw new CliException("usage", $"--{flag} '{part}': expected <domain>=<value> with domain desktop, web or mobile.", exitCode: 2);
            }

            if (!domains.Contains(domain))
            {
                throw new CliException("usage", $"--{flag} names {domain}, which is not in --domains ({string.Join(",", domains)}).", exitCode: 2);
            }

            result[domain] = value;
        }

        return result;
    }

    /// <summary>The planned config: the document to write (null = leave the file alone) and the outcome to report.</summary>
    internal sealed record Plan(JsonObject? Document, ConfigOutcome Outcome)
    {
        public bool Write => Outcome.status is "created" or "merged" or "replaced";
    }

    public static Plan Prepare(
        string root, string language, IReadOnlyCollection<string> domains, string? projectFlag, string? testCasesFlag,
        bool regenerate, bool interactive, IPrompter? prompter = null)
    {
        prompter ??= PrompterOverride ?? new ConsolePrompter();
        var ordered = BellatrixConfig.Domains.Where(domains.Contains).ToArray();
        var projects = ParsePerDomain(projectFlag, "project", ordered);
        var testCases = ParsePerDomain(testCasesFlag, "test-cases-dir", ordered);
        foreach (var (_, dir) in testCases)
        {
            ValidateRelative(dir, "--test-cases-dir");
        }

        var warnings = new List<string>();
        var existing = BellatrixConfig.Load(root);
        if (existing.Exists && !existing.Valid && !regenerate)
        {
            warnings.Add($"{BellatrixConfig.RelativePath} is invalid and was left unchanged: "
                         + string.Join("; ", existing.Errors.Select(e => $"{e.path}: {e.message}"))
                         + ". Fix it (check with `bellatrix config validate`) or re-run init with --regenerate.");
            return new Plan(null, new ConfigOutcome(BellatrixConfig.RelativePath, "kept-invalid", null, true, warnings.ToArray()));
        }

        var keep = existing.Valid && !regenerate;
        var before = keep ? BellatrixConfig.Serialize(existing.Document!) : null;
        var doc = keep
            ? (JsonObject)existing.Document!.DeepClone()
            : BellatrixConfig.Create(testCases.GetValueOrDefault("") ?? SpectraTestsDir(root) ?? BellatrixConfig.DefaultTestCasesDir);

        // Top-level test-case folder.
        if (testCases.TryGetValue("", out var topDir) && ProjectSettings.Str(doc["testCasesDir"]) is { } recordedTop && recordedTop != topDir)
        {
            warnings.Add($"testCasesDir stays \"{recordedTop}\" (recorded in {BellatrixConfig.RelativePath}); re-run with --regenerate to use \"{topDir}\".");
        }

        var domainsNode = doc["domains"] as JsonObject ?? new JsonObject();
        doc["domains"] = domainsNode;
        IReadOnlyList<ProjectCandidate>? candidates = null;

        foreach (var domain in ordered)
        {
            var node = domainsNode[domain] as JsonObject;
            if (node is null)
            {
                node = new JsonObject();
                domainsNode[domain] = node;
            }

            var recorded = ProjectSettings.From(node["project"]);
            ProjectSettings? chosen = null;
            var flagValue = projects.GetValueOrDefault(domain) ?? projects.GetValueOrDefault("");
            if (flagValue is not null)
            {
                var fromFlag = ProjectDiscovery.Describe(root, flagValue);
                if (recorded is not null && recorded.Path != fromFlag.Path)
                {
                    warnings.Add($"{domain} project stays {recorded.Path} (recorded in {BellatrixConfig.RelativePath}); re-run with --regenerate to use {fromFlag.Path}.");
                }
                else if (recorded is null)
                {
                    chosen = fromFlag;
                }
            }
            else if (recorded is null)
            {
                candidates ??= ProjectDiscovery.Find(root, language);
                chosen = Choose(domain, candidates.Where(c => c.Domain == domain).Select(c => c.Project).ToArray(),
                    root, interactive, prompter, warnings);
            }

            if (chosen is not null)
            {
                node["project"] = chosen.ToJson();
            }

            if (testCases.TryGetValue(domain, out var domainDir))
            {
                if (ProjectSettings.Str(node["testCasesDir"]) is { } recordedDir && recordedDir != domainDir)
                {
                    warnings.Add($"{domain} testCasesDir stays \"{recordedDir}\"; re-run with --regenerate to use \"{domainDir}\".");
                }
                else
                {
                    node["testCasesDir"] = domainDir;
                }
            }

            // Keep the file tidy: an empty domain object means "nothing recorded yet".
            if (node.Count == 0)
            {
                domainsNode.Remove(domain);
            }
        }

        string status;
        if (!existing.Exists)
        {
            status = "created";
        }
        else if (!keep)
        {
            status = "replaced";
        }
        else
        {
            status = BellatrixConfig.Serialize(doc) == before ? "unchanged" : "merged";
        }

        return new Plan(doc, new ConfigOutcome(BellatrixConfig.RelativePath, status, doc, true, warnings.ToArray()));
    }

    /// <summary>Picks a domain's project from its candidates (and the user, in a terminal).</summary>
    private static ProjectSettings? Choose(
        string domain, ProjectSettings[] candidates, string root, bool interactive, IPrompter prompter, List<string> warnings)
    {
        var proposal = candidates.Length switch
        {
            1 => candidates[0],
            > 1 => TieBreak(domain, candidates),
            _ => null,
        };

        if (interactive)
        {
            return Ask(domain, candidates, proposal, root, prompter, warnings);
        }

        if (proposal is not null)
        {
            return proposal;
        }

        if (candidates.Length > 1)
        {
            throw new CliException("usage",
                $"Several BELLATRIX {domain} test projects found: {string.Join(", ", candidates.Select(c => c.Path))}. "
                + $"Pick one: bellatrix init --project {domain}={candidates[0].Path} (or run init in a terminal to choose).",
                exitCode: 2);
        }

        warnings.Add($"No BELLATRIX {domain} test project found (a project referencing the BELLATRIX {domain} package). "
                     + $"Pass --project {domain}=<path>, or add it to {BellatrixConfig.RelativePath}; until then the {domain} skills ask you for it.");
        return null;
    }

    private static ProjectSettings? TieBreak(string domain, ProjectSettings[] candidates)
    {
        var named = candidates
            .Where(c => DomainWords[domain].Any(w => c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return named.Length == 1 ? named[0] : null;
    }

    private static ProjectSettings? Ask(
        string domain, ProjectSettings[] candidates, ProjectSettings? proposal, string root, IPrompter prompter, List<string> warnings)
    {
        var lines = new List<string> { $"[bellatrix init] BELLATRIX {domain} test project:" };
        for (var i = 0; i < candidates.Length; i++)
        {
            var mark = ReferenceEquals(candidates[i], proposal) ? "  (proposed)" : "";
            lines.Add($"  [{i + 1}] {candidates[i].Path}{mark}");
        }

        if (candidates.Length == 0)
        {
            lines.Add("  (none found)");
        }

        var defaultHint = proposal is not null ? $"Enter = {candidates.ToList().IndexOf(proposal) + 1}, " : "";
        var question = string.Join(Environment.NewLine, lines) + Environment.NewLine
                       + $"  {defaultHint}a number, a path to the project file, or '-' to skip: ";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var answer = prompter.Ask(question)?.Trim();
            if (answer is null)
            {
                break; // end of input: behave as non-interactive
            }

            if (answer.Length == 0 && proposal is not null)
            {
                return proposal;
            }

            if (answer == "-")
            {
                warnings.Add($"No {domain} test project recorded (skipped); the {domain} skills will ask for it.");
                return null;
            }

            if (int.TryParse(answer, out var n) && n >= 1 && n <= candidates.Length)
            {
                return candidates[n - 1];
            }

            if (answer.Length > 0)
            {
                try
                {
                    return ProjectDiscovery.Describe(root, answer);
                }
                catch (CliException ex)
                {
                    Console.Error.WriteLine($"[bellatrix init] {ex.Message}");
                }
            }
        }

        if (proposal is not null)
        {
            return proposal;
        }

        warnings.Add($"No {domain} test project recorded; pass --project {domain}=<path> or the {domain} skills will ask for it.");
        return null;
    }

    /// <summary>SPECTRA's test-case folder (<c>spectra.config.json</c> → <c>tests.dir</c>), trimmed of a trailing slash.</summary>
    internal static string? SpectraTestsDir(string root)
    {
        var path = Path.Combine(root, "spectra.config.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (json.RootElement.TryGetProperty("tests", out var tests) && tests.ValueKind == JsonValueKind.Object
                && tests.TryGetProperty("dir", out var dir) && dir.ValueKind == JsonValueKind.String)
            {
                var value = dir.GetString()!.Replace('\\', '/').Trim().TrimEnd('/');
                if (value.StartsWith("./", StringComparison.Ordinal))
                {
                    value = value[2..];
                }

                return value.Length > 0 ? value : null;
            }
        }
        catch (JsonException)
        {
            // An unreadable SPECTRA config is SPECTRA's problem; fall back to the default.
        }

        return null;
    }

    private static void ValidateRelative(string value, string flag)
    {
        var problems = BellatrixConfig.Validate(new JsonObject
        {
            ["version"] = BellatrixConfig.CurrentVersion, ["testCasesDir"] = value, ["domains"] = new JsonObject(),
        });
        if (problems.Count > 0)
        {
            throw new CliException("usage", $"{flag} '{value}': {problems[0].message}", exitCode: 2);
        }
    }
}
