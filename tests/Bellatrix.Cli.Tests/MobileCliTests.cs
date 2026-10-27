using Bellatrix.Cli;
using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Mobile dispatcher-level tests (M2): the domain/verb grammar and the recognized verb surface. Cases
/// that fail on a missing required option return before any Appium/registry access.
/// </summary>
public class MobileCliTests
{
    [Fact]
    public void Run_MobileUnknownVerb_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "mobile", "frobnicate" }));

    [Fact]
    public void Run_MobileMissingVerb_ReturnsError()
        => Assert.Equal(1, CliRunner.Run(new[] { "mobile" }));

    [Fact]
    public void Run_MobileStartSession_MissingPlatform_ReturnsUsageError()
        => Assert.Equal(1, CliRunner.Run(new[] { "mobile", "start-session", "--json" }));

    [Fact]
    public void KnownVerbs_CoverTheMobileSurface()
    {
        foreach (var verb in new[]
                 {
                     "start-session", "close-session", "list-sessions", "get-page-source", "snapshot",
                     "find", "take-screenshot", "click", "tap", "type-text", "clear", "get-text",
                     "get-attribute", "is-visible", "is-enabled", "swipe", "scroll-to-element",
                     "get-context", "set-context", "get-contexts", "background-app", "install-app",
                     "remove-app", "is-app-installed", "get-orientation", "set-orientation",
                 })
        {
            Assert.Contains(verb, MobileCommands.KnownVerbs);
        }
    }
}
