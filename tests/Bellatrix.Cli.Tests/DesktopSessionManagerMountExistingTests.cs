
using Bellatrix.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using OpenQA.Selenium;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Regression coverage for the attach-by-id mounting bug (fix/desktop-existing-session-attach):
/// <see cref="DesktopSessionManager.MountExisting"/> used to reflectively look up a
/// <c>WindowsDriver(ICommandExecutor, ICapabilities)</c> constructor that does not exist on
/// <c>WindowsDriver</c> in Appium.WebDriver 5.1.0 (constructors are never inherited), so every
/// mount attempt threw <see cref="NotSupportedException"/>. The fix binds through
/// <c>AttachedWindowsDriver</c>, a minimal concrete subclass of the shared <c>AppiumDriver</c> base
/// (which does expose that constructor, publicly). No live WinAppDriver or application is
/// required: <see cref="FakeAppiumServer"/> stands in for the driver server over real HTTP.
/// </summary>
public class DesktopSessionManagerMountExistingTests
{
    private static DesktopSessionManager NewManager() =>
        new(NullLogger<DesktopSessionManager>.Instance, new DesktopMcpSettings { SessionTimeoutMinutes = 0 });

    [Fact]
    public void MountExisting_DoesNotThrow_AndPreservesTheSuppliedSessionId()
    {
        // Deliberately an address nothing listens on: if a real NewSession request were ever sent,
        // this would fail with a connection error instead of returning cleanly.
        var manager = NewManager();

        var driver = manager.MountExisting(
            sessionId: "existing-session-id",
            serverUrl: "http://127.0.0.1:1",
            driverType: "WinAppDriver",
            commandTimeout: TimeSpan.FromSeconds(2));

        Assert.Equal("existing-session-id", driver.SessionId.ToString());
        Assert.Same(driver, manager.GetDriver("existing-session-id"));
    }

    [Fact]
    public void MountExisting_CalledTwiceForSameId_ReturnsTheCachedDriver_WithoutReconnecting()
    {
        var manager = NewManager();

        var first = manager.MountExisting("existing-session-id", "http://127.0.0.1:1", "WinAppDriver", TimeSpan.FromSeconds(2));
        var second = manager.MountExisting("existing-session-id", "http://127.0.0.1:1", "WinAppDriver", TimeSpan.FromSeconds(2));

        Assert.Same(first, second);
    }

    [Fact]
    public async Task MountedDriver_ExecutesDesktopServiceOperations_OverRealHttp_WithoutASecondNewSession()
    {
        using var server = new FakeAppiumServer();
        var manager = NewManager();
        var service = new DesktopService(manager, NullLogger<DesktopService>.Instance);

        manager.MountExisting("existing-session-id", server.BaseUrl, "WinAppDriver", TimeSpan.FromSeconds(5));

        // The attach executor must intercept NewSession — no real session-creation request is sent.
        Assert.Equal(0, server.NewSessionRequestCount);

        var title = await service.GetWindowTitleAsync("existing-session-id");
        Assert.Equal("FakeWindowTitle", title);

        var pageSource = await service.GetPageSourceAsync("existing-session-id");
        Assert.Equal("<Window/>", pageSource);

        await service.ClickElementAsync("existing-session-id", "//Button[@Name='OK']");
        Assert.True(server.ClickReceived);

        await service.ClearElementAsync("existing-session-id", "//Edit");
        Assert.True(server.ClearReceived);

        var text = await service.GetElementTextAsync("existing-session-id", "//Edit");
        Assert.Equal("FakeElementText", text);

        var attr = await service.GetElementAttributeAsync("existing-session-id", "//Edit", "AutomationId");
        Assert.Equal("FakeAttributeValue", attr);

        Assert.True(await service.IsElementVisibleAsync("existing-session-id", "//Edit"));
        Assert.True(await service.IsElementEnabledAsync("existing-session-id", "//Edit"));

        var screenshot = await service.TakeScreenshotAsync("existing-session-id");
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), screenshot);

        // Still exactly zero — every one of the calls above went to the mounted session, never re-created it.
        Assert.Equal(0, server.NewSessionRequestCount);
    }

    [Fact]
    public void StartSession_AgainstAnUnreachableServer_FailsWithARealConnectionError_NotATypeOrReflectionError()
    {
        // Confirms the fresh-session (WindowsDriver) path is untouched by the attach-by-id fix: it
        // still fails the way it always did — a genuine connection error — rather than the
        // NotSupportedException/TargetInvocationException the old reflection-based mount produced.
        var manager = NewManager();
        var app = new DesktopAppDefinition { Name = "ad-hoc", Path = @"C:\does\not\matter.exe" };

        var ex = Assert.ThrowsAny<Exception>(() =>
            manager.StartSession(app, "WinAppDriver", null, "http://127.0.0.1:1"));

        Assert.IsNotType<NotSupportedException>(ex);
        Assert.IsNotType<System.Reflection.TargetInvocationException>(ex);
    }
}
