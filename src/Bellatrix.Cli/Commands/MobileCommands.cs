using Bellatrix.Cli.Output;
using Bellatrix.Execution.Mobile;
using Bellatrix.Execution.SessionMounting;
using Bellatrix.Execution.Snapshots;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// Thin <c>bellatrix mobile &lt;verb&gt;</c> handlers: shape arguments → call the mobile engine → print
/// the JSON contract. No model, no credentials. Session-bearing verbs mount the selected
/// session by id (attach-by-id) before acting; <c>start-session</c> persists the real Appium session id.
/// Mirrors <see cref="DesktopCommands"/> — duplicate-per-domain by design.
/// </summary>
internal static class MobileCommands
{
    /// <summary>The mobile verb surface (Appium, Android/iOS): routing, help and drift tests read this.</summary>
    public static readonly IReadOnlyList<CommandSpec<MobileCommandContext>> Specs = BuildSpecs();

    /// <summary>Every routable mobile verb (canonical names + aliases), derived from <see cref="Specs"/>.</summary>
    public static IReadOnlySet<string> KnownVerbs => CommandRegistry.RoutedNames("mobile");

    private static CommandSpec<MobileCommandContext>[] BuildSpecs()
    {
        var session = Flags.Session;
        var locator = Flags.MobileLocator;
        CommandSpec<MobileCommandContext> Verb(string name, string summary, Func<MobileCommandContext, Task<int>> handler, string output, params object[] flags)
            => new("mobile", name, summary, handler) { Flags = Flags.Of(flags), Output = output };

        return new[]
        {
            Verb("start-session", "Start an app session on your Appium server and persist it.", StartSession,
                "{sessionId, platform, appPath}", session,
                Flags.Value("platform", "<platform>", "Target platform.", required: true, allowed: new[] { "android", "ios" }),
                Flags.Value("app", "<path>", "App file (.apk/.app/.ipa) to install and launch.", required: true),
                Flags.Value("device-name", "<name>", "Device or emulator name."),
                Flags.Value("appium-url", "<url>", "Your Appium server.", @default: MobileCommandContext.DefaultAppiumUrl)),
            Verb("close-session", "Close the app session and forget it.", CloseSession, "{success, sessionId}", session),
            Verb("list-sessions", "List the live mobile sessions (stale rows of this domain are pruned).", ListSessions, "{sessions[], count}"),
            Verb("get-context", "Get the current context (NATIVE_APP or a WEBVIEW).", GetContext, "{context}", session),
            Verb("set-context", "Switch context.", SetContext, "{success, context}", session,
                Flags.Value("context", "<name>", "Context name from get-contexts.", required: true)),
            Verb("get-contexts", "List the available contexts.", GetContexts, "{contexts[]}", session),
            Verb("background-app", "Send the app to the background for N seconds.", BackgroundApp, "{success}", session,
                Flags.Value("seconds", "<n>", "Seconds in the background.", required: true)),
            Verb("close-app", "Terminate an app.", CloseApp, "{success}", session,
                Flags.Value("app-id", "<id>", "Package name / bundle id.", required: true)),
            Verb("launch-app", "Activate an installed app.", LaunchApp, "{success}", session,
                Flags.Value("app-id", "<id>", "Package name / bundle id.", required: true)),
            Verb("install-app", "Install an app file.", InstallApp, "{success}", session,
                Flags.Value("app-path", "<path>", "App file to install.", required: true)),
            Verb("remove-app", "Uninstall an app from the device.", RemoveApp, "{success}", session,
                Flags.Value("app-id", "<id>", "Package name / bundle id.", required: true)),
            Verb("is-app-installed", "Whether an app is installed.", IsAppInstalled, "{installed}", session,
                Flags.Value("bundle-id", "<id>", "Package name / bundle id.", required: true)),
            Verb("get-page-source", "Return the raw page source XML (context-heavy; prefer snapshot + find).", GetPageSource, "{pageSource}", session),
            Verb("snapshot", "Capture the screen's element tree to a file and return only a compact reference.", Snapshot, Flags.SnapshotOutput,
                session, Flags.SnapshotCapture) with { Aliases = new[] { "get-view-snapshot" } },
            Verb("find", "Query a saved snapshot (--snapshot) or the live screen for a handful of matching nodes.", Find, Flags.FindOutput,
                session, Flags.SnapshotFile("mobile"),
                Flags.Value("resource-id", "<id>", "Nodes whose resource-id matches."),
                Flags.Value("content-desc", "<text>", "Nodes whose content-desc (accessibility id) matches."),
                Flags.Value("class", "<class>", "Nodes whose class matches (e.g. android.widget.TextView)."),
                Flags.FindText, Flags.FindGrep, Flags.FindInteractive) with { Aliases = new[] { "query-snapshot" } },
            Verb("take-screenshot", "Screenshot the device screen.", TakeScreenshot, Flags.ScreenshotOutput, session, Flags.Out, Flags.ScreenshotInline),
            Verb("click", "Tap an element.", Tap, "{success}", session, locator) with { Aliases = new[] { "tap" } },
            Verb("type-text", "Type text into an element.", TypeText, "{success}", session, locator,
                Flags.Value("text", "<text>", "Text to type.", required: true)),
            Verb("clear", "Clear an element's text.", Clear, "{success}", session, locator),
            Verb("get-text", "Get an element's text.", GetText, "{text}", session, locator),
            Verb("get-attribute", "Get an element attribute.", GetAttribute, "{attribute, value}", session, locator,
                Flags.Value("attribute", "<name>", "Attribute name.", required: true)),
            Verb("is-visible", "Whether an element is displayed.", IsVisible, "{visible}", session, locator),
            Verb("is-enabled", "Whether an element is enabled.", IsEnabled, "{enabled}", session, locator),
            Verb("get-orientation", "Get the screen orientation.", GetOrientation, "{orientation}", session),
            Verb("set-orientation", "Set the screen orientation.", SetOrientation, "{success, orientation}", session,
                Flags.Value("orientation", "<orientation>", "New orientation.", required: true, allowed: new[] { "portrait", "landscape" })),
            Verb("swipe", "Swipe between two screen points.", Swipe, "{success}", session,
                Flags.Value("start-x", "<px>", "Start X.", required: true),
                Flags.Value("start-y", "<px>", "Start Y.", required: true),
                Flags.Value("end-x", "<px>", "End X.", required: true),
                Flags.Value("end-y", "<px>", "End Y.", required: true),
                Flags.Value("duration", "<ms>", "Swipe duration.", @default: "1000")),
            Verb("scroll-to-element", "Scroll until an element is visible.", ScrollToElement, "{success}", session, locator),
        };
    }

