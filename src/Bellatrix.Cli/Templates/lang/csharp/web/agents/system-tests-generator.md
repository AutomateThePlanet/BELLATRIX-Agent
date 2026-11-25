<!-- lang:page-objects-step -->
3. Generate/edit page objects (3-part partial pattern per page; sections for blocks shared by ≥2 pages)
<!-- lang:target-project -->
## Target Project (C# / .NET)

All generated web tests, page objects, sections, and models go into the BELLATRIX web test project named in `.bellatrix/config.json` (`domains.web.project`; `{project.dir}` = the folder of its `path`):

```
{project.dir}/
  Pages/{PageName}/
    {PageName}.Map.cs
    {PageName}.Actions.cs
    {PageName}.Assertions.cs
  Sections/{SectionName}/
    {SectionName}.Map.cs
    {SectionName}.Actions.cs
    {SectionName}.Assertions.cs
  Components/
    {CustomComponent}.cs
  Models/
    {ModelName}.cs
  Tests/{FeatureArea}/
    {FeatureArea}Tests.cs
  ManualTestCaseAttribute.cs
  TestsInitialize.cs
  testFrameworkSettings.Debug.json
```

- **Namespaces**: `{project.rootNamespace}.Pages`, `.Sections`, `.Components`, `.Models`, `.Tests`
- **Build command**: `{project.buildCommand}`
- **Base classes**: pages `WebPage` (non-generic, `Bellatrix.Web`), sections `WebSection`, tests `WebTest` (`Bellatrix.Web.NUnit`; `Bellatrix.Web.MSTest` only if the project already uses MSTest)
- **SPECTRA traceability attribute**: `[ManualTestCase("TC-XXX")]` from `ManualTestCaseAttribute.cs` in the project root namespace. If it does not exist yet, create it:

```csharp
using System;

namespace {project.rootNamespace};

/// <summary>
/// Links an automated test to a SPECTRA manual test case for traceability.
/// Unlike NUnit's [TestCase], this attribute does not affect test parameterization —
/// it is purely a metadata marker used by SPECTRA coverage scanning.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ManualTestCaseAttribute : Attribute
{
    public string TestCaseId { get; }

    public ManualTestCaseAttribute(string testCaseId)
    {
        TestCaseId = testCaseId;
    }
}
```

Usage (one per method — never stacked):

```csharp
[Test]
[ManualTestCase("TC-210")]
[Category("Smoke")]
public void ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal()
{
    var cartPage = App.GoTo<CartPage>();
    cartPage.ApplyCoupon("happybirthday");
    cartPage.AssertCouponApplied();
    cartPage.AssertTotalPrice("54.00");
}
```

<!-- lang:lifecycle -->
## Browser Lifecycle Policy (Speed Optimization)

Two lifecycle scopes exist and they are intentionally different:

| Scope | Setting | Value | Why |
|-------|---------|-------|-----|
| **Global default** (`testFrameworkSettings.*.json`) | `webSettings.executionSettings.defaultLifeCycle` | `"restart every time"` | Safe default for ad-hoc runs and CI isolation |
| **Test class attribute** (default for generated classes) | `[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]` | `ReuseIfStarted` | Keeps one browser across the class's tests for speed |

### Rules

1. **Default for new test classes: `Lifecycle.ReuseIfStarted`.** Generate `[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]` unless the tests need a clean browser.
2. **Do NOT change the global config.** Leave `defaultLifeCycle` as `"restart every time"`; the class attribute overrides it.
3. **Switch a class (or one method) to `Lifecycle.RestartEveryTime` (or `RestartOnFail`) when:**
   - the test logs in, logs out, or otherwise changes auth cookies / tokens
   - the test leaves server- or client-side state the next test would see (cart contents, local/session storage, user settings) and cleanup isn't reliable
   - the test verifies first-visit behavior (cookie banner, onboarding, default sort/currency)
   - the test closes windows or crashes the page
4. **Prefer cleanup first:** reset state in `TestInit()` / `TestCleanup()` overrides (`App.Cookies.DeleteAllCookies()`, `App.Browser.ClearLocalStorage()`, `App.Browser.ClearSessionStorage()`, emptying the cart via the page object) and keep `ReuseIfStarted`. Every test still opens its own start page (`App.GoTo<TPage>()`).
5. **Per-method override:** `[Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]` on a single test method overrides the class attribute for that test only.
6. **Document the override** with a comment above the attribute.

### Example

```csharp
[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]   // class default — fast
public class CartTests : WebTest
{
    [Test, ManualTestCase("TC-210")]
    public void ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }

    // RestartEveryTime: verifies the cookie banner shown on the first visit
    [Test, ManualTestCase("TC-200")]
    [Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]
    public void FirstVisit_ShowsCookieBanner() { /* ... */ }
}
```

