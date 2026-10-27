using Bellatrix.Execution.SessionMounting;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Unit tests for the no-daemon attach-by-id core. No live driver is required:
/// the executor's decision logic and caps replay are pure. Session
/// persistence is covered by <see cref="SessionStoreTests"/> (the SQLite store superseded the JSON one).
/// </summary>
public class SessionMountingTests
{

    [Fact]
    public void AttachExecutor_InterceptsNewSession_AndBindsExistingId()
    {
        // It must intercept NewSession (so no real handshake is sent) and forward everything else.
        // "newSession" is the W3C command name (the value of DriverCommand.NewSession).
        Assert.True(AttachExistingSessionExecutor.IsNewSessionCommand("newSession"));
        Assert.False(AttachExistingSessionExecutor.IsNewSessionCommand("clickElement"));
        Assert.False(AttachExistingSessionExecutor.IsNewSessionCommand(null));

        var executor = new AttachExistingSessionExecutor(
            new Uri("http://127.0.0.1:4723"), "session-xyz", TimeSpan.FromMinutes(10));

        Assert.Equal("session-xyz", executor.SessionId);
        Assert.Equal("session-xyz", executor.BuildAttachResponse().SessionId);

        // Default (no caps) → empty bag, the verified behavior.
        Assert.Empty((IDictionary<string, object>)executor.BuildAttachResponse().Value!);
    }

    [Fact]
    public void AttachExecutor_ReplaysCachedCaps_WhenProvided()
    {
        var caps = new Dictionary<string, object> { ["platformName"] = "Windows", ["appium:automationName"] = "NovaWindows" };
        var executor = new AttachExistingSessionExecutor(
            new Uri("http://127.0.0.1:4723"), "session-xyz", TimeSpan.FromMinutes(10), caps);

        var value = (IDictionary<string, object>)executor.BuildAttachResponse().Value!;
        Assert.Equal("Windows", value["platformName"].ToString());
        Assert.Equal("NovaWindows", value["appium:automationName"].ToString());
    }

    /// <summary>
    /// Appium's driver construction path calls <c>ICommandExecutor.ExecuteAsync</c>, not the
    /// synchronous <c>Execute</c> (confirmed by tracing which override actually runs — see
    /// DesktopSessionManagerMountExistingTests). An executor that only overrode <c>Execute</c> would
    /// silently let NewSession through to the real server. Points at an address nothing listens on
    /// (127.0.0.1:1): if interception ever regressed to sync-only, this would hang/throw a connection
    /// error instead of returning immediately.
    /// </summary>
    [Fact]
    public async Task AttachExecutor_InterceptsNewSession_OnTheAsyncPath()
    {
        var executor = new AttachExistingSessionExecutor(
            new Uri("http://127.0.0.1:1"), "session-xyz", TimeSpan.FromSeconds(2));

        var response = await executor.ExecuteAsync(new Command(null, DriverCommand.NewSession, new Dictionary<string, object>()));

        Assert.Equal("session-xyz", response.SessionId);
    }
}
