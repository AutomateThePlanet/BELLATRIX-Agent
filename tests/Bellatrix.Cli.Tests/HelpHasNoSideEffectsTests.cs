using System.Text.Json;
using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>--help</c>, <c>-h</c> and <c>help</c> on any command print that command's help and exit 0 with zero side effects:
/// no scaffolded files, no <c>.bellatrix/sessions.db</c>, no manifests, no CLAUDE.md edit. Regression: <c>bellatrix
/// init --help</c> used to run a real install.
/// </summary>
public class HelpHasNoSideEffectsTests
{
    private static readonly string[] HelpForms = { "--help", "-h", "help" };

    /// <summary>Every routed name (canonical and alias) of every command in the registry, with each help form.</summary>
    public static IEnumerable<object?[]> EveryCommandAndHelpForm()
        => from spec in CommandRegistry.All
           from name in spec.RoutedNames
           from form in HelpForms
           select new object?[] { spec.Domain, name, form };

    [Theory]
    [MemberData(nameof(EveryCommandAndHelpForm))]
    public void HelpOnAnyCommand_PrintsItsHelp_AndLeavesAnEmptyDirectoryEmpty(string? domain, string name, string form)
    {
        var spec = CommandRegistry.Find(domain, name)!;
        var (exit, stdout, entries) = RunInEmptyDirectory(Command(domain, name, form));

        Assert.Equal(0, exit);
        Assert.Empty(entries);
        Assert.Contains("Usage: " + CommandRegistry.UsageOf(spec), stdout);
        Assert.StartsWith("bellatrix " + CliRunner.Version, stdout);
    }

    [Theory]
    [MemberData(nameof(EveryCommandAndHelpForm))]
    public void HelpJsonOnAnyCommand_DescribesExactlyThatCommand_AndLeavesAnEmptyDirectoryEmpty(string? domain, string name, string form)
    {
        var spec = CommandRegistry.Find(domain, name)!;
        var (exit, stdout, entries) = RunInEmptyDirectory(Command(domain, name, form).Append("--json").ToArray());

        Assert.Equal(0, exit);
        Assert.Empty(entries);
        var command = Assert.Single(JsonDocument.Parse(stdout).RootElement.GetProperty("commands").EnumerateArray());
        Assert.Equal(spec.Name, command.GetProperty("name").GetString());
        Assert.Equal(spec.Domain, command.GetProperty("domain").GetString());
    }

    [Theory]
    [InlineData("init", "--agent", "claude", "--domains", "web", "--help")]
    [InlineData("init", "--regenerate", "-h")]
    [InlineData("init", "--dry-run", "help")]
    [InlineData("update-skills", "--agent", "claude,copilot", "--language", "csharp", "--help")]
    [InlineData("sessions", "--prune", "--help")]
    [InlineData("help", "init")]
    [InlineData("--help", "init")]
    [InlineData("help", "desktop", "start-session")]
    [InlineData("desktop", "help", "start-session")]
    [InlineData("desktop", "start-session", "--app", "calc", "--session", "s1", "--help")]
    [InlineData("web", "start-session", "--browser", "chrome", "-h")]
    [InlineData("mobile", "start-session", "--platform", "android", "help")]
    public void HelpAnywhereAmongRealFlags_StillOnlyPrintsHelp(params string[] args)
    {
        var (exit, stdout, entries) = RunInEmptyDirectory(args);

        Assert.Equal(0, exit);
        Assert.Empty(entries);
        Assert.StartsWith("bellatrix " + CliRunner.Version, stdout);
    }

    [Theory]
    [InlineData("desktop", "type-text", "--text", "help")]
    [InlineData("desktop", "type-text", "--text", "-h")]
    [InlineData("web", "type-text", "--locator", "#q", "--text", "help")]
    public void HelpAsAFlagValue_IsNotAHelpRequest(params string[] args)
    {
        var spec = CommandRegistry.Find(args[0], args[1])!;
        Assert.False(CliRunner.WantsHelp(spec, args.Skip(2)));
    }

    [Fact]
    public void HelpAfterASwitch_IsAHelpRequest()
        => Assert.True(CliRunner.WantsHelp(CommandRegistry.Find(null, "init")!, new[] { "--dry-run", "help" }));

    private static string[] Command(string? domain, string name, string form)
        => (domain is null ? new[] { name, form } : new[] { domain, name, form });

    private static (int exit, string stdout, string[] entries) RunInEmptyDirectory(string[] args)
    {
        var original = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var temp = Directory.CreateTempSubdirectory("bellatrix-help-").FullName;
        using var writer = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(temp);
            Console.SetOut(writer);
            var exit = CliRunner.Run(args);
            return (exit, writer.ToString(), Directory.GetFileSystemEntries(temp, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.SetCurrentDirectory(original);
            Directory.Delete(temp, recursive: true);
        }
    }
}
