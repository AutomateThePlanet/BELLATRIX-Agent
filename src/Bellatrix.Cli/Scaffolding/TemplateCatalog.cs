using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>The contract role of a scaffolded template.</summary>
internal enum TemplateKind
{
    Skill,
    Agent,
    Fragment,
}

/// <summary>One vendored template: its kind, kebab slug (= emitted name), description, body (frontmatter
/// stripped), the profile it belongs to ("bellatrix", or a custom template prefix), and an optional source-authored
/// <c>tools:</c> restriction (verbatim value, e.g. <c>[Read, Grep, Glob, Bash]</c>) carried through to
/// the rendered frontmatter for skill/agent kinds that declared one.</summary>
internal sealed record TemplateItem(TemplateKind Kind, string Slug, string Name, string Description, string Body, string Profile, string? Tools = null);

/// <summary>
/// Loads the templates that <c>bellatrix init</c> scaffolds. The default
/// "bellatrix" profile is embedded in this assembly by repo-relative path (<c>tmpl/…</c>) and is split in two
/// layers so the language-agnostic part is written once:
/// <list type="bullet">
/// <item><c>core/&lt;domain&gt;/[agents/]&lt;topic&gt;.md</c> — CLI flow, locator ladder, SPECTRA bridge (any language);</item>
/// <item><c>lang/&lt;language&gt;/&lt;domain&gt;/[agents/]&lt;topic&gt;.md</c> — code, API tables, build command.</item>
/// </list>
/// A core body includes its language part at a <c>{{&gt; lang}}</c> line (the whole language file) or at
/// <c>{{&gt; lang:&lt;section&gt;}}</c> lines (one <c>&lt;!-- lang:&lt;section&gt; --&gt;</c> section each) — a literal
/// include, not a template engine. A core without a marker is language-agnostic. A core WITH a marker but no
/// language file for the requested language is unsupported for that language and is left out (reported by
/// <see cref="LoadWithGaps"/>). A language file may override the core's <c>description:</c> in its own frontmatter.
///
/// A custom template folder (<c>init --template-source &lt;dir&gt; --prefix &lt;p&gt;</c>, see
/// <see cref="CustomTemplateSource"/>) is <b>not</b> embedded: it is read from disk and only rendered — never
/// vendored. Source frontmatter other than <c>description:</c> / <c>tools:</c> is discarded; the kebab
/// <c>name:</c> is the slug (<c>bellatrix-&lt;domain&gt;-&lt;topic&gt;</c>, or <c>&lt;prefix&gt;-…</c>) and per-agent
/// frontmatter is rendered at emit time by <see cref="Emitters.AgentEmitter"/>. A Fragment item's body is
/// domain-filtered by <see cref="FilterDomainBlocks"/> and placed by each agent's emitter.
/// </summary>
internal static class TemplateCatalog
{
    private const string Prefix = "tmpl/";
    private static readonly Assembly Asm = typeof(TemplateCatalog).Assembly;

    /// <summary>The default (and pre-2.5 only) template language.</summary>
    public const string DefaultLanguage = "csharp";

