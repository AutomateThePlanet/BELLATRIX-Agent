using Bellatrix.Cli.Output;
using Bellatrix.Execution.SessionMounting;
using Bellatrix.Execution.Snapshots;
using Bellatrix.Execution.Web;
using OpenQA.Selenium.Remote;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// Thin <c>bellatrix web &lt;verb&gt;</c> handlers: shape arguments → call the web engine → print the
/// JSON contract. No model, no credentials. Session-bearing verbs mount the selected
/// session by id (attach-by-id) before acting; <c>start-session</c> persists the real Selenium session id
/// and server URL. Mirrors <see cref="DesktopCommands"/> — duplicate-per-domain by design.
/// </summary>
internal static class WebCommands
{
    /// <summary>The web verb surface (Selenium): routing, help and drift tests read this.</summary>
    public static readonly IReadOnlyList<CommandSpec<WebCommandContext>> Specs = BuildSpecs();

    /// <summary>Every routable web verb (canonical names + aliases), derived from <see cref="Specs"/>.</summary>
    public static IReadOnlySet<string> KnownVerbs => CommandRegistry.RoutedNames("web");

    private static CommandSpec<WebCommandContext>[] BuildSpecs()
    {
        var session = Flags.Session;
        var locator = Flags.WebLocator;
        CommandSpec<WebCommandContext> Verb(string name, string summary, Func<WebCommandContext, Task<int>> handler, string output, params object[] flags)
            => new("web", name, summary, handler) { Flags = Flags.Of(flags), Output = output };

        return new[]
        {
            Verb("start-session", "Start a browser session (the CLI starts a local driver, or uses --remote-url) and persist it.", StartSession,
                "{sessionId, browser, serverUrl, driverServer: local|remote}", session,
                Flags.Value("browser", "<browser>", "Browser to start.", @default: "chrome", allowed: new[] { "chrome", "firefox", "edge" }),
                Flags.Switch("headless", "Run the browser headless."),
                Flags.Value("width", "<px>", "Window width.", @default: "1920"),
                Flags.Value("height", "<px>", "Window height.", @default: "1080"),
                Flags.Value("remote-url", "<url>", "A Selenium server you run (e.g. http://localhost:4444); default: a local driver the CLI starts.")),
            Verb("close-session", "Close the browser; a local driver started by start-session is stopped too.", CloseSession,
                "{success, sessionId, driverStopped}", session),
            Verb("list-sessions", "List the live web sessions (stale rows of this domain are pruned).", ListSessions, "{sessions[], count}"),
            Verb("navigate", "Open a URL.", Navigate, "{success, url, title}", session,
                Flags.Value("url", "<url>", "Absolute URL (http:// or https://).", required: true)),
            Verb("back", "Browser back.", Back, "{success, url}", session),
            Verb("forward", "Browser forward.", Forward, "{success, url}", session),
            Verb("refresh", "Reload the page.", Refresh, "{success, url}", session),
            Verb("get-title", "Get the page title.", GetTitle, "{title}", session),
            Verb("get-url", "Get the current URL.", GetUrl, "{url}", session),
            Verb("get-page-source", "Return the raw HTML (context-heavy; prefer snapshot + find).", GetPageSource, "{pageSource}", session),
            Verb("snapshot", "Capture the page's element tree to a file and return only a compact reference.", Snapshot, Flags.SnapshotOutput,
                session, Flags.SnapshotCapture) with { Aliases = new[] { "get-view-snapshot" } },
            Verb("find", "Query a saved snapshot (--snapshot) or the live page for a handful of matching nodes.", Find, Flags.FindOutput,
                session, Flags.SnapshotFile("web"),
                Flags.Value("data-testid", "<value>", "Nodes whose data-testid matches."),
                Flags.Value("id", "<id>", "Nodes whose id matches."),
                Flags.Value("role", "<role>", "Nodes whose ARIA role matches."),
                Flags.Value("name", "<name>", "Nodes whose accessible name / name attribute matches."),
                Flags.FindText, Flags.FindGrep, Flags.FindInteractive) with { Aliases = new[] { "query-snapshot" } },
            Verb("click", "Click an element.", Click, "{success}", session, locator),
            Verb("type-text", "Type text into an element.", TypeText, "{success}", session, locator,
                Flags.Value("text", "<text>", "Text to type.", required: true)),
            Verb("clear", "Clear an input.", Clear, "{success}", session, locator),
            Verb("select", "Choose an option of a <select> by value.", Select, "{success}", session, locator,
                Flags.Value("value", "<value>", "Option value.", required: true)),
            Verb("get-text", "Get an element's text.", GetText, "{text}", session, locator),
            Verb("get-attribute", "Get an element attribute.", GetAttribute, "{attribute, value}", session, locator,
                Flags.Value("attribute", "<name>", "Attribute name.", required: true)),
            Verb("is-visible", "Whether an element is displayed.", IsVisible, "{visible}", session, locator),
            Verb("is-enabled", "Whether an element is enabled.", IsEnabled, "{enabled}", session, locator),
            Verb("wait-for-element", "Wait until an element exists.", WaitForElement, "{success, found[, error]}", session, locator,
                Flags.Value("timeout-ms", "<ms>", "Maximum wait.", @default: "30000")),
            Verb("wait-for-page-ready", "Wait until document.readyState is complete.", WaitForPageReady, "{success}", session),
            Verb("execute-js", "Run JavaScript in the page and return its result.", ExecuteJs, "{result}", session,
                Flags.Value("script", "<js>", "Script body (use return to produce a value).", required: true)),
            Verb("take-screenshot", "Screenshot the viewport.", TakeScreenshot, Flags.ScreenshotOutput, session, Flags.Out, Flags.ScreenshotInline),
        };
    }

