namespace Bellatrix.Execution;

/// <summary>
/// Defines a single desktop application that can be launched by the MCP server.
/// Multiple apps can be configured via McpSettings__Apps__N__* environment variables
/// (or the McpSettings:Apps array in appsettings.json) and selected at session start by Name.
/// </summary>
public class DesktopAppDefinition
{
    /// <summary>
    /// Logical name used by clients to select the app (e.g. "calculator", "notepad"). Required and case-insensitive.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to the executable or App User Model ID (UWP/MSIX). Required.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Working directory for the app. Sent as the Appium "appWorkingDir" capability.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Command-line arguments for the app. Sent as the Appium "appArguments" capability.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// When true, this app is selected by DesktopStartSession when no appName/appPath is provided.
    /// If multiple apps are marked default, the first one wins. If none are marked, the first in the list is used.
    /// </summary>
    public bool IsDefault { get; set; }
}
