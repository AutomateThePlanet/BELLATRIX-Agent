<!-- lang:target -->
**Target project:** `domains.mobile.project` in `.bellatrix/config.json` (`{project.name}`, folder `{project.dir}`).
- **Namespaces**: `{project.rootNamespace}.Pages`, `.Pages.Shared`, `.Components`, `.Models`, `.Tests`
- **Platform namespace**: `Bellatrix.Mobile.Android` **or** `Bellatrix.Mobile.IOS` per file
- **Build command**: `{project.buildCommand}`

<!-- lang:page-objects -->
### Folder Structure
```
Pages/
  {ScreenName}/
    {ScreenName}.Map.cs          — base class, PackageName/ActivityName (Android), component properties
    {ScreenName}.Actions.cs      — action methods
    {ScreenName}.Assertions.cs   — Assert* methods using Validate methods
```

### Map File (`{ScreenName}.Map.cs`) — Android

```csharp
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.PageObjects;

namespace {project.rootNamespace}.Pages;

public partial class {ScreenName} : AndroidPage
{
    // Required by AndroidPage — GoTo() starts this activity directly
    protected override string PackageName => Constants.AppId;
    protected override string ActivityName => ".{ActivityName}";

    // accessibilityid (content-desc) → CreateByDescription
    public Button SubmitButton =>
        App.Components.CreateByDescription<Button>("Submit");

    // id (full resource-id) → CreateById
    public TextField EmailField =>
        App.Components.CreateById<TextField>("com.example.shop:id/email");

    public Label StatusLabel =>
        App.Components.CreateById<Label>("com.example.shop:id/status");

    // dynamic text → method with a parameter, still a direct factory
    public Button ProductRow(string productName) =>
        App.Components.CreateByText<Button>(productName);

    // androiduiautomator → CreateByAndroidUIAutomator
    public Button LogoutButton =>
        App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log out\").clickable(true)");
}
```

### Map File (`{ScreenName}.Map.cs`) — iOS

```csharp
using Bellatrix.Mobile.IOS;
using Bellatrix.Mobile.PageObjects;

namespace {project.rootNamespace}.Pages;

public partial class {ScreenName} : IOSPage
{
    // accessibilityid (name) → CreateById
    public Button SubmitButton =>
        App.Components.CreateById<Button>("submitButton");

    public TextField EmailField =>
        App.Components.CreateById<TextField>("emailField");

    // iospredicate → CreateByIOSNsPredicate
    public Label StatusLabel =>
        App.Components.CreateByIOSNsPredicate<Label>("type == \"XCUIElementTypeStaticText\" AND name == \"status\"");

    // dynamic text → interpolated predicate
    public Button ProductRow(string productName) =>
        App.Components.CreateByIOSNsPredicate<Button>($"type == \"XCUIElementTypeCell\" AND label CONTAINS \"{productName}\"");
}
```

Keep the public member names identical on both platforms so the Actions and Assertions files differ
only in their `using` line.

**Locator factory rule** (validated CLI strategy → factory):

| Strategy | Android | iOS |
|----------|---------|-----|
| `accessibilityid` | `CreateByDescription<T>` | `CreateById<T>` |
| `id` | `CreateById<T>` (full `pkg:id/x`) | `CreateById<T>` |
| `androiduiautomator` / `iospredicate` | `CreateByAndroidUIAutomator<T>` | `CreateByIOSNsPredicate<T>` |
| `iosclasschain` | — | rewrite as `iospredicate`, re-validate |
| `classname` | `CreateByClass<T>` | `CreateByClass<T>` |
| `xpath` | `CreateByXPath<T>` | `CreateByXPath<T>` |

Use `CreateByXPath` ONLY for structural locators. `App.Components.CreateByAccessibilityId` does not
exist — see `bellatrix-mobile-locators-and-waits`.

### Actions File (`{ScreenName}.Actions.cs`)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;

namespace {project.rootNamespace}.Pages;

public partial class {ScreenName}
{
    public void SubmitEmail(string email)
    {
        EmailField.SetText(email);
        SubmitButton.Click();
    }

    public void OpenProduct(string productName)
    {
        var row = ProductRow(productName);
        row.ScrollToVisible(ScrollDirection.Down);
        row.Click();
    }

    public {NextScreen} GoToNext()
    {
        SubmitButton.Click();
        return App.Create<{NextScreen}>();
    }
}
```

### Assertions File (`{ScreenName}.Assertions.cs`)

```csharp
using Bellatrix.Mobile.Android;

namespace {project.rootNamespace}.Pages;

public partial class {ScreenName}
{
    public void AssertPageLoaded()
    {
        SubmitButton.ValidateIsVisible();
    }

