using Bellatrix.Execution;
using Bellatrix.Execution.Snapshots;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Snapshot store + narrow-query tests. Pure over node dictionaries — no live driver.
/// </summary>
public class SnapshotTests
{
    private static List<Dictionary<string, object>> SampleNodes() => new()
    {
        new() { ["Tag"] = "Button", ["ControlType"] = "Button", ["Name"] = "Login", ["AutomationId"] = "loginBtn" },
        new() { ["Tag"] = "Edit", ["ControlType"] = "Edit", ["Name"] = "Username", ["AutomationId"] = "user" },
        new() { ["Tag"] = "Text", ["ControlType"] = "Text", ["Name"] = "Welcome to the Login page" },
    };

    [Fact]
    public void Store_Save_ReturnsCompactReference_AndRoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bellatrix-snap-{Guid.NewGuid():N}");
        try
        {
            var store = new SnapshotStore(dir);
            var reference = store.Save("session-1", SampleNodes());

            Assert.Equal(3, reference.ElementCount);
            Assert.Equal("session-1", reference.SessionId);
            Assert.True(File.Exists(reference.Path));
            Assert.True(reference.Summary.ContainsKey("interactive"));
            Assert.Equal(2, Convert.ToInt32(reference.Summary["interactive"])); // Button + Edit, not Text

            var reloaded = store.Load(reference.Path);
            Assert.Equal(3, reloaded.Count);
            Assert.Equal("Button", reloaded[0]["ControlType"].ToString());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Query_ByControlType_MatchesButton()
    {
        var results = SnapshotQuery.Run(SampleNodes(), SnapshotQueryMode.ControlType, "Button");
        Assert.Single(results);
        Assert.Equal("loginBtn", results[0]["AutomationId"].ToString());
    }

    [Fact]
    public void Query_ByText_MatchesNameSubstring_CaseInsensitive()
    {
        var results = SnapshotQuery.Run(SampleNodes(), SnapshotQueryMode.Text, "login");
        Assert.Equal(2, results.Count); // "Login" button + "Welcome to the Login page" text
    }

    [Fact]
    public void Query_InteractiveOnly_ExcludesStaticText()
    {
        var results = SnapshotQuery.Run(SampleNodes(), SnapshotQueryMode.Interactive, null);
        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, n => n["ControlType"].ToString() == "Text");
    }

    // F2 — a ControlType-only interactive control (no Name, no AutomationId) must be RETAINED so the
    // locator specialist can still reach it; nameless non-interactive chrome stays pruned.
    [Fact]
    public void BuildElementDictionary_RetainsInteractiveControl_WithoutNameOrAutomationId()
    {
        var dict = DesktopService.BuildElementDictionary("Button", _ => null);

        Assert.NotNull(dict);
        Assert.Equal("Button", dict!["ControlType"].ToString());
        Assert.Equal("Button", dict["Tag"].ToString());
        Assert.False(dict.ContainsKey("Name"));
        Assert.False(dict.ContainsKey("AutomationId"));
    }

    [Fact]
    public void BuildElementDictionary_DropsNamelessNonInteractiveChrome()
    {
        var dict = DesktopService.BuildElementDictionary("Pane", _ => null);

        Assert.Null(dict);
    }

    [Fact]
    public void BuildElementDictionary_RetainsNamedElement_Unchanged()
    {
        var dict = DesktopService.BuildElementDictionary("Edit", attr => attr == "Name" ? "Username" : null);

        Assert.NotNull(dict);
        Assert.Equal("Username", dict!["Name"].ToString());
        Assert.Equal("Edit", dict["ControlType"].ToString());
    }
}