    public static Task<int> Dispatch(string verb, WebCommandContext ctx)
        => CommandRegistry.Find("web", verb) is CommandSpec<WebCommandContext> spec
            ? spec.Handler(ctx)
            : Task.FromResult(JsonOutput.Error("unknown_verb", $"Unknown verb 'web {verb}'."));

    // ===== Session lifecycle =====

    public static async Task<int> StartSession(WebCommandContext ctx)
    {
        await Task.CompletedTask;
        var browser = ctx.Opt("browser") ?? "chrome";
        var headless = ctx.Options.ContainsKey("headless");
        var width = ctx.Opt("width") is { } w ? int.Parse(w) : 1920;
        var height = ctx.Opt("height") is { } h ? int.Parse(h) : 1080;
        var remoteUrl = ctx.Opt("remote-url");

        var (manager, _) = ctx.BuildEngine();
        var localId = manager.StartSession(browser, headless, width, height, remoteUrl);
        var realId = ((RemoteWebDriver)manager.GetDriver(localId)).SessionId.ToString();
        var serverUrl = manager.GetServerUrl(localId);
        var driverPid = manager.GetDriverServerPid(localId);

        try
        {
            ctx.Registry.Upsert(new SessionRecord
            {
                LogicalName = ctx.SessionName,
                SessionId = realId,
                DriverType = "Selenium",
                ServerUrl = serverUrl,
                LastActivityUtc = DateTime.UtcNow,
                Caps = WebDriverSessionManager.BuildCapsJson(browser, driverPid),
            }, rejectDuplicate: true);
        }
        catch
        {
            // Duplicate name (or store failure): don't leave an unreachable browser + driver behind.
            manager.CloseSession(localId);
            WebDriverSessionManager.StopDriverServer(driverPid);
            throw;
        }

        // driverServer: "local" = a detached driver process this CLI started (stopped by close-session);
        // "remote" = the --remote-url server, which is yours to run.
        return JsonOutput.Success(new { sessionId = realId, browser, serverUrl, driverServer = driverPid is null ? "remote" : "local" });
    }

    public static async Task<int> CloseSession(WebCommandContext ctx)
    {
        await Task.CompletedTask;
        var (manager, _) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var driverPid = WebDriverSessionManager.ReadDriverPid(ctx.Registry.TryGet(ctx.SessionName)?.Caps);
        manager.CloseSession(realId);
        var driverStopped = WebDriverSessionManager.StopDriverServer(driverPid);
        ctx.Registry.Remove(ctx.SessionName);
        return JsonOutput.Success(new { success = true, sessionId = realId, driverStopped });
    }

    public static async Task<int> ListSessions(WebCommandContext ctx)
    {
        await Task.CompletedTask;
        var keepAlive = TimeSpan.FromMinutes(10);
        var now = DateTime.UtcNow;
        var live = new List<string>();

        foreach (var kv in ctx.Registry.ReadAll())
        {
            if (now - kv.Value.LastActivityUtc > keepAlive)
            {
                // A forgotten record can never be re-attached by name: stop its detached driver so it can't leak.
                WebDriverSessionManager.StopDriverServer(WebDriverSessionManager.ReadDriverPid(kv.Value.Caps));
                ctx.Registry.Remove(kv.Key);
            }
            else
            {
                live.Add(kv.Value.SessionId);
            }
        }

        return JsonOutput.Success(new { sessions = live, count = live.Count });
    }

    // ===== Navigation =====

    public static async Task<int> Navigate(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.NavigateAsync(realId, ctx.RequireOpt("url"));
        return JsonOutput.Success(result);
    }

    public static async Task<int> Back(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        return JsonOutput.Success(await service.GoBackAsync(realId));
    }

    public static async Task<int> Forward(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        return JsonOutput.Success(await service.GoForwardAsync(realId));
    }

    public static async Task<int> Refresh(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        return JsonOutput.Success(await service.RefreshAsync(realId));
    }

    // ===== Page information =====

    public static async Task<int> GetTitle(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var title = await service.GetTitleAsync(realId);
        return JsonOutput.Success(new { title });
    }

    public static async Task<int> GetUrl(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var url = await service.GetUrlAsync(realId);
        return JsonOutput.Success(new { url });
    }

    public static async Task<int> GetPageSource(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var pageSource = await service.GetPageSourceAsync(realId);
        return JsonOutput.Success(new { pageSource });
    }

