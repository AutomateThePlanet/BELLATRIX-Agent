using Bellatrix.Execution.SessionMounting;
using Bellatrix.Execution.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bellatrix.Cli;

/// <summary>
/// Per-invocation context for <c>bellatrix web &lt;verb&gt;</c>. Mirrors <see cref="CommandContext"/>
/// (desktop) but is typed to the web engine — duplicate-per-domain by design. Reuses the shared,
/// domain-neutral <see cref="SessionStore"/>. Holds no long-lived state.
/// </summary>
internal sealed class WebCommandContext
{
    public IReadOnlyDictionary<string, string> Options { get; }
    public string SessionName { get; }
    public SessionStore Registry { get; }

    public WebCommandContext(Dictionary<string, string> options)
    {
        Options = options;
        SessionName = options.TryGetValue("session", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "default";
        Registry = new SessionStore(domain: "web");
    }

    public bool Json => Options.ContainsKey("json");

    public string? Opt(string key) => Options.TryGetValue(key, out var v) ? v : null;

    public string RequireOpt(string key)
        => Opt(key) ?? throw new CliException("usage", $"Missing required option --{key}.");

    /// <summary>
    /// The selector strategy: explicit <c>--strategy</c>, else <c>xpath</c> when the locator looks like an XPath
    /// (<c>/…</c> or <c>(/…</c>), else CSS (the Selenium service default).
    /// </summary>
    public string Strategy => Opt("strategy") ?? (LooksLikeXPath(Opt("locator") ?? Opt("selector")) ? "xpath" : "css");

    /// <summary>The element locator: the unified <c>--locator</c> (shared by all three domains) or <c>--selector</c>.</summary>
    public string Locator()
        => Opt("locator") ?? Opt("selector")
           ?? throw new CliException("usage", "Missing required option --locator <selector> (or --selector).");

    internal static bool LooksLikeXPath(string? locator)
        => locator is not null && (locator.StartsWith('/') || locator.StartsWith("(/", StringComparison.Ordinal));

    public (WebDriverSessionManager Manager, WebDriverService Service) BuildEngine()
    {
        var manager = new WebDriverSessionManager(NullLogger<WebDriverSessionManager>.Instance);
        var service = new WebDriverService(manager, NullLogger<WebDriverService>.Instance);
        return (manager, service);
    }

    /// <summary>
    /// Resolves the selected session from the registry and mounts onto its live Selenium session by id
    /// (attach-by-id). Returns the real Selenium session id. Cross-process reattach requires the
    /// session to live on a persistent Selenium server (see WebDriverSessionManager).
    /// </summary>
    public string MountSession(WebDriverSessionManager manager)
    {
        var record = Registry.TryGet(SessionName)
            ?? throw new CliException("session_not_found",
                $"No session named '{SessionName}'. Start one with: bellatrix web start-session --browser <chrome|firefox|edge> --session {SessionName}");

        manager.MountExisting(record.SessionId, record.ServerUrl, TimeSpan.FromMinutes(10), record.Caps);
        Registry.Touch(SessionName, DateTime.UtcNow);
        return record.SessionId;
    }
}