    public void AssertStatusIs(string expected)
    {
        StatusLabel.ValidateTextIs(expected);
    }
}
```

Swap `Bellatrix.Mobile.Android` for `Bellatrix.Mobile.IOS` in iOS files.

<!-- lang:custom-components -->
### Folder
```
{project.dir}/
  Components/
    {CustomComponentName}.cs
```

### Option A — Extend the closest built-in with new behavior

`Label` and the bare `AndroidComponent` / `IOSComponent` have no `Click()`; derive tappable custom
components from `Button`:

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;

namespace {project.rootNamespace}.Components;

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

### Option B — Override a built-in's virtual member

```csharp
using Bellatrix.Mobile.Android;

namespace {project.rootNamespace}.Components;

public class ResilientComboBox : ComboBox
{
    public override void SelectByText(string value)
    {
        this.ToBeClickable().WaitToBe();
        base.SelectByText(value);
    }
}
```

`Click()`, `GetText()`, `SetText()`, `SelectByText()`, `Check()`, `TurnOn()`, … are `virtual` on the
built-ins (exception: iOS `RadioButton.GetText()` is not virtual).

### Using custom components in Map files

```csharp
using {project.rootNamespace}.Components;

public ScrollingButton CheckoutButton =>
    App.Components.CreateByDescription<ScrollingButton>("Checkout");

public ResilientComboBox CountrySpinner =>
    App.Components.CreateById<ResilientComboBox>("com.example.shop:id/country");
```

<!-- lang:models -->
```csharp
namespace {project.rootNamespace}.Models;

public class {ModelName}
{
    public string PropertyOne { get; set; }
    public string PropertyTwo { get; set; }
}
```

<!-- lang:test-class -->
### Test Class Template (Android)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.NUnit;
using {project.rootNamespace}.Pages;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[Android(Constants.AppPath,
    Constants.AppId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Constants.StartActivity,
    Lifecycle.ReuseIfStarted)]  // default for generated classes — speed optimization
public class {TestClassName} : AndroidTest
{
    public override void TestInit()
    {
        App.Create<{ScreenName}>().GoTo();   // reset to a known screen when the app is reused
    }

    [Test]
    [ManualTestCase("TC-XXX")]
    [Category("Smoke")]
    public void {TestMethodName}()
    {
        var page = App.Create<{ScreenName}>();
        page.AssertPageLoaded();

        // Actions
        page.SubmitEmail("demo@example.com");

        // Assertions
        page.AssertStatusIs("Check your inbox");
    }
}
```

### Test Class Template (iOS)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.IOS;
using Bellatrix.Mobile.NUnit;
using {project.rootNamespace}.Pages;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[IOS(Constants.AppPath,
    Constants.BundleId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Lifecycle.ReuseIfStarted)]
public class {TestClassName} : IOSTest
{
    [Test]
    [ManualTestCase("TC-XXX")]
    [Category("Smoke")]
    public void {TestMethodName}()
    {
        var page = App.Create<{ScreenName}>();
        page.AssertPageLoaded();
        page.SubmitEmail("demo@example.com");
        page.AssertStatusIs("Check your inbox");
    }
}
```

`[ManualTestCase("TC-XXX")]` is the project-local attribute in `ManualTestCaseAttribute.cs` (create it
if missing — see `bellatrix-mobile-project-structure`). Never use NUnit's `[TestCase]` for traceability.

<!-- lang:code-rules -->
### Syntax — Avoid Build Errors
1. **File-scoped namespaces**: `namespace X;` (NOT `namespace X { }`)
2. **Partial classes**: all page object files use `public partial class`; base class declared once (Map)
3. **Page base**: `AndroidPage` (override `PackageName` and `ActivityName`) or `IOSPage` — NOT `WebPage` / `DesktopPage`
4. **One platform namespace per file**: `using Bellatrix.Mobile.Android;` **or** `using Bellatrix.Mobile.IOS;` — both together make `Button`, `Label`, … ambiguous (CS0104) and `ValidateIsVisible`, `ValidateTextIs`, … ambiguous (CS0121)
5. **XPath capital P**: `CreateByXPath` (NOT `CreateByXpath`)
6. **No `App.Components.CreateByAccessibilityId`**: Android → `CreateByDescription`; iOS → `CreateById`
7. **Android resource-ids are fully qualified**: `CreateById<T>("com.app:id/login")`
8. **Validate methods**: `ValidateIsVisible()`, `ValidateTextIs()`, `ValidateIsDisabled()`, `ValidateIsChecked()`, `ValidateIsOn()` … — mobile has NO `ValidateInnerTextIs` and NO `Validate…Contains`
9. **Tappable = `Button`**: `Label` has no `Click()`; rows/cells/links you tap are `Button` (or a custom component deriving from it)
10. **Touch-action chains end with `.Perform()`**

### Architecture
11. **Page instantiation**: `App.Create<TPage>()` (NOT `new TPage()`)
12. **Lifecycle**: generated test classes default to `Lifecycle.ReuseIfStarted` in the `[Android]` / `[IOS]` attribute; the global config stays `restart every time`. Switch a class — or one method via a method-level attribute — to `RestartEveryTime` only when state cannot be reset in `TestInit()` (see the generator agent's lifecycle policy)
13. **No direct components in tests**: tests call page object methods only
14. **Generic methods** for dynamic elements (product names, row titles)
15. **No URL navigation** — use `App.Create<T>()`, `GoTo()` (Android), or tap through the UI
16. **No raw driver lookups** — never `WrappedDriver.FindElement(...)` / `MobileBy.*` in project code
17. **No `Thread.Sleep`** — use fluent waits (`ToBeVisible()`, `ToBeClickable()`, `ToHasContent()`, `ToNotExists()`)

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `foreach`, `while`, `do`, and `using` block MUST use `{ }` braces — even for
single statements. Braces on their own line (Allman style).

```csharp
// WRONG
foreach (var row in rows)
{
    if (!row.IsVisible) continue;
    row.ValidateIsVisible();
}

