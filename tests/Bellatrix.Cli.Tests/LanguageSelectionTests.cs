using Bellatrix.Cli.Commands;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary><c>--language csharp|java|typescript</c>, auto-detection, recorded language, and gaps.</summary>
public class LanguageSelectionTests
{
    [Theory]
    [InlineData("csharp", "csharp")]
    [InlineData("C#", "csharp")]
    [InlineData("dotnet", "csharp")]
    [InlineData("Java", "java")]
    [InlineData("ts", "typescript")]
    [InlineData("typescript", "typescript")]
    public void Parse_AcceptsLanguagesAndAliases(string raw, string expected)
        => Assert.Equal(expected, LanguageSelection.Parse(raw));

    [Fact]
    public void Parse_Unknown_IsUsageExit2()
    {
        var ex = Assert.Throws<CliException>(() => LanguageSelection.Parse("kotlin"));
        Assert.Equal("usage", ex.Code);
        Assert.Equal(2, ex.ExitCode);
    }

    [Theory]
    [InlineData("App.Tests.csproj", "csharp")]
    [InlineData("All.sln", "csharp")]
    [InlineData("pom.xml", "java")]
    [InlineData("build.gradle.kts", "java")]
    public void Detect_FromBuildFiles(string file, string expected)
    {
        using var repo = new TempRepo();
        repo.Write(file, "x");
        Assert.Equal(expected, LanguageSelection.Detect(repo.Root, recorded: null));
    }

    [Fact]
    public void Detect_TypeScript_NeedsPackageJsonAndTsconfig()
    {
        using var repo = new TempRepo();
        repo.Write("package.json", "{}");
        Assert.Equal("csharp", LanguageSelection.Detect(repo.Root, null, TextWriter.Null)); // package.json alone: not TS
        repo.Write("tsconfig.json", "{}");
        Assert.Equal("typescript", LanguageSelection.Detect(repo.Root, null));
    }

    [Fact]
    public void Detect_Several_IsUsageExit2_ButRecordedLanguageWins()
    {
        using var repo = new TempRepo();
        repo.Write("pom.xml", "x");
        repo.Write("Tests.csproj", "x");

        var ex = Assert.Throws<CliException>(() => LanguageSelection.Detect(repo.Root, null));
        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("csharp, java", ex.Message);
        Assert.Equal("java", LanguageSelection.Detect(repo.Root, recorded: "java"));
    }

    [Fact]
    public void Detect_Nothing_DefaultsToCSharp_WithNotice()
    {
        using var repo = new TempRepo();
        var notices = new StringWriter();
        Assert.Equal("csharp", LanguageSelection.Detect(repo.Root, null, notices));
        Assert.Contains("--language java|typescript", notices.ToString());
    }

    [Fact]
    public void Scaffold_RecordsLanguageInEveryAgentManifest_AndReportsIt()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude,cursor", regenerate: false,
            domains: ScaffoldCommand.ParseDomains("desktop"));

        Assert.Equal("csharp", result.language);
        Assert.Empty(result.unsupported);
        Assert.Contains("\"language\": \"csharp\"", repo.Read(".bellatrix/manifest.claude.json"));
        Assert.Contains("\"language\": \"csharp\"", repo.Read(".bellatrix/manifest.cursor.json"));
    }

    [Fact]
    public void Scaffold_LanguageWithoutParts_KeepsAgnosticTopics_AndWarns()
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false,
            domains: ScaffoldCommand.ParseDomains("desktop"), language: "cobol");

        Assert.Contains("bellatrix-desktop-components", result.unsupported);
        Assert.True(repo.Exists(".claude/skills/bellatrix-desktop-execute-spectra-test/SKILL.md")); // agnostic
        Assert.True(repo.Exists(".claude/agents/bellatrix-desktop-locator-specialist.md"));          // agnostic
        Assert.False(repo.Exists(".claude/skills/bellatrix-desktop-components/SKILL.md"));
        Assert.Contains(result.warnings, w => w.StartsWith("No cobol version of:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("mobile")]
    public void Scaffold_TypeScriptDesktopOrMobile_EmitsOnlyAgnosticCliSkills_AndWarnsWebOnly(string domain)
    {
        using var repo = new TempRepo();
        var result = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false,
            domains: ScaffoldCommand.ParseDomains(domain), language: "typescript");

        Assert.Contains(result.warnings, w => w.Contains("BELLATRIX TypeScript supports web only", StringComparison.Ordinal));
        Assert.Contains($"bellatrix-{domain}-components", result.unsupported);

        var skills = Directory.GetDirectories(Path.Combine(repo.Root, ".claude", "skills"))
            .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { $"bellatrix-{domain}-execute-scenario", $"bellatrix-{domain}-execute-spectra-test" }, skills);

        var agents = Directory.GetFiles(Path.Combine(repo.Root, ".claude", "agents"))
            .Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { $"bellatrix-{domain}-locator-specialist.md" }, agents);

        var scenario = repo.Read($".claude/skills/bellatrix-{domain}-execute-scenario/SKILL.md");
        Assert.Contains("TypeScript: CLI only", scenario);
        Assert.DoesNotContain("<!-- lang:", scenario);
    }
}
