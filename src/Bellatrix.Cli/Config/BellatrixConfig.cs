using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Config;

/// <summary>A BELLATRIX test project as recorded in <c>.bellatrix/config.json</c> (<c>domains.&lt;domain&gt;.project</c>).</summary>
internal sealed record ProjectSettings(string Path, string Name, string? RootNamespace, string BuildCommand)
{
    /// <summary>The folder that holds the project file, repo-relative (<c>.</c> for the repository root).</summary>
    public string Dir => Path.Contains('/') ? Path[..Path.LastIndexOf('/')] : ".";

    public JsonObject ToJson() => new()
    {
        ["path"] = Path,
        ["name"] = Name,
        ["rootNamespace"] = RootNamespace,
        ["buildCommand"] = BuildCommand,
    };

    public static ProjectSettings? From(JsonNode? node)
        => node is JsonObject o
           && Str(o["path"]) is { } path
           && Str(o["name"]) is { } name
           && Str(o["buildCommand"]) is { } build
            ? new ProjectSettings(path, name, Str(o["rootNamespace"]), build)
            : null;

    internal static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>One problem found by <see cref="BellatrixConfig.Validate"/>: a JSON path and a message.</summary>
internal sealed record ConfigProblem(string path, string message);

/// <summary>
/// <c>.bellatrix/config.json</c>: the single source of the user's project settings (test project per domain and
/// the test-case folder). <c>bellatrix init</c> creates it once and afterwards only merges in missing values
/// (R9); the skills and <c>bellatrix config show|validate</c> read it. The file is human-owned and meant to be
/// committed. Its format is <c>schemas/bellatrix-config.v1.schema.json</c> (embedded); <see cref="Validate"/>
/// mirrors that schema by hand so the CLI needs no schema library (a test keeps the two in agreement).
/// </summary>
internal static class BellatrixConfig
{
    public const string RelativePath = ".bellatrix/config.json";

    public const int CurrentVersion = 1;

    public const string DefaultTestCasesDir = "test-cases";

    public const string SchemaUrl =
        "https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/main/schemas/bellatrix-config.v1.schema.json";

    /// <summary>The embedded copy of <c>schemas/bellatrix-config.v1.schema.json</c>.</summary>
    public const string SchemaResource = "schemas/bellatrix-config.v1.schema.json";

    public static readonly IReadOnlyList<string> Domains = new[] { "desktop", "web", "mobile" };

    private static readonly string[] TopKeys = { "$schema", "version", "testCasesDir", "domains" };
    private static readonly string[] DomainKeys = { "project", "testCasesDir" };
    private static readonly string[] ProjectKeys = { "path", "name", "rootNamespace", "buildCommand" };