// CORRECT
foreach (var row in rows)
{
    if (!row.IsVisible)
    {
        continue;
    }

    row.ValidateIsVisible();
}
```

Applies everywhere — page objects, actions, assertions, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `App.Components.CreateByAccessibilityId<Button>("x")` | Android `CreateByDescription<Button>("x")`; iOS `CreateById<Button>("x")` |
| `App.Components.CreateById<Button>("login")` (Android) | `CreateById<Button>("com.app:id/login")` or `CreateByIdContaining<Button>("login")` |
| `App.Components.CreateByXpath<T>()` | `App.Components.CreateByXPath<T>()` |
| `label.Click()` | Create the element as `Button` |
| `element.ValidateInnerTextIs("x")` | `element.ValidateTextIs("x")` |
| `new LoginPage()` | `App.Create<LoginPage>()` |
| `public partial class LoginPage : AndroidPage { }` without overrides | override `PackageName` and `ActivityName` |
| `App.TouchActions.Swipe(...);` | `App.TouchActions.Swipe(...).Perform();` |
| `namespace X { }` | `namespace X;` |

<!-- lang:build -->
```bash
{project.buildCommand}
```

Common build errors:
- **CS0104 ambiguous reference** (`Button`, `Label`) → the file imports both `Bellatrix.Mobile.Android` and `Bellatrix.Mobile.IOS`
- **CS1061 no `CreateByAccessibilityId` / `CreateByName` (Android)** → use the platform's factory
- **CS0534 does not implement `PackageName` / `ActivityName`** → add the overrides to the Android Map file
- **CS0311 type cannot be used as `T`** → the Validate method is not supported by that component (e.g. `ValidateIsDisabled` on `Label`)
- Missing `using` for `ScrollDirection` / `Lifecycle` (`Bellatrix.Mobile`), `AndroidComponent` (`Bellatrix.Mobile.Controls.Android`), `ScreenOrientation` (`OpenQA.Selenium`)

<!-- lang:run-command -->
```bash
dotnet test {project.path} --filter "FullyQualifiedName~{TestMethodName}" --no-build --logger "console;verbosity=detailed"
```

(The user starts Appium and the device/emulator before running it.)

<!-- lang:output -->
### Files Created/Modified

```
[CREATED] Pages/{ScreenName}/{ScreenName}.Map.cs
[CREATED] Pages/{ScreenName}/{ScreenName}.Actions.cs
[CREATED] Pages/{ScreenName}/{ScreenName}.Assertions.cs
[EDITED]  Pages/{ExistingScreen}/{ExistingScreen}.Map.cs — added 3 new properties
[EDITED]  Pages/{ExistingScreen}/{ExistingScreen}.Actions.cs — added 2 new methods
[CREATED] Models/{ModelName}.cs
[CREATED] Tests/{FeatureArea}/{TestClassName}.cs
[EDITED]  bellatrix-mobile-project-structure skill — added Pages/{ScreenName}
```

### Build Verification
```
Build succeeded: {project.buildCommand}
```
<!-- lang:edit-split -->
- **Keep the partial-class split** — all files of one screen share one class name
<!-- lang:page-files -->
Each screen gets a dedicated folder with three partial files:

| File | Contains | Never contains |
|------|----------|----------------|
| **Map** | page base class, platform metadata, one property per validated locator | logic, assertions |
| **Actions** | business workflow methods; navigation methods return the next page object | assertions |
| **Assertions** | `Assert*` methods built on Validate methods | actions |

