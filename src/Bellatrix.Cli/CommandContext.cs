using Bellatrix.Execution;
using Bellatrix.Execution.SessionMounting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bellatrix.Cli;

/// <summary>
/// Per-invocation context shared by command handlers: parsed options, resolved settings, the on-disk
/// session registry, and helpers to build the engine and mount the selected session by id. Holds no
/// long-lived state — a fresh instance exists only for the lifetime of one CLI process.
/// </summary>
internal sealed class CommandContext
{
    public IReadOnlyDictionary<string, string> Options { get; }
    public string SessionName { get; }
    public DesktopMcpSettings Settings { get; }
    private SessionStore? _registry;

    /// <summary>
    /// The desktop session store, opened on first use: top-level commands (init, help, version) never touch it, so
    /// they no longer create <c>.bellatrix/sessions.db</c> as a side effect.
    /// </summary>
    public SessionStore Registry => _registry ??= new SessionStore(domain: "desktop");

    public CommandContext(Dictionary<string, string> options)
    {
        Options = options;
        SessionName = options.TryGetValue("session", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "default";
        Settings = DesktopSettingsLoader.Load();
    }

    // Output is ALWAYS JSON — the success/error JSON is the contract. `--json` is accepted for
    // compatibility (and as a self-documenting hint) but never changes behavior. `--no-interaction` only
    // matters to init (the one command that may prompt, and only in a terminal). Both pass through
    // ParseOptions harmlessly and never error.
    public bool Json => Options.ContainsKey("json");

    public string? Opt(string key) => Options.TryGetValue(key, out var v) ? v : null;

    public string RequireOpt(string key)
        => Opt(key) ?? throw new CliException("usage", $"Missing required option --{key}.");

    /// <summary>
    /// The element locator: the unified <c>--locator &lt;xpath&gt;</c> (shared by all three domains) or the
    /// original <c>--xpath</c>. Desktop locators are XPath only, so any other <c>--strategy</c> is a usage error.
    /// </summary>
    public string Locator()
    {
        if (Opt("strategy") is { } strategy && !strategy.Equals("xpath", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException("usage", $"Desktop locators are XPath only; --strategy '{strategy}' is not supported.");
        }

        return Opt("locator") ?? Opt("xpath")
            ?? throw new CliException("usage", "Missing required option --locator <xpath> (or --xpath).");
    }

    /// <summary>
    /// Builds the engine for this invocation with the in-process watchdog DISABLED (no daemon to host
    /// it). Idle cleanup is handled by the driver's newCommandTimeout plus registry pruning.
    /// </summary>
    public (DesktopSessionManager Manager, DesktopService Service) BuildEngine()
    {
        Settings.SessionTimeoutMinutes = 0; // disable the background watchdog in the CLI
        var manager = new DesktopSessionManager(NullLogger<DesktopSessionManager>.Instance, Settings);
        var service = new DesktopService(manager, NullLogger<DesktopService>.Instance);
        return (manager, service);
    }

    /// <summary>
    /// Resolves the selected session from the registry and mounts onto its live server session by id
    /// (attach-by-id). Returns the real W3C session id to pass to <see cref="DesktopService"/>.
    /// Throws <c>session_not_found</c> when the name is unknown.
    /// </summary>
    public string MountSession(DesktopSessionManager manager)
    {
        var record = Registry.TryGet(SessionName)
            ?? throw new CliException("session_not_found",
                $"No session named '{SessionName}'. Start one with: bellatrix desktop start-session --app <name> --session {SessionName}");

        var timeout = TimeSpan.FromMinutes(Settings.NewCommandTimeoutMinutes ?? 10);
        manager.MountExisting(record.SessionId, record.ServerUrl, record.DriverType, timeout, record.Caps);
        Registry.Touch(SessionName, DateTime.UtcNow);
        return record.SessionId;
    }
}
