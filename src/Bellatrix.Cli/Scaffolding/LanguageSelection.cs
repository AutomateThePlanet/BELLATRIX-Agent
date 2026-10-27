namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// Resolves the BELLATRIX flavor a scaffold run targets: an explicit <c>--language csharp|java|typescript</c>, else
/// the language the repo was scaffolded with before (recorded in the agent manifests), else one detected from build
/// files in the repo root (<c>*.csproj</c>/<c>*.sln</c> → csharp; <c>pom.xml</c>/<c>build.gradle*</c> → java;
/// <c>package.json</c> + <c>tsconfig.json</c> → typescript). Several detected → <c>usage</c> (exit 2). None detected →
/// csharp, the only flavor before 2.5, with a stderr notice.
/// </summary>
internal static class LanguageSelection
{
    /// <summary>The supported languages, in report order.</summary>
    public static readonly IReadOnlyList<string> Known = new[] { "csharp", "java", "typescript" };

    private const string TypeScriptWebOnly =
        "BELLATRIX TypeScript supports web only (@bellatrix/appium has raw Windows/Android drivers, no components or " +
        "page objects, no iOS) — only the language-agnostic CLI skills (execute-scenario, execute-spectra-test, " +
        "locator specialist) are scaffolded for this domain. Use --language csharp|java for desktop/mobile test code.";

    /// <summary>
    /// Framework limits per language × domain, reported as a warning (the topics themselves are still scaffolded
    /// for the parts that exist).
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Language, string Domain), string> Limits =
        new Dictionary<(string, string), string>
        {
            [("typescript", "desktop")] = TypeScriptWebOnly,
            [("typescript", "mobile")] = TypeScriptWebOnly,
        };

    public static string Parse(string raw)
    {
        var language = raw.Trim().ToLowerInvariant() switch
        {
            "c#" or "cs" or "dotnet" => "csharp",
            "ts" => "typescript",
            var other => other,
        };

        return Known.Contains(language)
            ? language
            : throw new CliException("usage", exitCode: 2,
                message: $"Unknown --language '{raw}'. One of: {string.Join(", ", Known)}.");
    }

    /// <summary>Language for a run without <c>--language</c>; see the class summary for the order.</summary>
    public static string Detect(string root, string? recorded, TextWriter? notices = null)
    {
        if (!string.IsNullOrEmpty(recorded) && Known.Contains(recorded))
        {
            return recorded;
        }

        var detected = new List<string>();
        if (Glob(root, "*.csproj") || Glob(root, "*.sln") || Glob(root, "*.slnx"))
        {
            detected.Add("csharp");
        }

        if (File.Exists(Path.Combine(root, "pom.xml")) || Glob(root, "build.gradle*"))
        {
            detected.Add("java");
        }

        if (File.Exists(Path.Combine(root, "package.json")) && File.Exists(Path.Combine(root, "tsconfig.json")))
        {
            detected.Add("typescript");
        }

        switch (detected.Count)
        {
            case 1:
                return detected[0];
            case 0:
                (notices ?? Console.Error).WriteLine(
                    "[bellatrix init] notice: no *.csproj, pom.xml/build.gradle or package.json+tsconfig.json found; " +
                    "using --language csharp. Pass --language java|typescript for the other BELLATRIX flavors.");
                return "csharp";
            default:
                throw new CliException("usage", exitCode: 2,
                    message: $"Several BELLATRIX languages detected ({string.Join(", ", detected)}). Pass --language with one of them " +
                             "(run init again with --out <subdir> for a mixed repo).");
        }
    }

    private static bool Glob(string root, string pattern)
        => Directory.Exists(root) && Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly).Any();
}
