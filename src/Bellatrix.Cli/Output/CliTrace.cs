using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bellatrix.Cli.Output;

/// <summary>
/// Opt-in invocation tracing. When <c>BELLATRIX_TRACE=&lt;file&gt;</c> is set, every CLI invocation
/// appends ONE JSON line to that file: timestamp, domain, verb, args (<c>--text</c> values redacted), exit code,
/// bytes written to stdout, and whether the call was "context-heavy" (<c>snapshot --inline</c>,
/// <c>get-page-source</c>, or more than 8 KB of stdout). The agent-conformance harness reads it to prove that an
/// agent delegated locator work instead of pulling a UI tree into its context.
/// With the variable unset the runner is called directly — no writer swap, no behavior change. A failure to
/// write the trace is swallowed: tracing never changes stdout or the exit code.
/// </summary>
public static class CliTrace
{
    public const string EnvironmentVariable = "BELLATRIX_TRACE";

    /// <summary>Stdout above this many bytes marks the call context-heavy.</summary>
    public const int HeavyStdoutBytes = 8 * 1024;

    // A local diagnostics file, not HTML: keep '+' in timestamps and quotes in XPath args readable.
    private static readonly JsonSerializerOptions LineOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase) { "desktop", "web", "mobile" };

    public static int Run(string[] args, Func<string[], int> runner, string? tracePath)
    {
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return runner(args);
        }

        var original = Console.Out;
        var counting = new CountingTextWriter(original);
        Console.SetOut(counting);
        int exitCode;
        try
        {
            exitCode = runner(args);
        }
        finally
        {
            counting.Flush();
            Console.SetOut(original);
        }

        TryAppend(tracePath, BuildLine(args, exitCode, counting.BytesWritten, DateTimeOffset.UtcNow));
        return exitCode;
    }

    /// <summary>Builds the trace record for one invocation (pure; unit-tested).</summary>
    internal static string BuildLine(string[] args, int exitCode, long stdoutBytes, DateTimeOffset timestamp)
    {
        string? domain = null;
        string? verb = null;
        if (args.Length > 0 && Domains.Contains(args[0]))
        {
            domain = args[0].ToLowerInvariant();
            verb = args.Length > 1 ? args[1].ToLowerInvariant() : null;
        }
        else if (args.Length > 0)
        {
            verb = args[0].ToLowerInvariant();
        }

        var heavyReasons = new List<string>();
        var hasInline = args.Skip(1).Any(a => string.Equals(a, "--inline", StringComparison.OrdinalIgnoreCase));
        if (domain is not null && verb is "snapshot" or "get-view-snapshot" && hasInline)
        {
            heavyReasons.Add("snapshot-inline");
        }

        if (domain is not null && verb == "take-screenshot" && hasInline)
        {
            heavyReasons.Add("screenshot-inline");
        }

        if (domain is not null && verb == "get-page-source")
        {
            heavyReasons.Add("get-page-source");
        }

        if (stdoutBytes > HeavyStdoutBytes)
        {
            heavyReasons.Add("stdout>8KB");
        }

        var record = new
        {
            ts = timestamp.ToString("o"),
            version = CliRunner.Version,
            domain,
            verb,
            args = Redact(args),
            exitCode,
            stdoutBytes,
            contextHeavy = heavyReasons.Count > 0,
            heavyReasons,
            cwd = Directory.GetCurrentDirectory(),
        };

        return JsonSerializer.Serialize(record, LineOptions);
    }

    /// <summary>Replaces the value that follows every <c>--text</c> with <c>***</c>.</summary>
    internal static string[] Redact(string[] args)
    {
        var copy = (string[])args.Clone();
        for (var i = 0; i < copy.Length - 1; i++)
        {
            if (string.Equals(copy[i], "--text", StringComparison.OrdinalIgnoreCase)
                && !copy[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                copy[i + 1] = "***";
            }
        }

        return copy;
    }

    private static void TryAppend(string path, string line)
    {
        // Agents may run several bellatrix processes at once: append-mode, shared handle, one write per line,
        // and a short retry when another process holds the file.
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(bytes, 0, bytes.Length);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(20 * (attempt + 1));
            }
            catch (Exception)
            {
                return; // unwritable path, bad name, no permission — tracing is best-effort
            }
        }
    }

    /// <summary>Forwards to the real stdout and counts the UTF-8 bytes written.</summary>
    private sealed class CountingTextWriter : TextWriter
    {
        private readonly TextWriter _inner;

        public CountingTextWriter(TextWriter inner) => _inner = inner;

        public long BytesWritten { get; private set; }

        public override Encoding Encoding => _inner.Encoding;

        public override void Write(char value)
        {
            BytesWritten += Encoding.UTF8.GetByteCount(new[] { value });
            _inner.Write(value);
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            BytesWritten += Encoding.UTF8.GetByteCount(value);
            _inner.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            BytesWritten += Encoding.UTF8.GetByteCount(buffer, index, count);
            _inner.Write(buffer, index, count);
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write(CoreNewLine, 0, CoreNewLine.Length);
        }

        public override void Flush() => _inner.Flush();
    }
}
