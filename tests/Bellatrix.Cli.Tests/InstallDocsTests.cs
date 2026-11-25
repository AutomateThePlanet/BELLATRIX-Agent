using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// The install instructions match how the package is really distributed: GitHub Releases, not NuGet.org
/// (<c>release.yml</c> pushes nothing to NuGet.org). Once the package is on NuGet.org, change the workflow and these
/// docs together, and this test with them.
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

    [Theory]
    [MemberData(nameof(InstallDocs))]
    public void EveryToolInstall_NamesTheDownloadedPackageSource(string relative)
    {
        var lines = Read(relative).Split('\n');
        var bad = lines
            .Select((line, i) => (line, i))
            .Where(x => Regex.IsMatch(x.line, @"dotnet tool (install|update) (-g|--global) Bellatrix\.Cli") && !x.line.Contains("--add-source"))
            .Select(x => $"{relative}:{x.i + 1}: {x.line.Trim()}")
            .ToArray();

        Assert.True(bad.Length == 0, "Bellatrix.Cli is not on NuGet.org; installs need --add-source:\n" + string.Join("\n", bad));
    }

    [Theory]
    [MemberData(nameof(InstallDocs))]
    public void NoDocClaimsTheNuGetOrgPackage(string relative)
    {
        var text = Read(relative);
        Assert.DoesNotContain("nuget.org/packages/Bellatrix.Cli", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("img.shields.io/nuget", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Changelog_DoesNotClaimANuGetOrgRelease()
    {
        var changelog = Read("CHANGELOG.md");
        Assert.DoesNotMatch(new Regex(@"first NuGet\.org release", RegexOptions.IgnoreCase), changelog);
        Assert.Contains("No version is on NuGet.org yet", changelog);
    }
}