    public static Task<int> Dispatch(string verb, MobileCommandContext ctx)
        => CommandRegistry.Find("mobile", verb) is CommandSpec<MobileCommandContext> spec
            ? spec.Handler(ctx)
            : Task.FromResult(JsonOutput.Error("unknown_verb", $"Unknown verb 'mobile {verb}'."));

    // ===== Session lifecycle =====

    public static async Task<int> StartSession(MobileCommandContext ctx)
    {
        await Task.CompletedTask;
        var platform = ctx.RequireOpt("platform");
        var appPath = ctx.RequireOpt("app");
        var deviceName = ctx.Opt("device-name");
        var appiumUrl = ctx.Opt("appium-url") ?? MobileCommandContext.DefaultAppiumUrl;

        var (manager, _) = ctx.BuildEngine();
        var localId = manager.StartSession(platform, appPath, deviceName, null, appiumUrl);
        var realId = manager.GetDriver(localId).SessionId.ToString();
        var resolvedPlatform = MobileSessionManager.IsIOS(platform) ? "iOS" : "Android";

        ctx.Registry.Upsert(new SessionRecord
        {
            LogicalName = ctx.SessionName,
            SessionId = realId,
            DriverType = "Appium",
            ServerUrl = appiumUrl,
            LastActivityUtc = DateTime.UtcNow,
            Caps = MobileSessionManager.BuildCapsJson(platform),
        }, rejectDuplicate: true);

        return JsonOutput.Success(new { sessionId = realId, platform = resolvedPlatform, appPath });
    }

    public static async Task<int> CloseSession(MobileCommandContext ctx)
    {
        await Task.CompletedTask;
        var (manager, _) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        manager.CloseSession(realId);
        ctx.Registry.Remove(ctx.SessionName);
        return JsonOutput.Success(new { success = true, sessionId = realId });
    }

    public static async Task<int> ListSessions(MobileCommandContext ctx)
    {
        await Task.CompletedTask;
        var keepAlive = TimeSpan.FromMinutes(10);
        var now = DateTime.UtcNow;
        var live = new List<string>();

        foreach (var kv in ctx.Registry.ReadAll())
        {
            if (now - kv.Value.LastActivityUtc > keepAlive)
            {
                ctx.Registry.Remove(kv.Key);
            }
            else
            {
                live.Add(kv.Value.SessionId);
            }
        }

        return JsonOutput.Success(new { sessions = live, count = live.Count });
    }

    // ===== App operations =====

    public static async Task<int> GetContext(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var context = await service.GetContextAsync(realId);
        return JsonOutput.Success(new { context });
    }

