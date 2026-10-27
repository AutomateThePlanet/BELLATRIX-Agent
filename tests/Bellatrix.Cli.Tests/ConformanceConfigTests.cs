using System.Runtime.CompilerServices;
using System.Text.Json;
using Bellatrix.Cli.Scaffolding;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// The conformance harness is data-driven (<c>scripts/conformance/agents.json</c>,
/// <c>languages.json</c>). These tests keep its tables in lock-step with the CLI, so adding an agent or a language
/// is a data change that cannot silently leave the harness (or the report) behind.
/// </summary>
public class ConformanceConfigTests
{
    [Fact]
    public void AgentTable_ListsExactlyTheCliAgents_InCliOrder()
    {
        var ids = Ids(Load("scripts", "conformance", "agents.json"), "agents");

        Assert.Equal(AgentSelection.Names, ids);
    }

    [Fact]
    public void AgentTable_EveryAgentHasTheFieldsTheHarnessReads()
    {
        foreach (var agent in Load("scripts", "conformance", "agents.json").GetProperty("agents").EnumerateArray())
        {
            var id = agent.GetProperty("id").GetString();
            Assert.True(agent.GetProperty("binaries").GetArrayLength() > 0, $"{id}: binaries");
            Assert.True(agent.GetProperty("headless").GetArrayLength() > 0, $"{id}: headless");
            Assert.Contains("{prompt}", agent.GetProperty("headless").EnumerateArray().Select(a => a.GetString()));
            Assert.True(agent.GetProperty("docs").GetArrayLength() > 0, $"{id}: docs URLs (verified headless syntax)");
            Assert.False(string.IsNullOrWhiteSpace(agent.GetProperty("transcript").GetString()), $"{id}: transcript");
            Assert.False(string.IsNullOrWhiteSpace(agent.GetProperty("cost").GetString()), $"{id}: cost note printed before a run");
        }
    }

    [Fact]
    public void SelfTestAgentTable_UsesOnlyCliAgentIds()
    {
        var ids = Ids(Load("scripts", "conformance", "selftest-agents.json"), "agents");

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.Contains(id, AgentSelection.Names));
    }

    [Fact]
    public void LanguageTable_ListsExactlyTheCliLanguages_WithKnownDomains()
    {
        var table = Load("scripts", "conformance", "languages.json");
        var domains = Ids(table, "domains");

        Assert.Equal(LanguageSelection.Known, Ids(table, "languages"));
        Assert.Equal(new[] { "desktop", "web", "mobile" }, domains);
        foreach (var language in table.GetProperty("languages").EnumerateArray())
        {
            var supported = language.GetProperty("domains").EnumerateArray().Select(d => d.GetString()!).ToArray();
            Assert.NotEmpty(supported);
            Assert.All(supported, d => Assert.Contains(d, domains));
            Assert.True(language.GetProperty("markerFiles").EnumerateObject().Any(), "markerFiles");
        }
    }

    [Fact]
    public void LanguageTable_EveryDomainFixtureAndAnswerKeyExists_AndMatchesTheDomain()
    {
        foreach (var domain in Load("scripts", "conformance", "languages.json").GetProperty("domains").EnumerateArray())
        {
            var fixture = domain.GetProperty("fixture").GetString()!;
            var dir = Path.Combine(RepoRoot(), "tests", "fixtures", "snapshots");
            Assert.True(File.Exists(Path.Combine(dir, fixture + ".json")), fixture + ".json");
            var key = Load("tests", "fixtures", "snapshots", fixture + ".answers.json");
            Assert.Equal(domain.GetProperty("id").GetString(), key.GetProperty("domain").GetString());
            Assert.Equal(fixture + ".json", key.GetProperty("fixture").GetString());
            Assert.Contains(key.GetProperty("synthetic").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
        }
    }

    private static string[] Ids(JsonElement table, string array)
        => table.GetProperty(array).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();

    private static JsonElement Load(params string[] parts)
    {
        var text = File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
