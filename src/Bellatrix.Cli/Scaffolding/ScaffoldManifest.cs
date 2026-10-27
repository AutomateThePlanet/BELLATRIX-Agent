using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>One owned file recorded in the manifest: repo-relative path + SHA-256 of the bytes init last wrote.</summary>
internal sealed class ManifestEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// The <c>.bellatrix/manifest.json</c> record of every file <c>bellatrix init</c> authored (R6). On re-run,
/// a file whose on-disk hash differs from its recorded hash is a developer edit and is skipped (unless
/// <c>--regenerate</c>). Only BELLATRIX-owned files are ever recorded or hashed (R8).
/// </summary>
internal sealed class ScaffoldManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = "2.0";
    [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;
    /// <summary>BELLATRIX flavor the files were rendered for (csharp|java|typescript); null in pre-2.5 manifests (= csharp).</summary>
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("files")] public List<ManifestEntry> Files { get; set; } = new();

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static ScaffoldManifest Load(string path)
        => File.Exists(path)
            ? JsonSerializer.Deserialize<ScaffoldManifest>(File.ReadAllText(path)) ?? new ScaffoldManifest()
            : new ScaffoldManifest();

    public void Save(string path)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, Opts));
    }

    public string? HashFor(string relativePath)
        => Files.FirstOrDefault(f => f.Path == relativePath)?.Sha256;

    /// <summary>
    /// SHA-256 of the content with line endings normalized (CRLF → LF), so a Git checkout with
    /// <c>core.autocrlf=true</c> rewriting LF to CRLF is not mistaken for a developer edit (R6). Manifests written
    /// before this normalization recorded hashes of LF-only bytes, so they stay compatible.
    /// </summary>
    public static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(NormalizeLineEndings(bytes))).ToLowerInvariant();

    public static string Sha256HexOfFile(string path)
        => Sha256Hex(File.ReadAllBytes(path));

    private static byte[] NormalizeLineEndings(byte[] bytes)
    {
        if (Array.IndexOf(bytes, (byte)'\r') < 0)
        {
            return bytes;
        }

        var normalized = new List<byte>(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
            {
                continue; // drop the CR of a CRLF pair
            }

            normalized.Add(bytes[i]);
        }

        return normalized.ToArray();
    }
}
