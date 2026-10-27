using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Remote;

namespace Bellatrix.Execution.SessionMounting;

/// <summary>
/// The core of attach-by-id: a command executor that suppresses the W3C
/// <c>NewSession</c> handshake and instead binds the driver to a <em>pre-existing</em> server
/// session id read from the <see cref="SessionStore"/>. Every other command is forwarded to
/// the real server at <c>/session/{id}/...</c>. This lets a fresh, daemon-less CLI process mount
/// onto a session the driver server still holds, rather than creating a new one.
/// </summary>
public sealed class AttachExistingSessionExecutor : HttpCommandExecutor
{
    private readonly string _sessionId;
    private readonly IReadOnlyDictionary<string, object>? _caps;

    public AttachExistingSessionExecutor(
        Uri serverUri, string sessionId, TimeSpan commandTimeout, IReadOnlyDictionary<string, object>? caps = null)
        : base(serverUri, commandTimeout)
    {
        _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _caps = caps;
    }

    /// <summary>The existing server session id this executor mounts onto.</summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// Pure decision: is this the session-creation command that must be intercepted rather than
    /// sent to the server? Exposed for unit testing without a live driver.
    /// </summary>
    public static bool IsNewSessionCommand(string? commandName)
        => string.Equals(commandName, DriverCommand.NewSession, StringComparison.Ordinal);

    /// <summary>
    /// Builds the synthetic <c>NewSession</c> response that reports the already-open session id, so
    /// the constructed driver believes it created a session but is in fact bound to the existing one.
    /// Replays the cached capabilities from the original session when available, so the
    /// driver's <c>Capabilities</c> reflect the live session; falls back to an empty bag otherwise
    /// (the verified default behavior).
    /// </summary>
    public Response BuildAttachResponse()
        => new()
        {
            SessionId = _sessionId,
            Value = _caps is null ? new Dictionary<string, object>() : new Dictionary<string, object>(_caps),
        };

    public override Response Execute(Command commandToExecute)
    {
        if (commandToExecute is not null && IsNewSessionCommand(commandToExecute.Name))
        {
            return BuildAttachResponse();
        }

        return base.Execute(commandToExecute);
    }

    /// <summary>
    /// <see cref="AppiumDriver"/>'s session-start path calls the async overload, not <see cref="Execute"/>
    /// — confirmed by tracing which override the .NET client actually invokes when constructing a driver
    /// (live-validated). Without this override, NewSession would still be sent to the real server despite
    /// <see cref="Execute"/> being intercepted, defeating attach-by-id.
    /// </summary>
    public override Task<Response> ExecuteAsync(Command commandToExecute)
    {
        if (commandToExecute is not null && IsNewSessionCommand(commandToExecute.Name))
        {
            return Task.FromResult(BuildAttachResponse());
        }

        return base.ExecuteAsync(commandToExecute);
    }
}
