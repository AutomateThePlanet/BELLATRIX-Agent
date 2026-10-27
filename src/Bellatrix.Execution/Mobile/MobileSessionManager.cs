using Bellatrix.Execution.SessionMounting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.iOS;
using System.Collections.Concurrent;
using System.Reflection;

namespace Bellatrix.Execution.Mobile;

/// <summary>
/// Manages mobile (Android/iOS) application sessions over Appium for the model-free CLI. Mirrors
/// <see cref="DesktopSessionManager"/>: an in-process registry of live drivers plus attach-by-id
/// mounting (<see cref="MountExisting"/>) so a fresh, daemon-less CLI process can bind onto a session
/// the Appium server still holds. Holds no model and no credentials.
/// </summary>
public class MobileSessionManager : IDisposable
{
    private sealed class SessionEntry
    {
        public required AppiumDriver Driver { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly ILogger<MobileSessionManager> _logger;
    private const string DefaultAppiumUrl = "http://127.0.0.1:4723";

    public MobileSessionManager(ILogger<MobileSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a new mobile session. <paramref name="platform"/> is <c>android</c> or <c>ios</c> and
    /// selects the driver + automation engine (UiAutomator2 / XCUITest). Returns the local session id;
    /// the real Appium session id is read from the constructed driver by the caller.
    /// </summary>
    public string StartSession(
        string platform,
        string appPath,
        string? deviceName = null,
        string? sessionId = null,
        string? appiumUrl = null,
        Dictionary<string, object>? additionalCapabilities = null)
    {
        sessionId ??= Guid.NewGuid().ToString();
        var serverUrl = appiumUrl ?? DefaultAppiumUrl;
        var isIOS = IsIOS(platform);

        try
        {
            var options = new AppiumOptions();
            options.AddAdditionalAppiumOption(MobileCapabilityType.PlatformName, isIOS ? "iOS" : "Android");
            options.AddAdditionalAppiumOption(MobileCapabilityType.DeviceName, deviceName ?? (isIOS ? "iPhone Simulator" : "Android Emulator"));
            options.AddAdditionalAppiumOption(MobileCapabilityType.App, appPath);
            options.AddAdditionalAppiumOption(MobileCapabilityType.AutomationName, isIOS ? "XCUITest" : "UiAutomator2");

            if (additionalCapabilities != null)
            {
                foreach (var cap in additionalCapabilities)
                {
                    options.AddAdditionalAppiumOption(cap.Key, cap.Value);
                }
            }

            AppiumDriver driver = isIOS
                ? new IOSDriver(new Uri(serverUrl), options)
                : new AndroidDriver(new Uri(serverUrl), options);

            if (!TryAddSession(sessionId, driver))
            {
                driver.Quit();
                throw new InvalidOperationException($"Session {sessionId} already exists");
            }

            _logger.LogInformation("Started {Platform} session {SessionId} for app {AppPath}", isIOS ? "iOS" : "Android", sessionId, appPath);
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {Platform} session for {AppPath}", platform, appPath);
            throw;
        }
    }

    /// <summary>
    /// Mounts onto an EXISTING Appium session by id (attach-by-id) WITHOUT a NewSession
    /// handshake, via <see cref="AttachExistingSessionExecutor"/>. The platform is read from the
    /// replayed capabilities (<c>platformName</c>) to select the Android/iOS driver type. Like desktop,
    /// Appium v5's driver classes expose no public <c>(ICommandExecutor, ICapabilities)</c> constructor,
    /// so it is invoked via reflection; fail loud if the installed Appium build lacks it.
    /// </summary>
    public AppiumDriver MountExisting(
        string sessionId, string serverUrl, TimeSpan? commandTimeout = null, string? capsJson = null)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            existing.LastActivityUtc = DateTime.UtcNow;
            return existing.Driver;
        }

        var timeout = commandTimeout ?? TimeSpan.FromMinutes(10);
        var caps = DeserializeCaps(capsJson);
        var executor = new AttachExistingSessionExecutor(new Uri(serverUrl), sessionId, timeout, caps);

        var platformName = caps != null && caps.TryGetValue("platformName", out var p) ? p?.ToString() : null;
        var automationName = caps != null && caps.TryGetValue("appium:automationName", out var a) ? a?.ToString() : null;
        var isIOS = IsIOS(platformName);

        var options = new AppiumOptions { PlatformName = isIOS ? "iOS" : "Android" };
        options.AutomationName = automationName ?? (isIOS ? "XCUITest" : "UiAutomator2");

        // NewSession is intercepted by the executor, so these capabilities are never transmitted —
        // the driver simply binds to the supplied sessionId.
        var driverType = isIOS ? typeof(IOSDriver) : typeof(AndroidDriver);
        var driver = ConstructDriverWithExecutor(driverType, executor, options.ToCapabilities());
        TryAddSession(sessionId, driver);
        _logger.LogInformation("Mounted existing mobile session {SessionId} at {ServerUrl} ({Platform})",
            sessionId, serverUrl, isIOS ? "iOS" : "Android");
        return driver;
    }

