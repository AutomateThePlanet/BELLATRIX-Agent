using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

public class ClaudeMdRegistrarTests
{
    [Fact]
    public void Register_AbsentFile_CreatesMinimalFileWithHeadingAndImport()
    {
        using var repo = new TempRepo();
        var path = repo.Path("CLAUDE.md");

        var outcome = ClaudeMdRegistrar.Register(path);

        Assert.Equal(RegistrationOutcome.Created, outcome);
        var content = File.ReadAllText(path);
        Assert.Contains("# Tool instructions", content);
        Assert.Contains("@.bellatrix/bellatrix.instructions.md", content);
    }

    [Fact]
    public void Register_ExistingHumanBody_IsPreservedByteForByte()
    {
        using var repo = new TempRepo();
        var human = "# My Project\n\nHuman-owned notes.\nMore prose.\n";
        repo.Write("CLAUDE.md", human);
        var path = repo.Path("CLAUDE.md");

        var outcome = ClaudeMdRegistrar.Register(path);

        Assert.Equal(RegistrationOutcome.Added, outcome);
        var content = File.ReadAllText(path);
        Assert.StartsWith(human, content);                       // append-only: original bytes intact
        Assert.Contains("# Tool instructions", content);
        Assert.Contains("@.bellatrix/bellatrix.instructions.md", content);
    }

    [Fact]
    public void Register_Twice_IsIdempotent_NoDuplicateImportLine()
    {
        using var repo = new TempRepo();
        repo.Write("CLAUDE.md", "# My Project\n\nNotes.\n");
        var path = repo.Path("CLAUDE.md");

        ClaudeMdRegistrar.Register(path);
        var afterFirst = File.ReadAllText(path);
        var second = ClaudeMdRegistrar.Register(path);

        Assert.Equal(RegistrationOutcome.AlreadyPresent, second);
        Assert.Equal(afterFirst, File.ReadAllText(path));        // no change on re-run
        var occurrences = afterFirst.Split("@.bellatrix/bellatrix.instructions.md").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Register_PreservesForeignImportLines()
    {
        using var repo = new TempRepo();
        repo.Write("CLAUDE.md", "# Tool instructions\n\n@.spectra/spectra.instructions.md\n");
        var path = repo.Path("CLAUDE.md");

        ClaudeMdRegistrar.Register(path);

        var content = File.ReadAllText(path);
        Assert.Contains("@.spectra/spectra.instructions.md", content);   // foreign import untouched
        Assert.Contains("@.bellatrix/bellatrix.instructions.md", content);
        // existing heading reused — not duplicated
        Assert.Equal(1, content.Split("# Tool instructions").Length - 1);
    }
}
