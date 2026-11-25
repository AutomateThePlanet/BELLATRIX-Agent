using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bellatrix.Cli.Config;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// A new user's first run: <c>bellatrix init</c> in an otherwise empty repository that holds one BELLATRIX test
/// project named <c>Acme.UiTests</c> (not a <c>Demo.*</c> sample). <c>.bellatrix/config.json</c> must record that
/// project, and no scaffolded file may contain a project name, a project path, a sample project or a test-case path
/// other than <c>{testCasesDir}/…</c>: the skills get those values from the config at run time. The catalog-level test
/// applies the same rules to every template of every language, including ones a given agent does not emit.
/// </summary>
public class FirstRunScaffoldTests : IDisposable
{
    public FirstRunScaffoldTests() => InitConfig.InteractiveOverride = false;

    public void Dispose() => InitConfig.InteractiveOverride = null;

    /// <summary>The user's project per language, and what init must record for it.</summary>
    private sealed record Fixture(string Language, string Domains, string[] ExpectedDomains, string Path, string Name, string? RootNamespace, string Build);

    private static readonly Dictionary<string, Fixture> Fixtures = new()
    {
        ["csharp"] = new("csharp", "desktop,web,mobile", new[] { "desktop", "web", "mobile" },
            "src/Acme.UiTests/Acme.UiTests.csproj", "Acme.UiTests", "Acme.UiTests", "dotnet build src/Acme.UiTests/Acme.UiTests.csproj"),
        ["java"] = new("java", "desktop,web,mobile", new[] { "desktop", "web", "mobile" },
            "Acme.UiTests/pom.xml", "acme-ui-tests", "com.acme.uitests", "mvn -q -f Acme.UiTests/pom.xml test-compile"),
        ["typescript"] = new("typescript", "web", new[] { "web" },
            "Acme.UiTests/package.json", "acme-ui-tests", null, "npx tsc --noEmit -p Acme.UiTests/tsconfig.json"),
    };

