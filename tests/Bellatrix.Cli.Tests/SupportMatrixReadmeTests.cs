using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>docs/support-matrix.json</c> is the single source of what BELLATRIX Agent supports. Its public fields
/// (languages, domains, agents, support, footnotes) feed README.md, the NuGet README and the website; its
/// <c>internal</c> block is the maintainers' verification tracking and is never published. These tests keep the
/// file complete and in step with the CLI, keep the generated README tables equal to the public fields
/// (<c>scripts/gen-readme-matrix.ps1</c> regenerates them), and keep verification status out of the public docs.
/// </summary>
public class SupportMatrixReadmeTests
{
    private const string Supported = "✓";
    private const string NotAvailable = "—";

    private static readonly string[] StatusWording =
    {
        "READY", "PARTIAL", "MISSING (framework)", "verified live", "confirmed live", "not yet verified", "live check",
        "live use", "synthetic fixtures", "VERIFICATION-CHECKLIST", "conformance-report",
    };

    [Fact]
    public void Matrix_ListsExactlyTheCliAgentsLanguagesAndDomains()
    {
        var m = Matrix();

        Assert.Equal(AgentSelection.Names, Ids(m, "agents"));
        Assert.Equal(LanguageSelection.Known, Ids(m, "languages"));
        Assert.Equal(new[] { "desktop", "web", "mobile" }, Ids(m, "domains"));
    }

    [Fact]
    public void Matrix_Support_HasOneEntryPerLanguageDomain_MatchingTheFrameworks()
    {
        var m = Matrix();
        var languages = ConformanceLanguages();
        var support = m.GetProperty("support").EnumerateArray().ToList();
        var keys = support.Select(s => $"{s.GetProperty("language").GetString()}|{s.GetProperty("domain").GetString()}").ToList();

        Assert.Equal(Ids(m, "languages").Length * Ids(m, "domains").Length, support.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var s in support)
        {
            var expected = languages[s.GetProperty("language").GetString()!].Contains(s.GetProperty("domain").GetString());
            Assert.Equal(expected, s.GetProperty("supported").GetBoolean());
        }
    }

    [Fact]
    public void Matrix_InternalCells_OnePerLanguageDomainAgent_WithALegendStatus()
    {
        var m = Matrix();
        var statuses = Internal(m, "legend").Select(l => l.GetProperty("status").GetString()).ToHashSet();
        var cells = Internal(m, "cells");
        var keys = cells.Select(Key).ToList();

        Assert.Equal(Ids(m, "languages").Length * Ids(m, "domains").Length * Ids(m, "agents").Length, cells.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var language in Ids(m, "languages"))
        {
            foreach (var domain in Ids(m, "domains"))
            {
                foreach (var agent in Ids(m, "agents"))
                {
                    Assert.Contains($"{language}|{domain}|{agent}", keys);
                }
            }
        }

        Assert.All(cells, c => Assert.Contains(c.GetProperty("status").GetString(), statuses));
    }

    [Fact]
    public void Matrix_InternalMissingCells_AreExactlyTheUnsupportedLanguageDomains()
    {
        var m = Matrix();
        var supported = m.GetProperty("support").EnumerateArray().ToDictionary(
            s => $"{s.GetProperty("language").GetString()}|{s.GetProperty("domain").GetString()}",
            s => s.GetProperty("supported").GetBoolean());

        foreach (var cell in Internal(m, "cells"))
        {
            var missing = cell.GetProperty("status").GetString()!.StartsWith("MISSING", StringComparison.Ordinal);
            Assert.True(supported[$"{cell.GetProperty("language").GetString()}|{cell.GetProperty("domain").GetString()}"] != missing, Key(cell));
        }
    }

    [Fact]
    public void Matrix_InternalRowsCoverEveryLanguageDomainOnce()
    {
        var m = Matrix();
        var rows = Internal(m, "rows").Select(r => $"{r.GetProperty("language").GetString()}|{r.GetProperty("domain").GetString()}").ToList();

        Assert.Equal(Ids(m, "languages").Length * Ids(m, "domains").Length, rows.Count);
        Assert.Equal(rows.Count, rows.Distinct().Count());
    }

    [Fact]
    public void Readme_GeneratedSupportTable_EqualsThePublicFields()
    {
        AssertSupportTable(Block(ReadText("README.md"), "support-matrix"));
    }

