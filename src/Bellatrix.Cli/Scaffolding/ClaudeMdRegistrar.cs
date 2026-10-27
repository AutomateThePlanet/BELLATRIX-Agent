using System.Text;

namespace Bellatrix.Cli.Scaffolding;

internal enum RegistrationOutcome
{
    Created,
    Added,
    AlreadyPresent,
}

/// <summary>
/// Registers the BELLATRIX instructions fragment in the consumer's root <c>CLAUDE.md</c> (R3) with a
/// single idempotent <c>@import</c> line under a <c># Tool instructions</c> heading. The human body is
/// never read for meaning, reformatted, or reordered — writes are append-only, so existing bytes are
/// preserved exactly (R1).
/// </summary>
internal static class ClaudeMdRegistrar
{
    public const string Heading = "# Tool instructions";
    public const string ImportLine = "@.bellatrix/bellatrix.instructions.md";

    public static RegistrationOutcome Register(string claudeMdPath, string importLine = ImportLine, bool dryRun = false)
    {
        if (!File.Exists(claudeMdPath) || new FileInfo(claudeMdPath).Length == 0)
        {
            if (!dryRun)
            {
                File.WriteAllText(claudeMdPath, Heading + "\n\n" + importLine + "\n");
            }

            return RegistrationOutcome.Created;
        }

        var original = File.ReadAllText(claudeMdPath);
        var lines = original.Replace("\r\n", "\n").Split('\n');

        if (lines.Any(l => l.Trim() == importLine))
        {
            return RegistrationOutcome.AlreadyPresent; // idempotent no-op
        }

        var hasHeading = lines.Any(l => l.Trim() == Heading);

        var append = new StringBuilder();
        if (!original.EndsWith("\n"))
        {
            append.Append('\n');
        }

        if (!hasHeading)
        {
            append.Append('\n').Append(Heading).Append('\n');
        }

        append.Append('\n').Append(importLine).Append('\n');
        if (!dryRun)
        {
            File.AppendAllText(claudeMdPath, append.ToString());
        }

        return RegistrationOutcome.Added;
    }
}
