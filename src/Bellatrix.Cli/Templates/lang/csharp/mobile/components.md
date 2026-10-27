## CLI Verb → Component Member

| CLI verb | Component member (generated code) |
|----------|-----------------------------------|
| `click` / `tap` | `Click()` (`Button`, `ImageButton`, `RadioButton`); `Check()` / `TurnOn()` for checkboxes / switches |
| `type-text` | `SetText(…)` (`TextField`), `SetPassword(…)` (`Password`), `SetNumber(…)` (`Number`) |
| `clear` | `SetText("")` |
| `get-text` | `GetText()` |
| `get-attribute` | `GetAttribute(name)` |
| `is-visible` / `is-enabled` | `IsVisible` / `IsDisabled` (assert with `ValidateIsVisible()` / `ValidateIsNotDisabled()`) |
| `scroll-to-element` | `ScrollToVisible(ScrollDirection.Down)` |
| `swipe` | `App.TouchActions.Swipe(startX, startY, endX, endY, durationMs).Perform()` |

## Component Hierarchy

Every mobile component derives from `Component<TDriver, AppiumElement>`; all except iOS `Grid<T>` and iOS `Progress` also derive from the platform base:

| Platform | Base class | Component namespace | Factories, waits, validations |
|----------|-----------|---------------------|-------------------------------|
| Android | `AndroidComponent` (`Bellatrix.Mobile.Controls.Android`) | `Bellatrix.Mobile.Android` | `Bellatrix.Mobile.Android` |
| iOS | `IOSComponent` (`Bellatrix.Mobile.Controls.IOS`) | `Bellatrix.Mobile.IOS` | `Bellatrix.Mobile.IOS` |

The names are identical on both platforms (`Button`, `TextField`, …) but they are **different types**.
Importing both `Bellatrix.Mobile.Android` and `Bellatrix.Mobile.IOS` in the same file makes the type names
(`Button`, `Label`, …) ambiguous (CS0104) and the shared `Validate*` calls ambiguous (CS0121) — keep Android and
iOS code in separate files.

## Android Components (`Bellatrix.Mobile.Android`)

| Component | Native class | Key Methods | Key Properties |
|-----------|-------------|-------------|----------------|
| `Button` | `android.widget.Button`, clickable rows | `Click()`, `GetText()` | `IsDisabled` |
| `ImageButton` | `android.widget.ImageButton` | `Click()`, `GetText()` | `IsDisabled` |
| `TextField` | `android.widget.EditText` | `SetText(string)`, `GetText()` | `IsDisabled` |
| `Password` | `android.widget.EditText` (password) | `SetPassword(string)`, `GetPassword()` | `IsDisabled` |
| `Number` | `android.widget.EditText` (numeric) | `SetNumber(int)`, `GetNumber()` | `IsDisabled` |
| `Label` | `android.widget.TextView` | `GetText()` | — (no `Click()`, no `IsDisabled`) |
| `CheckBox` | `android.widget.CheckBox` | `Check(bool isChecked = true)`, `Uncheck()`, `GetText()` | `IsChecked`, `IsDisabled` |
| `RadioButton` | `android.widget.RadioButton` | `Click()`, `GetText()` | `IsChecked`, `IsDisabled` |
| `RadioGroup` | `android.widget.RadioGroup` | `ClickByText(string)`, `ClickByIndex(int)`, `GetChecked()`, `GetAll()` | — |
| `Switch` | `android.widget.Switch` | `TurnOn()`, `TurnOff()`, `GetText()` | `IsOn`, `IsDisabled` |
| `ToggleButton` | `android.widget.ToggleButton` | `TurnOn()`, `TurnOff()`, `GetText()` | `IsOn`, `IsDisabled` |
| `ComboBox` | `android.widget.Spinner` | `SelectByText(string)`, `GetText()` | `IsDisabled` |
| `SeekBar` | `android.widget.SeekBar` | `Set(double percentage)` | `IsDisabled` |
| `Progress` | `android.widget.ProgressBar` | — | `IsDisabled` |
| `Image` | `android.widget.ImageView` | — | — |
| `Tabs<T>` / `Grid<T>` | tab strip / grid container | `GetAll(string searchClass)` | — |
| `AndroidComponent` | any (container, custom) | nested `Create*` | — |

