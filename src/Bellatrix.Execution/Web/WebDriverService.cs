using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Bellatrix.Execution.Web;

/// <summary>
/// Web automation services over native Selenium, mirroring <see cref="DesktopService"/>: navigation,
/// element interaction by selector strategy, a DOM/accessibility snapshot for context-economy capture,
/// plus web-only operations (select, JS execution, waits). Holds no model and no credentials.
/// </summary>
public class WebDriverService
{
    private readonly WebDriverSessionManager _sessionManager;
    private readonly ILogger<WebDriverService> _logger;

    public WebDriverService(WebDriverSessionManager sessionManager, ILogger<WebDriverService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    #region Navigation

    public async Task<object> NavigateAsync(string sessionId, string url)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        driver.Navigate().GoToUrl(url);
        return new { success = true, url = driver.Url, title = driver.Title };
    }

    public async Task<object> GoBackAsync(string sessionId)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        driver.Navigate().Back();
        return new { success = true, url = driver.Url };
    }

    public async Task<object> GoForwardAsync(string sessionId)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        driver.Navigate().Forward();
        return new { success = true, url = driver.Url };
    }

    public async Task<object> RefreshAsync(string sessionId)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        driver.Navigate().Refresh();
        return new { success = true, url = driver.Url };
    }

    #endregion

    #region Page Information

    public async Task<string> GetUrlAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).Url;
    }

    public async Task<string> GetTitleAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).Title;
    }

    public async Task<string> GetPageSourceAsync(string sessionId)
    {
        await Task.CompletedTask;
        return _sessionManager.GetDriver(sessionId).PageSource;
    }

    private const string SnapshotScript = @"
    return JSON.stringify((function deepScan(root) {
        const result = [];
        const stack = [{ node: root, depth: 0 }];
        while (stack.length) {
            const { node, depth } = stack.pop();
            if (!(node instanceof Element)) continue;
            const style = window.getComputedStyle(node);
            const rect = node.getBoundingClientRect();
            if (style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0) {
                result.push({
                    tag: node.tagName.toLowerCase(),
                    id: node.id,
                    class: node.className.toString().trim(),
                    text: node.innerText ? node.innerText.trim() : '',
                    type: node.getAttribute('type'),
                    name: node.getAttribute('name'),
                    placeholder: node.getAttribute('placeholder'),
                    ariaLabel: node.getAttribute('aria-label'),
                    role: node.getAttribute('role'),
                    href: node.getAttribute('href'),
                    value: node.getAttribute('value'),
                    dataTestid: node.getAttribute('data-testid')
                });
            }
            for (const child of node.children) stack.push({ node: child, depth });
            if (node.shadowRoot) for (const sc of node.shadowRoot.children) stack.push({ node: sc, depth: depth + 1 });
        }
        return result;
    })(document.body));
    ";

    /// <summary>
    /// Captures the current DOM/accessibility tree as a compact list of element dictionaries. Deep-scans
    /// visible elements (incl. Shadow DOM) in the browser, then parses the result into dictionaries keyed
    /// for the web locator ladder (data-testid → id → aria-label → role+text → css).
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetPageSnapshotAsync(string sessionId)
    {
        await Task.CompletedTask;
        try
        {
            var driver = _sessionManager.GetDriver(sessionId);
            var json = ((IJavaScriptExecutor)driver).ExecuteScript(SnapshotScript)?.ToString() ?? "[]";
            return ParseSnapshotJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get page snapshot for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to get page snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses the browser snapshot JSON (array of element objects) into PascalCase-keyed dictionaries,
    /// dropping nodes with no locator identity (no id/text/name/role/aria-label/data-testid/href). Pure
    /// and unit-testable without a live browser.
    /// </summary>
    internal static List<Dictionary<string, object>> ParseSnapshotJson(string json)
    {
        var elements = new List<Dictionary<string, object>>();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return elements;
        }

        foreach (var node in doc.RootElement.EnumerateArray())
        {
            var tag = Str(node, "tag");
            var id = Str(node, "id");
            var text = Str(node, "text");
            var name = Str(node, "name");
            var role = Str(node, "role");
            var ariaLabel = Str(node, "ariaLabel");
            var dataTestid = Str(node, "dataTestid");
            var href = Str(node, "href");

            // Retain only nodes with some locator identity (context economy).
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(name)
                && string.IsNullOrWhiteSpace(role) && string.IsNullOrWhiteSpace(ariaLabel)
                && string.IsNullOrWhiteSpace(dataTestid) && string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var dict = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(tag)) dict["Tag"] = tag;
            AddIfNotEmpty(dict, "Id", id);
            AddIfNotEmpty(dict, "DataTestid", dataTestid);
            AddIfNotEmpty(dict, "Name", name);
            AddIfNotEmpty(dict, "Role", role);
            AddIfNotEmpty(dict, "AriaLabel", ariaLabel);
            AddIfNotEmpty(dict, "Text", text);
            AddIfNotEmpty(dict, "Type", Str(node, "type"));
            AddIfNotEmpty(dict, "Placeholder", Str(node, "placeholder"));
            AddIfNotEmpty(dict, "Href", href);
            AddIfNotEmpty(dict, "Class", Str(node, "class"));
            AddIfNotEmpty(dict, "Value", Str(node, "value"));
            elements.Add(dict);
        }

        return elements;
    }

    private static string? Str(JsonElement node, string key)
        => node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static void AddIfNotEmpty(Dictionary<string, object> dict, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            dict[key] = value!;
    }

    #endregion

    #region Element Interaction

    private static IWebElement FindElement(IWebDriver driver, string selector, string strategy)
    {
        try
        {
            return driver.FindElement(ByFor(selector, strategy));
        }
        catch (NoSuchElementException ex)
        {
            throw new InvalidOperationException($"Element not found with {strategy} selector: {selector}", ex);
        }
    }

    private static By ByFor(string selector, string strategy) => strategy.ToLowerInvariant() switch
    {
        "css" => By.CssSelector(selector),
        "xpath" => By.XPath(selector),
        "id" => By.Id(selector),
        "class" => By.ClassName(selector),
        "name" => By.Name(selector),
        "tag" => By.TagName(selector),
        "linktext" => By.LinkText(selector),
        "partiallinktext" => By.PartialLinkText(selector),
        _ => throw new ArgumentException($"Unknown selector strategy: {strategy}")
    };

    public async Task<object> ClickElementAsync(string sessionId, string selector, string strategy = "css")
    {
        await Task.CompletedTask;
        FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).Click();
        return new { success = true };
    }

    public async Task<object> TypeTextAsync(string sessionId, string selector, string text, string strategy = "css")
    {
        await Task.CompletedTask;
        var element = FindElement(_sessionManager.GetDriver(sessionId), selector, strategy);
        element.Clear();
        element.SendKeys(text);
        return new { success = true };
    }

    public async Task<object> ClearElementAsync(string sessionId, string selector, string strategy = "css")
    {
        await Task.CompletedTask;
        FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).Clear();
        return new { success = true };
    }

    public async Task<object> SelectOptionAsync(string sessionId, string selector, string value, string strategy = "css")
    {
        await Task.CompletedTask;
        var element = FindElement(_sessionManager.GetDriver(sessionId), selector, strategy);
        new SelectElement(element).SelectByText(value);
        return new { success = true };
    }

    #endregion

    #region Element Inspection

    public async Task<string> GetElementTextAsync(string sessionId, string selector, string strategy = "css")
    {
        await Task.CompletedTask;
        return FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).Text;
    }

    public async Task<string?> GetElementAttributeAsync(string sessionId, string selector, string attribute, string strategy = "css")
    {
        await Task.CompletedTask;
        return FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).GetAttribute(attribute);
    }

    public async Task<bool> IsElementVisibleAsync(string sessionId, string selector, string strategy = "css")
    {
        await Task.CompletedTask;
        try
        {
            return FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).Displayed;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsElementEnabledAsync(string sessionId, string selector, string strategy = "css")
    {
        await Task.CompletedTask;
        try
        {
            return FindElement(_sessionManager.GetDriver(sessionId), selector, strategy).Enabled;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region JavaScript, Screenshots, Waits

    public async Task<object?> ExecuteJavaScriptAsync(string sessionId, string script, params object?[] args)
    {
        await Task.CompletedTask;
        var executor = (IJavaScriptExecutor)_sessionManager.GetDriver(sessionId);
        return executor.ExecuteScript(script, args);
    }

    public async Task<string> TakeScreenshotAsync(string sessionId)
    {
        await Task.CompletedTask;
        var screenshot = ((ITakesScreenshot)_sessionManager.GetDriver(sessionId)).GetScreenshot();
        return Convert.ToBase64String(screenshot.AsByteArray);
    }

    public async Task<object> WaitForElementAsync(string sessionId, string selector, int timeoutMs = 30000, string strategy = "css")
    {
        await Task.CompletedTask;
        try
        {
            var driver = _sessionManager.GetDriver(sessionId);
            var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(timeoutMs));
            var element = wait.Until(d => d.FindElement(ByFor(selector, strategy)));
            return new { success = true, found = element != null };
        }
        catch (WebDriverTimeoutException)
        {
            return new { success = false, found = false, error = "Element not found within timeout" };
        }
    }

    public async Task<object> WaitForPageReadyAsync(string sessionId)
    {
        await Task.CompletedTask;
        var driver = _sessionManager.GetDriver(sessionId);
        var executor = (IJavaScriptExecutor)driver;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        wait.Until(_ => executor.ExecuteScript("return document.readyState")?.ToString() == "complete");
        return new { success = true };
    }

    #endregion
}
