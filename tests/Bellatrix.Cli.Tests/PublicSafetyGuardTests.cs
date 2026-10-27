using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// This repository is public. Fails when any tracked (or new, not ignored) text file matches a rule in
/// <c>guard-patterns.json</c>: local machine paths, secret-shaped strings, links to repositories that are not public,
/// and email addresses outside the documented allow-list.
/// </summary>
public class PublicSafetyGuardTests
{
    private const string PatternFile = "tests/Bellatrix.Cli.Tests/guard-patterns.json";

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".nupkg", ".snupkg", ".dll", ".exe", ".pdb", ".snk", ".zip",
    };

    [Fact]
    public void Repository_ContainsNoForbiddenPattern()
    {
        var root = RepoRoot();
        var rules = LoadRules(root);
        var hits = new List<string>();

        foreach (var relative in RepositoryFiles(root))
        {
            if (relative == PatternFile || BinaryExtensions.Contains(Path.GetExtension(relative)))
            {
                continue;
            }

            var full = Path.Combine(root, relative);
            if (!File.Exists(full))
            {
                continue; // deleted in the working tree
            }

            var bytes = File.ReadAllBytes(full);
            if (Array.IndexOf(bytes, (byte)0) >= 0)
            {
                continue; // binary
            }

            hits.AddRange(Scan(relative, Encoding.UTF8.GetString(bytes), rules));
        }

        Assert.True(hits.Count == 0, "Forbidden patterns found:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void PatternFile_IsValid()
    {
        var rules = LoadRules(RepoRoot());

        Assert.NotEmpty(rules);
        Assert.Equal(rules.Count, rules.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void Scanner_FindsPathsSecretsPrivateLinksAndEmails()
    {
        var rules = LoadRules(RepoRoot());

        // Built at runtime so this file never contains a literal hit.
        var text = string.Join("\n",
            "path: " + "C:" + "\\" + "SourceCode" + "\\repo",
            "key = \"" + "AKIA" + new string('A', 16) + "\"",
            "see https://github.com/" + "AutomateThePlanet/" + "Some-Private-Repo",
            "mail " + "someone" + "@" + "company.test.org");

        var hits = Scan("sample.txt", text, rules).ToList();

        Assert.Contains(hits, h => h.EndsWith("windows-dev-path", StringComparison.Ordinal));
        Assert.Contains(hits, h => h.EndsWith("aws-access-key", StringComparison.Ordinal));
        Assert.Contains(hits, h => h.EndsWith("non-public-repository-link", StringComparison.Ordinal));
        Assert.Contains(hits, h => h.EndsWith("email-address", StringComparison.Ordinal));

        var allowed = string.Join("\n",
            "bellatrix desktop snapshot --session calc",
            "https://github.com/AutomateThePlanet/BELLATRIX-Java/issues and https://github.com/AutomateThePlanet/BELLATRIX",
            "user@example.com, info@bellatrix.solutions, `@bellatrix/web`, @agent-bellatrix-web-locator-specialist");
        Assert.Empty(Scan("clean.txt", allowed, rules));
    }

    private static IEnumerable<string> Scan(string relative, string text, IReadOnlyList<(string Id, Regex Regex)> rules)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (id, regex) in rules)
            {
                if (regex.IsMatch(lines[i]))
                {
                    yield return $"{relative}:{i + 1}: {id}";
                }
            }
        }
    }

    private static IReadOnlyList<(string Id, Regex Regex)> LoadRules(string root)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, PatternFile)));
        return doc.RootElement.GetProperty("regex").EnumerateArray()
            .Select(p => (p.GetProperty("id").GetString()!, new Regex(p.GetProperty("pattern").GetString()!, RegexOptions.Compiled)))
            .ToList();
    }

    /// <summary>Tracked plus new (not ignored) files from git; a directory walk when git is unavailable.</summary>
    private static IReadOnlyList<string> RepositoryFiles(string root)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "ls-files -z --cached --others --exclude-standard")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var git = Process.Start(psi)!;
            var output = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            if (git.ExitCode == 0)
            {
                return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // git not installed: fall back to the file system
        }

        var skip = new[] { ".git", "bin", "obj", "artifacts", "node_modules" };
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !f.Split('/').Any(part => skip.Contains(part, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
