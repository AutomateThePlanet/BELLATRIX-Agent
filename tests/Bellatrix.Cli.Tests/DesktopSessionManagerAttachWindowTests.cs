using Bellatrix.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using OpenQA.Selenium;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Regression coverage for the two pre-existing bugs uncovered by a live desktop-app validation of the
/// attach-by-id fix (fix/desktop-existing-session-attach), both in <c>DesktopSessionManager</c>'s
/// window-lookup helper — neither is the <c>MountExisting</c> reflection bug that fix already covers.
/// </summary>
public class DesktopSessionManagerAttachWindowTests
{
    /// <summary>
    /// Bug 2: WinAppDriver's <c>appTopLevelWindow</c> capability requires the HWND as lowercase hex;
    /// UI Automation's <c>NativeWindowHandle</c> attribute is decimal. Passing the decimal value
    /// through directly (the old behavior) made a real, live-running window unattachable with
    /// "Cannot find active window specified by capabilities: appTopLevelWindow" — confirmed against
    /// a real, live application window before this fix.
    /// </summary>
    [Theory]
    [InlineData("0", "0")]
    [InlineData("255", "ff")]
    [InlineData("65536", "10000")]
    [InlineData("4295359848", "10005fd68")]
    public void ToAppTopLevelWindowHex_ConvertsDecimalNativeWindowHandleToLowercaseHex(string decimalHandle, string expectedHex)
    {
        Assert.Equal(expectedHex, DesktopSessionManager.ToAppTopLevelWindowHex(decimalHandle));
    }

    /// <summary>
    /// Bug 1: the window-lookup retry loop's <c>catch (NoSuchElementException) when (attempt &lt; retryCount)</c>
    /// let the raw exception escape on the final attempt instead of reaching the intended, descriptive
    /// <see cref="InvalidOperationException"/>. That raw exception is itself a <see cref="WebDriverException"/>,
    /// so the CLI's generic handler misreported it as "driver_unreachable" even though the driver was
    /// perfectly reachable — the window just was never found. retryCount/retryDelayMs are passed small
    /// only so this test doesn't wait the real 20s; production callers still default to 10/2000.
    /// </summary>
    [Fact]
    public void AttachToWindow_AfterExhaustingRetries_ThrowsDescriptiveInvalidOperationException_NotARawNoSuchElementException()
    {
        using var server = new FakeAppiumServer { AlwaysFailFindElement = true };
        var manager = new DesktopSessionManager(NullLogger<DesktopSessionManager>.Instance, new DesktopMcpSettings { SessionTimeoutMinutes = 0 });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.AttachToWindow("Window That Never Appears", "WinAppDriver", null, server.BaseUrl, settings: null, retryCount: 2, retryDelayMs: 10));

        Assert.Contains("Window That Never Appears", ex.Message);
        Assert.Contains("not found after 2 attempts", ex.Message);
    }
}
