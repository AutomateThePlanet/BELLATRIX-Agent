<!-- lang:target -->
## Target Project

All generated mobile tests, page objects, sections, and models go into the BELLATRIX mobile test project
recorded in `bellatrix-mobile-project-structure` (placeholder `MyProject.MobileTests` — use the real
name):

```
MyProject.MobileTests/
  Pages/{ScreenName}/
    {ScreenName}.Map.cs
    {ScreenName}.Actions.cs
    {ScreenName}.Assertions.cs
  Pages/Shared/
  Components/
  Models/
  Tests/{FeatureArea}/
    {TestClassName}.cs
  Constants.cs
  ManualTestCaseAttribute.cs
  TestsInitialize.cs
  testFrameworkSettings.Debug.json
```

- **Namespaces**: `MyProject.MobileTests.Pages`, `.Pages.Shared`, `.Components`, `.Models`, `.Tests`
- **Build command**: `dotnet build MyProject.MobileTests/MyProject.MobileTests.csproj`
- **Bases**: Android `AndroidPage` / `AndroidTest` / `[Android(...)]`; iOS `IOSPage` / `IOSTest` / `[IOS(...)]`
- This project references `Bellatrix.Mobile` only (no Desktop reference; Core and Web come in transitively)

<!-- lang:lifecycle -->
### Example

```csharp
[TestFixture]
[Android(Constants.AppPath, Constants.AppId, Constants.PlatformVersion, Constants.DeviceName,
    Constants.StartActivity, Lifecycle.ReuseIfStarted)]  // class default — fast
public class ShopCartTests : AndroidTest
{
    public override void TestInit()
    {
        App.Create<CartPage>().GoTo();   // reset to a known screen instead of restarting the app
    }

    [Test, ManualTestCase("TC-142")]
    public void AddToCart_FromProductPage_IncrementsBadge() { /* ... */ }

    // RestartEveryTime: this test verifies the empty cart on first launch
    [Test, ManualTestCase("TC-100")]
    [Android(Constants.AppPath, Constants.AppId, Constants.PlatformVersion, Constants.DeviceName,
        Constants.StartActivity, Lifecycle.RestartEveryTime)]
    public void Cart_OnFirstLaunch_IsEmpty() { /* ... */ }
}
```

- Global config: `mobileSettings.executionSettings.defaultLifeCycle` = `"restart every time"` in
  `testFrameworkSettings.*.json` — leave it.
- Class default: `Lifecycle.ReuseIfStarted`; per-class or per-method override: `Lifecycle.RestartEveryTime`
  or `Lifecycle.RestartOnFail`.
- Per-test reset hook: `public override void TestInit()` (also `TestCleanup()`); never add your own
  `[SetUp]` / `[TearDown]`.
- iOS uses `[IOS(Constants.AppPath, Constants.BundleId, Constants.PlatformVersion, Constants.DeviceName, Lifecycle.…)]`
  and has no `GoTo()` — reset by navigating through the UI.

<!-- lang:critical-rules -->
### C# / BELLATRIX Rules

- **Prefer direct factories over XPath:** Android content-desc → `CreateByDescription<T>`, resource-id →
  `CreateById<T>("pkg:id/x")`, text → `CreateByText<T>`, UiSelector → `CreateByAndroidUIAutomator<T>`;
  iOS name → `CreateById<T>` / `CreateByName<T>`, predicate → `CreateByIOSNsPredicate<T>`. Use
  `CreateByXPath<T>` only for structural locators
- **No `App.Components.CreateByAccessibilityId`** on either platform; Android has no `CreateByName` /
  `CreateByTag`; iOS has no class-chain factory (rewrite as NSPredicate and re-validate)
- **Android resource-ids are fully qualified** in `CreateById` (`com.app:id/login`)
- **One platform namespace per file:** `using Bellatrix.Mobile.Android;` OR `using Bellatrix.Mobile.IOS;`
  — never both (CS0104 on `Button`, CS0121 on `ValidateIsVisible`, …)
- **Page bases:** `AndroidPage` must override `PackageName` and `ActivityName`; `IOSPage` has no
  overrides; never `WebPage` / `DesktopPage`
- **Tappable elements are `Button`:** `Label` has no `Click()`; rows/cells/links → `Button` or a custom
  component deriving from `Button`
- **Never drop to the raw driver:** no `WrappedDriver.FindElement(...)`, no `MobileBy.*` in project code —
  use `App.Components.Create*` / `CreateAllBy*` or nested factories on a parent component
- **Custom components** go in `Components/`: (a) derive from the closest built-in (`Button`,
  `ComboBox`, …) to add behavior, or (b) override its virtual members; use them in Map files via
  `App.Components.CreateByDescription<MyComponent>("…")`
- **Always brace control flow:** never `if (x) return;` or `if (row == null) continue;` without braces
- **File-scoped namespaces:** `namespace X;`
- **Partial classes** for all page object files; base class declared once (Map file)
- **`App.Create<TPage>()`**, never `new TPage()`
- **`CreateByXPath`** — capital P
- **Validate methods:** `ValidateIsVisible()`, `ValidateTextIs()`, `ValidateIsDisabled()`,
  `ValidateIsChecked()`, `ValidateIsOn()`, `ValidateNumberIs()` — mobile has NO `ValidateInnerTextIs`
  and NO `Validate…Contains`; use `Assert.That(x.GetText(), Does.Contain(…))` for partial text
- **No `Thread.Sleep`:** fluent waits (`ToBeVisible()`, `ToBeClickable()`, `ToHasContent()`,
  `ToNotExists().WaitToBe()`)
- **Touch actions end with `.Perform()`**
- **Tests use page objects only** — no `App.Components` in test methods
- **SPECTRA traceability:** `[ManualTestCase("TC-XXX")]` (project-local string attribute in
  `ManualTestCaseAttribute.cs` — create it if missing; NOT NUnit's `[TestCase]`, NOT
  `Bellatrix.Data.Attributes`) and update the SPECTRA file's `automated_by`
- **1 TC = 1 test method** — never stack `[ManualTestCase]` attributes, never merge TCs via `[TestCase]`
- **Separate test files by feature** — e.g. `Tests/Login/ShopLoginTests.cs`, `Tests/Cart/ShopCartTests.cs`
- **Build only:** `dotnet build MyProject.MobileTests/MyProject.MobileTests.csproj` until green — never
  `dotnet test` yourself
<!-- lang:po-step -->
3. Generate/edit page objects (3-file partial-class pattern per screen, one platform per file)
<!-- lang:po-rows -->
| Page object folder exists | **Edit** its Map / Actions / Assertions files — append new members |
| Page object folder missing | **Create** a folder with the 3 partial files |
<!-- lang:brace-rule -->
- **Always brace control flow** — every `if`/`else`/`for`/`foreach`/`while`/`do`/`using` body in braces,
  Allman style, no exceptions
