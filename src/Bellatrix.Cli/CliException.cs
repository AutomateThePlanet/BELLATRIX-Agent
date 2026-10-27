namespace Bellatrix.Cli;

/// <summary>
/// A command failure carrying a stable error <see cref="Code"/> and human message. Thrown by command
/// handlers and rendered by <see cref="CliRunner"/> as the `{ "error": { code, message } }` envelope.
/// </summary>
public sealed class CliException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }

    public CliException(string code, string message, int exitCode = 1) : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }
}