## iOS Components (`Bellatrix.Mobile.IOS`)

| Component | Native type | Key Methods | Key Properties |
|-----------|------------|-------------|----------------|
| `Button` | `XCUIElementTypeButton`, `XCUIElementTypeCell` | `Click()`, `GetText()` | `IsDisabled` |
| `ImageButton` | `XCUIElementTypeButton` (icon) | `Click()`, `GetText()` | `IsDisabled` |
| `TextField` | `XCUIElementTypeTextField` | `SetText(string)`, `GetText()` | `IsDisabled` |
| `Password` | `XCUIElementTypeSecureTextField` | `SetPassword(string)`, `GetPassword()` | `IsDisabled` |
| `Number` | `XCUIElementTypeTextField` (numeric) | `SetNumber(int)`, `GetNumber()` | `IsDisabled` |
| `Label` | `XCUIElementTypeStaticText` | `GetText()` | — (no `Click()`, no `IsDisabled`) |
| `CheckBox` | `XCUIElementTypeSwitch` used as a check | `Check(bool isChecked = true)`, `Uncheck()`, `GetText()` | `IsChecked`, `IsDisabled` |
| `RadioButton` | custom radio control | `Click()`, `GetText()` | `IsChecked`, `IsDisabled` |
| `RadioGroup` | custom radio group | `ClickByText(string)`, `ClickByIndex(int)`, `GetChecked()`, `GetAll()` | — |
| `ToggleButton` | `XCUIElementTypeSwitch` | `TurnOn()`, `TurnOff()`, `GetText()` | `IsOn`, `IsDisabled` |
| `ComboBox` | picker / pop-up button | `SelectByText(string)`, `GetText()` | `IsDisabled` |
| `SeekBar` | `XCUIElementTypeSlider` | `Set(double value)` | `IsDisabled` |
| `Image` | `XCUIElementTypeImage` | — | — |
| `Tabs<T>` / `Grid<T>` | tab bar / grid container | `GetAll(string searchClass)` | — |
| `IOSComponent` | any (container, custom) | nested `Create*` | — |

iOS has **no `Switch` component** — use `ToggleButton` (or `CheckBox`) for `XCUIElementTypeSwitch`. Do
not use the iOS `Progress` type: it derives from `AndroidComponent` (it resolves an `AndroidDriver`, and the iOS
`To*` waits reject it). iOS `Grid<T>` is not an `IOSComponent` either (it derives directly from `Component`).

**Tapping rule:** `Label` has no `Click()`. A tappable row, cell, or text link must be created as
`Button` (or a custom component deriving from `Button`).

---

## Creating Components

### Single Component (`App.Components`)

```csharp
// ---- Android (using Bellatrix.Mobile.Android;) ----
App.Components.CreateByDescription<Button>("Log in")                          // content-desc — PREFERRED
App.Components.CreateById<TextField>("com.example.shop:id/username")          // full resource-id
App.Components.CreateByIdContaining<Button>("login")                          // resource-id fragment
App.Components.CreateByText<Label>("Order placed")                            // exact text
App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log in\")")
App.Components.CreateByClass<SeekBar>("android.widget.SeekBar")
App.Components.CreateByXPath<Button>("//android.widget.Button[@text='Log in']")

// ---- iOS (using Bellatrix.Mobile.IOS;) ----
App.Components.CreateById<Button>("loginButton")                              // accessibility id — PREFERRED
App.Components.CreateByName<Button>("Log in")
App.Components.CreateByIOSNsPredicate<Password>("type == \"XCUIElementTypeSecureTextField\"")
App.Components.CreateByValueContaining<Label>("Total")
App.Components.CreateByClass<TextField>("XCUIElementTypeTextField")
App.Components.CreateByXPath<Button>("//XCUIElementTypeButton[@name='loginButton']")
```

