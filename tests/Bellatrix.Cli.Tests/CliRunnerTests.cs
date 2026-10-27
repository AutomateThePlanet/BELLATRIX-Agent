using Bellatrix.Cli;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Dispatcher-level tests for the CLI skeleton. Verb bodies arrive in the user-story phases; here we
/// pin the domain/verb grammar, exit codes, and the recognized contract surface.
/// </summary>
public class CliRunnerTests
{
    [Fact]
    public void Run_NoArgs_ReturnsUsageError()
        => Assert.Equal(1, CliRunner.Run(Array.Empty<string>()));

    [Fact]
    public void Run_UnknownDomain_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "tablet", "click" }));

    [Fact]
    public void Run_ListApps_Succeeds_WithoutDriver()
        => Assert.Equal(0, CliRunner.Run(new[] { "desktop", "list-apps", "--json" }));

    [Theory]
    [InlineData("--version")]
    [InlineData("version")]
    public void Run_Version_PrintsCsprojVersion_AndExitsZero(string arg)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        int exit;
        try
        {
            exit = CliRunner.Run(new[] { arg });
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(0, exit);
        var expected = typeof(CliRunner).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single().InformationalVersion.Split('+')[0];
        Assert.Equal($"{{\"version\":\"{expected}\"}}", writer.ToString().Trim());
        Assert.DoesNotContain("+", CliRunner.Version); // no source-revision suffix
    }

    [Fact]
    public void Run_UnknownVerb_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "desktop", "frobnicate" }));

    [Fact]
    public void KnownVerbs_CoverThe18CoreVerbsPlusAdditive()
    {
        // 18 desktop tools + query-snapshot + scaffold + snapshot + find = 22.
        Assert.Equal(22, CliRunner.KnownVerbs.Count);
        foreach (var verb in new[]
                 {
                     "start-session", "list-apps", "attach-window", "reattach-session", "close-session",
                     "list-sessions", "get-title", "maximize", "get-page-source", "get-view-snapshot",
                     "take-screenshot", "click", "type-text", "clear", "get-text", "get-attribute",
                     "is-visible", "is-enabled",
                 })
        {
            Assert.Contains(verb, CliRunner.KnownVerbs);
        }
    }
}
