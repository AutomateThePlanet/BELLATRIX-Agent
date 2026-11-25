using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bellatrix.Cli.Config;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>.bellatrix/config.json</c> (R9): the schema and the CLI validator agree, project discovery, the init planner
/// (flags, detection, tie-break, ambiguity, prompting, merge-never-overwrite, invalid files) and
/// <c>bellatrix config show|validate</c>.
/// </summary>
public class BellatrixConfigTests : IDisposable
{
    public BellatrixConfigTests()
    {
        InitConfig.InteractiveOverride = false;
        InitConfig.PrompterOverride = null;
    }

    public void Dispose()
    {
        InitConfig.InteractiveOverride = null;
        InitConfig.PrompterOverride = null;
    }

    private static string Csproj(params string[] references)
        => "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>"
           + string.Concat(references.Select(r => $"<PackageReference Include=\"{r}\" Version=\"1.0.0\" />"))
           + "</ItemGroup></Project>";

    private static readonly string[] AllDomains = { "desktop", "web", "mobile" };

    // ---------------------------------------------------------------- schema ⇔ validator

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    // Parsed once (the validator compiles the schema). Corvus.Json.Validator: Apache-2.0, test project only.
    private static readonly Lazy<Corvus.Json.Validator.JsonSchema> ParsedSchema = new(
        () => Corvus.Json.Validator.JsonSchema.FromText(
            File.ReadAllText(Path.Combine(RepoRoot(), "schemas", "bellatrix-config.v1.schema.json")), BellatrixConfig.SchemaUrl));

    private static bool SchemaAccepts(string json) => ParsedSchema.Value.Validate(JsonDocument.Parse(json).RootElement).IsValid;

    private const string Valid = """
        { "$schema": "x", "version": 1, "testCasesDir": "test-cases",
          "domains": { "desktop": { "project": { "path": "src/A.UiTests/A.UiTests.csproj", "name": "A.UiTests", "rootNamespace": "A.UiTests", "buildCommand": "dotnet build src/A.UiTests/A.UiTests.csproj" } },
                       "web": { "testCasesDir": "specs/web" } } }
        """;