### Component Collections (`CreateAllBy*`)

```csharp
// Returns ComponentsList<T, ...> — IEnumerable<T> with an indexer and Count()
var rows = App.Components.CreateAllById<Label>("com.example.shop:id/row_title");      // Android
var cells = App.Components.CreateAllByIOSNsPredicate<Button>("type == \"XCUIElementTypeCell\""); // iOS

var count = rows.Count();
var first = rows[0];
foreach (var row in rows)
{
    row.ValidateIsVisible();
}
```

### Nested Component Creation

```csharp
// Android — scope the search to a container
var products = App.Components.CreateById<AndroidComponent>("com.example.shop:id/products");
var pixel = products.CreateByText<Button>("Pixel 9");
var titles = products.CreateAllByClass<Label>("android.widget.TextView");

// iOS — nested factories include CreateByAccessibilityId / CreateByTag
var table = App.Components.CreateByIOSNsPredicate<IOSComponent>("type == \"XCUIElementTypeTable\"");
var cell = table.CreateByAccessibilityId<Button>("row_pixel9");
```

`AndroidComponent` needs `using Bellatrix.Mobile.Controls.Android;`, `IOSComponent` needs
`using Bellatrix.Mobile.Controls.IOS;`.

### MANDATORY: Use BELLATRIX Factories — Never Drop to the Raw Driver

Inside page objects, actions, sections, custom components, and tests, ALWAYS locate elements through
`App.Components.Create*` / `CreateAllBy*` or a parent component's nested factories. **Never** call
`WrappedDriver.FindElement(...)`, `driver.FindElements(MobileBy.…)`, or raw Appium `By` locators.

**Why:** factory-created components carry wait strategies, validations, BDD logging, screenshots on
failure, and the self-healing pipeline. Raw lookups bypass all of it and produce flaky tests.

```csharp
// WRONG — raw Appium bypasses BELLATRIX
var cells = button.WrappedDriver.FindElements(MobileBy.AccessibilityId("row"));

// CORRECT — BELLATRIX collection with full framework support (iOS)
var cells = App.Components.CreateAllByAccessibilityId<Button>("row");
```

Raw driver access is acceptable only for capabilities BELLATRIX does not expose — never to find or tap
elements.

### Custom Components

Extend a built-in component when you need control-level behavior it lacks (see
`bellatrix-mobile-generate-code` → Custom Components):

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;

namespace MyProject.MobileTests.Components;

public class ScrollingButton : Button
{
    public void ScrollAndTap()
    {
        this.ToExists().ToBeClickable().WaitToBe();
        ScrollToVisible(ScrollDirection.Down);
        Click();
    }

    public string Caption => GetText();

    public bool IsSelectedTab => GetAttribute("selected") == "true";
}
```

---

## Base Component Members (all components)

```csharp
// Presence and visibility
component.IsPresent        // bool — exists in the hierarchy
component.IsVisible        // bool — displayed

// Geometry
component.Location         // System.Drawing.Point
component.Size             // System.Drawing.Size

// Identity
component.ComponentName
component.PageName
component.By               // the find strategy (By.Value is the locator string)

// Attributes — raw platform attribute names
component.GetAttribute("content-desc")   // Android
component.GetAttribute("resource-id")    // Android
component.GetAttribute("checked")        // Android ("true" / "false")
component.GetAttribute("value")          // iOS
component.GetAttribute("label")          // iOS

// Actions
component.ScrollToVisible(ScrollDirection.Down)   // Up | Down | Left | Right (Bellatrix.Mobile)
component.WaitToBe()                              // force location + pending waits now
component.TakeScreenshot()                        // element screenshot, returns the file path
```