    public static async Task<int> SetContext(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.SetContextAsync(realId, ctx.RequireOpt("context"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> GetContexts(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.GetContextsAsync(realId);
        return JsonOutput.Success(result);
    }

    public static async Task<int> BackgroundApp(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var seconds = int.Parse(ctx.RequireOpt("seconds"));
        var result = await service.BackgroundAppAsync(realId, seconds);
        return JsonOutput.Success(result);
    }

    public static async Task<int> CloseApp(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.CloseAppAsync(realId, ctx.RequireOpt("app-id"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> LaunchApp(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.LaunchAppAsync(realId, ctx.RequireOpt("app-id"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> InstallApp(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.InstallAppAsync(realId, ctx.RequireOpt("app-path"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> RemoveApp(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.RemoveAppAsync(realId, ctx.RequireOpt("app-id"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> IsAppInstalled(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var installed = await service.IsAppInstalledAsync(realId, ctx.RequireOpt("bundle-id"));
        return JsonOutput.Success(new { installed });
    }

    // ===== View information =====

    public static async Task<int> GetPageSource(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var pageSource = await service.GetPageSourceAsync(realId);
        return JsonOutput.Success(new { pageSource });
    }

    /// <summary>
    /// `bellatrix mobile snapshot` (alias `get-view-snapshot`). Default: capture + save, returning the
    /// compact reference `{path, elementCount, summary}`. `--inline` returns the full node array;
    /// `--grep`/`--interactive-only` filter the captured nodes in-process.
    /// </summary>
    public static async Task<int> Snapshot(MobileCommandContext ctx)
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
            var matches = MobileSnapshotQuery.Run(nodes, MobileSnapshotQueryMode.Grep, pattern);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "grep", value = pattern } });
        }

        if (ctx.Options.ContainsKey("interactive-only"))
        {
            var matches = MobileSnapshotQuery.Run(nodes, MobileSnapshotQueryMode.Interactive, null);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "interactive-only", value = (string?)null } });
        }

        var reference = new SnapshotStore().Save(realId, nodes, MobileSnapshotQuery.IsInteractive);
        return JsonOutput.Success(reference);
    }

    /// <summary>
    /// `bellatrix mobile find` (alias `query-snapshot`). Filters by `--resource-id`/`--content-desc`/
    /// `--text`/`--class` (also `--grep`/`--interactive-only`). Queries a persisted artifact when
    /// `--snapshot &lt;path&gt;` is given (driver-free); otherwise captures the live view first.
    /// </summary>
    public static async Task<int> Find(MobileCommandContext ctx)
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

        MobileSnapshotQueryMode mode;
        string? value = null;
        if (ctx.Options.ContainsKey("interactive-only")) { mode = MobileSnapshotQueryMode.Interactive; }
        else if (ctx.Opt("grep") is { } g) { mode = MobileSnapshotQueryMode.Grep; value = g; }
        else if (ctx.Opt("text") is { } t) { mode = MobileSnapshotQueryMode.Text; value = t; }
        else if (ctx.Opt("resource-id") is { } r) { mode = MobileSnapshotQueryMode.ResourceId; value = r; }
        else if (ctx.Opt("content-desc") is { } c) { mode = MobileSnapshotQueryMode.ContentDesc; value = c; }
        else if (ctx.Opt("class") is { } cl) { mode = MobileSnapshotQueryMode.Class; value = cl; }
        else
        {
            throw new CliException("usage",
                "Provide one of --resource-id, --content-desc, --class, --text, --grep, or --interactive-only.");
        }

        var results = MobileSnapshotQuery.Run(nodes, mode, value);
        return JsonOutput.Success(new { nodes = results, count = results.Count, query = new { mode = mode.ToString(), value } });
    }

    public static async Task<int> TakeScreenshot(MobileCommandContext ctx)
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

    public static async Task<int> Tap(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.TapElementAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> TypeText(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.TypeTextAsync(realId, ctx.LocatorType(), ctx.LocatorValue(), ctx.RequireOpt("text"));
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> Clear(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.ClearElementAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> GetText(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var text = await service.GetElementTextAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(new { text });
    }

    public static async Task<int> GetAttribute(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var attribute = ctx.RequireOpt("attribute");
        var value = await service.GetElementAttributeAsync(realId, ctx.LocatorType(), ctx.LocatorValue(), attribute);
        return JsonOutput.Success(new { attribute, value });
    }

    public static async Task<int> IsVisible(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var visible = await service.IsElementVisibleAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(new { visible });
    }

    public static async Task<int> IsEnabled(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var enabled = await service.IsElementEnabledAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(new { enabled });
    }

    // ===== Device operations =====

    public static async Task<int> GetOrientation(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var orientation = await service.GetOrientationAsync(realId);
        return JsonOutput.Success(new { orientation });
    }

    public static async Task<int> SetOrientation(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.SetOrientationAsync(realId, ctx.RequireOpt("orientation"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> Swipe(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.SwipeAsync(
            realId,
            int.Parse(ctx.RequireOpt("start-x")), int.Parse(ctx.RequireOpt("start-y")),
            int.Parse(ctx.RequireOpt("end-x")), int.Parse(ctx.RequireOpt("end-y")),
            ctx.Opt("duration") is { } d ? int.Parse(d) : 1000);
        return JsonOutput.Success(result);
    }

    public static async Task<int> ScrollToElement(MobileCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.ScrollToElementAsync(realId, ctx.LocatorType(), ctx.LocatorValue());
        return JsonOutput.Success(result);
    }
}
