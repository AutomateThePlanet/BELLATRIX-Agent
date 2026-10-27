using Bellatrix.Cli.Output;
using Bellatrix.Execution;
using Bellatrix.Execution.SessionMounting;
using Bellatrix.Execution.Snapshots;
using OpenQA.Selenium.Appium;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// Thin verb handlers: shape arguments → call the shared engine → print the JSON contract. No model,
/// no credentials. Session-bearing verbs mount the selected session by id
/// before acting; session creation persists the real W3C session id to the registry.
/// </summary>
internal static class DesktopCommands
{
    /// <summary>The desktop verb surface (WinAppDriver/NovaWindows): routing, help and drift tests read this.</summary>
    public static readonly IReadOnlyList<CommandSpec<CommandContext>> Specs = BuildSpecs();

    private static CommandSpec<CommandContext>[] BuildSpecs()
    {
        var session = Flags.Session;
        var locator = Flags.DesktopLocator;
        CommandSpec<CommandContext> Verb(string name, string summary, Func<CommandContext, Task<int>> handler, string output, params object[] flags)
            => new("desktop", name, summary, handler) { Flags = Flags.Of(flags), Output = output };

        return new[]
        {
            Verb("start-session", "Launch a configured app (or --app-path) on the driver and persist the session.", StartSession,
                "{sessionId, appName, appPath, driverType}", session,
                Flags.Value("app", "<name>", "A configured app name (see list-apps). Default: the default configured app."),
                Flags.Value("app-path", "<path|AUMID>", "Ad-hoc app path or AppUserModelId instead of a configured app.")),
            Verb("list-apps", "List the configured apps (McpSettings__Apps__N__Name/Path).", ListApps,
                "{apps[{name, path, workingDirectory, arguments, isDefault}], count}"),
            Verb("attach-window", "Attach a new session to an already-running top-level window by title.", AttachWindow,
                "{sessionId, windowTitle, driverType}", session,
                Flags.Value("window-title", "<title>", "Exact window title to attach to.", required: true)),
            Verb("reattach-session", "Re-point an existing session at another window (e.g. the main window after a splash screen).", ReattachSession,
                "{success, sessionId, windowTitle, driverType}", session,
                Flags.Value("window-title", "<title>", "Exact window title to attach to.", required: true)),
            Verb("close-session", "Close the app session and forget it.", CloseSession, "{success, sessionId}", session),
            Verb("list-sessions", "List the live desktop sessions (stale rows of this domain are pruned).", ListSessions, "{sessions[], count}"),
            Verb("get-title", "Get the current window title.", GetTitle, "{title}", session),
            Verb("maximize", "Maximize the current window.", Maximize, "{success}", session),
            Verb("get-page-source", "Return the raw UIA page source XML (context-heavy; prefer snapshot + find).", GetPageSource, "{pageSource}", session),
            Verb("snapshot", "Capture the UI tree to a file and return only a compact reference.", Snapshot, Flags.SnapshotOutput,
                session, Flags.SnapshotCapture) with { Aliases = new[] { "get-view-snapshot" } },
            Verb("find", "Query a saved snapshot (--snapshot) or the live view for a handful of matching nodes.", Find, Flags.FindOutput,
                session, Flags.SnapshotFile("desktop"),
                Flags.Value("name", "<name>", "Nodes whose Name matches."),
                Flags.Value("automation-id", "<id>", "Nodes whose AutomationId matches."),
                Flags.Value("control-type", "<type>", "Nodes whose ControlType matches (e.g. Button)."),
                Flags.FindText, Flags.FindGrep, Flags.FindInteractive) with { Aliases = new[] { "query-snapshot" } },
            Verb("take-screenshot", "Screenshot the current window.", TakeScreenshot, Flags.ScreenshotOutput, session, Flags.Out, Flags.ScreenshotInline),
            Verb("click", "Click an element.", Click, "{success}", session, locator),
            Verb("type-text", "Type text into an element.", TypeText, "{success}", session, locator,
                Flags.Value("text", "<text>", "Text to type.", required: true)),
            Verb("clear", "Clear an element's text.", Clear, "{success}", session, locator),
            Verb("get-text", "Get an element's text.", GetText, "{text}", session, locator),
            Verb("get-attribute", "Get an element attribute.", GetAttribute, "{attribute, value}", session, locator,
                Flags.Value("attribute", "<name>", "Attribute name (e.g. Name, AutomationId, IsOffscreen).", required: true)),
            Verb("is-visible", "Whether an element is displayed.", IsVisible, "{visible}", session, locator),
            Verb("is-enabled", "Whether an element is enabled.", IsEnabled, "{enabled}", session, locator),
            new("desktop", "scaffold", "Former name of bellatrix init (desktop only).", ScaffoldCommand.Run)
            {
                Flags = CommandRegistry.InitFlags, Output = "same as init", DeprecatedUse = "bellatrix init",
            },
        };
    }

