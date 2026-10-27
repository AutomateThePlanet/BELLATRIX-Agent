namespace Bellatrix.Execution;

/// <summary>
/// Configuration settings for the Desktop MCP Server, populated from environment variables or appsettings.json.
/// </summary>
public class DesktopMcpSettings
{
    /// <summary>
    /// Driver type: "WinAppDriver" (default) or "NovaWindows"
    /// Env: McpSettings__DriverType
    /// </summary>
    public string DriverType { get; set; } = "WinAppDriver";

    /// <summary>
    /// Driver server URL. Default: http://127.0.0.1:4723
    /// Env: McpSettings__ServerUrl
    /// </summary>
    public string ServerUrl { get; set; } = "http://127.0.0.1:4723";

    /// <summary>
    /// Configured applications the MCP server can launch. Each entry has a Name (used by clients to
    /// pick the app), Path, optional WorkingDirectory and Arguments, and an IsDefault flag.
    /// Bound from McpSettings__Apps__N__* env vars (e.g. McpSettings__Apps__0__Name=calculator).
    /// When this list is empty, a single entry is synthesized from the legacy DefaultApp/AppWorkingDirectory/AppArguments fields.
    /// </summary>
    public List<DesktopAppDefinition> Apps { get; set; } = new();

    /// <summary>
    /// Legacy: default application path or App User Model ID. Used to synthesize an Apps entry
    /// when Apps is empty. Prefer the Apps array for new configurations.
    /// Env: McpSettings__DefaultApp
    /// </summary>
    public string? DefaultApp { get; set; }

    /// <summary>
    /// Legacy: working directory for the single configured app. Used to synthesize an Apps entry
    /// when Apps is empty. Prefer the Apps array for new configurations.
    /// Env: McpSettings__AppWorkingDirectory
    /// </summary>
    public string? AppWorkingDirectory { get; set; }

    /// <summary>
    /// Kill existing instances of the application before starting a new session. Default: false.
    /// Env: McpSettings__KillExistingApps
    /// </summary>
    public bool KillExistingApps { get; set; } = false;

    /// <summary>
    /// Timeout in seconds to wait for the application to launch. WinAppDriver default is 5.
    /// Env: McpSettings__WaitForAppLaunch
    /// </summary>
    public int? WaitForAppLaunch { get; set; }

    /// <summary>
    /// Legacy: command-line arguments for the single configured app. Used to synthesize an Apps entry
    /// when Apps is empty. Prefer the Apps array for new configurations.
    /// Env: McpSettings__AppArguments
    /// </summary>
    public string? AppArguments { get; set; }

    /// <summary>
    /// Timeout in seconds for creating a new session. Default: 1200 (10 minutes).
    /// Env: McpSettings__CreateSessionTimeout
    /// </summary>
    public int? CreateSessionTimeout { get; set; }

    /// <summary>
    /// Driver-level idle timeout in MINUTES between driver commands before the driver terminates
    /// the session (Appium/WinAppDriver "newCommandTimeout" capability). Appium's default is 60
    /// seconds, which is why sessions die after ~1 minute of inactivity. Set to a large value
    /// (e.g. 60 = 1h) for long-running interactive use, or 0 to disable the idle timeout entirely.
    /// Sent as the "newCommandTimeout" capability (converted to seconds when applied).
    /// Env: McpSettings__NewCommandTimeoutMinutes
    /// </summary>
    public int? NewCommandTimeoutMinutes { get; set; }

    /// <summary>
    /// Server-side session timeout in MINUTES. A background watchdog in the MCP server closes any
    /// session whose last tool call was longer ago than this value, freeing driver resources even
    /// if the client disappears without calling desktop_close_session. Independent of, and layered
    /// on top of, the driver-level NewCommandTimeoutMinutes. Default: 60 minutes. Set to 0 to
    /// disable the watchdog.
    /// Env: McpSettings__SessionTimeoutMinutes
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Mirror NovaWindows driver logs to a file (NovaWindows only — ignored by WinAppDriver).
    /// Accepted values:
    ///   "true"  → default location: %LOCALAPPDATA%\novawindows-driver\session-&lt;ts&gt;.log
    ///   folder path (e.g. "C:\\logs\\")          → timestamped file inside that folder
    ///   file path  (e.g. "D:\\tmp\\my.log")      → exact file path (appended)
    /// Sent as the "appium:logFile" capability.
    /// Env: McpSettings__LogFile
    /// </summary>
    public string? LogFile { get; set; }

    /// <summary>
    /// Additional Appium capabilities as key-value pairs.
    /// Allows setting any custom WinAppDriver or NovaWindows capability not explicitly modeled.
    /// </summary>
    public Dictionary<string, object>? AdditionalCapabilities { get; set; }

    /// <summary>
    /// If <see cref="Apps"/> is empty but a legacy DefaultApp is configured, synthesize a single
    /// Apps entry from the legacy fields so the rest of the system can treat everything uniformly.
    /// Safe to call multiple times; no-op once Apps already has entries.
    /// </summary>
    public void NormalizeApps()
    {
        if (Apps.Count > 0 || string.IsNullOrEmpty(DefaultApp))
        {
            return;
        }

        Apps.Add(new DesktopAppDefinition
        {
            Name = "default",
            Path = DefaultApp,
            WorkingDirectory = AppWorkingDirectory,
            Arguments = AppArguments,
            IsDefault = true,
        });
    }

    /// <summary>
    /// Resolves an app definition by name. If <paramref name="name"/> is null or empty, returns the
    /// default app: the first entry with IsDefault=true, or the first entry overall.
    /// Returns null when no apps are configured. Throws when a name is given but not found.
    /// </summary>
    public DesktopAppDefinition? ResolveApp(string? name)
    {
        if (Apps.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Apps.FirstOrDefault(a => a.IsDefault) ?? Apps[0];
        }

        var match = Apps.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"App '{name}' is not configured. Available apps: {string.Join(", ", Apps.Select(a => a.Name))}");
        return match;
    }
}
