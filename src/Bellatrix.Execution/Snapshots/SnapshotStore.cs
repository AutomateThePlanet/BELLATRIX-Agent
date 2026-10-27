using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bellatrix.Execution.Snapshots;

/// <summary>
/// The compact reference returned to the agent instead of the full tree. JSON keys are the
/// snapshot contract (`path`, `elementCount`); see docs/architecture.md, "Context economy".
/// </summary>
public sealed class SnapshotReference
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("elementCount")] public int ElementCount { get; set; }
    [JsonPropertyName("summary")] public Dictionary<string, object> Summary { get; set; } = new();
}

/// <summary>
/// Persists full UI snapshots to disk and returns only a compact reference, so the full
/// accessibility tree never enters the orchestrator's context. Artifacts
/// live under a working-directory-relative <c>.bellatrix/snapshots/</c>, making
/// them grep-friendly and co-located with the project rather than hidden in the user profile.
/// </summary>
public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly string _dir;

    public static string DefaultDir => System.IO.Path.Combine(
        Directory.GetCurrentDirectory(), ".bellatrix", "snapshots");

    public SnapshotStore(string? dir = null) => _dir = dir ?? DefaultDir;

    /// <summary>
    /// Writes the node array to disk and returns the compact reference. <paramref name="isInteractive"/> is the
    /// domain's own rule for the summary's <c>interactive</c> count (web and mobile nodes are not UIA control
    /// types); it defaults to the desktop <see cref="SnapshotQuery.IsInteractive"/>.
    /// </summary>
    public SnapshotReference Save(string sessionId, List<Dictionary<string, object>> nodes,
        Func<Dictionary<string, object>, bool>? isInteractive = null)
    {
        Directory.CreateDirectory(_dir);
        var path = System.IO.Path.Combine(_dir, $"{Sanitize(sessionId)}-{DateTime.UtcNow.Ticks}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(nodes, Json));

        return new SnapshotReference
        {
            Path = path,
            SessionId = sessionId,
            ElementCount = nodes.Count,
            Summary = BuildSummary(nodes, isInteractive ?? SnapshotQuery.IsInteractive),
        };
    }

    /// <summary>Loads a previously persisted snapshot for narrow querying.</summary>
    public List<Dictionary<string, object>> Load(string path)
        => JsonSerializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(path)) ?? new();

    private static Dictionary<string, object> BuildSummary(
        List<Dictionary<string, object>> nodes, Func<Dictionary<string, object>, bool> isInteractive)
    {
        var interactive = nodes.Count(isInteractive);
        var topControlTypes = nodes
            .Select(n => n.TryGetValue("ControlType", out var ct) ? ct?.ToString()
                : (n.TryGetValue("Tag", out var t) ? t?.ToString() : null))
            .Where(s => !string.IsNullOrEmpty(s))
            .GroupBy(s => s!)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .ToDictionary(g => g.Key, g => (object)g.Count());

        return new Dictionary<string, object>
        {
            ["interactive"] = interactive,
            ["topControlTypes"] = topControlTypes,
        };
    }

    private static string Sanitize(string s)
        => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
}
