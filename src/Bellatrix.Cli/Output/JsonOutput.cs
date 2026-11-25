using System.Text.Json;

namespace Bellatrix.Cli.Output;

/// <summary>
/// Machine-readable output for every command. Success payloads and the error envelope
/// `{ "error": { "code", "message" } }` are written to stdout; exit codes signal failure to the
/// host shell. The emitted JSON shape is the consumer contract.
/// </summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Serializes a success payload to stdout and returns exit code 0.</summary>
    public static int Success(object payload)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(payload, Options));
        return 0;
    }

    /// <summary>
    /// Serializes the error envelope to stdout and returns a non-zero exit code. Use
    /// <c>driver_unreachable</c> with the "start your driver at &lt;ServerUrl&gt;" message on
    /// connection failure and <c>session_not_found</c> for unknown/closed sessions.
    /// </summary>
    public static int Error(string code, string message, int exitCode = 1)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new { error = new { code, message } }, Options));
        return exitCode;
    }

    /// <summary>The error envelope with a <c>details</c> object: <c>{ "error": { "code", "message", "details" } }</c>.</summary>
    public static int Error(string code, string message, object details, int exitCode = 1)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new { error = new { code, message, details } }, Options));
        return exitCode;
    }
}
