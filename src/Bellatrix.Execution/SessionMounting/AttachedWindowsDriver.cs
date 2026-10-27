using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bellatrix.Execution.SessionMounting;

/// <summary>
/// A minimal concrete <see cref="AppiumDriver"/> for a mounted (attach-by-id) session.
/// <see cref="AppiumDriver"/> itself exposes a public
/// <c>AppiumDriver(ICommandExecutor, ICapabilities)</c> constructor but is abstract, and
/// <see cref="OpenQA.Selenium.Appium.Windows.WindowsDriver"/> does not redeclare (constructors are
/// never inherited in C#) a constructor with that signature — every <c>WindowsDriver</c> constructor
/// always sends a real W3C <c>NewSession</c> handshake. This subclass adds nothing but a public
/// forwarding constructor, letting a driver be bound directly to an <see cref="AttachExistingSessionExecutor"/>
/// without reflection. All UI automation members (FindElement, PageSource, Title, GetScreenshot, …)
/// are inherited unchanged from <see cref="AppiumDriver"/>, the same base <see cref="OpenQA.Selenium.Appium.Windows.WindowsDriver"/>
/// uses — so <see cref="DesktopService"/> works identically against either driver type.
/// </summary>
public sealed class AttachedWindowsDriver : AppiumDriver
{
    public AttachedWindowsDriver(ICommandExecutor executor, ICapabilities capabilities)
        : base(executor, capabilities)
    {
    }
}
