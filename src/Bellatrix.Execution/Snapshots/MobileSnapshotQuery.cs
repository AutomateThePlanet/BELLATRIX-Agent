namespace Bellatrix.Execution.Snapshots;

/// <summary>Narrow-query modes over a persisted mobile snapshot.</summary>
public enum MobileSnapshotQueryMode
{
    Text,
    Grep,
    ResourceId,
    ContentDesc,
    Class,
    Interactive,
}

/// <summary>
/// Filters a persisted mobile snapshot down to the matching nodes so flat locators never need the full
/// tree. Owns its OWN key set + interactive ladder (Appium resource-id/content-desc/class
/// for Android, name/label/type for iOS) — distinct from the desktop UIA <see cref="SnapshotQuery"/>,
/// keeping desktop untouched (duplicate-per-domain).
/// </summary>
public static class MobileSnapshotQuery
{
    // Interactive control-class fragments — matched as case-insensitive substrings of Class/Type/Tag
    // because Appium reports fully-qualified names (e.g. android.widget.Button, XCUIElementTypeButton).
    private static readonly string[] InteractiveFragments =
    {
        "Button", "EditText", "TextField", "CheckBox", "Switch", "Spinner", "RadioButton",
        "SeekBar", "ImageButton", "ToggleButton", "SearchField", "SecureTextField", "Cell",
    };

    /// <summary>True when a node's Class/Type/Tag denotes an interactable Android/iOS control.</summary>
    public static bool IsInteractive(IReadOnlyDictionary<string, object> node)
    {
        foreach (var key in new[] { "Class", "Type", "Tag" })
        {
            var v = Get(node, key);
            if (v is null) continue;
            foreach (var fragment in InteractiveFragments)
            {
                if (v.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Runs a query, returning only the matching nodes.</summary>
    public static List<Dictionary<string, object>> Run(
        List<Dictionary<string, object>> nodes, MobileSnapshotQueryMode mode, string? value)
        => mode switch
        {
            MobileSnapshotQueryMode.Interactive => nodes.Where(IsInteractive).ToList(),
            MobileSnapshotQueryMode.Grep => nodes.Where(n => MatchesRegex(n, value)).ToList(),
            MobileSnapshotQueryMode.Text => nodes.Where(n =>
                ContainsCI(Get(n, "Text"), value) || ContainsCI(Get(n, "ContentDesc"), value)
                || ContainsCI(Get(n, "Label"), value)).ToList(),
            MobileSnapshotQueryMode.ResourceId => nodes.Where(n => ContainsCI(Get(n, "ResourceId"), value)).ToList(),
            MobileSnapshotQueryMode.ContentDesc => nodes.Where(n => ContainsCI(Get(n, "ContentDesc"), value)).ToList(),
            MobileSnapshotQueryMode.Class => nodes.Where(n => ContainsCI(Get(n, "Class"), value)).ToList(),
            _ => new(),
        };

    private static readonly string[] GrepKeys =
        { "ResourceId", "ContentDesc", "Text", "Class", "Name", "Label", "Value", "Type", "Tag" };

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
}