    private static void WriteProject(TempRepo repo, string language)
    {
        switch (language)
        {
            case "csharp":
                repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <ItemGroup>
                        <PackageReference Include="Bellatrix.Desktop" Version="1.0.0" />
                        <PackageReference Include="Bellatrix.Web" Version="1.0.0" />
                        <PackageReference Include="Bellatrix.Mobile" Version="1.0.0" />
                      </ItemGroup>
                    </Project>
                    """);
                break;
            case "java":
                repo.Write("Acme.UiTests/pom.xml", """
                    <project>
                      <groupId>com.acme</groupId>
                      <artifactId>acme-ui-tests</artifactId>
                      <dependencies>
                        <dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.desktop</artifactId><version>1.0</version></dependency>
                        <dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.web</artifactId><version>1.0</version></dependency>
                        <dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.android</artifactId><version>1.0</version></dependency>
                      </dependencies>
                    </project>
                    """);
                repo.Write("Acme.UiTests/src/test/java/com/acme/uitests/tests/SmokeTests.java", "package com.acme.uitests.tests;\npublic class SmokeTests {}\n");
                break;
            default:
                repo.Write("Acme.UiTests/package.json", """{ "name": "acme-ui-tests", "type": "module", "devDependencies": { "@bellatrix/web": "^1.0.0" } }""");
                repo.Write("Acme.UiTests/tsconfig.json", "{}");
                break;
        }
    }

    public static TheoryData<string, string> AgentsAndLanguages()
    {
        var data = new TheoryData<string, string>();
        foreach (var language in LanguageSelection.Known)
        {
            foreach (var agent in AgentSelection.Names)
            {
                data.Add(agent, language);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AgentsAndLanguages))]
    public void FirstRun_InARepoWithAcmeUiTests_RecordsTheProject_AndScaffoldsNoProjectValues(string agent, string language)
    {
        var fixture = Fixtures[language];
        using var repo = new TempRepo();
        WriteProject(repo, language);
        var userFiles = Files(repo.Root).ToHashSet(StringComparer.Ordinal);

        var (exit, output, errors) = CliHarness.Run(repo.Root,
            "init", "--agent", agent, "--language", language, "--domains", fixture.Domains, "--no-interaction");

        Assert.True(exit == 0, $"init failed ({exit}): {output}\n{errors}");

        // 1. .bellatrix/config.json holds the user's project.
        var loaded = BellatrixConfig.Load(repo.Root);
        Assert.True(loaded.Valid, string.Join("; ", loaded.Errors.Select(e => $"{e.path}: {e.message}")));
        var config = loaded.Document!;
        Assert.Equal(BellatrixConfig.SchemaUrl, config["$schema"]!.GetValue<string>());
        Assert.Equal(1, config["version"]!.GetValue<int>());
        Assert.Equal("test-cases", config["testCasesDir"]!.GetValue<string>());
        Assert.Equal(fixture.ExpectedDomains, config["domains"]!.AsObject().Select(d => d.Key));
        foreach (var domain in fixture.ExpectedDomains)
        {
            var project = BellatrixConfig.ProjectOf(config, domain);
            Assert.NotNull(project);
            Assert.Equal((fixture.Path, fixture.Name, fixture.RootNamespace, fixture.Build),
                (project!.Path, project.Name, project.RootNamespace, project.BuildCommand));
        }

        Assert.Equal("created", JsonNode.Parse(output)!["config"]!["status"]!.GetValue<string>());
        Assert.Equal(0, CliHarness.Run(repo.Root, "config", "validate").Exit);

        // 2. No scaffolded file carries a project value: everything comes from the config.
        var scaffolded = Files(repo.Root)
            .Where(f => !userFiles.Contains(f) && f != BellatrixConfig.RelativePath && !f.StartsWith(".bellatrix/manifest", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(scaffolded);
        var problems = scaffolded
            .SelectMany(f => ProjectValueProblems(f, File.ReadAllText(repo.Path(f)), userProject: true))
            .ToArray();
        Assert.True(problems.Length == 0, string.Join(Environment.NewLine, problems.Take(40)));
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var language in LanguageSelection.Known)
        {
            data.Add(language);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryTemplate_OfEveryLanguage_HasNoProjectValues(string language)
    {
        var (items, _) = TemplateCatalog.LoadWithGaps(CustomTemplateSource.BuiltInProfile, null, language);
        var problems = items.SelectMany(i => ProjectValueProblems($"{language}:{i.Slug}", i.Body, userProject: false)).ToArray();
        Assert.True(problems.Length == 0, string.Join(Environment.NewLine, problems.Take(40)));
    }

    [Fact]
    public void TheRulesCatchWhatTheyAreFor()
    {
        string[] bad =
        {
            "dotnet build Demo.DesktopTests/Demo.DesktopTests.csproj",
            "namespace MyProject.WebTests.Pages;",
            "mvn -q -f demo-desktop-tests/pom.xml test-compile",
            "import demo.desktoptests.common.Constants;",
            "src/test/java/myproject/webtests/pages",
            "my-web-tests/tests/cart/CartTests.test.ts",
            "read tests/{suite}/TC-{id}.md",
            "read test-cases/{suite}/TC-{id}.md",
            "[EDITED]  tests/cart/TC-210.md",
            "dotnet build src/Shop.Tests/Shop.Tests.csproj",
            "mvn -f shop-tests/pom.xml test",
        };
        foreach (var line in bad)
        {
            Assert.True(ProjectValueProblems("x", line, userProject: false).Any(), line);
        }

        string[] good =
        {
            "{project.buildCommand}",
            "namespace {project.rootNamespace}.Pages;",
            "read {testCasesDir}/{suite}/TC-{id}.md",
            "├─ {project.name}.csproj",
            "<ProjectReference Include=\"..\\BELLATRIX\\src\\Bellatrix.Web\\Bellatrix.Web.csproj\" />",
            "scan the `*.csproj` that references `Bellatrix.Mobile`",
            "mvn -f {project.path} test -Dtest=CartTests",
            "[ManualTestCase(\"TC-1006\")] for TC-1006.md",
        };
        foreach (var line in good)
        {
            Assert.Empty(ProjectValueProblems("x", line, userProject: false));
        }

        Assert.NotEmpty(ProjectValueProblems("x", "go to acme.UiTests", userProject: true));
    }

    // ---------------------------------------------------------------- the rules

    private static readonly (Regex Pattern, string Why)[] Banned =
    {
        (new(@"Demo\.[A-Za-z]+Tests"), "a Demo.* sample project"),
        (new(@"demo-desktop-tests|demo[./]desktoptests", RegexOptions.IgnoreCase), "the Java demo project"),
        (new(@"my-?project|my-(web|mobile|desktop)-tests", RegexOptions.IgnoreCase), "a MyProject/my-*-tests sample project"),
        (new(@"(?<![\w}])(tests|test-cases)/(\{suite\}|[a-z0-9-]+)/TC-"), "a test-case path that is not {testCasesDir}/…"),
    };

    private static readonly Regex CsprojPath = new(@"[^\s`'""()<>]*\.csproj\b");
    private static readonly Regex PomPath = new(@"[^\s`'""()<>]+/pom\.xml\b");
    private static readonly Regex TestCaseFile = new(@"[^\s`'""()<>\[\]]+/TC-(\{id\}|\d+|XXX)\.md");
    private static readonly Regex UserProject = new(@"acme", RegexOptions.IgnoreCase);

    private static IEnumerable<string> ProjectValueProblems(string file, string text, bool userProject)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var at = $"{file}:{i + 1}";
            foreach (var (pattern, why) in Banned)
            {
                if (pattern.Match(line) is { Success: true } m)
                {
                    yield return $"{at}: {why} ('{m.Value}')";
                }
            }

            foreach (Match m in CsprojPath.Matches(line))
            {
                var value = m.Value;
                var name = value.Replace('\\', '/').Split('/')[^1];
                var allowed = value.Contains("{project.", StringComparison.Ordinal)
                              || name.StartsWith("Bellatrix.", StringComparison.Ordinal)  // a framework reference
                              || name == "*.csproj";                                        // prose about finding the project
                if (!allowed)
                {
                    yield return $"{at}: a hard-coded project file ('{value}'); use {{project.path}}";
                }
            }

            foreach (Match m in PomPath.Matches(line))
            {
                if (!m.Value.Contains("{project.", StringComparison.Ordinal))
                {
                    yield return $"{at}: a hard-coded pom.xml path ('{m.Value}'); use {{project.path}}";
                }
            }

            foreach (Match m in TestCaseFile.Matches(line))
            {
                if (!m.Value.StartsWith("{testCasesDir}/", StringComparison.Ordinal))
                {
                    yield return $"{at}: test-case path '{m.Value}' must start with {{testCasesDir}}/";
                }
            }

            if (userProject && UserProject.Match(line) is { Success: true } u)
            {
                yield return $"{at}: the user's project name ('{u.Value}') was baked into a scaffolded file";
            }
        }
    }

    private static IEnumerable<string> Files(string root)
        => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
}