    /// <summary>
    /// `bellatrix web snapshot` (alias `get-view-snapshot`). Default: capture + save, returning the
    /// compact reference; `--inline` returns the node array; `--grep`/`--interactive-only` filter in-process.
    /// </summary>
    public static async Task<int> Snapshot(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var nodes = await service.GetPageSnapshotAsync(realId);

        if (ctx.Options.ContainsKey("inline"))
        {
            return JsonOutput.Success(nodes);
        }

        if (ctx.Opt("grep") is { } pattern)
        {
            var matches = WebSnapshotQuery.Run(nodes, WebSnapshotQueryMode.Grep, pattern);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "grep", value = pattern } });
        }

        if (ctx.Options.ContainsKey("interactive-only"))
        {
            var matches = WebSnapshotQuery.Run(nodes, WebSnapshotQueryMode.Interactive, null);
            return JsonOutput.Success(new { nodes = matches, count = matches.Count, query = new { mode = "interactive-only", value = (string?)null } });
        }

        var reference = new SnapshotStore().Save(realId, nodes, WebSnapshotQuery.IsInteractive);
        return JsonOutput.Success(reference);
    }

    /// <summary>
    /// `bellatrix web find` (alias `query-snapshot`). Filters by `--data-testid`/`--id`/`--role`/`--name`/
    /// `--text` (also `--grep`/`--interactive-only`). Queries a persisted artifact when `--snapshot &lt;path&gt;`
    /// is given (driver-free); otherwise captures the live view first.
    /// </summary>
    public static async Task<int> Find(WebCommandContext ctx)
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
            nodes = await service.GetPageSnapshotAsync(realId);
        }

        WebSnapshotQueryMode mode;
        string? value = null;
        if (ctx.Options.ContainsKey("interactive-only")) { mode = WebSnapshotQueryMode.Interactive; }
        else if (ctx.Opt("grep") is { } g) { mode = WebSnapshotQueryMode.Grep; value = g; }
        else if (ctx.Opt("text") is { } t) { mode = WebSnapshotQueryMode.Text; value = t; }
        else if (ctx.Opt("data-testid") is { } d) { mode = WebSnapshotQueryMode.DataTestid; value = d; }
        else if (ctx.Opt("id") is { } i) { mode = WebSnapshotQueryMode.Id; value = i; }
        else if (ctx.Opt("role") is { } r) { mode = WebSnapshotQueryMode.Role; value = r; }
        else if (ctx.Opt("name") is { } n) { mode = WebSnapshotQueryMode.Name; value = n; }
        else
        {
            throw new CliException("usage",
                "Provide one of --data-testid, --id, --role, --name, --text, --grep, or --interactive-only.");
        }

        var results = WebSnapshotQuery.Run(nodes, mode, value);
        return JsonOutput.Success(new { nodes = results, count = results.Count, query = new { mode = mode.ToString(), value } });
    }

    // ===== Element interaction & inspection =====

    public static async Task<int> Click(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.ClickElementAsync(realId, ctx.Locator(), ctx.Strategy);
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> TypeText(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.TypeTextAsync(realId, ctx.Locator(), ctx.RequireOpt("text"), ctx.Strategy);
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> Clear(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.ClearElementAsync(realId, ctx.Locator(), ctx.Strategy);
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> Select(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        await service.SelectOptionAsync(realId, ctx.Locator(), ctx.RequireOpt("value"), ctx.Strategy);
        return JsonOutput.Success(new { success = true });
    }

    public static async Task<int> GetText(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var text = await service.GetElementTextAsync(realId, ctx.Locator(), ctx.Strategy);
        return JsonOutput.Success(new { text });
    }

    public static async Task<int> GetAttribute(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var attribute = ctx.RequireOpt("attribute");
        var value = await service.GetElementAttributeAsync(realId, ctx.Locator(), attribute, ctx.Strategy);
        return JsonOutput.Success(new { attribute, value });
    }

    public static async Task<int> IsVisible(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var visible = await service.IsElementVisibleAsync(realId, ctx.Locator(), ctx.Strategy);
        return JsonOutput.Success(new { visible });
    }

    public static async Task<int> IsEnabled(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var enabled = await service.IsElementEnabledAsync(realId, ctx.Locator(), ctx.Strategy);
        return JsonOutput.Success(new { enabled });
    }

    // ===== Waits, JS, screenshot =====

    public static async Task<int> WaitForElement(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var timeoutMs = ctx.Opt("timeout-ms") is { } t ? int.Parse(t) : 30000;
        var result = await service.WaitForElementAsync(realId, ctx.Locator(), timeoutMs, ctx.Strategy);
        return JsonOutput.Success(result);
    }

    public static async Task<int> WaitForPageReady(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        return JsonOutput.Success(await service.WaitForPageReadyAsync(realId));
    }

    public static async Task<int> ExecuteJs(WebCommandContext ctx)
    {
        var (manager, service) = ctx.BuildEngine();
        var realId = ctx.MountSession(manager);
        var result = await service.ExecuteJavaScriptAsync(realId, ctx.RequireOpt("script"));
        return JsonOutput.Success(new { result });
    }

    public static async Task<int> TakeScreenshot(WebCommandContext ctx)
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
}
