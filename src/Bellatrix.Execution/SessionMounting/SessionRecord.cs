namespace Bellatrix.Execution.SessionMounting;

/// <summary>
/// One persisted session record: maps a caller-supplied logical name to a live driver-server
/// session that a fresh, stateless CLI process can re-mount by id. The last
/// window title is retained for splash recovery only, never as the reconnection anchor.
/// Persisted by <see cref="SessionStore"/>.
/// </summary>
public sealed class SessionRecord
{
    /// <summary>Owning domain (<c>desktop</c>, <c>web</c>, <c>mobile</c>); part of the store key with the logical name.</summary>
    public string Domain { get; set; } = "desktop";
    public string LogicalName { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DriverType { get; set; } = "WinAppDriver";
    public string ServerUrl { get; set; } = "http://127.0.0.1:4723";
    public DateTime LastActivityUtc { get; set; }
    public string? LastWindowTitle { get; set; }

    // Cached capabilities replayed at mount, plus parallel-routing hints
    // (depends_on/priority/ordinal). Optional and defaulted so older records round-trip cleanly.
    public string? Caps { get; set; }
    public string? DependsOn { get; set; }
    public int Priority { get; set; }
    public int Ordinal { get; set; }
}
