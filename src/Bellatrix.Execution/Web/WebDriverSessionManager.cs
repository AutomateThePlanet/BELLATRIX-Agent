using Bellatrix.Execution.SessionMounting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using System.Collections.Concurrent;
using WebDriverManager;
using WebDriverManager.DriverConfigs;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;

namespace Bellatrix.Execution.Web;

/// <summary>
/// Manages web (Selenium) browser sessions for the model-free CLI. Mirrors the desktop/mobile managers:
/// an in-process registry of live drivers plus attach-by-id mounting (<see cref="MountExisting"/>). The
/// web mount is the one path that differs from desktop/mobile — Selenium exposes a PUBLIC
/// <c>RemoteWebDriver(ICommandExecutor, ICapabilities)</c> constructor, so no reflection is needed.
/// Every session is reattachable across invocations: with <c>--remote-url</c> it lives on that Selenium server
/// (Grid/standalone); otherwise the browser driver (chromedriver/msedgedriver/geckodriver) is started as a
/// <b>detached driver-server process</b> on a free port that outlives this CLI process, and the CLI talks to it
/// over W3C HTTP exactly like a remote server. Its PID is cached so <c>close-session</c> can stop it
/// (<see cref="StopDriverServer"/>).
/// </summary>
public class WebDriverSessionManager : IDisposable
{
    private sealed class SessionEntry
    {
        public required IWebDriver Driver { get; set; }
        public required string ServerUrl { get; set; }
        public DateTime LastActivityUtc { get; set; }

        /// <summary>The detached local driver server (null for <c>--remote-url</c> or mounted sessions). Held so it
        /// is never collected while this process runs; it is intentionally NOT disposed, so it outlives the CLI.</summary>
        public DriverService? DriverServer { get; set; }
    }

    /// <summary>Process names of the browser drivers BELLATRIX may start; <see cref="StopDriverServer"/> only stops these.</summary>
    private static readonly string[] DriverProcessNames = { "chromedriver", "msedgedriver", "geckodriver" };

    /// <summary>Capability key (vendor-prefixed, W3C extension style) caching the detached driver server PID.</summary>
    public const string DriverPidCapability = "bellatrix:driverPid";

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly ILogger<WebDriverSessionManager> _logger;