    /// <summary>
    /// The capabilities cached at session creation for replay on a later attach-by-id mount, mirroring
    /// desktop's cached-capabilities replay. The platform/automation pair is what mobile attach needs to pick the
    /// driver class.
    /// </summary>
    public static string BuildCapsJson(string platform)
    {
        var isIOS = IsIOS(platform);
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["platformName"] = isIOS ? "iOS" : "Android",
            ["appium:automationName"] = isIOS ? "XCUITest" : "UiAutomator2",
        });
    }

    private static IReadOnlyDictionary<string, object>? DeserializeCaps(string? capsJson)
    {
        if (string.IsNullOrWhiteSpace(capsJson))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(capsJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static AppiumDriver ConstructDriverWithExecutor(Type driverType, AttachExistingSessionExecutor executor, ICapabilities capabilities)
    {
        var ctor = driverType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ICommandExecutor), typeof(ICapabilities) },
            modifiers: null);

        if (ctor is null)
        {
            throw new NotSupportedException(
                $"The installed Appium.WebDriver does not expose a {driverType.Name}(ICommandExecutor, ICapabilities) constructor required for attach-by-id. " +
                "Validate the mounting path against the target Appium version.");
        }

        return (AppiumDriver)ctor.Invoke(new object[] { executor, capabilities });
    }

    /// <summary>True when the platform string denotes iOS; defaults to Android otherwise.</summary>
    public static bool IsIOS(string? platform)
        => platform != null && (platform.Equals("ios", StringComparison.OrdinalIgnoreCase)
                                || platform.Equals("iphone", StringComparison.OrdinalIgnoreCase));

    /// <summary>Gets a driver for an existing session, refreshing its last-activity timestamp.</summary>
    public AppiumDriver GetDriver(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }
        entry.LastActivityUtc = DateTime.UtcNow;
        return entry.Driver;
    }

    /// <summary>Gets the platform (Android/iOS) for a live session by driver type.</summary>
    public string GetPlatform(string sessionId)
        => GetDriver(sessionId) is IOSDriver ? "iOS" : "Android";

    public void CloseSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var entry))
        {
            try
            {
                entry.Driver.Quit();
                _logger.LogInformation("Closed session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing session {SessionId}", sessionId);
            }
        }
    }

    public void CloseAllSessions()
    {
        foreach (var sessionId in _sessions.Keys.ToList())
        {
            CloseSession(sessionId);
        }
    }

    public IEnumerable<string> GetActiveSessions() => _sessions.Keys;

    public bool SessionExists(string sessionId) => _sessions.ContainsKey(sessionId);

    private bool TryAddSession(string sessionId, AppiumDriver driver)
    {
        var entry = new SessionEntry { Driver = driver, LastActivityUtc = DateTime.UtcNow };
        return _sessions.TryAdd(sessionId, entry);
    }

    public void Dispose() => CloseAllSessions();
}
