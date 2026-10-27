using System.Runtime.CompilerServices;
using System.Text.Json;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// <c>docs/support-matrix.json</c> is the single source of the language × domain × agent matrix. These
/// tests keep it complete and in step with the CLI, and keep README.md's generated table equal to it
/// (<c>scripts/gen-readme-matrix.ps1</c> regenerates the README block).
/// </summary>
public class SupportMatrixReadmeTests
{
    private const string Arrow = "←";

    [Fact]
    public void Matrix_ListsExactlyTheCliAgentsLanguagesAndDomains()
    {
        var m = Matrix();

        Assert.Equal(AgentSelection.Names, Ids(m, "agents"));
        Assert.Equal(LanguageSelection.Known, Ids(m, "languages"));
        Assert.Equal(new[] { "desktop", "web", "mobile" }, Ids(m, "domains"));
    }

    [Fact]
    public void Matrix_HasOneCellPerLanguageDomainAgent_WithALegendStatus()
    {
        var m = Matrix();
        var statuses = m.GetProperty("legend").EnumerateArray().Select(l => l.GetProperty("status").GetString()).ToHashSet();
        var cells = m.GetProperty("cells").EnumerateArray().ToList();
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
    public void Matrix_MissingCells_AreExactlyTheDomainsTheLanguageLacks()
    {
        var languages = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "conformance", "languages.json")))
            .RootElement.GetProperty("languages").EnumerateArray()
            .ToDictionary(l => l.GetProperty("id").GetString()!, l => l.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).ToHashSet());

        foreach (var cell in Matrix().GetProperty("cells").EnumerateArray())
        {
            var supported = languages[cell.GetProperty("language").GetString()!].Contains(cell.GetProperty("domain").GetString());
            var missing = cell.GetProperty("status").GetString()!.StartsWith("MISSING", StringComparison.Ordinal);
            Assert.True(supported != missing, Key(cell));
        }
    }

    [Fact]
    public void Matrix_RowsCoverEveryLanguageDomainOnce()
    {
        var m = Matrix();
        var rows = m.GetProperty("rows").EnumerateArray()
            .Select(r => $"{r.GetProperty("language").GetString()}|{r.GetProperty("domain").GetString()}").ToList();

        Assert.Equal(Ids(m, "languages").Length * Ids(m, "domains").Length, rows.Count);
        Assert.Equal(rows.Count, rows.Distinct().Count());
    }

    [Fact]
    public void Readme_GeneratedTable_EqualsTheMatrix()
    {
        var m = Matrix();
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md")).Replace("\r\n", "\n");
        var begin = readme.IndexOf("<!-- support-matrix:begin -->", StringComparison.Ordinal);
        var end = readme.IndexOf("<!-- support-matrix:end -->", StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, "README.md has no support-matrix block");
        var block = readme[begin..end].Split('\n');
        var table = block.Where(l => l.StartsWith('|')).Select(Cells).ToList();
        var agents = m.GetProperty("agents").EnumerateArray().ToList();

        Assert.Equal(agents.Select(a => a.GetProperty("name").GetString()), table[0].Skip(1).Take(agents.Count));
        var rows = m.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(rows.Count, table.Count - 2);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = table[r + 2];
            Assert.Equal(row.GetProperty("label").GetString(), line[0]);
            Assert.Equal(row.GetProperty("evidence").GetString(), line[^1]);
            for (var a = 0; a < agents.Count; a++)
            {
                var shown = line[a + 1];
                for (var left = a; shown == Arrow; left--)
                {
                    shown = line[left];
                }

                var expected = Cell(m, row.GetProperty("language").GetString()!, row.GetProperty("domain").GetString()!, agents[a].GetProperty("id").GetString()!);
                Assert.Equal(expected, shown);
            }
        }

        foreach (var legend in m.GetProperty("legend").EnumerateArray())
        {
            Assert.Contains($"- **{legend.GetProperty("status").GetString()}:** {legend.GetProperty("meaning").GetString()}", block);
        }

        foreach (var note in m.GetProperty("notes").EnumerateArray())
        {
            Assert.Contains($"- {note.GetString()}", block);
        }
    }

    [Fact]
    public void Readme_GeneratedProviderTable_EqualsTheMatrixAgents()
    {
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md")).Replace("\r\n", "\n");
        var begin = readme.IndexOf("<!-- provider-matrix:begin -->", StringComparison.Ordinal);
        var end = readme.IndexOf("<!-- provider-matrix:end -->", StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, "README.md has no provider-matrix block");
        var table = readme[begin..end].Split('\n').Where(l => l.StartsWith('|')).Skip(2).Select(Cells).ToList();
        var agents = Matrix().GetProperty("agents").EnumerateArray().ToList();

        Assert.Equal(agents.Count, table.Count);
        for (var i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            var aka = a.TryGetProperty("aka", out var k) ? $" ({k.GetString()})" : "";
            Assert.Equal($"**{a.GetProperty("fullName").GetString()}**{aka}", table[i][0]);
            Assert.Equal(new[] { "instructions", "skills", "subagents", "delegation" }.Select(f => a.GetProperty(f).GetString()), table[i].Skip(1));
        }
    }

    private static string Cell(JsonElement m, string language, string domain, string agent)
        => m.GetProperty("cells").EnumerateArray().Single(c => Key(c) == $"{language}|{domain}|{agent}").GetProperty("status").GetString()!;

    private static string[] Cells(string line) => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static string Key(JsonElement c)
        => $"{c.GetProperty("language").GetString()}|{c.GetProperty("domain").GetString()}|{c.GetProperty("agent").GetString()}";

    private static string[] Ids(JsonElement m, string array)
        => m.GetProperty(array).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();

    private static JsonElement Matrix()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "docs", "support-matrix.json"))).RootElement.Clone();

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