    private static readonly Regex LangMarker = new(
        @"^\{\{> lang(?::(?<name>[a-z0-9-]+))?\}\}\n", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex LangSection = new(
        @"^<!-- lang:(?<name>[a-z0-9-]+) -->\n", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>Every embedded template, keyed by '/'-normalized path below <c>Templates/</c>.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Embedded = new(() =>
        Asm.GetManifestResourceNames()
            .Where(r => r.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(r => r[Prefix.Length..].Replace('\\', '/'), r => r, StringComparer.Ordinal));

    /// <summary>The languages that have at least one embedded language file (<c>lang/&lt;language&gt;/…</c>).</summary>
    public static IReadOnlyList<string> Languages => Embedded.Value.Keys
        .Where(k => k.StartsWith("lang/", StringComparison.Ordinal))
        .Select(k => k.Split('/')[1])
        .Distinct(StringComparer.Ordinal)
        .OrderBy(l => l, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Loads template items for a profile. A custom profile (its name is the template prefix) requires
    /// <paramref name="templateSourceRoot"/> — a folder validated by <see cref="CustomTemplateSource"/> — and reads
    /// its skills/agents/instructions from disk instead of embedded resources.</summary>
    public static IReadOnlyList<TemplateItem> Load(
        string profile = CustomTemplateSource.BuiltInProfile, string? templateSourceRoot = null, string language = DefaultLanguage)
        => LoadWithGaps(profile, templateSourceRoot, language).Items;

    /// <summary>
    /// <see cref="Load"/> plus the slugs that have no <paramref name="language"/> part (unsupported for that
    /// language, e.g. a framework without that domain). Always empty for a custom template folder.
    /// </summary>
    public static (IReadOnlyList<TemplateItem> Items, IReadOnlyList<string> Unsupported) LoadWithGaps(
        string profile = CustomTemplateSource.BuiltInProfile, string? templateSourceRoot = null, string language = DefaultLanguage)
    {
        if (CustomTemplateSource.IsCustom(profile))
        {
            if (string.IsNullOrWhiteSpace(templateSourceRoot))
            {
                throw new InvalidOperationException($"A template source folder is required for the custom profile '{profile}'.");
            }

            return (LoadCustomFromDisk(templateSourceRoot, profile), Array.Empty<string>());
        }

        var items = new List<TemplateItem>();
        var unsupported = new List<string>();

        if (Embedded.Value.TryGetValue("bellatrix-instructions.md", out var fragmentResource))
        {
            var (description, _, body) = SplitFrontmatter(ReadResource(fragmentResource));
            items.Add(new TemplateItem(TemplateKind.Fragment, "bellatrix-instructions", "bellatrix-instructions",
                description, body, "bellatrix"));
        }

        foreach (var (path, resource) in Embedded.Value.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            // core/<domain>/<topic>.md  or  core/<domain>/agents/<name>.md
            var parts = path.Split('/');
            if (parts[0] != "core" || !path.EndsWith(".md", StringComparison.Ordinal))
            {
                continue;
            }

            var domain = parts[1];
            var isAgent = parts.Length == 4 && parts[2] == "agents";
            var topic = Path.GetFileNameWithoutExtension(parts[^1]);
            var slug = $"bellatrix-{domain}-{topic}";

            var (description, _, coreBody) = SplitFrontmatter(ReadResource(resource));
            var body = coreBody;
            if (LangMarker.IsMatch(coreBody))
            {
                var langPath = $"lang/{language}/{domain}/{(isAgent ? "agents/" : string.Empty)}{topic}.md";
                if (!Embedded.Value.TryGetValue(langPath, out var langResource))
                {
                    unsupported.Add(slug);
                    continue;
                }

                var (langDescription, langBody) = ReadLangPart(ReadResource(langResource));
                body = Compose(coreBody, langBody, langPath);
                if (!string.IsNullOrEmpty(langDescription))
                {
                    description = langDescription;
                }
            }

            // The "bellatrix" profile's own source frontmatter never carries a tools: restriction through.
            items.Add(new TemplateItem(isAgent ? TemplateKind.Agent : TemplateKind.Skill, slug, slug,
                description, body, "bellatrix", Tools: null));
        }

        return (items, unsupported);
    }

    /// <summary>
    /// Replaces each <c>{{&gt; lang}}</c> / <c>{{&gt; lang:&lt;section&gt;}}</c> line of <paramref name="core"/> with the
    /// whole language body or the named section of it. A missing section is a packaging bug and throws.
    /// </summary>
    internal static string Compose(string core, string lang, string langPath = "")
    {
        var pieces = LangSection.Split(lang);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i + 1 < pieces.Length; i += 2)
        {
            sections[pieces[i]] = pieces[i + 1];
        }

        return LangMarker.Replace(core, m =>
        {
            var name = m.Groups["name"];
            if (!name.Success)
            {
                return lang;
            }

            return sections.TryGetValue(name.Value, out var section)
                ? section
                : throw new InvalidOperationException($"Language part '{langPath}' has no section '{name.Value}'.");
        });
    }

    /// <summary>A language file: optional frontmatter (only <c>description:</c> is read), then the body verbatim —
    /// unlike <see cref="SplitFrontmatter"/>, leading blank lines are kept so the include is byte-exact.</summary>
    private static (string Description, string Body) ReadLangPart(string raw)
    {
        var text = raw.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (string.Empty, text);
        }

        var (description, _, body) = SplitFrontmatter(text);
        return (description, body);
    }

    /// <summary>Reads a custom template folder (<c>instructions.md</c>, <c>skills/&lt;name&gt;/SKILL.md</c>,
    /// <c>agents/&lt;name&gt;.md</c>) from disk and applies the same frontmatter-split/re-render pipeline as the
    /// embedded loader. Every skill/agent name is namespaced under <paramref name="prefix"/>; agents carry a
    /// source-authored <c>tools:</c> restriction through verbatim.</summary>
    private static IReadOnlyList<TemplateItem> LoadCustomFromDisk(string sourceRoot, string prefix)
    {
        var items = new List<TemplateItem>();

        var instructionsPath = Path.Combine(sourceRoot, "instructions.md");
        if (File.Exists(instructionsPath))
        {
            var (description, _, body) = SplitFrontmatter(File.ReadAllText(instructionsPath));
            items.Add(new TemplateItem(TemplateKind.Fragment, $"{prefix}-instructions", $"{prefix}-instructions", description, body, prefix));
        }

        var skillsDir = Path.Combine(sourceRoot, "skills");
        if (Directory.Exists(skillsDir))
        {
            foreach (var dir in Directory.GetDirectories(skillsDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var skillFile = Path.Combine(dir, "SKILL.md");
                if (!File.Exists(skillFile))
                {
                    continue;
                }

                var slug = CustomTemplateSource.Namespaced(prefix, Path.GetFileName(dir));
                var (description, _, body) = SplitFrontmatter(File.ReadAllText(skillFile));
                items.Add(new TemplateItem(TemplateKind.Skill, slug, slug, description, body, prefix));
            }
        }

        var agentsDir = Path.Combine(sourceRoot, "agents");
        if (Directory.Exists(agentsDir))
        {
            foreach (var file in Directory.GetFiles(agentsDir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
            {
                var slug = CustomTemplateSource.Namespaced(prefix, Path.GetFileNameWithoutExtension(file));
                var (description, tools, body) = SplitFrontmatter(File.ReadAllText(file));
                items.Add(new TemplateItem(TemplateKind.Agent, slug, slug, description, body, prefix, tools));
            }
        }

        return items;
    }

    private static string ReadResource(string name)
    {
        using var stream = Asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded template '{name}' not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Strips a leading YAML frontmatter block (--- … ---) if present, returning the <c>description</c>
    /// value (empty when absent), an optional verbatim <c>tools:</c> value (null when absent), and the
    /// remaining body. Every other source frontmatter key (title, tags, …) is intentionally dropped;
    /// runtime re-renders target-appropriate frontmatter from just these two carried-through values.
    /// </summary>
    internal static (string Description, string? Tools, string Body) SplitFrontmatter(string raw)
    {
        var text = raw.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (string.Empty, null, text.TrimStart('\n'));
        }

        var fenceEnd = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (fenceEnd < 0)
        {
            return (string.Empty, null, text);
        }

        var front = text[4..fenceEnd];
        var afterFence = text.IndexOf('\n', fenceEnd + 1); // end of the closing "---" line
        var body = afterFence >= 0 ? text[(afterFence + 1)..] : string.Empty;

        var description = string.Empty;
        string? tools = null;
        foreach (var line in front.Split('\n'))
        {
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                description = line["description:".Length..].Trim();
            }
            else if (line.StartsWith("tools:", StringComparison.OrdinalIgnoreCase))
            {
                tools = line["tools:".Length..].Trim();
            }
        }

        return (description, tools, body.TrimStart('\n'));
    }

    /// <summary>
    /// Matches one <c>&lt;!-- bellatrix:domain=&lt;d&gt; --&gt;</c> … <c>&lt;!-- /bellatrix:domain --&gt;</c> block
    /// (plus the blank line after it) in the fragment template.
    /// </summary>
    private static readonly Regex DomainBlock = new(
        @"<!-- bellatrix:domain=(?<domain>[a-z]+) -->\n(?<content>.*?)<!-- /bellatrix:domain -->\n?\n?",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>
    /// Keeps the content of the domain blocks for the selected domains, drops the others, and strips every
    /// marker so the emitted fragment is plain markdown. Text without markers (e.g. a custom template fragment) is
    /// returned unchanged.
    /// </summary>
    internal static string FilterDomainBlocks(string body, IReadOnlySet<string>? domains)
    {
        if (!body.Contains("<!-- bellatrix:domain=", StringComparison.Ordinal))
        {
            return body;
        }

        var text = body.Replace("\r\n", "\n");
        var filtered = DomainBlock.Replace(text, m =>
            domains is null || domains.Contains(m.Groups["domain"].Value)
                ? m.Groups["content"].Value + "\n"
                : string.Empty);

        return filtered.TrimEnd('\n') + "\n";
    }
}