    [Fact]
    public void PackageReadme_SupportSection_EqualsThePublicFields()
    {
        var package = ReadText("src", "Bellatrix.Cli", "Package", "README.md");
        var begin = package.IndexOf("## What is supported\n", StringComparison.Ordinal);
        Assert.True(begin >= 0, "the package README has no '## What is supported' section");
        var end = package.IndexOf("\n## ", begin + 1, StringComparison.Ordinal);
        AssertSupportTable(package[begin..end].Split('\n'));
    }

    [Fact]
    public void Readme_GeneratedAgentTable_EqualsTheMatrixAgents()
    {
        var table = Block(ReadText("README.md"), "provider-matrix").Where(l => l.StartsWith('|')).Skip(2).Select(Cells).ToList();
        var agents = Matrix().GetProperty("agents").EnumerateArray().ToList();

        Assert.Equal(agents.Count, table.Count);
        for (var i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            var aka = a.TryGetProperty("aka", out var k) ? $" ({k.GetString()})" : "";
            Assert.Equal($"**{a.GetProperty("fullName").GetString()}**{aka}", table[i][0]);
            Assert.Equal($"`{a.GetProperty("id").GetString()}`", table[i][1]);
            Assert.Equal(new[] { "instructions", "skills", "subagents" }.Select(f => a.GetProperty(f).GetString()), table[i].Skip(2));
        }
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("src/Bellatrix.Cli/Package/README.md")]
    [InlineData("docs/TESTING.md")]
    public void PublicDocs_ShowNoVerificationStatus(string path)
    {
        var text = ReadText(path.Split('/'));

        Assert.All(StatusWording, w => Assert.DoesNotContain(w, text, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("gen-readme-matrix.ps1")]
    [InlineData("export-website-data.ps1")]
    public void PublicGenerators_NeverReadTheInternalTracking(string script)
    {
        var text = ReadText("scripts", script);

        Assert.DoesNotMatch(new Regex(@"\.internal\b|\.cells\b|\.legend\b|\.status\b"), text);
    }

    private static void AssertSupportTable(string[] lines)
    {
        var m = Matrix();
        var table = lines.Where(l => l.StartsWith('|')).Select(Cells).ToList();
        var domains = m.GetProperty("domains").EnumerateArray().ToList();
        var languages = m.GetProperty("languages").EnumerateArray().ToList();
        var support = m.GetProperty("support").EnumerateArray().ToList();

        Assert.Equal(
            new[] { "Language" }.Concat(domains.Select(d => d.GetProperty("name").GetString() + (d.TryGetProperty("detail", out var x) ? $" ({x.GetString()})" : ""))),
            table[0]);
        Assert.Equal(languages.Count, table.Count - 2);
        for (var r = 0; r < languages.Count; r++)
        {
            var language = languages[r];
            var expected = domains.Select(d => support.Single(s =>
                s.GetProperty("language").GetString() == language.GetProperty("id").GetString() &&
                s.GetProperty("domain").GetString() == d.GetProperty("id").GetString()).GetProperty("supported").GetBoolean() ? Supported : NotAvailable);
            Assert.Equal(new[] { language.GetProperty("name").GetString()! }.Concat(expected), table[r + 2]);
        }

        foreach (var note in m.GetProperty("footnotes").EnumerateArray())
        {
            Assert.Contains(note.GetString(), lines);
        }
    }

    private static string[] Block(string text, string name)
    {
        var begin = text.IndexOf($"<!-- {name}:begin -->", StringComparison.Ordinal);
        var end = text.IndexOf($"<!-- {name}:end -->", StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, $"README.md has no {name} block");
        return text[begin..end].Split('\n');
    }

    private static Dictionary<string, HashSet<string?>> ConformanceLanguages()
        => JsonDocument.Parse(ReadText("scripts", "conformance", "languages.json"))
            .RootElement.GetProperty("languages").EnumerateArray()
            .ToDictionary(l => l.GetProperty("id").GetString()!, l => l.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).ToHashSet());

    private static List<JsonElement> Internal(JsonElement m, string array)
        => m.GetProperty("internal").GetProperty(array).EnumerateArray().ToList();

    private static string[] Cells(string line) => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static string Key(JsonElement c)
        => $"{c.GetProperty("language").GetString()}|{c.GetProperty("domain").GetString()}|{c.GetProperty("agent").GetString()}";

    private static string[] Ids(JsonElement m, string array)
        => m.GetProperty(array).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();

    private static JsonElement Matrix()
        => JsonDocument.Parse(ReadText("docs", "support-matrix.json")).RootElement.Clone();

    private static string ReadText(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
