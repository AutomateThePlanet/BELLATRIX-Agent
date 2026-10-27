using Bellatrix.Execution.Mobile;
using Bellatrix.Execution.SessionMounting;
using Bellatrix.Execution.Snapshots;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Mobile engine tests (M1): the pure snapshot-node parser and the shared session store carrying an
/// Appium row. No live Appium server — <see cref="MobileService.BuildElementDictionary"/> is pure over
/// an attribute lookup.
/// </summary>
public class MobileSnapshotTests
{
    [Fact]
    public void BuildElementDictionary_RetainsAndroidNode_WithResourceIdAndText()
    {
        var attrs = new Dictionary<string, string?>
        {
            ["resource-id"] = "com.app:id/login",
            ["text"] = "Login",
            ["class"] = "android.widget.Button",
            ["content-desc"] = "Login button",
        };
        var dict = MobileService.BuildElementDictionary("android.widget.Button", a => attrs.GetValueOrDefault(a));

        Assert.NotNull(dict);
        Assert.Equal("com.app:id/login", dict!["ResourceId"].ToString());
        Assert.Equal("Login", dict["Text"].ToString());
        Assert.Equal("Login button", dict["ContentDesc"].ToString());
        Assert.Equal("android.widget.Button", dict["Class"].ToString());
    }

    [Fact]
    public void BuildElementDictionary_RetainsIOSNode_WithNameLabelType()
    {
        var attrs = new Dictionary<string, string?>
        {
            ["name"] = "loginButton",
            ["label"] = "Log in",
            ["type"] = "XCUIElementTypeButton",
        };
        var dict = MobileService.BuildElementDictionary("XCUIElementTypeButton", a => attrs.GetValueOrDefault(a));

        Assert.NotNull(dict);
        Assert.Equal("loginButton", dict!["Name"].ToString());
        Assert.Equal("Log in", dict["Label"].ToString());
        Assert.Equal("XCUIElementTypeButton", dict["Type"].ToString());
    }

    [Fact]
    public void BuildElementDictionary_DropsNode_WithNoLocatorIdentity()
    {
        var dict = MobileService.BuildElementDictionary("android.widget.LinearLayout", _ => null);
        Assert.Null(dict);
    }

    // ===== M3: MobileSnapshotQuery (own key set + interactive ladder) =====

    private static List<Dictionary<string, object>> SampleNodes() => new()
    {
        new() { ["Tag"] = "android.widget.Button", ["Class"] = "android.widget.Button", ["ResourceId"] = "com.app:id/login", ["Text"] = "Login" },
        new() { ["Tag"] = "android.widget.EditText", ["Class"] = "android.widget.EditText", ["ResourceId"] = "com.app:id/user", ["ContentDesc"] = "Username field" },
        new() { ["Tag"] = "android.widget.TextView", ["Class"] = "android.widget.TextView", ["Text"] = "Welcome to Login" },
    };

    [Fact]
    public void Query_ByResourceId_MatchesSubstring()
    {
        var results = MobileSnapshotQuery.Run(SampleNodes(), MobileSnapshotQueryMode.ResourceId, "login");
        Assert.Single(results);
        Assert.Equal("Login", results[0]["Text"].ToString());
    }

    [Fact]
    public void Query_ByContentDesc_MatchesSubstring()
    {
        var results = MobileSnapshotQuery.Run(SampleNodes(), MobileSnapshotQueryMode.ContentDesc, "username");
        Assert.Single(results);
        Assert.Equal("com.app:id/user", results[0]["ResourceId"].ToString());
    }

    [Fact]
    public void Query_InteractiveOnly_ExcludesStaticText()
    {
        var results = MobileSnapshotQuery.Run(SampleNodes(), MobileSnapshotQueryMode.Interactive, null);
        Assert.Equal(2, results.Count); // Button + EditText, not TextView
        Assert.DoesNotContain(results, n => n["Class"].ToString()!.Contains("TextView"));
    }

    [Fact]
    public void Query_ByText_MatchesTextAndContentDesc_CaseInsensitive()
    {
        var results = MobileSnapshotQuery.Run(SampleNodes(), MobileSnapshotQueryMode.Text, "login");
        Assert.Equal(2, results.Count); // "Login" button + "Welcome to Login" text
    }

    [Fact]
    public void SessionStore_RoundTrips_AppiumRow_WithReplayCaps()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bellatrix-mob-{Guid.NewGuid():N}");
        try
        {
            var store = new SessionStore(Path.Combine(dir, "sessions.db"));
            var caps = MobileSessionManager.BuildCapsJson("android");
            store.Upsert(new SessionRecord
            {
                LogicalName = "m1",
                SessionId = "appium-session-123",
                DriverType = "Appium",
                ServerUrl = "http://127.0.0.1:4723",
                Caps = caps,
                LastActivityUtc = DateTime.UtcNow,
            });

            var record = store.TryGet("m1");
            Assert.NotNull(record);
            Assert.Equal("appium-session-123", record!.SessionId);
            Assert.Equal("Appium", record.DriverType);
            Assert.Contains("UiAutomator2", record.Caps);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
