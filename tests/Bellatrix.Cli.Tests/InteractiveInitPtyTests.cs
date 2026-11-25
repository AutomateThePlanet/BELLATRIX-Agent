using Bellatrix.Cli.Config;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>bellatrix init</c> in a real terminal (a Windows pseudo console): the prompt appears only there, Enter confirms
/// the proposal, a typed number or path replaces it, Ctrl+C cancels with nothing written, and <c>CI</c>,
/// <c>--dry-run</c> and <c>--no-interaction</c> never prompt. The CLI runs as its own process (the built
/// <c>bellatrix.exe</c>), so nothing here is simulated.
/// </summary>
public class InteractiveInitPtyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private const string Prompt = "a number, a path to the project file, or '-' to skip";

    private static string Exe => Path.Combine(AppContext.BaseDirectory, "bellatrix.exe");

    private static string Csproj(params string[] references)
        => "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>"
           + string.Concat(references.Select(r => $"<PackageReference Include=\"{r}\" Version=\"1.0.0\" />"))
           + "</ItemGroup></Project>";

    /// <summary>The test runner's environment without CI markers (GitHub Actions sets CI=true), plus <paramref name="set"/>.</summary>
    private static Dictionary<string, string> Env(params (string, string?)[] set)
        => PseudoTerminal.Environment(new[] { ("CI", (string?)null), ("TF_BUILD", null), ("DOTNET_NOLOGO", "1") }.Concat(set).ToArray());

    private static PseudoTerminal Init(TempRepo repo, string extra = "", Dictionary<string, string>? env = null)
        => PseudoTerminal.Start($"\"{Exe}\" init --agent claude --language csharp --domains desktop {extra}".TrimEnd(), repo.Root, env ?? Env());

    private static string? DesktopProject(TempRepo repo)
        => BellatrixConfig.Load(repo.Root).Document is { } doc ? BellatrixConfig.ProjectOf(doc, "desktop")?.Path : null;

    [WindowsFact]
    public void Enter_ConfirmsTheProposedProject()
    {
        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));

        using var pty = Init(repo);
        Assert.True(pty.WaitFor(Prompt, Timeout), pty.Screen);
        Assert.True(pty.WaitFor("src/Acme.UiTests/Acme.UiTests.csproj  (proposed)", TimeSpan.FromSeconds(5)), pty.Screen);
        pty.Type("\r");

        Assert.Equal(0, pty.WaitForExit(Timeout));
        Assert.Equal("src/Acme.UiTests/Acme.UiTests.csproj", DesktopProject(repo));
        Assert.True(repo.Exists(".claude/agents/bellatrix-desktop-system-tests-generator.md"));
    }

    [WindowsFact]
    public void RejectingTheProposal_WithABadThenAGoodPath_RecordsTheTypedProject()
    {
        using var repo = new TempRepo();
        repo.Write("src/Acme.UiTests/Acme.UiTests.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("qa/Acme.Desktop.Specs/Acme.Desktop.Specs.csproj", Csproj());

        using var pty = Init(repo);
        Assert.True(pty.WaitFor(Prompt, Timeout), pty.Screen);
        pty.Type("nope/Missing.csproj\r");
        Assert.True(pty.WaitFor("does not exist", Timeout), pty.Screen);
        pty.Type("qa/Acme.Desktop.Specs/Acme.Desktop.Specs.csproj\r");

        Assert.Equal(0, pty.WaitForExit(Timeout));
        Assert.Equal("qa/Acme.Desktop.Specs/Acme.Desktop.Specs.csproj", DesktopProject(repo));
    }

    [WindowsFact]
    public void SeveralCandidates_ArePrompted_AndANumberPicksOne()
    {
        using var repo = new TempRepo();
        repo.Write("a/Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/Acme.Other/Acme.Other.csproj", Csproj("Bellatrix.Desktop"));

        using var pty = Init(repo);
        Assert.True(pty.WaitFor(Prompt, Timeout), pty.Screen);
        Assert.True(pty.WaitFor("[1] a/Acme.Shared/Acme.Shared.csproj", TimeSpan.FromSeconds(5)), pty.Screen);
        Assert.True(pty.WaitFor("[2] b/Acme.Other/Acme.Other.csproj", TimeSpan.FromSeconds(5)), pty.Screen);
        pty.Type("2\r");

        Assert.Equal(0, pty.WaitForExit(Timeout));
        Assert.Equal("b/Acme.Other/Acme.Other.csproj", DesktopProject(repo));
    }

    [WindowsFact]
    public void CtrlC_AtThePrompt_WritesNothing()
    {
        using var repo = new TempRepo();
        repo.Write("a/Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/Acme.Other/Acme.Other.csproj", Csproj("Bellatrix.Desktop"));
        var before = Directory.GetFileSystemEntries(repo.Root, "*", SearchOption.AllDirectories).Order().ToArray();

        using var pty = Init(repo);
        Assert.True(pty.WaitFor(Prompt, Timeout), pty.Screen);
        pty.Type("\x03");

        Assert.NotEqual(0, pty.WaitForExit(Timeout));
        Assert.Equal(before, Directory.GetFileSystemEntries(repo.Root, "*", SearchOption.AllDirectories).Order().ToArray());
    }

    [WindowsFact]
    public void CI_DryRun_And_NoInteraction_NeverPrompt()
    {
        using var repo = new TempRepo();
        repo.Write("a/Acme.Shared/Acme.Shared.csproj", Csproj("Bellatrix.Desktop"));
        repo.Write("b/Acme.Other/Acme.Other.csproj", Csproj("Bellatrix.Desktop"));

        // Two candidates and no prompt: each run fails fast with the "pick one" usage error (exit 2) and writes nothing.
        foreach (var (extra, env) in new[]
                 {
                     ("", Env(("CI", "true"))),
                     ("--dry-run", Env()),
                     ("--no-interaction", Env()),
                 })
        {
            using var pty = Init(repo, extra, env);
            var exit = pty.WaitForExit(Timeout);
            Assert.True(exit == 2, $"[{extra} CI={env.GetValueOrDefault("CI")}] exit {exit}:\n{pty.Screen}");
            Assert.True(pty.WaitFor("Several BELLATRIX desktop test projects", TimeSpan.FromSeconds(5)), pty.Screen);
            Assert.False(pty.WaitFor(Prompt, TimeSpan.Zero), "prompted:\n" + pty.Screen);
            Assert.False(Directory.Exists(repo.Path(".bellatrix")));
        }
    }
}
