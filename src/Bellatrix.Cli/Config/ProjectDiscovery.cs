using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Config;

/// <summary>A test project that references a BELLATRIX domain package, found by <see cref="ProjectDiscovery"/>.</summary>
internal sealed record ProjectCandidate(string Domain, ProjectSettings Project);

/// <summary>
/// Finds the user's BELLATRIX test projects for one language by scanning the repository (at most
/// <see cref="MaxDepth"/> levels, skipping build output, dependencies and VCS folders):
/// <list type="bullet">
/// <item>C#: <c>*.csproj</c> with a PackageReference/ProjectReference to <c>Bellatrix.Desktop</c> (desktop),
/// <c>Bellatrix.Web</c>/<c>Bellatrix.Playwright</c> (web) or <c>Bellatrix.Mobile</c> (mobile). Projects whose own
/// name starts with <c>Bellatrix.</c> are the framework (or its own tests/templates, e.g. a vendored copy) and are
/// skipped.</item>
/// <item>Java: <c>pom.xml</c> / <c>build.gradle(.kts)</c> depending on <c>solutions.bellatrix:bellatrix.desktop</c>,
/// <c>bellatrix.web</c>/<c>bellatrix.playwright</c> or <c>bellatrix.android</c>/<c>bellatrix.ios</c>; modules whose own
/// artifactId starts with <c>bellatrix</c> are the framework and are skipped.</item>
/// <item>TypeScript: <c>package.json</c> depending on <c>@bellatrix/web</c> (web) or <c>@bellatrix/appium</c>
/// (mobile); packages named <c>@bellatrix/*</c> are the framework and are skipped.</item>
/// </list>
/// Package ids come from the framework repositories (BELLATRIX project names, BELLATRIX-Java poms, BELLATRIX-JavaScript
/// package.json files). Discovery never invents a project: no match means no candidate.
/// </summary>
internal static class ProjectDiscovery
{
    public const int MaxDepth = 6;

    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".bellatrix", "target", "dist", "out", "build", ".gradle", ".idea", ".vs",
        "packages", "TestResults",
    };

    private static readonly Regex CsReference = new(
        @"<(?:PackageReference|ProjectReference)\b[^>]*\bInclude\s*=\s*""(?<include>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CsRootNamespace = new(@"<RootNamespace>\s*(?<ns>[^<$]+?)\s*</RootNamespace>", RegexOptions.Compiled);

    private static readonly Regex JavaDependency = new(
        @"<dependency>(?<body>.*?)</dependency>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GradleDependency = new(
        @"solutions\.bellatrix:(?<artifact>bellatrix\.[a-z]+)", RegexOptions.Compiled);

    private static readonly Regex JavaPackage = new(@"^\s*package\s+(?<pkg>[\w.]+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>C# reference name → domain.</summary>
    internal static string? CSharpDomainOf(string reference)
    {
        // A ProjectReference names a path; a PackageReference names an id. Both end in the project/package name.
        var name = reference.Replace('\\', '/').Split('/')[^1];
        if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^".csproj".Length];
        }

        return name.ToLowerInvariant() switch
        {
            "bellatrix.desktop" => "desktop",
            "bellatrix.web" or "bellatrix.playwright" => "web",
            "bellatrix.mobile" => "mobile",
            _ => null,
        };
    }

    /// <summary>Java artifactId (group <c>solutions.bellatrix</c>) → domain.</summary>
    internal static string? JavaDomainOf(string artifactId) => artifactId.ToLowerInvariant() switch
    {
        "bellatrix.desktop" => "desktop",
        "bellatrix.web" or "bellatrix.playwright" => "web",
        "bellatrix.android" or "bellatrix.ios" => "mobile",
        _ => null,
    };

    /// <summary>npm package → domain.</summary>
    internal static string? TypeScriptDomainOf(string package) => package switch
    {
        "@bellatrix/web" => "web",
        "@bellatrix/appium" => "mobile",
        _ => null,
    };

    /// <summary>Every candidate for <paramref name="language"/>, ordered by path (stable output).</summary>
    public static IReadOnlyList<ProjectCandidate> Find(string root, string language)
    {
        var found = language switch
        {
            "csharp" => FindCSharp(root),
            "java" => FindJava(root),
            "typescript" => FindTypeScript(root),
            _ => Enumerable.Empty<ProjectCandidate>(),
        };

        return found
            .OrderBy(c => c.Domain, StringComparer.Ordinal)
            .ThenBy(c => c.Project.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<ProjectCandidate> FindCSharp(string root)
    {
        foreach (var file in Files(root, f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("Bellatrix.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = SafeRead(file);
            var domains = CsReference.Matches(text)
                .Select(m => CSharpDomainOf(m.Groups["include"].Value))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal);

            var ns = CsRootNamespace.Match(text) is { Success: true } m ? m.Groups["ns"].Value : name;
            var relative = Relative(root, file);
            var project = new ProjectSettings(relative, name, ns, $"dotnet build {relative}");
            foreach (var domain in domains)
            {
                yield return new ProjectCandidate(domain, project);
            }
        }
    }

    private static IEnumerable<ProjectCandidate> FindJava(string root)
    {
        var files = Files(root, f => Path.GetFileName(f) is "pom.xml" or "build.gradle" or "build.gradle.kts");
        foreach (var file in files)
        {
            var text = SafeRead(file);
            var dir = Path.GetDirectoryName(file)!;
            var relative = Relative(root, file);
            var relativeDir = relative.Contains('/') ? relative[..relative.LastIndexOf('/')] : ".";
            IEnumerable<string> artifacts;
            string name;
            string build;

            if (Path.GetFileName(file) == "pom.xml")
            {
                name = OwnArtifactId(text) ?? Path.GetFileName(dir);
                artifacts = JavaDependency.Matches(text)
                    .Select(m => m.Groups["body"].Value)
                    .Where(b => Tag(b, "groupId") == "solutions.bellatrix")
                    .Select(b => Tag(b, "artifactId") ?? "");
                build = $"mvn -q -f {relative} test-compile";
            }
            else
            {
                if (File.Exists(Path.Combine(dir, "pom.xml")))
                {
                    continue; // one project per folder; the pom wins
                }

                name = Path.GetFileName(dir);
                artifacts = GradleDependency.Matches(text).Select(m => m.Groups["artifact"].Value);
                var wrapper = File.Exists(Path.Combine(root, "gradlew")) ? "./gradlew" : "gradle";
                build = relativeDir == "." ? $"{wrapper} testClasses" : $"{wrapper} -p {relativeDir} testClasses";
            }

            if (name.StartsWith("bellatrix", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var domains = artifacts.Select(JavaDomainOf).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
            if (domains.Length == 0)
            {
                continue;
            }

            var project = new ProjectSettings(relative, name, JavaBasePackage(dir), build);
            foreach (var domain in domains)
            {
                yield return new ProjectCandidate(domain, project);
            }
        }
    }

    private static IEnumerable<ProjectCandidate> FindTypeScript(string root)
    {
        foreach (var file in Files(root, f => Path.GetFileName(f) == "package.json"))
        {
            JsonElement json;
            try
            {
                json = JsonDocument.Parse(SafeRead(file)).RootElement;
            }
            catch (JsonException)
            {
                continue;
            }

            var dir = Path.GetDirectoryName(file)!;
            var name = json.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : Path.GetFileName(dir);
            if (name.StartsWith("@bellatrix/", StringComparison.Ordinal))
            {
                continue;
            }

            var packages = new List<string>();
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (json.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
                {
                    packages.AddRange(deps.EnumerateObject().Select(p => p.Name));
                }
            }

            var relative = Relative(root, file);
            var relativeDir = relative.Contains('/') ? relative[..relative.LastIndexOf('/')] : ".";
            var tsconfig = relativeDir == "." ? "tsconfig.json" : $"{relativeDir}/tsconfig.json";
            var project = new ProjectSettings(relative, name, null, $"npx tsc --noEmit -p {tsconfig}");
            foreach (var domain in packages.Select(TypeScriptDomainOf).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                yield return new ProjectCandidate(domain, project);
            }
        }
    }

    /// <summary>
    /// The settings of a project file the user named (<c>--project</c> or the init prompt), whatever it references:
    /// a <c>.csproj</c>, <c>pom.xml</c>, <c>build.gradle(.kts)</c> or <c>package.json</c>, or a folder holding exactly
    /// one of them. Throws a usage error for anything else.
    /// </summary>
    public static ProjectSettings Describe(string root, string userPath)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(userPath) ? userPath : Path.Combine(root, userPath));
        if (Directory.Exists(full))
        {
            var inside = Directory.EnumerateFiles(full)
                .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                            || Path.GetFileName(f) is "pom.xml" or "build.gradle" or "build.gradle.kts" or "package.json")
                .ToArray();
            if (inside.Length != 1)
            {
                throw new CliException("usage",
                    $"--project '{userPath}' is a folder with {(inside.Length == 0 ? "no" : "several")} project files; name the project file (.csproj, pom.xml, build.gradle or package.json).", exitCode: 2);
            }

            full = inside[0];
        }

        if (!File.Exists(full))
        {
            throw new CliException("usage", $"--project '{userPath}' does not exist (paths are relative to the repository root).", exitCode: 2);
        }

        var relative = Relative(root, full);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new CliException("usage", $"--project '{userPath}' is outside the repository ({root}).", exitCode: 2);
        }

        var dir = Path.GetDirectoryName(full)!;
        var relativeDir = relative.Contains('/') ? relative[..relative.LastIndexOf('/')] : ".";
        var text = SafeRead(full);
        var fileName = Path.GetFileName(full);
        if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(full);
            var ns = CsRootNamespace.Match(text) is { Success: true } m ? m.Groups["ns"].Value : name;
            return new ProjectSettings(relative, name, ns, $"dotnet build {relative}");
        }

        if (fileName == "pom.xml")
        {
            return new ProjectSettings(relative, OwnArtifactId(text) ?? Path.GetFileName(dir), JavaBasePackage(dir), $"mvn -q -f {relative} test-compile");
        }

        if (fileName is "build.gradle" or "build.gradle.kts")
        {
            var wrapper = File.Exists(Path.Combine(root, "gradlew")) ? "./gradlew" : "gradle";
            return new ProjectSettings(relative, Path.GetFileName(dir), JavaBasePackage(dir),
                relativeDir == "." ? $"{wrapper} testClasses" : $"{wrapper} -p {relativeDir} testClasses");
        }

        if (fileName == "package.json")
        {
            string name;
            try
            {
                var json = JsonDocument.Parse(text).RootElement;
                name = json.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : Path.GetFileName(dir);
            }
            catch (JsonException)
            {
                name = Path.GetFileName(dir);
            }

            var tsconfig = relativeDir == "." ? "tsconfig.json" : $"{relativeDir}/tsconfig.json";
            return new ProjectSettings(relative, name, null, $"npx tsc --noEmit -p {tsconfig}");
        }

        throw new CliException("usage", $"--project '{userPath}' is not a .csproj, pom.xml, build.gradle(.kts) or package.json.", exitCode: 2);
    }

    /// <summary>The pom's own artifactId (not the parent's, a dependency's or a plugin's).</summary>
    private static string? OwnArtifactId(string pom)
    {
        var stripped = Regex.Replace(pom,
            @"<(parent|dependencies|dependencyManagement|build|profiles|reporting|pluginManagement)\b.*?</\1>", "",
            RegexOptions.Singleline);
        return Tag(stripped, "artifactId");
    }

    private static string? Tag(string xml, string tag)
        => Regex.Match(xml, $@"<{tag}>\s*(?<v>[^<]+?)\s*</{tag}>") is { Success: true } m ? m.Groups["v"].Value : null;

    /// <summary>The longest package prefix shared by the test sources under <c>src/test/java</c>, or null.</summary>
    private static string? JavaBasePackage(string projectDir)
    {
        var sources = Path.Combine(projectDir, "src", "test", "java");
        if (!Directory.Exists(sources))
        {
            return null;
        }

        string[]? common = null;
        foreach (var file in Directory.EnumerateFiles(sources, "*.java", SearchOption.AllDirectories).Take(500))
        {
            if (JavaPackage.Match(SafeRead(file)) is not { Success: true } m)
            {
                continue;
            }

            var parts = m.Groups["pkg"].Value.Split('.');
            common = common is null ? parts : common.Zip(parts).TakeWhile(p => p.First == p.Second).Select(p => p.First).ToArray();
        }

        return common is { Length: > 0 } ? string.Join('.', common) : null;
    }

    private static IEnumerable<string> Files(string root, Func<string, bool> match)
    {
        var results = new List<string>();
        void Walk(string dir, int depth)
        {
            IEnumerable<string> files, dirs;
            try
            {
                files = Directory.EnumerateFiles(dir).ToArray();
                dirs = Directory.EnumerateDirectories(dir).ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return;
            }

            results.AddRange(files.Where(match));
            if (depth >= MaxDepth)
            {
                return;
            }

            foreach (var sub in dirs)
            {
                var name = Path.GetFileName(sub);
                if (!SkippedDirs.Contains(name) && !name.StartsWith('.'))
                {
                    Walk(sub, depth + 1);
                }
            }
        }

        Walk(root, 0);
        return results;
    }

    private static string SafeRead(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return string.Empty;
        }
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');
}
