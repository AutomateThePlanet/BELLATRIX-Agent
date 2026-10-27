namespace Bellatrix.Execution.Snapshots;

/// <summary>Narrow-query modes over a persisted snapshot.</summary>
public enum SnapshotQueryMode
{
    Text,
    Grep,
    Name,
    AutomationId,
    ControlType,
    Interactive,
}

/// <summary>
/// Filters a persisted snapshot down to the matching nodes so flat locators never need the full
/// tree. Pure over the node dictionaries produced by the view-snapshot parser.
/// </summary>
public static class SnapshotQuery
{
    private static readonly HashSet<string> InteractiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "Edit", "CheckBox", "ComboBox", "RadioButton", "Hyperlink", "MenuItem",
        "ListItem", "TabItem", "Slider", "Spinner", "SplitButton", "Tab", "Document", "Text",
    };

    /// <summary>True when a node's control type (or tag) is an interactable UIA type.</summary>
    public static bool IsInteractive(IReadOnlyDictionary<string, object> node)
    {
        var type = Get(node, "ControlType") ?? Get(node, "Tag");
        return type is not null && InteractiveTypes.Contains(type) && type != "Text" && type != "Document";
    }

    /// <summary>Runs a query, returning only the matching nodes.</summary>
    public static List<Dictionary<string, object>> Run(
        List<Dictionary<string, object>> nodes, SnapshotQueryMode mode, string? value)
        => mode switch
        {
            SnapshotQueryMode.Interactive => nodes.Where(IsInteractive).ToList(),
            SnapshotQueryMode.Grep => nodes.Where(n => MatchesRegex(n, value)).ToList(),
            SnapshotQueryMode.Text => nodes.Where(n =>
                ContainsCI(Get(n, "Name"), value) || ContainsCI(Get(n, "Value"), value)
                || ContainsCI(Get(n, "HelpText"), value)).ToList(),
            SnapshotQueryMode.Name => nodes.Where(n => ContainsCI(Get(n, "Name"), value)).ToList(),
            SnapshotQueryMode.AutomationId => nodes.Where(n => ContainsCI(Get(n, "AutomationId"), value)).ToList(),
            SnapshotQueryMode.ControlType => nodes.Where(n =>
                EqualsCI(Get(n, "ControlType"), value) || EqualsCI(Get(n, "Tag"), value)).ToList(),
            _ => new(),
        };

    private static readonly string[] GrepKeys = { "Name", "AutomationId", "ControlType", "Tag", "Value", "HelpText", "ClassName" };

    /// <summary>True when any text-bearing key matches the (case-insensitive) regex (the `--grep` mode).</summary>
    private static bool MatchesRegex(IReadOnlyDictionary<string, object> node, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var rx = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var key in GrepKeys)
        {
            var v = Get(node, key);
            if (v is not null && rx.IsMatch(v))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Get(IReadOnlyDictionary<string, object> node, string key)
        => node.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static bool ContainsCI(string? haystack, string? needle)
        => haystack is not null && needle is not null
           && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsCI(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
