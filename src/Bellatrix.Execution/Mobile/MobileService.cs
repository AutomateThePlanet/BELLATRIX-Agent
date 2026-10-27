using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace Bellatrix.Execution.Mobile;

/// <summary>
/// Mobile automation services over Appium, mirroring <see cref="DesktopService"/>: element interaction
/// by locator strategy, an attribute-rich view snapshot for context-economy capture, plus the
/// mobile-only app/device/touch operations. Holds no model and no credentials.
/// </summary>
public class MobileService
{
    private readonly MobileSessionManager _sessionManager;
    private readonly ILogger<MobileService> _logger;

    public MobileService(MobileSessionManager sessionManager, ILogger<MobileService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    #region App Operations

    public async Task<string> GetContextAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).Context;
    }

    public async Task<object> SetContextAsync(string sessionId, string context)
    {
        await Task.CompletedTask;
        _sessionManager.GetDriver(sessionId).Context = context;
        return new { success = true, context };
    }

    public async Task<object> GetContextsAsync(string sessionId)
    {
        await Task.CompletedTask;
        var contexts = _sessionManager.GetDriver(sessionId).Contexts;
        return new { contexts };
    }

    public async Task<object> BackgroundAppAsync(string sessionId, int seconds)
    {
        await Task.CompletedTask;
        _sessionManager.GetDriver(sessionId).BackgroundApp(TimeSpan.FromSeconds(seconds));
        return new { success = true };
    }

    public async Task<object> CloseAppAsync(string sessionId, string? appId = null)
    {
        await Task.CompletedTask;
        if (string.IsNullOrEmpty(appId))
        {
            throw new ArgumentException("appId is required to terminate app");
        }

        _sessionManager.GetDriver(sessionId).TerminateApp(appId);
        return new { success = true };
    }

    public async Task<object> LaunchAppAsync(string sessionId, string? appId = null)
    {
        await Task.CompletedTask;
        if (string.IsNullOrEmpty(appId))
        {
            throw new ArgumentException("appId is required to launch app");
        }

        _sessionManager.GetDriver(sessionId).ActivateApp(appId);
        return new { success = true };
    }

    public async Task<object> InstallAppAsync(string sessionId, string appPath)
    {
        await Task.CompletedTask;
        _sessionManager.GetDriver(sessionId).InstallApp(appPath);
        return new { success = true };
    }

    public async Task<object> RemoveAppAsync(string sessionId, string appId)
    {
        await Task.CompletedTask;
        _sessionManager.GetDriver(sessionId).RemoveApp(appId);
        return new { success = true };
    }

    public async Task<bool> IsAppInstalledAsync(string sessionId, string bundleId)
    {
        await Task.CompletedTask;
        try
        {
            return _sessionManager.GetDriver(sessionId).IsAppInstalled(bundleId);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    #endregion

    #region Page/View Information

    /// <summary>Gets the XML page source from Appium.</summary>
    public async Task<string> GetPageSourceAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).PageSource;
    }

    /// <summary>
    /// Captures the current view as a compact list of element dictionaries for AI analysis. Parses the
    /// Appium page-source XML and keeps the locator-relevant attributes per element (resource-id,
    /// content-desc, class, text for Android; name, label, value, type for iOS).
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetViewSnapshotAsync(string sessionId)
    {
        await Task.CompletedTask;
        try
        {
            var driver = _sessionManager.GetDriver(sessionId);
            return ParsePageSource(driver.PageSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get view snapshot for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to get view snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses Appium page-source XML (uiautomator2 / XCUITest) into the compact snapshot node list. Pure (no
    /// driver), so recorded or synthetic page sources go through exactly the production parsing — this is how
    /// the conformance fixtures under <c>tests/fixtures/snapshots/</c> are generated.
    /// </summary>
    internal static List<Dictionary<string, object>> ParsePageSource(string xml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(xml);

        var nodes = doc.DocumentNode.SelectNodes("//*");
        if (nodes == null || nodes.Count == 0)
        {
            return new List<Dictionary<string, object>>();
        }

        var elements = new List<Dictionary<string, object>>();
        foreach (var node in nodes)
        {
            // OriginalName: HtmlAgilityPack lowercases Name ("android.widget.TextView" → "android.widget.textview").
            var dict = BuildElementDictionary(node.OriginalName, attr => node.GetAttributeValue(attr, null));
            if (dict != null)
            {
                elements.Add(dict);
            }
        }

        return elements;
    }

    /// <summary>
    /// Builds the compact snapshot dictionary for a single Appium node, or returns <c>null</c> when the
    /// node carries no locator identity (no resource-id / name / text / content-desc / label). Pure over
    /// an attribute lookup so it is unit-testable without a live driver. Keys are PascalCase to match the
    /// desktop snapshot convention; both Android and iOS attribute names are pulled.
    /// </summary>
    internal static Dictionary<string, object>? BuildElementDictionary(string tagName, Func<string, string?> getAttr)
    {
        var resourceId = getAttr("resource-id");
        var name = getAttr("name");
        var text = getAttr("text");
        var contentDesc = getAttr("content-desc");
        var label = getAttr("label");

        // Retain a node only when it has some locator identity (context economy).
        if (string.IsNullOrWhiteSpace(resourceId) && string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(contentDesc)
            && string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var dict = new Dictionary<string, object> { ["Tag"] = tagName };
        AddIfNotEmpty(dict, "ResourceId", resourceId);
        AddIfNotEmpty(dict, "Name", name);
        AddIfNotEmpty(dict, "Text", text);
        AddIfNotEmpty(dict, "ContentDesc", contentDesc);
        AddIfNotEmpty(dict, "Label", label);
        AddIfNotEmpty(dict, "Class", getAttr("class"));
        AddIfNotEmpty(dict, "Value", getAttr("value"));
        AddIfNotEmpty(dict, "Type", getAttr("type"));
        return dict;
    }

    private static void AddIfNotEmpty(Dictionary<string, object> dict, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            dict[key] = value;
    }

    #endregion

    #region Element Interaction

    /// <summary>Finds an element by locator strategy (platform-guarded for the native query languages).</summary>
    private static AppiumElement FindElement(AppiumDriver driver, string locatorType, string locatorValue)
    {
        try
        {
            return locatorType.ToLowerInvariant() switch
            {
                "id" => driver.FindElement(MobileBy.Id(locatorValue)),
                "xpath" => driver.FindElement(MobileBy.XPath(locatorValue)),
                "accessibilityid" => driver.FindElement(MobileBy.AccessibilityId(locatorValue)),
                "classname" => driver.FindElement(MobileBy.ClassName(locatorValue)),
                "androiduiautomator" => driver is AndroidDriver
                    ? driver.FindElement(MobileBy.AndroidUIAutomator(locatorValue))
                    : throw new NotSupportedException("AndroidUIAutomator only supported on Android"),
                "iospredicate" => driver is IOSDriver
                    ? driver.FindElement(MobileBy.IosNSPredicate(locatorValue))
                    : throw new NotSupportedException("iOSNSPredicate only supported on iOS"),
                "iosclasschain" => driver is IOSDriver
                    ? driver.FindElement(MobileBy.IosClassChain(locatorValue))
                    : throw new NotSupportedException("iOSClassChain only supported on iOS"),
                _ => throw new ArgumentException($"Unsupported locator type: {locatorType}")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Element not found with {locatorType}: {locatorValue}", ex);
        }
    }

    public async Task<object> TapElementAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        var element = FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue);
        element.Click();
        return new { success = true };
    }

    public async Task<object> TypeTextAsync(string sessionId, string locatorType, string locatorValue, string text)
    {
        await Task.CompletedTask;
        var element = FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue);
        element.Clear();
        element.SendKeys(text);
        return new { success = true };
    }

    public async Task<object> ClearElementAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        var element = FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue);
        element.Clear();
        return new { success = true };
    }

    #endregion

    #region Element Inspection

    public async Task<string> GetElementTextAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        return FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue).Text;
    }

    public async Task<string?> GetElementAttributeAsync(string sessionId, string locatorType, string locatorValue, string attribute)
    {
        await Task.CompletedTask;
        return FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue).GetAttribute(attribute);
    }

    public async Task<bool> IsElementVisibleAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        try
        {
            return FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue).Displayed;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsElementEnabledAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        try
        {
            return FindElement(_sessionManager.GetDriver(sessionId), locatorType, locatorValue).Enabled;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Device Operations

    public async Task<string> GetOrientationAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).Orientation.ToString();
    }

    public async Task<object> SetOrientationAsync(string sessionId, string orientation)
    {
        await Task.CompletedTask;
        _sessionManager.GetDriver(sessionId).Orientation = Enum.Parse<ScreenOrientation>(orientation, true);
        return new { success = true, orientation };
    }

    public async Task<string> TakeScreenshotAsync(string sessionId)
    {
        await Task.CompletedTask;
        var screenshot = _sessionManager.GetDriver(sessionId).GetScreenshot();
        return Convert.ToBase64String(screenshot.AsByteArray);
    }

    #endregion

    #region Touch Actions

    public async Task<object> SwipeAsync(string sessionId, int startX, int startY, int endX, int endY, int duration = 1000)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
        actions.ClickAndHold()
            .MoveByOffset(endX - startX, endY - startY)
            .Release()
            .Perform();
        return new { success = true };
    }

    public async Task<object> ScrollToElementAsync(string sessionId, string locatorType, string locatorValue)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        var element = FindElement(driver, locatorType, locatorValue);
        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
        actions.MoveToElement(element).Perform();
        return new { success = true };
    }

    #endregion
}
