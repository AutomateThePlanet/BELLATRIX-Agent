using Bellatrix.Execution.SessionMounting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Bellatrix.Execution;

/// <summary>
/// Manages Windows desktop application sessions supporting both WinAppDriver and NovaWindows driver.
/// Uses the same approach as BELLATRIX WrappedWebDriverCreateService.
/// Tracks per-session last-activity time and runs a background watchdog that closes idle sessions
/// older than <see cref="DesktopMcpSettings.SessionTimeoutMinutes"/>.
/// </summary>
public class DesktopSessionManager : IDisposable
{
    private sealed class SessionEntry
    {
        public required AppiumDriver Driver { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly ILogger<DesktopSessionManager> _logger;
    private readonly DesktopMcpSettings _settings;
    private readonly Timer? _watchdogTimer;
    private const string DefaultServerUrl = "http://127.0.0.1:4723";

    public DesktopSessionManager(ILogger<DesktopSessionManager> logger, DesktopMcpSettings settings)
    {
        _logger = logger;
        _settings = settings;

        if (_settings.SessionTimeoutMinutes > 0)
        {
            // Run the idle check at a cadence of min(1min, timeout/4) so short timeouts still fire promptly.
            var periodMs = Math.Max(10_000, Math.Min(60_000, _settings.SessionTimeoutMinutes * 60_000 / 4));
            _watchdogTimer = new Timer(WatchdogTick, null, periodMs, periodMs);
            _logger.LogInformation("Session watchdog enabled — closing sessions idle > {Minutes}m (check every {PeriodMs}ms)",
                _settings.SessionTimeoutMinutes, periodMs);
        }
        else
        {
            _logger.LogInformation("Session watchdog disabled (SessionTimeoutMinutes=0)");
        }
    }

    /// <summary>
    /// Starts a new desktop application session for the given app definition.
    /// Per-app fields (Path, WorkingDirectory, Arguments) come from <paramref name="app"/>;
    /// global driver behavior (timeouts, log file, additional capabilities) comes from <paramref name="settings"/>.
    /// </summary>
    public string StartSession(DesktopAppDefinition app, string driverType = "WinAppDriver", string? sessionId = null, string? serverUrl = null, DesktopMcpSettings? settings = null)
    {
        sessionId ??= Guid.NewGuid().ToString();
        serverUrl ??= DefaultServerUrl;

        try
        {
            if (settings?.KillExistingApps == true)
            {
                KillExistingAppInstances(app.Path);
            }

            var appiumOptions = new Dictionary<string, object>
            {
                { "app", app.Path }
            };

            ApplyAppOptions(appiumOptions, app);
            ApplySettingsToCapabilities(appiumOptions, settings);

            var driver = CreateDriver(serverUrl, driverType, appiumOptions, settings?.CreateSessionTimeout);

            if (!TryAddSession(sessionId, driver))
            {
                driver.Quit();
                throw new InvalidOperationException($"Session {sessionId} already exists");
            }

            _logger.LogInformation("Started {DriverType} session {SessionId} for app '{AppName}' ({AppPath})", driverType, sessionId, app.Name, app.Path);
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {DriverType} session for app '{AppName}' ({AppPath})", driverType, app.Name, app.Path);
            throw;
        }
    }

    /// <summary>
    /// Attaches to an existing application window by title. <paramref name="retryCount"/>/
    /// <paramref name="retryDelayMs"/> default to the production values (10 / 2000ms) and only exist
    /// so tests can exercise the retry-exhaustion path without waiting 20 seconds.
    /// </summary>
    public string AttachToWindow(string windowTitle, string driverType = "WinAppDriver", string? sessionId = null, string? serverUrl = null, DesktopMcpSettings? settings = null, int retryCount = 10, int retryDelayMs = 2000)
    {
        sessionId ??= Guid.NewGuid().ToString();
        serverUrl ??= DefaultServerUrl;

        try
        {
            var (windowHandle, rootDriver) = FindWindowByTitle(serverUrl, driverType, windowTitle, retryCount, retryDelayMs);
            rootDriver.Quit();

            // Create session attached to specific window
            var windowOptions = new Dictionary<string, object>
            {
                { "appTopLevelWindow", windowHandle }
            };
            ApplyIdleTimeout(windowOptions, settings);

            var driver = CreateDriver(serverUrl, driverType, windowOptions, settings?.CreateSessionTimeout);

            if (!TryAddSession(sessionId, driver))
            {
                driver.Quit();
                throw new InvalidOperationException($"Session {sessionId} already exists");
            }

            _logger.LogInformation("Attached to window '{WindowTitle}' with {DriverType} session {SessionId}", windowTitle, driverType, sessionId);
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to window '{WindowTitle}'", windowTitle);
            throw;
        }
    }

    /// <summary>
    /// Creates a WindowsDriver using the same pattern as BELLATRIX WrappedWebDriverCreateService.
    /// Nests capabilities under "appium:options" which works with both WinAppDriver and NovaWindows.
    /// </summary>
    private static WindowsDriver CreateDriver(string serverUrl, string driverType, Dictionary<string, object> appiumOptions, int? createSessionTimeoutSeconds = null)
    {
        // Appium 2.0 always requires automationName
        var isNovaWindows = driverType.Equals("NovaWindows", StringComparison.OrdinalIgnoreCase);
        appiumOptions["automationName"] = isNovaWindows ? "NovaWindows" : "Windows";

        var driverOptions = new AppiumOptions();
        driverOptions.PlatformName = "Windows";
        driverOptions.AddAdditionalAppiumOption("appium:options", appiumOptions);

        var timeout = createSessionTimeoutSeconds.HasValue
            ? TimeSpan.FromSeconds(createSessionTimeoutSeconds.Value)
            : TimeSpan.FromMinutes(10);

        return new WindowsDriver(new Uri(serverUrl), driverOptions, timeout);
    }

    /// <summary>
    /// Applies per-app options (working directory, arguments) to the Appium capabilities dictionary.
    /// </summary>
    private static void ApplyAppOptions(Dictionary<string, object> appiumOptions, DesktopAppDefinition app)
    {
        if (!string.IsNullOrEmpty(app.WorkingDirectory))
        {
            appiumOptions["appWorkingDir"] = app.WorkingDirectory;
        }

        if (!string.IsNullOrEmpty(app.Arguments))
        {
            appiumOptions["appArguments"] = app.Arguments;
        }
    }

    /// <summary>
    /// Applies global driver settings (timeouts, log file, additional capabilities) to the Appium capabilities dictionary.
    /// </summary>
    private static void ApplySettingsToCapabilities(Dictionary<string, object> appiumOptions, DesktopMcpSettings? settings)
    {
        if (settings == null) return;

        if (settings.WaitForAppLaunch.HasValue)
        {
            // NovaWindows auto-detects units: values ≤120 are seconds, >120 are ms.
            // To keep the setting documented as seconds across both drivers, convert
            // explicitly to ms for NovaWindows so values >120s aren't misread as ms.
            var raw = settings.WaitForAppLaunch.Value;
            appiumOptions["ms:waitForAppLaunch"] =
                string.Equals(settings.DriverType, "NovaWindows", StringComparison.OrdinalIgnoreCase)
                    ? raw * 1000
                    : raw;
        }

        ApplyIdleTimeout(appiumOptions, settings);

        if (!string.IsNullOrEmpty(settings.LogFile))
        {
            // "true" → boolean (driver picks default %LOCALAPPDATA% path). Anything else → path string.
            appiumOptions["logFile"] = settings.LogFile.Equals("true", StringComparison.OrdinalIgnoreCase)
                ? true
                : settings.LogFile;
        }

        if (settings.AdditionalCapabilities != null)
        {
            foreach (var kvp in settings.AdditionalCapabilities)
            {
                appiumOptions[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Applies the configured driver idle timeout (Appium "newCommandTimeout" capability) to the options dictionary.
    /// Called for every session creation — fresh start, attach-to-window, and re-attach — so idle timeout stays consistent.
    /// NewCommandTimeoutMinutes is converted to seconds (Appium expects seconds).
    /// </summary>
    private static void ApplyIdleTimeout(Dictionary<string, object> appiumOptions, DesktopMcpSettings? settings)
    {
        if (settings?.NewCommandTimeoutMinutes.HasValue == true)
        {
            appiumOptions["newCommandTimeout"] = settings.NewCommandTimeoutMinutes.Value * 60;
        }
    }

    /// <summary>
    /// Kills existing instances of the application before starting a new session.
    /// Supports both executable paths and App User Model IDs (UWP/MSIX).
    /// </summary>
    private void KillExistingAppInstances(string appPath)
    {
        try
        {
            // Extract process name from executable path (e.g., "C:\...\calc.exe" -> "calc")
            var processName = Path.GetFileNameWithoutExtension(appPath);

            if (string.IsNullOrEmpty(processName))
            {
                _logger.LogWarning("Could not extract process name from '{AppPath}', skipping kill", appPath);
                return;
            }

            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                _logger.LogDebug("No existing instances of '{ProcessName}' found", processName);
                return;
            }

            foreach (var process in processes)
            {
                try
                {
                    _logger.LogInformation("Killing existing instance of '{ProcessName}' (PID: {Pid})", processName, process.Id);
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill process '{ProcessName}' (PID: {Pid})", processName, process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while attempting to kill existing app instances for '{AppPath}'", appPath);
        }
    }

    /// <summary>
    /// Re-attaches an existing session to a new window by title.
    /// Used when the original window (e.g. splash screen) has closed and a new main window has appeared.
    /// Disposes the stale driver and replaces it with one attached to the new window.
    /// Retries finding the window for up to 30 seconds (the main window may not appear immediately after splash closes).
    /// </summary>
    public void ReattachSession(string sessionId, string windowTitle, string driverType = "WinAppDriver", string? serverUrl = null, DesktopMcpSettings? settings = null)
    {
        serverUrl ??= DefaultServerUrl;

        if (!_sessions.TryGetValue(sessionId, out var staleEntry))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        try
        {
            // Dispose the stale driver (ignore errors — the window is already gone)
            try { staleEntry.Driver.Quit(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Expected error disposing stale driver for session {SessionId}", sessionId); }

            var (windowHandle, rootDriver) = FindWindowByTitle(serverUrl, driverType, windowTitle, retryCount: 15, retryDelayMs: 2000);
            rootDriver.Quit();

            // Create a new session attached to the found window
            var windowOptions = new Dictionary<string, object> { { "appTopLevelWindow", windowHandle } };
            ApplyIdleTimeout(windowOptions, settings);
            var newDriver = CreateDriver(serverUrl, driverType, windowOptions, settings?.CreateSessionTimeout);

            // Replace the driver in the session map and reset activity
            staleEntry.Driver = newDriver;
            staleEntry.LastActivityUtc = DateTime.UtcNow;

            _logger.LogInformation("Re-attached session {SessionId} to window '{WindowTitle}' (handle: {Handle})", sessionId, windowTitle, windowHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-attach session {SessionId} to window '{WindowTitle}'", sessionId, windowTitle);
            throw;
        }
    }

    /// <summary>
    /// Finds a top-level window by title using a Root session with XPath contains() match and retry logic.
    /// Uses XPath instead of By.Name() because NovaWindows driver does not support CSS selectors.
    /// Returns the window handle already converted to the hex string WinAppDriver's <c>appTopLevelWindow</c>
    /// capability expects (see <see cref="ToAppTopLevelWindowHex"/>), and the root driver (caller must Quit it).
    /// </summary>
    private (string windowHandle, WindowsDriver rootDriver) FindWindowByTitle(string serverUrl, string driverType, string windowTitle, int retryCount = 10, int retryDelayMs = 2000)
    {
        var rootOptions = new Dictionary<string, object> { { "app", "Root" } };
        var rootDriver = CreateDriver(serverUrl, driverType, rootOptions);

        // Use XPath with contains() for partial title matching — By.Name() sends CSS selector which NovaWindows doesn't support
        var xpath = $"//Window[contains(@Name, '{windowTitle}')]";

        for (int attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                _logger.LogDebug("Finding window with title containing '{WindowTitle}' (attempt {Attempt}/{Max})", windowTitle, attempt, retryCount);
                var window = rootDriver.FindElement(By.XPath(xpath));
                var nativeWindowHandle = window.GetAttribute("NativeWindowHandle");
                var actualName = window.GetAttribute("Name");
                var windowHandle = ToAppTopLevelWindowHex(nativeWindowHandle);
                _logger.LogInformation(
                    "Found window '{ActualName}' (nativeHandle: {NativeHandle}, appTopLevelWindow: {Handle}) matching '{WindowTitle}'",
                    actualName, nativeWindowHandle, windowHandle, windowTitle);
                return (windowHandle, rootDriver);
            }
            catch (NoSuchElementException)
            {
                if (attempt == retryCount)
                {
                    break;
                }

                _logger.LogDebug("Window '{WindowTitle}' not found yet, retrying in {Delay}ms...", windowTitle, retryDelayMs);
                Thread.Sleep(retryDelayMs);
            }
        }

        rootDriver.Quit();
        throw new InvalidOperationException($"Window containing '{windowTitle}' not found after {retryCount} attempts ({retryCount * retryDelayMs / 1000}s)");
    }

    /// <summary>
    /// Converts the decimal <c>NativeWindowHandle</c> UI Automation reports into the lowercase hex HWND
    /// string WinAppDriver's <c>appTopLevelWindow</c> capability requires — passing the decimal value
    /// through directly makes the driver report "Cannot find active window specified by capabilities:
    /// appTopLevelWindow" even though the window exists. Public (like <see cref="AttachExistingSessionExecutor.IsNewSessionCommand"/>):
    /// exposed for unit testing without a live driver.
    /// </summary>
    public static string ToAppTopLevelWindowHex(string nativeWindowHandle)
    {
        var handle = long.Parse(nativeWindowHandle, CultureInfo.InvariantCulture);
        return handle.ToString("x", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Mounts onto an EXISTING driver-server session by id (attach-by-id) WITHOUT sending a
    /// NewSession handshake, via <see cref="AttachExistingSessionExecutor"/>. Registers the bound
    /// driver in the in-process store so the rest of this single CLI invocation can act on it through
    /// <see cref="GetDriver"/>. The server session outlives the process (kept alive by the driver's
    /// newCommandTimeout), so a later invocation can mount again — no daemon.
    /// </summary>
    public AppiumDriver MountExisting(
        string sessionId, string serverUrl, string driverType, TimeSpan? commandTimeout = null, string? capsJson = null)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            existing.LastActivityUtc = DateTime.UtcNow;
            return existing.Driver;
        }

        var timeout = commandTimeout ?? TimeSpan.FromMinutes(10);
        // Replay cached caps from the original session when available; harmless when absent.
        var caps = DeserializeCaps(capsJson);
        var executor = new AttachExistingSessionExecutor(new Uri(serverUrl), sessionId, timeout, caps);

        var isNovaWindows = driverType.Equals("NovaWindows", StringComparison.OrdinalIgnoreCase);
        var options = new AppiumOptions { PlatformName = "Windows" };
        options.AutomationName = isNovaWindows ? "NovaWindows" : "Windows";

        // NewSession is intercepted by the executor, so these capabilities are never transmitted — the
        // driver simply binds to the supplied sessionId. WindowsDriver exposes no (ICommandExecutor,
        // ICapabilities) constructor (constructors are never inherited), so we bind through
        // AttachedWindowsDriver, which adds only a public forwarding constructor onto the shared
        // AppiumDriver base — no reflection required.
        AppiumDriver driver = new AttachedWindowsDriver(executor, options.ToCapabilities());
        TryAddSession(sessionId, driver);
        _logger.LogInformation("Mounted existing session {SessionId} at {ServerUrl} ({DriverType})",
            sessionId, serverUrl, driverType);
        return driver;
    }

    /// <summary>
    /// Deserializes the cached capabilities JSON persisted at session creation into a bag
    /// the attach executor can replay. Returns null on null/blank/malformed input so mounting degrades
    /// to the verified empty-caps behavior rather than failing.
    /// </summary>
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

    /// <summary>
    /// Gets a driver for an existing session. Also resets the session's last-activity timestamp
    /// so the watchdog treats this call as fresh activity.
    /// </summary>
    public AppiumDriver GetDriver(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }
        entry.LastActivityUtc = DateTime.UtcNow;
        return entry.Driver;
    }

    /// <summary>
    /// Closes a specific session
    /// </summary>
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

    /// <summary>
    /// Closes all active sessions
    /// </summary>
    public void CloseAllSessions()
    {
        foreach (var sessionId in _sessions.Keys.ToList())
        {
            CloseSession(sessionId);
        }
    }

    /// <summary>
    /// Gets all active session IDs
    /// </summary>
    public IEnumerable<string> GetActiveSessions() => _sessions.Keys;

    /// <summary>
    /// Checks if a session exists
    /// </summary>
    public bool SessionExists(string sessionId) => _sessions.ContainsKey(sessionId);

    private bool TryAddSession(string sessionId, AppiumDriver driver)
    {
        var entry = new SessionEntry { Driver = driver, LastActivityUtc = DateTime.UtcNow };
        return _sessions.TryAdd(sessionId, entry);
    }

    private void WatchdogTick(object? state)
    {
        try
        {
            var timeout = TimeSpan.FromMinutes(_settings.SessionTimeoutMinutes);
            var now = DateTime.UtcNow;
            foreach (var (sessionId, entry) in _sessions)
            {
                var idle = now - entry.LastActivityUtc;
                if (idle > timeout)
                {
                    _logger.LogInformation("Session {SessionId} idle for {IdleMinutes:F1}m (> {TimeoutMinutes}m), closing via watchdog",
                        sessionId, idle.TotalMinutes, _settings.SessionTimeoutMinutes);
                    CloseSession(sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Watchdog tick failed");
        }
    }

    public void Dispose()
    {
        _watchdogTimer?.Dispose();
        CloseAllSessions();
    }
}
