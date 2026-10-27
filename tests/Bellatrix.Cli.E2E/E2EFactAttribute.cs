namespace Bellatrix.Cli.E2E;

/// <summary>
/// A <see cref="SkippableFactAttribute"/> that is skipped unless <c>BELLATRIX_E2E=1</c> and every gate listed in
/// <see cref="Gates"/> (<c>BELLATRIX_E2E_&lt;GATE&gt;=1</c>) is set, so a machine runs only what it has. Leave
/// <see cref="Gates"/> empty for tests that need no driver. At runtime a test calls
/// <see cref="CliProcess.SkipIfUnreachable"/> so an unreachable driver is reported as SKIPPED, never FAILED.
/// (Gates are a named property: SkippableFact's discoverer reads constructor arguments as exception types.)
/// </summary>
public sealed class E2EFactAttribute : SkippableFactAttribute
{
    private string _gates = string.Empty;

    public E2EFactAttribute()
    {
        if (!IsOn("BELLATRIX_E2E"))
        {
            Skip = "Live E2E is off: set BELLATRIX_E2E=1 (scripts/run-e2e.ps1 does this).";
        }
    }

    /// <summary>Comma-separated domain gates, e.g. <c>"DESKTOP,WEB"</c>.</summary>
    public string Gates
    {
        get => _gates;
        set
        {
            _gates = value;
            if (Skip is not null) return;

            var missing = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(g => !IsOn($"BELLATRIX_E2E_{g}")).ToArray();
            if (missing.Length > 0)
            {
                Skip = "Gate not set: " + string.Join(", ", missing.Select(g => $"BELLATRIX_E2E_{g}=1"));
            }
        }
    }

    private static bool IsOn(string name) => Environment.GetEnvironmentVariable(name) == "1";
}