    // Same as the schema's relativePath pattern: no leading / or ~, no drive letter, no '..' segment, no backslash.
    private static readonly Regex RelativePathPattern = new(@"^(?![/~])(?![A-Za-z]:)(?!(?:.*/)?\.\.(?:/|$))[^\\]+$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string FullPath(string root) => Path.Combine(root, ".bellatrix", "config.json");

    /// <summary>The embedded schema text.</summary>
    public static string SchemaText()
    {
        using var stream = typeof(BellatrixConfig).Assembly.GetManifestResourceStream(SchemaResource)
                           ?? throw new InvalidOperationException($"Embedded resource '{SchemaResource}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>The result of reading the file: whether it exists, the parsed document and its problems.</summary>
    internal sealed record Loaded(bool Exists, JsonObject? Document, IReadOnlyList<ConfigProblem> Errors)
    {
        public bool Valid => Exists && Document is not null && Errors.Count == 0;
    }

    public static Loaded Load(string root)
    {
        var path = FullPath(root);
        if (!File.Exists(path))
        {
            return new Loaded(false, null, new[] { new ConfigProblem("$", $"{RelativePath} does not exist. Run `bellatrix init`.") });
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            return new Loaded(true, null, new[] { new ConfigProblem("$", $"Not valid JSON: {ex.Message}") });
        }

        if (node is not JsonObject obj)
        {
            return new Loaded(true, null, new[] { new ConfigProblem("$", "The file must contain a JSON object.") });
        }

        return new Loaded(true, obj, Validate(obj));
    }

    /// <summary>Checks a document against the v1 format (a hand-written mirror of the JSON schema).</summary>
    public static IReadOnlyList<ConfigProblem> Validate(JsonNode? node)
    {
        var problems = new List<ConfigProblem>();
        if (node is not JsonObject root)
        {
            problems.Add(new ConfigProblem("$", "The file must contain a JSON object."));
            return problems;
        }

        Unknown(root, "$", TopKeys, problems);

        if (root.TryGetPropertyValue("$schema", out var schema) && !IsString(schema))
        {
            problems.Add(new ConfigProblem("$.$schema", "Must be a string."));
        }

        if (!root.TryGetPropertyValue("version", out var version))
        {
            problems.Add(new ConfigProblem("$.version", $"Required; must be {CurrentVersion}."));
        }
        else if (!(version is JsonValue v && v.TryGetValue<int>(out var n) && n == CurrentVersion))
        {
            problems.Add(new ConfigProblem("$.version", $"Must be {CurrentVersion}."));
        }

        if (!root.TryGetPropertyValue("testCasesDir", out var testCases))
        {
            problems.Add(new ConfigProblem("$.testCasesDir", $"Required (e.g. \"{DefaultTestCasesDir}\")."));
        }
        else
        {
            RelativePathOf(testCases, "$.testCasesDir", problems);
        }

        if (!root.TryGetPropertyValue("domains", out var domainsNode))
        {
            problems.Add(new ConfigProblem("$.domains", "Required (an object; may be empty)."));
        }
        else if (domainsNode is not JsonObject domains)
        {
            problems.Add(new ConfigProblem("$.domains", "Must be an object."));
        }
        else
        {
            Unknown(domains, "$.domains", Domains, problems);
            foreach (var domain in Domains)
            {
                if (!domains.TryGetPropertyValue(domain, out var dNode))
                {
                    continue;
                }

                var at = $"$.domains.{domain}";
                if (dNode is not JsonObject d)
                {
                    problems.Add(new ConfigProblem(at, "Must be an object."));
                    continue;
                }

                Unknown(d, at, DomainKeys, problems);
                if (d.TryGetPropertyValue("testCasesDir", out var dTests))
                {
                    RelativePathOf(dTests, at + ".testCasesDir", problems);
                }

                if (d.TryGetPropertyValue("project", out var pNode))
                {
                    ValidateProject(pNode, at + ".project", problems);
                }
            }
        }

        return problems;
    }

    private static void ValidateProject(JsonNode? node, string at, List<ConfigProblem> problems)
    {
        if (node is not JsonObject p)
        {
            problems.Add(new ConfigProblem(at, "Must be an object."));
            return;
        }

        Unknown(p, at, ProjectKeys, problems);
        if (!p.TryGetPropertyValue("path", out var path))
        {
            problems.Add(new ConfigProblem(at + ".path", "Required: the project file, relative to the repository root."));
        }
        else
        {
            RelativePathOf(path, at + ".path", problems);
        }

        foreach (var key in new[] { "name", "buildCommand" })
        {
            if (!p.TryGetPropertyValue(key, out var value))
            {
                problems.Add(new ConfigProblem($"{at}.{key}", "Required."));
            }
            else if (!IsNonEmptyString(value))
            {
                problems.Add(new ConfigProblem($"{at}.{key}", "Must be a non-empty string."));
            }
        }

        if (p.TryGetPropertyValue("rootNamespace", out var ns) && ns is not null && !IsNonEmptyString(ns))
        {
            problems.Add(new ConfigProblem(at + ".rootNamespace", "Must be a non-empty string or null."));
        }
    }

    private static void Unknown(JsonObject obj, string at, IEnumerable<string> allowed, List<ConfigProblem> problems)
    {
        var known = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var (key, _) in obj)
        {
            if (!known.Contains(key))
            {
                problems.Add(new ConfigProblem($"{at}.{key}", $"Unknown property. Allowed: {string.Join(", ", known)}."));
            }
        }
    }

    private static void RelativePathOf(JsonNode? node, string at, List<ConfigProblem> problems)
    {
        if (!IsNonEmptyString(node))
        {
            problems.Add(new ConfigProblem(at, "Must be a non-empty string."));
        }
        else if (!RelativePathPattern.IsMatch(node!.GetValue<string>()))
        {
            problems.Add(new ConfigProblem(at, "Must be a repository-relative path with forward slashes and no '..' (e.g. \"src/My.Tests/My.Tests.csproj\")."));
        }
    }

    private static bool IsString(JsonNode? node) => node is JsonValue v && v.GetValueKind() == JsonValueKind.String;

    private static bool IsNonEmptyString(JsonNode? node) => IsString(node) && node!.GetValue<string>().Length > 0;

    /// <summary>Checks that only exist on disk (not in the schema): project files that are missing.</summary>
    public static IReadOnlyList<ConfigProblem> DiskWarnings(string root, JsonObject document)
    {
        var warnings = new List<ConfigProblem>();
        if (document["domains"] is not JsonObject domains)
        {
            return warnings;
        }

        foreach (var domain in Domains)
        {
            if (ProjectSettings.From(domains[domain]?["project"]) is { } project
                && !File.Exists(Path.Combine(root, project.Path.Replace('/', Path.DirectorySeparatorChar))))
            {
                warnings.Add(new ConfigProblem($"$.domains.{domain}.project.path", $"{project.Path} does not exist."));
            }
        }

        return warnings;
    }

    /// <summary>A new v1 document (property order: $schema, version, testCasesDir, domains).</summary>
    public static JsonObject Create(string testCasesDir) => new()
    {
        ["$schema"] = SchemaUrl,
        ["version"] = CurrentVersion,
        ["testCasesDir"] = testCasesDir,
        ["domains"] = new JsonObject(),
    };

    public static string Serialize(JsonObject document) => document.ToJsonString(WriteOptions).Replace("\r\n", "\n") + "\n";

    public static void Save(string root, JsonObject document)
    {
        var path = FullPath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(document), new UTF8Encoding(false));
    }

    /// <summary>The project of a domain, or null.</summary>
    public static ProjectSettings? ProjectOf(JsonObject document, string domain) => ProjectSettings.From(document["domains"]?[domain]?["project"]);

    /// <summary>The test-case folder of a domain: its own <c>testCasesDir</c>, else the top-level one, else the default.</summary>
    public static string TestCasesDirOf(JsonObject document, string domain)
        => ProjectSettings.Str(document["domains"]?[domain]?["testCasesDir"])
           ?? ProjectSettings.Str(document["testCasesDir"])
           ?? DefaultTestCasesDir;
}
