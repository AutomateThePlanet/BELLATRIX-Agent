using Bellatrix.Execution;

namespace Bellatrix.Cli;

/// <summary>
/// Builds <see cref="DesktopMcpSettings"/> from the documented <c>McpSettings__*</c> environment
/// matrix — the same configuration the consumer's <c>.vscode/mcp.json</c> passes to the MCP server
/// (research §5/§6). Env-only: the CLI is launched per-command with these variables set, so there is
/// no appsettings layering to honor. Secrets are never read here.
/// </summary>
public static class DesktopSettingsLoader
{
    public static DesktopMcpSettings Load()
    {
        var settings = new DesktopMcpSettings
        {
            DriverType = Env("McpSettings__DriverType") ?? "WinAppDriver",
            ServerUrl = Env("McpSettings__ServerUrl") ?? "http://127.0.0.1:4723",
            DefaultApp = Env("McpSettings__DefaultApp"),
            AppWorkingDirectory = Env("McpSettings__AppWorkingDirectory"),
            AppArguments = Env("McpSettings__AppArguments"),
            KillExistingApps = bool.TryParse(Env("McpSettings__KillExistingApps"), out var kill) && kill,
        };

        if (int.TryParse(Env("McpSettings__WaitForAppLaunch"), out var wait)) settings.WaitForAppLaunch = wait;
        if (int.TryParse(Env("McpSettings__CreateSessionTimeout"), out var cst)) settings.CreateSessionTimeout = cst;
        if (int.TryParse(Env("McpSettings__NewCommandTimeoutMinutes"), out var nct)) settings.NewCommandTimeoutMinutes = nct;

        // Keep-alive default 10 minutes when unset. The in-process watchdog is disabled
        // by the CLI (no daemon) — see CommandContext.BuildEngine.
        settings.NewCommandTimeoutMinutes ??= 10;

        // Apps__N__{Name,Path,WorkingDirectory,Arguments,IsDefault}
        for (var i = 0; ; i++)
        {
            var path = Env($"McpSettings__Apps__{i}__Path");
            var name = Env($"McpSettings__Apps__{i}__Name");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
            {
                if (i == 0 && string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name))
                {
                    continue; // tolerate a gap at index 0; stop only after a real gap below
                }

                break;
            }

            settings.Apps.Add(new DesktopAppDefinition
            {
                Name = name,
                Path = path,
                WorkingDirectory = Env($"McpSettings__Apps__{i}__WorkingDirectory"),
                Arguments = Env($"McpSettings__Apps__{i}__Arguments"),
                IsDefault = bool.TryParse(Env($"McpSettings__Apps__{i}__IsDefault"), out var def) && def,
            });
        }

        settings.NormalizeApps();
        return settings;
    }

    private static string? Env(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