    public static TheoryData<string, bool> Samples() => new()
    {
        { Valid, true },
        { """{ "version": 1, "testCasesDir": "test-cases", "domains": {} }""", true },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "mobile": { "project": { "path": "p/package.json", "name": "n", "rootNamespace": null, "buildCommand": "b" } } } }""", true },
        { """{ "version": 2, "testCasesDir": "test-cases", "domains": {} }""", false },
        { """{ "testCasesDir": "test-cases", "domains": {} }""", false },
        { """{ "version": 1, "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "test-cases" }""", false },
        { """{ "version": 1, "testCasesDir": "/abs", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "C:/abs", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "../up", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "a/../b", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "a\\b", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "", "domains": {} }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": {}, "extra": 1 }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "api": {} } }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "web": { "project": { "path": "a.csproj", "name": "a" } } } }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "web": { "project": { "path": "a.csproj", "name": "", "buildCommand": "b" } } } }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "web": { "project": { "path": "a.csproj", "name": "a", "buildCommand": "b", "rootNamespace": "" } } } }""", false },
        { """{ "version": 1, "testCasesDir": "t", "domains": { "web": { "project": { "path": "a.csproj", "name": "a", "buildCommand": "b", "x": 1 } } } }""", false },
        { """{ "version": 1, "testCasesDir": 3, "domains": {} }""", false },
        { """[]""", false },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Schema_AndCliValidator_Agree(string json, bool expected)
    {
        var node = JsonNode.Parse(json);
        var bySchema = SchemaAccepts(json);
        var byCli = BellatrixConfig.Validate(node).Count == 0;

        Assert.Equal(expected, bySchema);
        Assert.Equal(expected, byCli);
    }

    [Fact]
    public void EmbeddedSchema_IsTheRepositoryFile_AndCarriesTheUrlInTheConfig()
    {
        var repo = File.ReadAllText(Path.Combine(RepoRoot(), "schemas", "bellatrix-config.v1.schema.json")).Replace("\r\n", "\n");
        Assert.Equal(repo, BellatrixConfig.SchemaText().Replace("\r\n", "\n"));
        Assert.Equal(BellatrixConfig.SchemaUrl, JsonNode.Parse(repo)!["$id"]!.GetValue<string>());
        Assert.Equal(BellatrixConfig.SchemaUrl, BellatrixConfig.Create("test-cases")["$schema"]!.GetValue<string>());
    }

    [Fact]
    public void TheSchemaValidator_IsATestOnlyDependency()
    {
        // The CLI validates with its own BellatrixConfig.Validate; the schema library only cross-checks it here.
        var cli = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Bellatrix.Cli", "Bellatrix.Cli.csproj"));
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex("Corvus|JsonSchema", System.Text.RegularExpressions.RegexOptions.IgnoreCase), cli);
        Assert.Contains("Corvus.Json.Validator", File.ReadAllText(Path.Combine(RepoRoot(), "tests", "Bellatrix.Cli.Tests", "Bellatrix.Cli.Tests.csproj")));
    }

    [Fact]
    public void GeneratedConfig_SchemaUrl_IsTheReleaseTagOfThePackageVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Bellatrix.Cli", "Bellatrix.Cli.csproj"));
        var version = System.Text.RegularExpressions.Regex.Match(csproj, "<Version>([^<]+)</Version>").Groups[1].Value;
        Assert.Equal(version, CliRunner.Version);

        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));
        Assert.Equal(0, CliHarness.Run(repo.Root, "init", "--agent", "claude", "--language", "csharp", "--domains", "desktop", "--no-interaction").Exit);

        var url = BellatrixConfig.Load(repo.Root).Document!["$schema"]!.GetValue<string>();
        Assert.Equal($"https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-AI-Agents/v{version}/schemas/bellatrix-config.v1.schema.json", url);
        Assert.Contains($"/v{version}/", url);
        Assert.DoesNotContain("/main/", url);

        // The schema file names the same tag, and so do the README example and the release checklist.
        Assert.Equal(url, JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "schemas", "bellatrix-config.v1.schema.json")))!["$id"]!.GetValue<string>());
        Assert.Contains(url, File.ReadAllText(Path.Combine(RepoRoot(), "README.md")));
    }

    // ---------------------------------------------------------------- discovery

    [Fact]
    public void Discovery_CSharp_FindsDomainsByReference_AndSkipsTheVendoredFramework()
    {
        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop", "Bellatrix.Web"));
        repo.Write("src/Acme.Mobile/Acme.Mobile.csproj",
            "<Project><PropertyGroup><RootNamespace>Acme.Phone</RootNamespace></PropertyGroup><ItemGroup><ProjectReference Include=\"..\\..\\BELLATRIX\\src\\Bellatrix.Mobile\\Bellatrix.Mobile.csproj\" /><PackageReference Include=\"Bellatrix.Mobile.DemoApps\" /></ItemGroup></Project>");
        repo.Write("src/Acme.Core/Acme.Core.csproj", Csproj("Newtonsoft.Json"));
        repo.Write("BELLATRIX/src/Bellatrix.Desktop/Bellatrix.Desktop.csproj", Csproj("Bellatrix.Core"));
        repo.Write("BELLATRIX/tests/Bellatrix.Desktop.Tests/Bellatrix.Desktop.Tests.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("src/Acme.UiTests/bin/Debug/Copy.csproj", Csproj("Bellatrix.Desktop"));

        var found = ProjectDiscovery.Find(repo.Root, "csharp");

        Assert.Equal(
            new[] { "desktop src/Acme.UiTests/Acme.UiTests.csproj", "mobile src/Acme.Mobile/Acme.Mobile.csproj", "web src/Acme.UiTests/Acme.UiTests.csproj" },
            found.Select(c => $"{c.Domain} {c.Project.Path}"));
        var mobile = found.Single(c => c.Domain == "mobile").Project;
        Assert.Equal("Acme.Phone", mobile.RootNamespace);
        Assert.Equal("dotnet build src/Acme.Mobile/Acme.Mobile.csproj", mobile.BuildCommand);
        Assert.Equal("src/Acme.Mobile", mobile.Dir);
    }

    [Fact]
    public void Discovery_Java_ReadsThePom_AndTheBasePackage()
    {
        using var repo = new TempRepo();
        repo.Write("acme-ui-tests/pom.xml", """
            <project><parent><groupId>com.acme</groupId><artifactId>acme-parent</artifactId></parent>
              <artifactId>acme-ui-tests</artifactId>
              <dependencies>
                <dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.web</artifactId></dependency>
                <dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.android</artifactId></dependency>
              </dependencies></project>
            """);
        repo.Write("acme-ui-tests/src/test/java/com/acme/ui/pages/LoginPage.java", "package com.acme.ui.pages;\nclass LoginPage {}");
        repo.Write("acme-ui-tests/src/test/java/com/acme/ui/tests/LoginTests.java", "package com.acme.ui.tests;\nclass LoginTests {}");
        repo.Write("bellatrix/bellatrix.web/pom.xml",
            "<project><artifactId>bellatrix.web</artifactId><dependencies><dependency><groupId>solutions.bellatrix</groupId><artifactId>bellatrix.core</artifactId></dependency></dependencies></project>");

        var found = ProjectDiscovery.Find(repo.Root, "java");

        Assert.Equal(new[] { "mobile", "web" }, found.Select(c => c.Domain));
        var project = found[0].Project;
        Assert.Equal(("acme-ui-tests/pom.xml", "acme-ui-tests", "com.acme.ui", "mvn -q -f acme-ui-tests/pom.xml test-compile"),
            (project.Path, project.Name, project.RootNamespace, project.BuildCommand));
    }

    [Fact]
    public void Discovery_TypeScript_ReadsPackageJson()
    {
        using var repo = new TempRepo();
        repo.Write("acme-ui-tests/package.json", """{ "name": "acme-ui-tests", "devDependencies": { "@bellatrix/web": "^1.0.0" } }""");
        repo.Write("acme-ui-tests/node_modules/@bellatrix/web/package.json", """{ "name": "@bellatrix/web", "dependencies": { "@bellatrix/core": "1" } }""");

        var found = ProjectDiscovery.Find(repo.Root, "typescript");

        var only = Assert.Single(found);
        Assert.Equal(("web", "acme-ui-tests", null, "npx tsc --noEmit -p acme-ui-tests/tsconfig.json"),
            (only.Domain, only.Project.Name, only.Project.RootNamespace, only.Project.BuildCommand));
    }

    // ---------------------------------------------------------------- init planner

    private static InitConfig.Plan Prepare(TempRepo repo, string? project = null, string? testCases = null, bool regenerate = false,
        bool interactive = false, IPrompter? prompter = null, string language = "csharp", params string[] domains)
        => InitConfig.Prepare(repo.Root, language, domains.Length > 0 ? domains : AllDomains, project, testCases, regenerate, interactive, prompter);

    [Fact]
    public void Prepare_EmptyRepo_CreatesAConfigWithoutProjects_AndWarnsPerDomain()
    {
        using var repo = new TempRepo();

        var plan = Prepare(repo);

        Assert.Equal("created", plan.Outcome.status);
        Assert.Empty(plan.Document!["domains"]!.AsObject());
        Assert.Equal("test-cases", plan.Document["testCasesDir"]!.GetValue<string>());
        Assert.Equal(3, plan.Outcome.warnings.Count(w => w.StartsWith("No BELLATRIX", StringComparison.Ordinal)));
        Assert.Empty(BellatrixConfig.Validate(plan.Document));
    }

    [Fact]
    public void Prepare_Ambiguous_IsAUsageErrorNamingTheCandidates()
    {
        using var repo = new TempRepo();
        repo.Write("a/Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/Acme.Other/Acme.Other.csproj", Csproj("Bellatrix.Desktop"));

        var ex = Assert.Throws<CliException>(() => Prepare(repo, domains: "desktop"));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("a/Acme.Shared/Acme.Shared.csproj", ex.Message);
        Assert.Contains("b/Acme.Other/Acme.Other.csproj", ex.Message);
        Assert.Contains("--project desktop=", ex.Message);
    }

    [Fact]
    public void Prepare_TieBreak_ProposesTheOneNamedForTheDomain()
    {
        using var repo = new TempRepo();
        repo.Write("Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("Acme.DesktopTests/Acme.DesktopTests.csproj", Csproj("Bellatrix.Desktop"));

        var plan = Prepare(repo, domains: "desktop");

        Assert.Equal("Acme.DesktopTests", BellatrixConfig.ProjectOf(plan.Document!, "desktop")!.Name);
    }

    [Fact]
    public void Prepare_ProjectFlag_PlainAndPerDomain()
    {
        using var repo = new TempRepo();
        repo.Write("a/A.csproj", Csproj());
        repo.Write("b/pom.xml", "<project><artifactId>b-tests</artifactId></project>");

        var plain = Prepare(repo, project: "a/A.csproj", domains: new[] { "desktop", "web" });
        Assert.Equal("a/A.csproj", BellatrixConfig.ProjectOf(plain.Document!, "desktop")!.Path);
        Assert.Equal("a/A.csproj", BellatrixConfig.ProjectOf(plain.Document!, "web")!.Path);

        var perDomain = Prepare(repo, project: "desktop=a, web=b/pom.xml", domains: new[] { "desktop", "web" });
        Assert.Equal("a/A.csproj", BellatrixConfig.ProjectOf(perDomain.Document!, "desktop")!.Path);
        Assert.Equal(("b-tests", "mvn -q -f b/pom.xml test-compile"),
            (BellatrixConfig.ProjectOf(perDomain.Document!, "web")!.Name, BellatrixConfig.ProjectOf(perDomain.Document!, "web")!.BuildCommand));

        Assert.Equal(2, Assert.Throws<CliException>(() => Prepare(repo, project: "missing/X.csproj", domains: "desktop")).ExitCode);
        Assert.Equal(2, Assert.Throws<CliException>(() => Prepare(repo, project: "mobile=a/A.csproj", domains: "desktop")).ExitCode);
        Assert.Throws<CliException>(() => Prepare(repo, testCases: "../outside", domains: "desktop"));
    }

    [Fact]
    public void Prepare_TestCasesDir_FlagWins_ThenSpectraConfig_ThenDefault()
    {
        using var repo = new TempRepo();
        Assert.Equal("test-cases", Prepare(repo).Document!["testCasesDir"]!.GetValue<string>());

        repo.Write("spectra.config.json", """{ "tests": { "dir": "./specs/" } }""");
        Assert.Equal("specs", Prepare(repo).Document!["testCasesDir"]!.GetValue<string>());

        Assert.Equal("qa/cases", Prepare(repo, testCases: "qa/cases").Document!["testCasesDir"]!.GetValue<string>());

        var perDomain = Prepare(repo, testCases: "web=qa/web", domains: new[] { "desktop", "web" }).Document!;
        Assert.Equal("specs", BellatrixConfig.TestCasesDirOf(perDomain, "desktop"));
        Assert.Equal("qa/web", BellatrixConfig.TestCasesDirOf(perDomain, "web"));
    }

    [Fact]
    public void Prepare_ReRun_KeepsRecordedValues_MergesNewDomains_AndRegenerateReplaces()
    {
        using var repo = new TempRepo();
        repo.Write("Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop", "Bellatrix.Web"));
        repo.Write("Other/Other.csproj", Csproj());
        BellatrixConfig.Save(repo.Root, Prepare(repo, domains: "desktop").Document!);

        // A hand edit survives a plain re-run.
        var edited = BellatrixConfig.Load(repo.Root).Document!;
        edited["domains"]!["desktop"]!["project"]!["buildCommand"] = "dotnet build -c Release Acme.UiTests/Acme.UiTests.csproj";
        edited["testCasesDir"] = "my-cases";
        BellatrixConfig.Save(repo.Root, edited);

        var same = Prepare(repo, domains: "desktop");
        Assert.Equal("unchanged", same.Outcome.status);
        Assert.False(same.Write);

        var merged = Prepare(repo, project: "desktop=Other/Other.csproj", testCases: "elsewhere", domains: new[] { "desktop", "web" });
        Assert.Equal("merged", merged.Outcome.status);
        Assert.Equal("dotnet build -c Release Acme.UiTests/Acme.UiTests.csproj", BellatrixConfig.ProjectOf(merged.Document!, "desktop")!.BuildCommand);
        Assert.Equal("Acme.UiTests/Acme.UiTests.csproj", BellatrixConfig.ProjectOf(merged.Document!, "web")!.Path);
        Assert.Equal("my-cases", merged.Document!["testCasesDir"]!.GetValue<string>());
        Assert.Contains(merged.Outcome.warnings, w => w.Contains("desktop project stays", StringComparison.Ordinal));
        Assert.Contains(merged.Outcome.warnings, w => w.Contains("testCasesDir stays", StringComparison.Ordinal));

        var replaced = Prepare(repo, project: "desktop=Other/Other.csproj", regenerate: true, domains: "desktop");
        Assert.Equal("replaced", replaced.Outcome.status);
        Assert.Equal("Other/Other.csproj", BellatrixConfig.ProjectOf(replaced.Document!, "desktop")!.Path);
        Assert.Equal("test-cases", replaced.Document!["testCasesDir"]!.GetValue<string>());
    }

    [Fact]
    public void Prepare_InvalidFile_IsLeftUntouched_UnlessRegenerate()
    {
        using var repo = new TempRepo();
        repo.Write(".bellatrix/config.json", "{ \"version\": 7 ");

        var kept = Prepare(repo);
        Assert.Equal("kept-invalid", kept.Outcome.status);
        Assert.Null(kept.Document);
        Assert.Contains("--regenerate", Assert.Single(kept.Outcome.warnings));

        Assert.Equal("replaced", Prepare(repo, regenerate: true).Outcome.status);
    }

    private sealed class ScriptedPrompter : IPrompter
    {
        private readonly Queue<string?> _answers;

        public ScriptedPrompter(params string?[] answers) => _answers = new Queue<string?>(answers);

        public List<string> Questions { get; } = new();

        public string? Ask(string question)
        {
            Questions.Add(question);
            return _answers.Count > 0 ? _answers.Dequeue() : null;
        }
    }

    [Fact]
    public void Prepare_Interactive_ConfirmsPicksOrTakesAPath()
    {
        using var repo = new TempRepo();
        repo.Write("a/Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/Acme.Other/Acme.Other.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("c/Typed/Typed.csproj", Csproj());

        var pick = new ScriptedPrompter("2");
        Assert.Equal("b/Acme.Other/Acme.Other.csproj",
            BellatrixConfig.ProjectOf(Prepare(repo, interactive: true, prompter: pick, domains: "desktop").Document!, "desktop")!.Path);
        Assert.Contains("[1] a/Acme.Shared/Acme.Shared.csproj", pick.Questions[0]);

        var typed = new ScriptedPrompter("nope/Missing.csproj", "c/Typed/Typed.csproj");
        Assert.Equal("Typed",
            BellatrixConfig.ProjectOf(Prepare(repo, interactive: true, prompter: typed, domains: "desktop").Document!, "desktop")!.Name);

        var skip = new ScriptedPrompter("-");
        Assert.Null(BellatrixConfig.ProjectOf(Prepare(repo, interactive: true, prompter: skip, domains: "desktop").Document!, "desktop"));
    }

    [Fact]
    public void Prepare_Interactive_EndOfInput_CancelsWithExit130()
    {
        using var repo = new TempRepo();
        repo.Write("Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));

        var ex = Assert.Throws<CliException>(() => Prepare(repo, interactive: true, prompter: new ScriptedPrompter(), domains: "desktop"));

        Assert.Equal(("cancelled", 130), (ex.Code, ex.ExitCode));
        Assert.False(repo.Exists(".bellatrix/config.json"));
    }

    [Fact]
    public void Prepare_Interactive_EnterAcceptsTheProposal()
    {
        using var repo = new TempRepo();
        repo.Write("Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));

        var prompter = new ScriptedPrompter("");
        var plan = Prepare(repo, interactive: true, prompter: prompter, domains: "desktop");

        Assert.Equal("Acme.UiTests", BellatrixConfig.ProjectOf(plan.Document!, "desktop")!.Name);
        Assert.Contains("(proposed)", prompter.Questions.Single());
    }

    // ---------------------------------------------------------------- bellatrix init / config through the CLI

    [Fact]
    public void Init_WritesTheConfig_ReportsIt_AndAsksToCommitIt()
    {
        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));

        var (exit, output, errors) = CliHarness.Run(repo.Root, "init", "--agent", "claude", "--language", "csharp", "--domains", "desktop", "--no-interaction");

        Assert.Equal(0, exit);
        var config = JsonNode.Parse(output)!["config"]!;
        Assert.Equal("created", config["status"]!.GetValue<string>());
        Assert.True(config["commit"]!.GetValue<bool>());
        Assert.Contains("Commit .bellatrix/config.json", JsonNode.Parse(output)!["decisions"]!.ToJsonString());
        Assert.Contains("Commit .bellatrix/config.json", errors);
        Assert.Equal("src/Acme.UiTests/Acme.UiTests.csproj", BellatrixConfig.ProjectOf(BellatrixConfig.Load(repo.Root).Document!, "desktop")!.Path);

        // Not tracked as a scaffolded file: a re-run never lists it, and it is never removed.
        Assert.DoesNotContain(".bellatrix/config.json", repo.Read(".bellatrix/manifest.claude.json"));
    }

    [Fact]
    public void Init_DryRun_ReportsTheConfig_WithoutWritingIt()
    {
        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));

        var (exit, output, _) = CliHarness.Run(repo.Root, "init", "--agent", "claude", "--language", "csharp", "--domains", "desktop", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Equal("created", JsonNode.Parse(output)!["config"]!["status"]!.GetValue<string>());
        Assert.False(repo.Exists(".bellatrix/config.json"));
    }

    [Fact]
    public void Init_Ambiguous_FailsBeforeWritingAnything()
    {
        using var repo = new TempRepo();
        repo.Write("a/A.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/B.csproj", Csproj("Bellatrix.Desktop"));

        var (exit, output, _) = CliHarness.Run(repo.Root, "init", "--agent", "claude", "--language", "csharp", "--domains", "desktop");

        Assert.Equal(2, exit);
        Assert.Contains("Several BELLATRIX desktop test projects", output);
        Assert.False(Directory.Exists(repo.Path(".bellatrix")));
        Assert.False(Directory.Exists(repo.Path(".claude")));
    }

    [Fact]
    public void ConfigValidate_And_Show()
    {
        using var repo = new TempRepo();

        var (missing, missingOut, _) = CliHarness.Run(repo.Root, "config", "validate");
        Assert.Equal(1, missing);
        Assert.Equal("config_missing", JsonNode.Parse(missingOut)!["error"]!["code"]!.GetValue<string>());

        repo.Write(".bellatrix/config.json", """{ "version": 1, "testCasesDir": "test-cases", "domains": { "desktop": { "project": { "path": "x.csproj" } } } }""");
        var (invalid, invalidOut, _) = CliHarness.Run(repo.Root, "config", "validate");
        Assert.Equal(1, invalid);
        var error = JsonNode.Parse(invalidOut)!["error"]!;
        Assert.Equal("config_invalid", error["code"]!.GetValue<string>());
        Assert.Contains(error["details"]!["errors"]!.AsArray(), e => e!["path"]!.GetValue<string>() == "$.domains.desktop.project.name");

        repo.Write("src/A/A.csproj", Csproj());
        repo.Write(".bellatrix/config.json", """
            { "version": 1, "testCasesDir": "test-cases", "domains": {
                "desktop": { "project": { "path": "src/A/A.csproj", "name": "A", "rootNamespace": "A", "buildCommand": "dotnet build src/A/A.csproj" } },
                "web": { "project": { "path": "gone/B.csproj", "name": "B", "buildCommand": "dotnet build gone/B.csproj" }, "testCasesDir": "web-cases" } } }
            """);
        var (ok, okOut, _) = CliHarness.Run(repo.Root, "config", "validate");
        Assert.Equal(0, ok);
        Assert.Contains("gone/B.csproj does not exist", okOut);

        var (shown, showOut, _) = CliHarness.Run(repo.Root, "config", "show");
        Assert.Equal(0, shown);
        var resolved = JsonNode.Parse(showOut)!["resolved"]!;
        Assert.Equal("src/A", resolved["desktop"]!["projectDir"]!.GetValue<string>());
        Assert.Equal("test-cases", resolved["desktop"]!["testCasesDir"]!.GetValue<string>());
        Assert.Equal("web-cases", resolved["web"]!["testCasesDir"]!.GetValue<string>());

        var (_, outside, _) = CliHarness.Run(Path.GetTempPath(), "config", "show", "--out", repo.Root);
        Assert.True(JsonNode.Parse(outside)!["valid"]!.GetValue<bool>());
    }
}
