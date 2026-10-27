using Bellatrix.Execution;
using Bellatrix.Execution.Mobile;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// The full page-source → snapshot path (<c>ParsePageSource</c>), not just the per-node builder. HtmlAgilityPack
/// lowercases element names; the snapshot must keep the driver's original casing because the Tag/ControlType keys
/// are what agents turn into XPath (<c>//ListItem[@Name='…']</c>, <c>//android.widget.TextView[…]</c>), and driver
/// XPath is case-sensitive. Found while generating the v.2 conformance fixtures through the real parser.
/// </summary>
public class PageSourceParsingTests
{
    [Fact]
    public void Desktop_KeepsMultiWordUiaTypeCasing()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <Window Name="App" AutomationId="" ClassName="Main">
              <List Name="History" AutomationId="HistoryList">
                <ListItem Name="1 + 1 = 2" AutomationId="" />
              </List>
              <CheckBox Name="Remember me" AutomationId="remember" />
              <ComboBox Name="Mode" AutomationId="modeCombo" />
              <TabItem Name="Settings" AutomationId="" />
            </Window>
            """;

        var nodes = DesktopService.ParsePageSource(xml);
        var types = nodes.Select(n => n["ControlType"].ToString()).ToList();

        Assert.Equal(new[] { "Window", "List", "ListItem", "CheckBox", "ComboBox", "TabItem" }, types);
        Assert.All(nodes, n => Assert.Equal(n["Tag"].ToString(), n["ControlType"].ToString()));
    }

    [Fact]
    public void Mobile_KeepsAndroidAndIosClassCasing()
    {
        const string android = """
            <?xml version='1.0' encoding='UTF-8' standalone='yes' ?>
            <hierarchy index="0" class="hierarchy">
              <android.widget.TextView class="android.widget.TextView" text="Views" content-desc="Views" resource-id="android:id/text1" />
            </hierarchy>
            """;
        const string ios = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AppiumAUT><XCUIElementTypeApplication name="TestApp"><XCUIElementTypeButton name="ComputeSumButton" label="Compute Sum" /></XCUIElementTypeApplication></AppiumAUT>
            """;

        Assert.Equal("android.widget.TextView", Assert.Single(MobileService.ParsePageSource(android))["Tag"].ToString());
        var iosTags = MobileService.ParsePageSource(ios).Select(n => n["Tag"].ToString()).ToList();
        Assert.Equal(new[] { "XCUIElementTypeApplication", "XCUIElementTypeButton" }, iosTags);
    }
}
