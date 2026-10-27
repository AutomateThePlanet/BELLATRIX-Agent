using Bellatrix.Execution.Snapshots;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Bellatrix.Execution
{
    /// <summary>
    /// Provides Windows desktop automation services using BELLATRIX Desktop framework
    /// </summary>
    public class DesktopService
    {
        private readonly DesktopSessionManager _sessionManager;
        private readonly ILogger<DesktopService> _logger;

        public DesktopService(DesktopSessionManager sessionManager, ILogger<DesktopService> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        #region Window Operations

        /// <summary>
        /// Gets the current window title
        /// </summary>
        public async Task<string> GetWindowTitleAsync(string sessionId)
        {
            var driver = _sessionManager.GetDriver(sessionId);
            return driver.Title;
        }

        /// <summary>
        /// Maximizes the application window
        /// </summary>
        public async Task<object> MaximizeWindowAsync(string sessionId)
        {
            var driver = _sessionManager.GetDriver(sessionId);
            try
            {
                driver.Manage().Window.Maximize();
            }
            catch (NotImplementedException)
            {
                // NovaWindows driver does not support Window.Maximize().
                // Fall back to getting screen size and setting window position/size manually.
                _logger.LogWarning("Window.Maximize() not supported by driver, falling back to manual resize");
                try
                {
                    var screenWidth = driver.Manage().Window.Size.Width;
                    var screenHeight = driver.Manage().Window.Size.Height;
                    driver.Manage().Window.Position = new System.Drawing.Point(0, 0);
                    driver.Manage().Window.Size = new System.Drawing.Size(screenWidth, screenHeight);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Manual maximize fallback also failed");
                    return new { success = false, reason = "Maximize not supported by this driver" };
                }
            }

            return new { success = true };
        }

        /// <summary>
        /// Closes the application window
        /// </summary>
        public async Task<object> CloseWindowAsync(string sessionId)
        {
            var driver = _sessionManager.GetDriver(sessionId);
            driver.Close();

            return new { success = true };
        }

        #endregion

        #region Page/View Information

        /// <summary>
        /// Gets the XML page source from WinAppDriver
        /// </summary>
        public async Task<string> GetPageSourceAsync(string sessionId)
        {
            var driver = _sessionManager.GetDriver(sessionId);
            return driver.PageSource;
        }

        /// <summary>
        /// Gets the current view snapshot for AI analysis using BELLATRIX's approach.
        /// This method mirrors AppService.GetCurrentViewSnapshot() from Bellatrix.Desktop.
        /// It parses the WinAppDriver XML and extracts key attributes for each visible element.
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetViewSnapshotAsync(string sessionId)
        {
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
        /// Parses WinAppDriver/NovaWindows page-source XML into the compact snapshot node list. Pure (no driver),
        /// so recorded or synthetic page sources go through exactly the production parsing — this is how the
        /// conformance fixtures under <c>tests/fixtures/snapshots/</c> are generated.
        /// </summary>
        internal static List<Dictionary<string, object>> ParsePageSource(string sourceXml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(sourceXml);

            var nodes = doc.DocumentNode.SelectNodes("//*");
            if (nodes == null || nodes.Count == 0)
            {
                return new List<Dictionary<string, object>>();
            }

            var elements = new List<Dictionary<string, object>>();

            foreach (var node in nodes)
            {
                // OriginalName: HtmlAgilityPack lowercases Name ("ListItem" → "listitem"), and UIA XPath is case-sensitive.
                var dict = BuildElementDictionary(node.OriginalName, attr => node.GetAttributeValue(attr, null));
                if (dict != null)
                {
                    elements.Add(dict);
                }
            }

            return elements;
        }

        /// <summary>
        /// Builds the compact snapshot dictionary for a single UIA node, or returns <c>null</c> when the
        /// node should be pruned for context economy. Pure over an attribute lookup so it is unit-testable
        /// without a live driver. <paramref name="getAttr"/> returns the raw attribute value (or null).
        /// </summary>
        /// <remarks>
        /// Retention rule: keep a node when it has a <c>Name</c> OR an <c>AutomationId</c> OR is an
        /// actionable control type (<see cref="SnapshotQuery.IsInteractive"/>). A ControlType-only
        /// interactive control (no Name/AutomationId) MUST be kept so locators can still reach it.
        /// Pure non-interactive chrome with no identity (Pane/Group/Text containers) is dropped.
        /// </remarks>
        internal static Dictionary<string, object>? BuildElementDictionary(string tagName, Func<string, string?> getAttr)
        {
            var automationId = getAttr("AutomationId");
            var name = getAttr("Name");

            var dict = new Dictionary<string, object>();

            // Always include tag (PascalCase for WinAppDriver XPath compatibility)
            dict["Tag"] = char.ToUpper(tagName[0]) + tagName.Substring(1);

            // Always include ControlType: resolves the consumer
            // `AutomationId > Name > ControlType > ClassName` assumption. Falls back to the
            // PascalCased tag when the driver does not surface an explicit ControlType.
            var controlType = getAttr("ControlType");
            dict["ControlType"] = !string.IsNullOrWhiteSpace(controlType) ? controlType : dict["Tag"];

            // Prune only nameless, AutomationId-less, non-interactive chrome (preserves context economy
            // while keeping ControlType-only interactive controls reachable).
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(automationId)
                && !SnapshotQuery.IsInteractive(dict))
            {
                return null;
            }

            // Core identity fields — only include if non-empty
            AddIfNotEmpty(dict, "AutomationId", automationId);
            AddIfNotEmpty(dict, "Name", name);
            AddIfNotEmpty(dict, "ClassName", getAttr("ClassName"));

            // State fields — only include non-default values
            var isEnabled = ParseNullableBool(getAttr("IsEnabled"));
            if (isEnabled == false) dict["IsEnabled"] = false; // Only show when disabled

            var isOffscreen = ParseNullableBool(getAttr("IsOffscreen"));
            if (isOffscreen == true) dict["IsOffscreen"] = true; // Only show when offscreen

            var isPassword = ParseNullableBool(getAttr("IsPassword"));
            if (isPassword == true) dict["IsPassword"] = true;

            var isSelected = ParseNullableBool(getAttr("IsSelected"));
            if (isSelected == true) dict["IsSelected"] = true;

            AddIfNotEmpty(dict, "Value", getAttr("Value.Value") ?? getAttr("Value"));
            AddIfNotEmpty(dict, "HelpText", getAttr("HelpText"));
            AddIfNotEmpty(dict, "ToggleState", getAttr("ToggleState"));
            AddIfNotEmpty(dict, "ExpandCollapseState", getAttr("ExpandCollapseState"));

            return dict;
        }

        private static void AddIfNotEmpty(Dictionary<string, object> dict, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                dict[key] = value;
        }

        private static bool? ParseNullableBool(string? value)
        {
            if (value == null)
                return null;
            if (bool.TryParse(value, out var result))
                return result;
            return null;
        }

        #endregion

        #region Element Interaction

        /// <summary>
        /// Finds an element using XPath
        /// </summary>
        private AppiumElement FindElement(AppiumDriver driver, string xpath)
        {
            try
            {
                return driver.FindElement(By.XPath(xpath));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Element not found with XPath: {xpath}", ex);
            }
        }

        /// <summary>
        /// Clicks an element found by XPath
        /// </summary>
        public async Task<object> ClickElementAsync(string sessionId, string xpath)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                element.Click();

                return new { success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to click element {XPath}", xpath);
                throw new InvalidOperationException($"Failed to click element: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Types text into an element
        /// </summary>
        public async Task<object> TypeTextAsync(string sessionId, string xpath, string text)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                element.Clear();
                element.SendKeys(text);

                return new { success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to type text into element {XPath}", xpath);
                throw new InvalidOperationException($"Failed to type text: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears the text in an element
        /// </summary>
        public async Task<object> ClearElementAsync(string sessionId, string xpath)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                element.Clear();

                return new { success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear element {XPath}", xpath);
                throw new InvalidOperationException($"Failed to clear element: {ex.Message}", ex);
            }
        }

        #endregion

        #region Element Inspection

        /// <summary>
        /// Gets the text content of an element
        /// </summary>
        public async Task<string> GetElementTextAsync(string sessionId, string xpath)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                return element.Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get text from element {XPath}", xpath);
                throw new InvalidOperationException($"Failed to get element text: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets an attribute value from an element
        /// </summary>
        public async Task<string?> GetElementAttributeAsync(string sessionId, string xpath, string attribute)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                return element.GetAttribute(attribute);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attribute {Attribute} from element {XPath}", attribute, xpath);
                throw new InvalidOperationException($"Failed to get element attribute: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if an element is visible/displayed
        /// </summary>
        public async Task<bool> IsElementVisibleAsync(string sessionId, string xpath)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                return element.Displayed;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an element is enabled
        /// </summary>
        public async Task<bool> IsElementEnabledAsync(string sessionId, string xpath)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var element = FindElement(driver, xpath);
                return element.Enabled;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Screenshots

        /// <summary>
        /// Takes a screenshot of the application window
        /// </summary>
        public async Task<string> TakeScreenshotAsync(string sessionId)
        {
            try
            {
                var driver = _sessionManager.GetDriver(sessionId);
                var screenshot = driver.GetScreenshot();
                return Convert.ToBase64String(screenshot.AsByteArray);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to take screenshot");
                throw new InvalidOperationException($"Failed to take screenshot: {ex.Message}", ex);
            }
        }

        #endregion

        // Locator generation is intentionally NOT implemented here: the model-free CLI delegates all
        // inference to the host AI CLI's isolated locator specialist. The
        // former GenerateXPathLocatorAsync stub and its DesktopElementSummary helper were removed.
    }
}
