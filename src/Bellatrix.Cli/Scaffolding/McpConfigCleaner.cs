using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// Removes BELLATRIX-owned MCP server entries from a config file (R7 under the "no MCP servers" decision).
/// BELLATRIX registers no MCP server any more; this only cleans up keys a previous <c>bellatrix init</c>
/// wrote. Uses a mutable <see cref="JsonNode"/> DOM so every foreign key (e.g. <c>spectra</c>,
/// <c>testimize</c>, <c>inputs</c>) is preserved verbatim. Both the VS Code <c>servers</c> and the
/// Claude/Cursor <c>mcpServers</c> containers are cleaned. The file is never created or deleted, and is only
/// rewritten when something was actually removed.
/// </summary>
internal static class McpConfigCleaner
{
    private static readonly JsonDocumentOptions DocOpts = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };

    private static readonly string[] Containers = { "servers", "mcpServers", "mcp" }; // "mcp": OpenCode

    /// <summary>Returns true when at least one owned key was removed and the file rewritten.</summary>
    public static bool RemoveOwnedKeys(string filePath, IReadOnlyCollection<string> keys, bool dryRun = false)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(File.ReadAllText(filePath), nodeOptions: null, DocOpts);
        }
        catch (JsonException)
        {
            return false; // not JSON we can read (e.g. a hand-edited JSONC variant): leave the human's file alone
        }

        if (parsed is not JsonObject root)
        {
            return false;
        }

        var removed = false;
        foreach (var container in Containers)
        {
            if (root[container] is not JsonObject servers)
            {
                continue;
            }

            foreach (var key in keys)
            {
                removed |= servers.Remove(key); // remove ONLY our own key; foreign keys untouched
            }
        }

        if (removed && !dryRun)
        {
            File.WriteAllText(filePath, root.ToJsonString(WriteOpts));
        }

        return removed;
    }
}
