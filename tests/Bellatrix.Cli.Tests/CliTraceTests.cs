using System.Text;
using System.Text.Json;
using Bellatrix.Cli;
using Bellatrix.Cli.Output;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// The opt-in <c>BELLATRIX_TRACE</c> invocation log. Pins the line format, <c>--text</c> redaction,
/// the context-heavy rules, the stdout byte count, append semantics, and "no behavior change when unset".
/// </summary>
public class CliTraceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bellatrix-trace-" + Guid.NewGuid().ToString("N"));

    public CliTraceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string TracePath => Path.Combine(_dir, "trace.jsonl");

    private static (int exit, string stdout) Capture(Func<int> run)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exit = run();
            return (exit, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private JsonElement[] ReadLines()
        => File.ReadAllLines(TracePath).Where(l => l.Length > 0)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToArray();

    [Fact]
    public void Unset_CallsRunnerDirectly_WithoutSwappingStdout()
    {
        var original = Console.Out;
        TextWriter? seen = null;
        var exit = CliTrace.Run(new[] { "desktop", "get-title" }, _ => { seen = Console.Out; return 7; }, tracePath: null);

        Assert.Equal(7, exit);
        Assert.Same(original, seen);
        Assert.False(File.Exists(TracePath));

        CliTrace.Run(Array.Empty<string>(), _ => 0, tracePath: "   ");
        Assert.False(File.Exists(TracePath));
    }

    [Fact]
    public void Set_AppendsOneLine_WithTheDocumentedShape()
    {
        var (exit, stdout) = Capture(() => CliTrace.Run(new[] { "--version" }, CliRunner.Run, TracePath));

        Assert.Equal(0, exit);
        var line = Assert.Single(ReadLines());
        Assert.Equal(JsonValueKind.String, line.GetProperty("ts").ValueKind);
        Assert.True(DateTimeOffset.TryParse(line.GetProperty("ts").GetString(), out _));
        Assert.Equal(CliRunner.Version, line.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("domain").ValueKind);
        Assert.Equal("--version", line.GetProperty("verb").GetString());
        Assert.Equal("--version", line.GetProperty("args")[0].GetString());
        Assert.Equal(0, line.GetProperty("exitCode").GetInt32());
        Assert.Equal(Encoding.UTF8.GetByteCount(stdout), line.GetProperty("stdoutBytes").GetInt64());
        Assert.False(line.GetProperty("contextHeavy").GetBoolean());
        Assert.Equal(0, line.GetProperty("heavyReasons").GetArrayLength());
        Assert.False(string.IsNullOrEmpty(line.GetProperty("cwd").GetString()));
    }

    [Fact]
    public void Set_StdoutIsStillWritten_AndExitCodePassesThrough()
    {
        var (exit, stdout) = Capture(() => CliTrace.Run(new[] { "desktop", "frobnicate" }, CliRunner.Run, TracePath));

        Assert.Equal(1, exit);
        Assert.Contains("unknown_verb", stdout);
        var line = Assert.Single(ReadLines());
        Assert.Equal("desktop", line.GetProperty("domain").GetString());
        Assert.Equal("frobnicate", line.GetProperty("verb").GetString());
        Assert.Equal(1, line.GetProperty("exitCode").GetInt32());
        Assert.Equal(Encoding.UTF8.GetByteCount(stdout), line.GetProperty("stdoutBytes").GetInt64());
    }

    [Fact]
    public void TextValues_AreRedacted_OtherArgsKept()
    {
        var line = JsonDocument.Parse(CliTrace.BuildLine(
            new[] { "web", "type-text", "--session", "w1", "--locator", "#q", "--text", "s3cret", "--json" },
            0, 10, DateTimeOffset.UtcNow)).RootElement;

        var args = line.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToArray();
        Assert.Equal(new[] { "web", "type-text", "--session", "w1", "--locator", "#q", "--text", "***", "--json" }, args);
        Assert.DoesNotContain("s3cret", line.GetRawText());
    }

    [Theory]
    [InlineData(new[] { "desktop", "snapshot", "--session", "s", "--inline" }, 100, "snapshot-inline")]
    [InlineData(new[] { "web", "get-view-snapshot", "--inline", "--json" }, 100, "snapshot-inline")]
    [InlineData(new[] { "mobile", "get-page-source", "--session", "m" }, 100, "get-page-source")]
    [InlineData(new[] { "web", "take-screenshot", "--session", "w", "--inline" }, 100, "screenshot-inline")]
    [InlineData(new[] { "desktop", "find", "--snapshot", "p.json", "--name", "x" }, 8193, "stdout>8KB")]
    public void ContextHeavy_Rules(string[] args, long bytes, string reason)
    {
        var line = JsonDocument.Parse(CliTrace.BuildLine(args, 0, bytes, DateTimeOffset.UtcNow)).RootElement;

        Assert.True(line.GetProperty("contextHeavy").GetBoolean());
        Assert.Contains(reason, line.GetProperty("heavyReasons").EnumerateArray().Select(r => r.GetString()));
    }

    [Theory]
    [InlineData(new[] { "desktop", "snapshot", "--session", "s" }, 8192L)]
    [InlineData(new[] { "desktop", "find", "--snapshot", "p.json", "--automation-id", "num7Button" }, 300L)]
    [InlineData(new[] { "desktop", "find", "--text", "--inline" }, 300L)]
    [InlineData(new[] { "web", "take-screenshot", "--session", "w" }, 120L)]
    public void NotContextHeavy_PlainSnapshotAndFind(string[] args, long bytes)
    {
        var line = JsonDocument.Parse(CliTrace.BuildLine(args, 0, bytes, DateTimeOffset.UtcNow)).RootElement;

        Assert.False(line.GetProperty("contextHeavy").GetBoolean());
        Assert.Equal(0, line.GetProperty("heavyReasons").GetArrayLength());
    }

    [Fact]
    public void TwoRuns_AppendTwoParseableLines()
    {
        Capture(() => CliTrace.Run(new[] { "--version" }, CliRunner.Run, TracePath));
        Capture(() => CliTrace.Run(new[] { "desktop", "list-apps", "--json" }, CliRunner.Run, TracePath));

        var lines = ReadLines();
        Assert.Equal(2, lines.Length);
        Assert.Equal("list-apps", lines[1].GetProperty("verb").GetString());
    }

    [Fact]
    public void UnwritableTracePath_DoesNotThrow_AndKeepsExitCode()
    {
        // A directory path cannot be opened as a file; an invalid name cannot be created at all.
        var exit1 = Capture(() => CliTrace.Run(new[] { "--version" }, CliRunner.Run, _dir)).exit;
        var exit2 = Capture(() => CliTrace.Run(new[] { "desktop", "frobnicate" }, CliRunner.Run, Path.Combine(_dir, "bad\0name.jsonl"))).exit;

        Assert.Equal(0, exit1);
        Assert.Equal(1, exit2);
    }
}
