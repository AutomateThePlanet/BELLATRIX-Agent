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
    public void ReleaseWorkflow_StillPublishesToGitHubReleasesOnly()
    {
        var workflow = Read(".github/workflows/release.yml");
        Assert.Contains("Nothing is pushed to NuGet.org", workflow);
        Assert.DoesNotContain("dotnet nuget push", workflow);
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
    public void Changelog_DoesNotClaimANuGetOrgRelease()
    {
        var changelog = Read("CHANGELOG.md");
        Assert.DoesNotMatch(new Regex(@"first NuGet\.org release", RegexOptions.IgnoreCase), changelog);
        Assert.Contains("No version is on NuGet.org yet", changelog);
    }
}
