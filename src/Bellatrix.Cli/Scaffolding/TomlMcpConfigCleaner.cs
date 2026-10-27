using System.Text.RegularExpressions;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// The TOML twin of <see cref="McpConfigCleaner"/> for Codex's <c>.codex/config.toml</c>: removes only the
/// <c>[mcp_servers.&lt;owned-key&gt;]</c> tables (and their <c>[mcp_servers.&lt;owned-key&gt;.*]</c> sub-tables)
/// line by line, so every other byte — foreign servers, comments, formatting — is preserved. Never creates or
/// deletes the file.
/// </summary>
internal static class TomlMcpConfigCleaner
{
    private static readonly Regex Header = new(@"^\s*\[\[?\s*(?<name>[^\]]+?)\s*\]\]?\s*(#.*)?$", RegexOptions.CultureInvariant);

    public static bool RemoveOwnedKeys(string filePath, IReadOnlyCollection<string> keys, bool dryRun = false)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var original = File.ReadAllText(filePath);
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = original.Replace("\r\n", "\n").Split('\n');

        var kept = new List<string>(lines.Length);
        var skipping = false;
        var removed = false;
        foreach (var line in lines)
        {
            var header = Header.Match(line);
            if (header.Success)
            {
                skipping = IsOwned(header.Groups["name"].Value, keys);
                removed |= skipping;
            }

            if (!skipping)
            {
                kept.Add(line);
            }
        }

        if (removed && !dryRun)
        {
            File.WriteAllText(filePath, string.Join(newline, kept));
        }

        return removed;
    }

    /// <summary>True for <c>mcp_servers.&lt;key&gt;</c> or a sub-table of it; the key may be quoted.</summary>
    private static bool IsOwned(string tableName, IReadOnlyCollection<string> keys)
    {
        var parts = tableName.Split('.', 3, StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            && parts[0] == "mcp_servers"
            && keys.Contains(parts[1].Trim('"', '\''));
    }
}
