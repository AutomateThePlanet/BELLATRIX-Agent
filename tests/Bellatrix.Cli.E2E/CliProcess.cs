using System.Diagnostics;
using System.Text.Json;
using Bellatrix.Execution.SessionMounting;

namespace Bellatrix.Cli.E2E;

/// <summary>One CLI invocation's outcome: exit code, raw stdout, and the parsed JSON (if stdout was JSON).</summary>
public sealed record CliResult(int ExitCode, string Stdout, string Stderr, JsonElement? Json)
{
    public JsonElement Ok
    {
        get
        {
            Assert.True(ExitCode == 0 && Json is not null, $"Expected success, got exit {ExitCode}: {Stdout} {Stderr}");
            return Json!.Value;
        }
    }

    public string? ErrorCode
        => Json is { } j && j.ValueKind == JsonValueKind.Object && j.TryGetProperty("error", out var e)
            ? e.GetProperty("code").GetString()
            : null;
}

/// <summary>
/// Runs the installed <c>bellatrix</c> (or <c>BELLATRIX_E2E_EXE</c>) as a separate process per step, from a private
/// working directory — so every test has its own <c>.bellatrix/sessions.db</c>, snapshots and trace file
/// (<c>BELLATRIX_TRACE</c>). Never starts or stops WinAppDriver/Appium/Selenium.
/// </summary>
public sealed class CliProcess : IDisposable
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(120);
    private readonly List<(string domain, string session)> _open = new();

    public CliProcess(string name)
    {
        WorkDir = Path.Combine(Path.GetTempPath(), "bellatrix-e2e", $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(WorkDir);
        TracePath = Path.Combine(WorkDir, "trace.jsonl");
        Env["BELLATRIX_TRACE"] = TracePath;

        // Calculator (Windows Store app) as the desktop app definition, unless the caller configured one.
        if (Environment.GetEnvironmentVariable("McpSettings__Apps__0__Path") is null)
        {
            Env["McpSettings__Apps__0__Name"] = "calculator";
            Env["McpSettings__Apps__0__Path"] = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        }
    }

    public string WorkDir { get; }

    public string TracePath { get; }

    public Dictionary<string, string> Env { get; } = new();

    public static string Exe => Environment.GetEnvironmentVariable("BELLATRIX_E2E_EXE") is { Length: > 0 } exe ? exe : "bellatrix";

    public string DbPath => Path.Combine(WorkDir, ".bellatrix", "sessions.db");

    public SessionStore Store(string? domain) => new(DbPath, domain);

    public CliResult Run(params string[] args)
    {
        var psi = new ProcessStartInfo(Exe)
        {
            WorkingDirectory = WorkDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in Env) psi.Environment[k] = v;

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start '{Exe}'.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(StepTimeout))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException($"bellatrix {string.Join(' ', args)} did not finish in {StepTimeout.TotalSeconds}s.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult().Trim();
        var stderr = stderrTask.GetAwaiter().GetResult();
        JsonElement? json = null;
        try { json = JsonDocument.Parse(stdout).RootElement.Clone(); } catch (JsonException) { }

        // Remember opened sessions so Dispose can close them even when an assertion fails mid-test.
        if (args.Length >= 2 && args[1] == "start-session" && process.ExitCode == 0)
        {
            var i = Array.IndexOf(args, "--session");
            if (i >= 0 && i + 1 < args.Length) _open.Add((args[0], args[i + 1]));
        }

        return new CliResult(process.ExitCode, stdout, stderr, json);
    }

    /// <summary>A driver/server the user did not start is SKIPPED, never FAILED.</summary>
    public static void SkipIfUnreachable(CliResult result, string what)
        => Skip.If(result.ErrorCode == "driver_unreachable", $"{what} not reachable: {result.Stdout}");

    /// <summary>The trace lines written by this test's invocations.</summary>
    public IReadOnlyList<JsonElement> Trace()
        => File.Exists(TracePath)
            ? File.ReadAllLines(TracePath).Where(l => l.Length > 0).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList()
            : new List<JsonElement>();

    public void Dispose()
    {
        foreach (var (domain, session) in _open)
        {
            try { Run(domain, "close-session", "--session", session, "--json"); } catch { /* best effort */ }
        }

        try { Directory.Delete(WorkDir, recursive: true); } catch { /* SQLite handle may linger briefly */ }
    }
}
