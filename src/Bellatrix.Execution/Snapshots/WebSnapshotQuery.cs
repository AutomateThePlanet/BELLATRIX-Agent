namespace Bellatrix.Execution.Snapshots;

/// <summary>Narrow-query modes over a persisted web snapshot.</summary>
public enum WebSnapshotQueryMode
{
    Text,
    Grep,
    Id,
    Role,
    DataTestid,
    Name,
    Interactive,
}

/// <summary>
/// Filters a persisted web (DOM/accessibility) snapshot down to the matching nodes so flat selectors
/// never need the full tree. Owns its OWN key set + interactive ladder
/// (data-testid → id → aria-label → role+text → css) — distinct from the desktop UIA
/// <see cref="SnapshotQuery"/> and the mobile <see cref="MobileSnapshotQuery"/>, keeping desktop
/// untouched (duplicate-per-domain).
/// </summary>
public static class WebSnapshotQuery
{
    // `a` counts only with an href and `input` not when hidden; see IsInteractive.
    private static readonly HashSet<string> InteractiveTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "input", "select", "textarea",
    };

    private static readonly HashSet<string> InteractiveRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "link", "checkbox", "radio", "textbox", "searchbox", "combobox", "listbox", "option",
        "menuitem", "menuitemcheckbox", "menuitemradio", "tab", "switch", "slider", "spinbutton",
    };

    /// <summary>
    /// True when a node is an interactable web control: <c>button</c>, <c>a[href]</c>, <c>input</c> (not hidden),
    /// <c>select</c>, <c>textarea</c>, or an element with an interactive ARIA role.
    /// </summary>
    public static bool IsInteractive(IReadOnlyDictionary<string, object> node)
    {
        var tag = Get(node, "Tag");
        if (tag is not null)
        {
            if (string.Equals(tag, "a", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Get(node, "Href")))
            {
                return true;
            }

            if (InteractiveTags.Contains(tag)
                && !(string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(Get(node, "Type"), "hidden", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        var role = Get(node, "Role");
        return role is not null && InteractiveRoles.Contains(role);
    }

    /// <summary>Runs a query, returning only the matching nodes.</summary>
    public static List<Dictionary<string, object>> Run(
        List<Dictionary<string, object>> nodes, WebSnapshotQueryMode mode, string? value)
        => mode switch
        {
            WebSnapshotQueryMode.Interactive => nodes.Where(IsInteractive).ToList(),
            WebSnapshotQueryMode.Grep => nodes.Where(n => MatchesRegex(n, value)).ToList(),
            WebSnapshotQueryMode.Text => nodes.Where(n =>
                ContainsCI(Get(n, "Text"), value) || ContainsCI(Get(n, "AriaLabel"), value)
                || ContainsCI(Get(n, "Placeholder"), value)).ToList(),
            WebSnapshotQueryMode.Id => nodes.Where(n => ContainsCI(Get(n, "Id"), value)).ToList(),
            WebSnapshotQueryMode.Role => nodes.Where(n => EqualsCI(Get(n, "Role"), value)).ToList(),
            WebSnapshotQueryMode.DataTestid => nodes.Where(n => ContainsCI(Get(n, "DataTestid"), value)).ToList(),
            WebSnapshotQueryMode.Name => nodes.Where(n => ContainsCI(Get(n, "Name"), value)).ToList(),
            _ => new(),
        };

    private static readonly string[] GrepKeys =
        { "Id", "DataTestid", "Name", "Role", "AriaLabel", "Text", "Placeholder", "Href", "Class", "Value", "Tag" };

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