    // ===== Session lifecycle =====

    public static async Task<int> StartSession(CommandContext ctx)
    {
        await Task.CompletedTask;
        var appName = ctx.Opt("app");
        var appPath = ctx.Opt("app-path");

        DesktopAppDefinition app;
        if (!string.IsNullOrWhiteSpace(appPath))
        {
            if (!string.IsNullOrWhiteSpace(appName))
            {
                throw new CliException("usage", "Provide either --app or --app-path, not both.");
            }

            app = new DesktopAppDefinition { Name = "ad-hoc", Path = appPath };
        }
        else
        {
            app = ctx.Settings.ResolveApp(appName)
                ?? throw new CliException("usage",
                    "No app specified and none configured. Provide --app-path, or configure McpSettings__Apps__0__Path.");
        }

        var (manager, _) = ctx.BuildEngine();
        var localId = manager.StartSession(app, ctx.Settings.DriverType, null, ctx.Settings.ServerUrl, ctx.Settings);
        var driver = manager.GetDriver(localId);
        var realId = driver.SessionId.ToString();

        ctx.Registry.Upsert(new SessionRecord
        {
            LogicalName = ctx.SessionName,
            SessionId = realId,
            DriverType = ctx.Settings.DriverType,
            ServerUrl = ctx.Settings.ServerUrl,
            LastActivityUtc = DateTime.UtcNow,
            LastWindowTitle = SafeTitle(driver),
            // Cache the capabilities a later invocation replays when mounting by id. The
            // platform/automation pair is what desktop attach needs; server-negotiated caps capture is
            // a live-validation refinement.
            Caps = CapsJson(ctx.Settings.DriverType),
        }, rejectDuplicate: true);

        return JsonOutput.Success(new
        {
            sessionId = realId,
            appName = app.Name,
            appPath = app.Path,
            driverType = ctx.Settings.DriverType,
        });
    }

    public static async Task<int> ListApps(CommandContext ctx)
    {
        await Task.CompletedTask;
        var apps = ctx.Settings.Apps.Select(a => new
        {
            name = a.Name,
            path = a.Path,
            workingDirectory = a.WorkingDirectory,
            arguments = a.Arguments,
            isDefault = a.IsDefault,
        }).ToList();

        return JsonOutput.Success(new { apps, count = apps.Count });
    }

    public static async Task<int> AttachWindow(CommandContext ctx)
    {
        await Task.CompletedTask;
        var windowTitle = ctx.RequireOpt("window-title");
        var (manager, _) = ctx.BuildEngine();
        var localId = manager.AttachToWindow(windowTitle, ctx.Settings.DriverType, null, ctx.Settings.ServerUrl, ctx.Settings);
        var realId = manager.GetDriver(localId).SessionId.ToString();

        ctx.Registry.Upsert(new SessionRecord
        {
            LogicalName = ctx.SessionName,
            SessionId = realId,
            DriverType = ctx.Settings.DriverType,
            ServerUrl = ctx.Settings.ServerUrl,
            LastActivityUtc = DateTime.UtcNow,
            LastWindowTitle = windowTitle,
        }, rejectDuplicate: true);

        return JsonOutput.Success(new { sessionId = realId, windowTitle, driverType = ctx.Settings.DriverType });
    }

