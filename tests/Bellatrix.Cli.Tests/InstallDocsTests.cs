using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// The install instructions match how the package is really distributed: NuGet.org first, and the same .nupkg as a
/// GitHub release asset for pinned or offline installs with <c>--add-source</c>.
/// </summary>
public class InstallDocsTests
{
    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot(), relative)).Replace("\r\n", "\n");

    public static TheoryData<string> InstallDocs() => new()
    {
        "README.md",
        "src/Bellatrix.Cli/Package/README.md",
        "docs/INSTALL-PREVIEW.md",
        "CONTRIBUTING.md",
        "docs/TESTING.md",
    };

    [Fact]
    public void ReleaseWorkflow_PushesWithTrustedPublishing_AndKeepsTheReleaseAssets()
    {
        var workflow = Read(".github/workflows/release.yml");

        // Trusted Publishing: the nuget.org policy names this file and the "release" environment; no API key secret.
        Assert.Contains("environment: release", workflow);
        Assert.Contains("id-token: write", workflow);
        Assert.Contains("uses: NuGet/login@v1", workflow);
        Assert.Contains("user: angelovstanton", workflow);
        Assert.Contains("--api-key \"${{ steps.login.outputs.NUGET_API_KEY }}\"", workflow);
        Assert.Contains("--skip-duplicate", workflow);
        Assert.DoesNotContain("secrets.", workflow);

        // Nothing is published before the tag/version and schema guards pass.
        var push = workflow.IndexOf("dotnet nuget push", StringComparison.Ordinal);
        Assert.True(workflow.IndexOf("Tag matches the package version", StringComparison.Ordinal) is var v && v >= 0 && v < push);
        Assert.True(workflow.IndexOf("Config schema is served at this tag", StringComparison.Ordinal) is var s && s >= 0 && s < push);

        // The .nupkg and its .sha256 stay GitHub release assets (the --add-source install in INSTALL-PREVIEW).
        Assert.Contains("gh release create $env:TAG $env:PACKAGE \"$env:PACKAGE.sha256\"", workflow);
    }

    [Fact]
    public void Readme_QuickStart_InstallsFromNuGetOrg()
    {
        var readme = Read("README.md");
        Assert.Contains("dotnet tool install -g Bellatrix.Cli\n", readme);
        Assert.Contains("https://www.nuget.org/packages/Bellatrix.Cli", readme);
        Assert.DoesNotContain("coming soon", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InstallDocs))]
    public void EveryLocalSourceInstall_PinsTheVersion(string relative)
    {
        // An install from a downloaded .nupkg folder must name the version, or it silently picks NuGet.org's.
        var bad = Read(relative).Split('\n')
            .Select((line, i) => (line, i))
            .Where(x => Regex.IsMatch(x.line, @"dotnet tool (install|update) (-g|--global) Bellatrix\.Cli") && x.line.Contains("--add-source") && !x.line.Contains("--version"))
            .Select(x => $"{relative}:{x.i + 1}: {x.line.Trim()}")
            .ToArray();

        Assert.True(bad.Length == 0, "--add-source installs need --version:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void InstallPreview_KeepsTheGitHubReleasePackagePath()
    {
        // The .nupkg stays a GitHub release asset, so the documented download-and---add-source install keeps working.
        var install = Read("docs/INSTALL-PREVIEW.md");
        Assert.Contains("gh release download", install);
        Assert.Contains("--add-source", install);
    }

    [Fact]
    public void Changelog_NamesTheFirstNuGetOrgVersion()
    {
        var changelog = Read("CHANGELOG.md");
        Assert.DoesNotContain("No version is on NuGet.org yet", changelog);
        Assert.Contains("3.2.0 is the first version on NuGet.org", changelog);
    }
}
