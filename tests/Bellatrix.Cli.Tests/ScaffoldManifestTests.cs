using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

public class ScaffoldManifestTests
{
    private const string SampleSkill = ".claude/skills/bellatrix-desktop-components/SKILL.md";

    [Fact]
    public void Scaffold_CleanRerun_IsIdempotent_NoChanges()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
        var before = repo.Read(SampleSkill);

        var second = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Empty(second.emitted);                    // nothing re-written
        Assert.Empty(second.skipped);
        Assert.Equal(before, repo.Read(SampleSkill));
    }

    [Fact]
    public void Scaffold_CrlfCheckout_IsNotTreatedAsUserEdit()
    {
        // Git with core.autocrlf=true checks the committed LF scaffold out as CRLF; that is not a developer edit.
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
        var crlf = repo.Read(SampleSkill).Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(repo.Path(SampleSkill), crlf);

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.DoesNotContain(SampleSkill, rerun.skipped);
        Assert.DoesNotContain(SampleSkill, rerun.emitted); // same content → bytes left untouched (no EOL churn)
        Assert.Equal(crlf, repo.Read(SampleSkill));
    }

    [Fact]
    public void Scaffold_CrlfCheckout_OfOutdatedTemplate_IsRefreshed()
    {
        // An unedited-but-outdated CRLF copy (recorded hash = old LF bytes) must be refreshed, not skipped.
        using var repo = new TempRepo();
        var recordedHash = Bellatrix.Cli.Scaffolding.ScaffoldManifest.Sha256Hex(System.Text.Encoding.UTF8.GetBytes("old template body\n"));
        repo.Write(".bellatrix/manifest.json",
            $"{{ \"version\": \"2.0\", \"target\": \"claude\", \"files\": [ {{ \"path\": \"{SampleSkill}\", \"sha256\": \"{recordedHash}\" }} ] }}");
        repo.Write(SampleSkill, "old template body\r\n");

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Contains(SampleSkill, rerun.emitted);
        Assert.DoesNotContain("old template body", repo.Read(SampleSkill));
    }

    [Fact]
    public void Scaffold_PreservesUserEditedFile_AndReportsSkipped()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        var edited = repo.Read(SampleSkill) + "\n<!-- LOCAL EDIT -->\n";
        repo.Write(SampleSkill, edited);

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.Contains(SampleSkill, rerun.skipped);
        Assert.Contains("<!-- LOCAL EDIT -->", repo.Read(SampleSkill));   // edit preserved (R6)
    }

    [Fact]
    public void Scaffold_Regenerate_OverwritesUserEditedFile()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
        repo.Write(SampleSkill, repo.Read(SampleSkill) + "\n<!-- LOCAL EDIT -->\n");

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: true);

        Assert.DoesNotContain(SampleSkill, rerun.skipped);
        Assert.DoesNotContain("<!-- LOCAL EDIT -->", repo.Read(SampleSkill));   // refreshed (R8 --regenerate)
    }

    [Fact]
    public void Scaffold_RecreatesDeletedOwnedFile()
    {
        using var repo = new TempRepo();
        ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);
        File.Delete(repo.Path(SampleSkill));

        var rerun = ScaffoldCommand.Scaffold(repo.Root, "claude", regenerate: false);

        Assert.True(repo.Exists(SampleSkill));
        Assert.Contains(SampleSkill, rerun.emitted);
    }
}
