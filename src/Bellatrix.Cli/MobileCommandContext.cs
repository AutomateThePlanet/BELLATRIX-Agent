using Bellatrix.Execution.Mobile;
using Bellatrix.Execution.SessionMounting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bellatrix.Cli;

/// <summary>
/// Per-invocation context for <c>bellatrix mobile &lt;verb&gt;</c>. Mirrors <see cref="CommandContext"/>
/// (desktop) but is typed to the mobile engine — duplicate-per-domain by design. Reuses the shared,
/// domain-neutral <see cref="SessionStore"/>. Holds no long-lived state.
/// </summary>
internal sealed class MobileCommandContext
{
    /// <summary>Default Appium server — the user owns its lifecycle; the CLI never starts it.</summary>
    public const string DefaultAppiumUrl = "http://127.0.0.1:4723";

    public IReadOnlyDictionary<string, string> Options { get; }
    public string SessionName { get; }
    public SessionStore Registry { get; }

    public MobileCommandContext(Dictionary<string, string> options)
    {
        Options = options;
        SessionName = options.TryGetValue("session", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "default";
        Registry = new SessionStore(domain: "mobile");
    }

    public bool Json => Options.ContainsKey("json");

    public string? Opt(string key) => Options.TryGetValue(key, out var v) ? v : null;

    public string RequireOpt(string key)
        => Opt(key) ?? throw new CliException("usage", $"Missing required option --{key}.");

    /// <summary>The element locator value: the unified <c>--locator</c> (shared by all three domains) or <c>--locator-value</c>.</summary>
    public string LocatorValue()
        => Opt("locator") ?? Opt("locator-value")
           ?? throw new CliException("usage", "Missing required option --locator <value> (or --locator-value).");

    /// <summary>
    /// The locator strategy: <c>--strategy</c> (unified) or <c>--locator-type</c>; when neither is given, <c>xpath</c>
    /// for a value starting with <c>/</c> or <c>(/</c>, else <c>accessibilityid</c> (the most stable mobile strategy).
    /// </summary>
    public string LocatorType()
        => Opt("strategy") ?? Opt("locator-type")
           ?? (WebCommandContext.LooksLikeXPath(Opt("locator") ?? Opt("locator-value")) ? "xpath" : "accessibilityid");

    /// <summary>Builds the mobile engine for this invocation (no watchdog — daemon-less CLI).</summary>
    public (MobileSessionManager Manager, MobileService Service) BuildEngine()
    {
        var manager = new MobileSessionManager(NullLogger<MobileSessionManager>.Instance);
        var service = new MobileService(manager, NullLogger<MobileService>.Instance);
        return (manager, service);
    }

    /// <summary>
    /// Resolves the selected session from the registry and mounts onto its live Appium session by id
    /// (attach-by-id). Returns the real Appium session id. Throws <c>session_not_found</c> when
    /// the name is unknown.
    /// </summary>
    public string MountSession(MobileSessionManager manager)
    {
        var record = Registry.TryGet(SessionName)
            ?? throw new CliException("session_not_found",
                $"No session named '{SessionName}'. Start one with: bellatrix mobile start-session --platform <android|ios> --app <path> --session {SessionName}");

        manager.MountExisting(record.SessionId, record.ServerUrl, TimeSpan.FromMinutes(10), record.Caps);
        Registry.Touch(SessionName, DateTime.UtcNow);
        return record.SessionId;
    }
}