<!-- lang:critical-rules -->
## Critical Rules (C#)

- **`WebPage` base class (non-generic)** on the page's Actions part, with `public override string Url => "…";` and `public override void Open()`. NOT `DesktopPage`, NOT a generic WebPage with type arguments, NOT the obsolete `NavigatablePage`. Sections inherit `WebSection`
- **`CreateByXpath` — lowercase p.** `CreateByXPath` / `CreateAllByXPath` do not exist in BELLATRIX Web (they are desktop/mobile spellings)
- **Prefer direct factories over XPath:** `[data-testid]` → `CreateByCss<T>("[data-testid='…']")`; static id → `CreateById<T>("…")`; `name` → `CreateByCss<T>("[name='…']")`; exact link text → `CreateByLinkText<Anchor>("…")`. Reserve `CreateByXpath` for text-based (`normalize-space()`) or structural locators (label → input, row → cell) and dynamic fragments. There is NO `CreateByDataTestId` and NO `CreateByName`
- **Component types from the live element:** `Button`, `TextField`, `Password`, `Email`, `Phone`, `Number`, `CheckBox`, `RadioButton`, `Select`, `Option`, `TextArea`, `Anchor`, `Label`, `Heading`, `Span`, `Div`, `Image`, `InputFile`, `Frame`, `Table`. There is no `H1`, `ListItem`, or `ComboBox`. `Div`/`Span`/`Label`/`Heading` have no `Click()` — type clickable non-buttons as `Button`
- **Validate methods:** `ValidateIsVisible()`, `ValidateInnerTextIs()`, `ValidateInnerTextContains()` (web HAS contains), `ValidateValueIs()`, `ValidateIsChecked()`, `ValidateIsDisabled()`/`ValidateIsNotDisabled()`, `ValidateHrefIs()` — only on components that support them (`Select` → validate `GetSelected()`; `Password` has no inner text). There is no `ValidateExists`/`ValidateAttributeIs`. Use `Assert.That(...)` (test files) / `App.Assert` (page objects) only for URL/title strings, counts, and arbitrary attributes. `App.Browser.Url` is a `Uri` — call `.ToString()`
- **Waits:** fluent `ToBeVisible()`, `ToBeClickable()`, `ToHasContent()`, `ToNotBeVisible()`, `ToExists()`, `ToNotExists()` (+ `WaitToBe()` to force), and `App.Browser.WaitUntilReady()` / `WaitForAjax()`, `App.Navigation.WaitForPartialUrl(…)`. Timeouts are in seconds. Never `Thread.Sleep`
- **Never drop to raw Selenium** for finding or clicking: no `WrappedDriver.FindElement(s)`, `By.*`, or `WebDriverWait` in test-project code. Use `App.Components.Create*` / `CreateAllBy*`, scoped `component.CreateBy…`, and inside custom components `this.Create…` or the protected `ComponentCreateService`. Use `App.Browser` (frames, tabs), `App.Dialogs` (native alerts), `App.Interactions` (hover chains, drag-and-drop), `App.JavaScript`, `App.Cookies` for low-level needs
- **Custom components when built-ins aren't enough:** under `Components/`, either a new component inheriting `Bellatrix.Web.Component` or a child of an existing component overriding its virtual members (e.g. `Button.Click()`); use it via `App.Components.CreateByCss<MyComponent>(…)`. Never modify BELLATRIX source
- **Navigation:** `App.Navigation.Navigate(url)` (NOT `App.Browser.NavigateToUrl`); pages via `App.GoTo<TPage>()` (opens) / `App.Create<TPage>()` (already open) — never `new TPage()`
- **Tests use page objects only** — no `App.Components` in test methods
- **File-scoped namespaces** (`namespace X;`), **`partial` classes** for all page/section parts, `using Bellatrix.Web;` in every file
- **Always brace control flow:** every `if`/`else`/`for`/`foreach`/`while`/`do`/`using` body uses `{ }` on its own lines (Allman), even single statements — no `if (x) continue;`
- **SPECTRA traceability:** `[ManualTestCase("TC-XXX")]` (NOT NUnit `[TestCase]`) on every automated SPECTRA case, plus `[Category("Smoke")]` for high priority / `[Category("Regression")]` otherwise; update the SPECTRA file's `automated_by` with `{project.dir}/Tests/{FeatureArea}/{FeatureArea}Tests.cs`
- **Build-only:** `{project.buildCommand}` until it succeeds. Never run `dotnet test` yourself; give the user `dotnet test {project.path} --filter "FullyQualifiedName~{TestMethodName}"`