    public static async Task<int> ReattachSession(CommandContext ctx)
    {
        await Task.CompletedTask;
        var windowTitle = ctx.RequireOpt("window-title");
        var (manager, _) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);

        // Splash recovery within a live session: re-derive the window (creates a fresh server session).
        manager.ReattachSession(realId, windowTitle, ctx.Settings.DriverType, ctx.Settings.ServerUrl, ctx.Settings);
        var newId = manager.GetDriver(realId).SessionId.ToString();

        var record = ctx.Registry.TryGet(ctx.SessionName)!;
        record.SessionId = newId;
        record.LastWindowTitle = windowTitle;
        record.LastActivityUtc = DateTime.UtcNow;
        ctx.Registry.Upsert(record);

        return JsonOutput.Success(new { success = true, sessionId = newId, windowTitle, driverType = ctx.Settings.DriverType });
    }

    public static async Task<int> CloseSession(CommandContext ctx)
    {
        await Task.CompletedTask;
        var (manager, _) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        manager.CloseSession(realId); // DELETE /session/{id}
        ctx.Registry.Remove(ctx.SessionName);
        return JsonOutput.Success(new { success = true, sessionId = realId });
    }

    public static async Task<int> ListSessions(CommandContext ctx)
    {
        await Task.CompletedTask;
        var keepAlive = TimeSpan.FromMinutes(ctx.Settings.NewCommandTimeoutMinutes ?? 10);
        var now = DateTime.UtcNow;
        var live = new List<string>();

        foreach (var kv in ctx.Registry.ReadAll())
        {
            if (now - kv.Value.LastActivityUtc > keepAlive)
            {
                ctx.Registry.Remove(kv.Key); // idle prune without a daemon, time-based
            }
            else
            {
                live.Add(kv.Value.SessionId);
            }
        }

        return JsonOutput.Success(new { sessions = live, count = live.Count });
    }

    // ===== Window info =====

    public static async Task<int> GetTitle(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var title = await service.GetWindowTitleAsync(realId);
        return JsonOutput.Success(new { title });
    }

    public static async Task<int> Maximize(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.MaximizeWindowAsync(realId);
        return JsonOutput.Success(new { success = true });
    }

    // ===== View information =====

    public static async Task<int> GetPageSource(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var pageSource = await service.GetPageSourceAsync(realId);
        return JsonOutput.Success(new { pageSource });
    }

    /// <summary>
    /// `bellatrix desktop snapshot` (alias `get-view-snapshot`). Default: capture the view and save it,
    /// returning only the compact reference `{path, elementCount, summary}`.
    /// `--inline` returns the full node array (MCP-shim parity). `--grep <regex>` / `--interactive-only`
    /// filter the captured nodes IN-PROCESS so only matches enter the model.
    /// </summary>
    public static async Task<int> Snapshot(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var nodes = await service.GetViewSnapshotAsync(realId);

        if (ctx.Options.ContainsKey("inline"))
        {
            return JsonOutput.Success(nodes);
        }

        if (ctx.Opt("grep") is { } pattern)
        {
            var matches = SnapshotQuery.Run(nodes, SnapshotQueryMode.Grep, pattern);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "grep", value = pattern } });
        }

        if (ctx.Options.ContainsKey("interactive-only"))
        {
            var matches = SnapshotQuery.Run(nodes, SnapshotQueryMode.Interactive, null);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "interactive-only", value = (string?)null } });
        }

        var reference = new SnapshotStore().Save(realId, nodes);
        return JsonOutput.Success(reference);
    }

    /// <summary>
    /// `bellatrix desktop find` (alias `query-snapshot`). Filters by `--name`/`--automation-id`/
    /// `--control-type` (also `--text`/`--grep`/`--interactive-only`). Queries a persisted artifact when
    /// `--snapshot &lt;path&gt;` is given (driver-free); otherwise captures the live view first.
    /// </summary>
    public static async Task<int> Find(CommandContext ctx)
    {
        List<Dictionary<string, object>> nodes;
        if (ctx.Opt("snapshot") is { } path)
        {
            nodes = new SnapshotStore().Load(path);
        }
        else
        {
            var (manager, service) = ctx.BuildEngine();
            var realId = ctx.MountSession(manager);
            nodes = await service.GetViewSnapshotAsync(realId);
        }

        SnapshotQueryMode mode;
        string? value = null;
        if (ctx.Options.ContainsKey("interactive-only")) { mode = SnapshotQueryMode.Interactive; }
        else if (ctx.Opt("grep") is { } g) { mode = SnapshotQueryMode.Grep; value = g; }
        else if (ctx.Opt("text") is { } t) { mode = SnapshotQueryMode.Text; value = t; }
        else if (ctx.Opt("name") is { } n) { mode = SnapshotQueryMode.Name; value = n; }
        else if (ctx.Opt("automation-id") is { } a) { mode = SnapshotQueryMode.AutomationId; value = a; }
        else if (ctx.Opt("control-type") is { } c) { mode = SnapshotQueryMode.ControlType; value = c; }
        else
        {
            throw new CliException("usage",
                "Provide one of --name, --automation-id, --control-type, --text, --grep, or --interactive-only.");
        }

        var results = SnapshotQuery.Run(nodes, mode, value);
        return JsonOutput.Success(new { nodes = results, count = results.Count, query = new { mode = mode.ToString(), value } });
    }

    public static async Task<int> TakeScreenshot(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var screenshot = await service.TakeScreenshotAsync(realId);

        if (ctx.Options.ContainsKey("inline"))
        {
            return JsonOutput.Success(new { screenshot });
        }

        var shot = new ScreenshotStore().Save(realId, screenshot, ctx.Opt("out"));
        return JsonOutput.Success(new { path = shot.Path, width = shot.Width, height = shot.Height });
    }

    // ===== Element interaction & inspection =====

    public static async Task<int> Click(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.ClickElementAsync(realId, ctx.Locator());
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> TypeText(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.TypeTextAsync(realId, ctx.Locator(), ctx.RequireOpt("text"));
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> Clear(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.ClearElementAsync(realId, ctx.Locator());
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> GetText(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var text = await service.GetElementTextAsync(realId, ctx.Locator());
        return JsonOutput.Success(new { text });
    }

    public static async Task<int> GetAttribute(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var attribute = ctx.RequireOpt("attribute");
        var value = await service.GetElementAttributeAsync(realId, ctx.Locator(), attribute);
        return JsonOutput.Success(new { attribute, value });
    }

    public static async Task<int> IsVisible(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var visible = await service.IsElementVisibleAsync(realId, ctx.Locator());
        return JsonOutput.Success(new { visible });
    }

    public static async Task<int> IsEnabled(CommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var enabled = await service.IsElementEnabledAsync(realId, ctx.Locator());
        return JsonOutput.Success(new { enabled });
    }

    private static string? SafeTitle(AppiumDriver driver)
    {
        try { return driver.Title; }
        catch { return null; }
    }

    /// <summary>
    /// The capabilities cached at session creation for replay on a later attach-by-id mount.
    /// Holds the deterministic platform/automation pair the desktop driver needs.
    /// </summary>
    private static string CapsJson(string driverType)
    {
        var automationName = driverType.Equals("NovaWindows", StringComparison.OrdinalIgnoreCase) ? "NovaWindows" : "Windows";
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["platformName"] = "Windows",
            ["appium:automationName"] = automationName,
        });
    }
}