    public WebDriverSessionManager(ILogger<WebDriverSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a new browser session. When <paramref name="remoteUrl"/> is given the session is created on
    /// that Selenium server (Grid/standalone); otherwise a detached local driver server is started. Either way
    /// the session survives this process for later attach-by-id. Returns the local key; the caller reads the
    /// real Selenium session id, server URL and driver PID from <see cref="GetDriver"/> /
    /// <see cref="GetServerUrl"/> / <see cref="GetDriverServerPid"/>.
    /// </summary>
    public string StartSession(
        string browserType = "chrome",
        bool headless = false,
        int? width = 1920,
        int? height = 1080,
        string? remoteUrl = null,
        string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString();

        try
        {
            var (driver, serverUrl, driverServer) = remoteUrl is not null
                ? (CreateRemoteDriver(remoteUrl, browserType, headless), remoteUrl, (DriverService?)null)
                : CreateDetachedLocalDriver(browserType, headless);

            if (width.HasValue && height.HasValue)
            {
                driver.Manage().Window.Size = new System.Drawing.Size(width.Value, height.Value);
            }

            if (!TryAddSession(sessionId, driver, serverUrl, driverServer))
            {
                driver.Quit();
                StopDriverServer(driverServer?.ProcessId);
                throw new InvalidOperationException($"Session {sessionId} already exists");
            }

            _logger.LogInformation("Started {Browser} session {SessionId} at {ServerUrl}", browserType, sessionId, serverUrl);
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {Browser} session", browserType);
            throw;
        }
    }

    /// <summary>
    /// Mounts onto an EXISTING Selenium session by id (attach-by-id) WITHOUT a NewSession
    /// handshake, via <see cref="AttachExistingSessionExecutor"/> + Selenium's public executor ctor.
    /// </summary>
    public IWebDriver MountExisting(
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

        var browserName = caps != null && caps.TryGetValue("browserName", out var b) ? b?.ToString() : null;
        var capabilities = BuildOptions(browserName ?? "chrome", headless: false).ToCapabilities();

        // NewSession is intercepted by the executor — capabilities are never transmitted, the driver binds
        // to the supplied id. Selenium's (ICommandExecutor, ICapabilities) ctor is public (no reflection).
        var driver = new RemoteWebDriver(executor, capabilities);
        TryAddSession(sessionId, driver, serverUrl);
        _logger.LogInformation("Mounted existing web session {SessionId} at {ServerUrl}", sessionId, serverUrl);
        return driver;
    }

    /// <summary>
    /// The capabilities cached at session creation for replay on a later attach-by-id mount, plus the PID of a
    /// detached local driver server (when one was started) so <c>close-session</c> can stop it.
    /// </summary>
    public static string BuildCapsJson(string browserType, int? driverPid = null)
    {
        var caps = new Dictionary<string, object> { ["browserName"] = NormalizeBrowserName(browserType) };
        if (driverPid is { } pid)
        {
            caps[DriverPidCapability] = pid;
        }

        return System.Text.Json.JsonSerializer.Serialize(caps);
    }

    /// <summary>The detached driver server PID cached in a session's caps JSON, if any.</summary>
    public static int? ReadDriverPid(string? capsJson)
    {
        if (string.IsNullOrWhiteSpace(capsJson))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(capsJson);
            return doc.RootElement.TryGetProperty(DriverPidCapability, out var pid) && pid.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stops a detached local driver server (and the browser it owns) by PID. Only a process whose name is a
    /// known browser driver is ever touched, so a recycled PID is never killed. Returns true when it was stopped.
    /// </summary>
    public static bool StopDriverServer(int? pid)
    {
        if (pid is null)
        {
            return false;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid.Value);
            if (!DriverProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (ArgumentException)
        {
            return false; // already gone
        }
        catch (InvalidOperationException)
        {
            return false; // exited between lookup and kill
        }
    }

    private static string NormalizeBrowserName(string browserType) => browserType.ToLowerInvariant() switch
    {
        "firefox" => "firefox",
        "edge" => "MicrosoftEdge",
        _ => "chrome",
    };

    private static DriverOptions BuildOptions(string browserType, bool headless)
    {
        switch (browserType.ToLowerInvariant())
        {
            case "firefox":
                var ff = new FirefoxOptions();
                if (headless) ff.AddArgument("--headless");
                ff.AcceptInsecureCertificates = true;
                return ff;
            case "edge":
            case "microsoftedge":
                var edge = new EdgeOptions();
                if (headless) edge.AddArgument("--headless=new");
                edge.AddArguments("--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu");
                edge.AcceptInsecureCertificates = true;
                return edge;
            default:
                var chrome = new ChromeOptions();
                if (headless) chrome.AddArgument("--headless=new");
                chrome.AddArguments("--no-sandbox", "--disable-dev-shm-usage",
                    "--disable-blink-features=AutomationControlled", "--disable-gpu");
                chrome.AcceptInsecureCertificates = true;
                return chrome;
        }
    }

    private static IWebDriver CreateRemoteDriver(string remoteUrl, string browserType, bool headless)
        => new RemoteWebDriver(new Uri(remoteUrl), BuildOptions(browserType, headless));

    /// <summary>
    /// Starts the browser driver as a standalone W3C server on a free port and opens the session over HTTP.
    /// The <see cref="DriverService"/> is deliberately never disposed: its process outlives this CLI invocation,
    /// which is what makes a later <c>bellatrix web &lt;verb&gt;</c> able to re-attach by session id.
    /// </summary>
    private static (IWebDriver Driver, string ServerUrl, DriverService DriverServer) CreateDetachedLocalDriver(
        string browserType, bool headless)
    {
        DriverService service;
        switch (browserType.ToLowerInvariant())
        {
            case "firefox":
                new DriverManager().SetUpDriver(new FirefoxConfig());
                service = FirefoxDriverService.CreateDefaultService();
                break;
            case "edge":
            case "microsoftedge":
                SetUpDriverMatchingBrowser(new EdgeConfig());
                service = EdgeDriverService.CreateDefaultService();
                break;
            default:
                SetUpDriverMatchingBrowser(new ChromeConfig());
                service = ChromeDriverService.CreateDefaultService();
                break;
        }

        service.SuppressInitialDiagnosticInformation = true;
        service.HideCommandPromptWindow = true;
        StdHandleInheritance.DisableForChildren(); // the detached driver must not hold the caller's stdout pipe open
        service.Start();

        try
        {
            var driver = new RemoteWebDriver(service.ServiceUrl, BuildOptions(browserType, headless));
            return (driver, service.ServiceUrl.ToString(), service);
        }
        catch
        {
            StopDriverServer(service.ProcessId);
            throw;
        }
    }

    /// <summary>
    /// Fetches the driver for the INSTALLED browser version. WebDriverManager's default (<c>Latest</c>) puts the
    /// newest chromedriver/msedgedriver first on PATH, so every session fails with "only supports Chrome version N"
    /// whenever the local browser is one release behind (found by the v.1 live E2E). Falls back to <c>Latest</c>
    /// only when the browser version cannot be detected.
    /// </summary>
    private static void SetUpDriverMatchingBrowser(IDriverConfig config)
    {
        try
        {
            new DriverManager().SetUpDriver(config, VersionResolveStrategy.MatchingBrowser);
        }
        catch (Exception)
        {
            new DriverManager().SetUpDriver(config);
        }
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

    public IWebDriver GetDriver(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }
        entry.LastActivityUtc = DateTime.UtcNow;
        return entry.Driver;
    }

    /// <summary>The PID of the detached local driver server this process started for the session, if any.</summary>
    public int? GetDriverServerPid(string sessionId)
        => _sessions.TryGetValue(sessionId, out var entry) ? entry.DriverServer?.ProcessId : null;

    /// <summary>The Selenium server URL the session was created against (persisted for attach-by-id).</summary>
    public string GetServerUrl(string sessionId)
        => _sessions.TryGetValue(sessionId, out var entry)
            ? entry.ServerUrl
            : throw new InvalidOperationException($"Session {sessionId} not found");

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

    private bool TryAddSession(string sessionId, IWebDriver driver, string serverUrl, DriverService? driverServer = null)
    {
        var entry = new SessionEntry
        {
            Driver = driver, ServerUrl = serverUrl, LastActivityUtc = DateTime.UtcNow, DriverServer = driverServer,
        };
        return _sessions.TryAdd(sessionId, entry);
    }

    public void Dispose() => CloseAllSessions();
}
