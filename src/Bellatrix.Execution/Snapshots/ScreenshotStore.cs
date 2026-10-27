using System.Buffers.Binary;

namespace Bellatrix.Execution.Snapshots;

/// <summary>The compact reference returned instead of an inline base64 image.</summary>
public sealed record ScreenshotReference(string Path, int Width, int Height);

/// <summary>
/// Persists screenshots to disk and returns only <c>{path, width, height}</c>, so a base64 image never enters the
/// orchestrator's context by default (same pattern as <see cref="SnapshotStore"/>). Files live under a
/// working-directory-relative <c>.bellatrix/screenshots/</c> unless the caller names a file.
/// </summary>
public sealed class ScreenshotStore
{
    private readonly string _dir;

    /// <summary><c>.bellatrix/screenshots/</c> under the current working directory.</summary>
    public static string DefaultDir => System.IO.Path.Combine(
        Directory.GetCurrentDirectory(), ".bellatrix", "screenshots");

    /// <summary>Creates a store over <paramref name="dir"/>, or <see cref="DefaultDir"/> when null.</summary>
    public ScreenshotStore(string? dir = null) => _dir = dir ?? DefaultDir;

    /// <summary>Writes the base64 PNG to <paramref name="outPath"/>, or to a fresh file in the store directory.</summary>
    public ScreenshotReference Save(string sessionId, string base64Png, string? outPath = null)
    {
        var bytes = Convert.FromBase64String(base64Png);
        string path;
        if (outPath is null)
        {
            Directory.CreateDirectory(_dir);
            path = System.IO.Path.Combine(_dir, $"{Sanitize(sessionId)}-{DateTime.UtcNow.Ticks}.png");
        }
        else
        {
            path = System.IO.Path.GetFullPath(outPath);
            var parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        File.WriteAllBytes(path, bytes);
        var (width, height) = PngSize(bytes);
        return new ScreenshotReference(path, width, height);
    }

    /// <summary>Width and height from the PNG IHDR chunk; (0, 0) when the bytes are not a PNG.</summary>
    public static (int Width, int Height) PngSize(ReadOnlySpan<byte> png)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (png.Length < 24 || !png[..8].SequenceEqual(signature) || png[12] != (byte)'I' || png[13] != (byte)'H'
            || png[14] != (byte)'D' || png[15] != (byte)'R')
        {
            return (0, 0);
        }

        return ((int)BinaryPrimitives.ReadUInt32BigEndian(png[16..20]), (int)BinaryPrimitives.ReadUInt32BigEndian(png[20..24]));
    }

    private static string Sanitize(string s)
        => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
}
