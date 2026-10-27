using System.Text;

namespace Bellatrix.Cli.Scaffolding;

/// <summary>
/// Registers BELLATRIX in a human-owned instructions file that has no other place for a tool to own (Codex
/// <c>AGENTS.md</c>, Gemini <c>GEMINI.md</c>) with ONE marker block holding a single line — an <c>@import</c>
/// where the provider supports it, else a pointer to the shared fragment (R1/R3):
/// <code>
/// &lt;!-- &gt;&gt;&gt; bellatrix &gt;&gt;&gt; --&gt;
/// @.bellatrix/bellatrix.instructions.md
/// &lt;!-- &lt;&lt;&lt; bellatrix &lt;&lt;&lt; --&gt;
/// </code>
/// Bytes outside the block are never rewritten. A block whose line changed is replaced in place; a missing
/// block is appended.
/// </summary>
internal static class MarkerBlockRegistrar
{
    public static string Open(string stem) => $"<!-- >>> {stem} >>> -->";

    public static string Close(string stem) => $"<!-- <<< {stem} <<< -->";

    public static string Block(string stem, string line) => $"{Open(stem)}\n{line}\n{Close(stem)}\n";

    public static RegistrationOutcome Register(string hostPath, string stem, string line, bool dryRun = false)
    {
        var block = Block(stem, line);
        if (!File.Exists(hostPath) || new FileInfo(hostPath).Length == 0)
        {
            if (!dryRun)
            {
                File.WriteAllText(hostPath, block);
            }

            return RegistrationOutcome.Created;
        }

        var original = File.ReadAllText(hostPath);
        var open = original.IndexOf(Open(stem), StringComparison.Ordinal);
        var close = open < 0 ? -1 : original.IndexOf(Close(stem), open, StringComparison.Ordinal);

        if (open >= 0 && close > open)
        {
            var end = close + Close(stem).Length;
            var existing = original[open..end].Replace("\r\n", "\n");
            if (existing + "\n" == block)
            {
                return RegistrationOutcome.AlreadyPresent;
            }

            // Replace only our block, keeping the file's own line ending style inside it.
            var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var replacement = block.TrimEnd('\n').Replace("\n", newline);
            if (!dryRun)
            {
                File.WriteAllText(hostPath, original[..open] + replacement + original[end..]);
            }

            return RegistrationOutcome.Added;
        }

        var append = new StringBuilder();
        if (!original.EndsWith('\n'))
        {
            append.Append('\n');
        }

        append.Append('\n').Append(block);
        if (!dryRun)
        {
            File.AppendAllText(hostPath, append.ToString());
        }

        return RegistrationOutcome.Added;
    }
}
