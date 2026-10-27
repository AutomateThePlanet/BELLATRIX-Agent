using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// A caller-supplied template folder for <c>bellatrix init --template-source &lt;dir&gt; [--prefix &lt;p&gt;]</c>: the
/// same contract (R1–R8, per-agent manifests) as the built-in BELLATRIX templates, rendered under the caller's own
/// namespace. Layout: <c>instructions.md</c> (optional fragment), <c>skills/&lt;name&gt;/SKILL.md</c>,
/// <c>agents/&lt;name&gt;.md</c>. The prefix names the fragment (<c>.bellatrix/&lt;prefix&gt;.instructions.md</c>), the
/// manifests (<c>.bellatrix/&lt;prefix&gt;-manifest.&lt;agent&gt;.json</c>), the host marker block, and every emitted
/// skill/agent (a name without the <c>&lt;prefix&gt;-</c> prefix gets it). Nothing about any particular template set
/// ships in the CLI.
/// </summary>
internal static class CustomTemplateSource
{
    /// <summary>The built-in profile; reserved, so a custom prefix can never collide with BELLATRIX's own files.</summary>
    public const string BuiltInProfile = "bellatrix";

    private static readonly Regex PrefixPattern = new("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    /// <summary>True for a custom-template profile (anything but the built-in BELLATRIX profile).</summary>
    public static bool IsCustom(string profile) => !string.Equals(profile, BuiltInProfile, StringComparison.Ordinal);

    /// <summary>Validates the folder and returns its full path. It must contain <c>skills/</c> or <c>agents/</c>.</summary>
    public static string Validate(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)
            || !(Directory.Exists(Path.Combine(full, "skills")) || Directory.Exists(Path.Combine(full, "agents"))))
        {
            throw new CliException("template_source_invalid",
                $"--template-source '{path}' is not a template folder (expected 'skills/<name>/SKILL.md' and/or " +
                "'agents/<name>.md', optionally 'instructions.md').");
        }

        return full;
    }

    /// <summary>
    /// The namespace for a template folder: <paramref name="explicitPrefix"/> when given, otherwise the common first
    /// segment (before the first '-') of every skill and agent name. Lower-case kebab-case; <c>bellatrix</c> is reserved.
    /// </summary>
    public static string ResolvePrefix(string sourceRoot, string? explicitPrefix)
    {
        var prefix = explicitPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            var heads = Names(sourceRoot)
                .Select(n => n.Split('-')[0])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (heads.Length != 1)
            {
                throw new CliException("usage",
                    "Could not infer a name prefix from the template folder (its skill/agent names do not share one " +
                    "first segment). Pass --prefix <name>.");
            }

            prefix = heads[0];
        }

        prefix = prefix.Trim();
        if (!PrefixPattern.IsMatch(prefix))
        {
            throw new CliException("usage", $"--prefix '{prefix}' must be lower-case kebab-case (e.g. 'acme' or 'acme-qa').");
        }

        if (!IsCustom(prefix))
        {
            throw new CliException("usage",
                $"--prefix '{BuiltInProfile}' is reserved for the built-in BELLATRIX templates; choose your own prefix.");
        }

        return prefix;
    }

    /// <summary>The emitted name for a source item: unchanged when it already starts with <c>&lt;prefix&gt;-</c>.</summary>
    public static string Namespaced(string prefix, string name)
        => name.StartsWith(prefix + "-", StringComparison.Ordinal) ? name : $"{prefix}-{name}";

    private static IEnumerable<string> Names(string sourceRoot)
    {
        var skills = Path.Combine(sourceRoot, "skills");
        if (Directory.Exists(skills))
        {
            foreach (var dir in Directory.GetDirectories(skills).Where(d => File.Exists(Path.Combine(d, "SKILL.md"))))
            {
                yield return Path.GetFileName(dir);
            }
        }

        var agents = Path.Combine(sourceRoot, "agents");
        if (Directory.Exists(agents))
        {
            foreach (var file in Directory.GetFiles(agents, "*.md"))
            {
                yield return Path.GetFileNameWithoutExtension(file);
            }
        }
    }
}
