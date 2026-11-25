<!-- lang:page-matrix -->
| Page object folder exists for this page | **EDIT** — add new members to the existing partial files |
| Page object folder does NOT exist | **CREATE** — new folder with the 3 partial files |
<!-- lang:edit-naming -->
- **Keep the partial class name identical** across the page's files
<!-- lang:page-layout -->
Each page (or modal / wizard step) gets a dedicated folder with three partial parts:

```
Pages/
  {PageName}/
    {PageName}.Actions.*      — page base class, Url, Open(), workflow methods
    {PageName}.Map.*          — component properties (validated locators only)
    {PageName}.Assertions.*   — Assert* methods built from Validate* methods
```
<!-- lang:target -->
**Target project:** `domains.web.project` in `.bellatrix/config.json` (`{project.name}`, folder `{project.dir}`).
- **Namespaces**: `{project.rootNamespace}.Pages`, `{project.rootNamespace}.Sections`, `{project.rootNamespace}.Components`, `{project.rootNamespace}.Models`, `{project.rootNamespace}.Tests`
- **Build command**: `{project.buildCommand}`
- **Language**: C# with file-scoped namespaces; NUnit + `Bellatrix.Web.NUnit.WebTest` unless the project already uses MSTest

<!-- lang:page-objects -->
### Map File (`{PageName}.Map.cs`)

```csharp
using Bellatrix.Web;

namespace {project.rootNamespace}.Pages;

public partial class {PageName}
{
    // PREFERRED — direct factories for single-attribute locators
    public TextField EmailField =>
        App.Components.CreateByCss<TextField>("[data-testid='email']");

    public Select CountrySelect =>
        App.Components.CreateById<Select>("billing_country");

    public TextField UsernameField =>
        App.Components.CreateByCss<TextField>("[name='username']");      // no CreateByName in web

    // XPath for text-based / structural locators (lowercase "p": CreateByXpath)
    public Button SubmitButton =>
        App.Components.CreateByXpath<Button>("//button[normalize-space()='Place order']").ToBeClickable();

    // Dynamic locator — a method, not a property
    public Anchor RemoveLinkFor(string productName) =>
        App.Components.CreateByXpath<Anchor>($"//tr[td[normalize-space()='{productName}']]//a[contains(@class,'remove')]");

    // Collections
    public ComponentsList<Div> ProductCards =>
        App.Components.CreateAllByCss<Div>("[data-testid='product-card']");
}
```

**Locator factory rule:** `[data-testid]` / any CSS → `CreateByCss`; static id → `CreateById`; `name` → `CreateByCss("[name='…']")`; exact link text → `CreateByLinkText`; text/structural → `CreateByXpath`. See `bellatrix-web-locators-and-waits` for the full mapping from a validated CLI `{ strategy, selector }`.

**Component rule:** pick the type from the element recorded during execution (see the HTML → component table in `bellatrix-web-execute-scenario`). Remember `Div`, `Span`, `Label`, `Heading` have no `Click()` — type clickable non-buttons as `Button`.

### Actions File (`{PageName}.Actions.cs`)

```csharp
using Bellatrix.Web;
using {project.rootNamespace}.Models;

namespace {project.rootNamespace}.Pages;

public partial class {PageName} : WebPage
{
    public override string Url => "https://example.com/checkout/";

    public override void Open()
    {
        App.Navigation.Navigate(Url);
        App.Browser.WaitUntilReady();
    }

    public void FillBillingInfo(BillingInfo billingInfo)
    {
        EmailField.SetText(billingInfo.Email);
        CountrySelect.SelectByText(billingInfo.Country);
    }

    public OrderReceivedPage PlaceOrder()
    {
        SubmitButton.Click();
        App.Navigation.WaitForPartialUrl("/order-received");
        return App.Create<OrderReceivedPage>();
    }
}
```

Every wait you needed during live execution (spinner, AJAX, URL change) belongs in the action method that triggers it.

### Assertions File (`{PageName}.Assertions.cs`)

```csharp
using Bellatrix.Web;

namespace {project.rootNamespace}.Pages;

public partial class {PageName}
{
    public void AssertPageLoaded()
    {
        SubmitButton.ValidateIsVisible();
        EmailField.ValidateIsVisible();
    }

    public void AssertErrorMessageIs(string expected)
    {
        ErrorMessage.ValidateIsVisible();
        ErrorMessage.ValidateInnerTextIs(expected);
    }

    public void AssertOnPage()
    {
        AssertLandedOnPage("/checkout");
    }
}
```

Use the exact texts observed during live execution. `Select` has no inner-text validation — validate `CountrySelect.GetSelected()` instead.

<!-- lang:custom-components -->
### Option A — New component inheriting `Component`

```csharp
using Bellatrix.Web;
using Bellatrix.Web.Contracts;

namespace {project.rootNamespace}.Components;

public class Toast : Component, IComponentInnerText
{
    public virtual string InnerText => WrappedElement.Text;

    public void Dismiss() => this.CreateByCss<Button>("button[aria-label='Close']").Click();
}
```

### Option B — Child component overriding a built-in

```csharp
using Bellatrix.Web;

namespace {project.rootNamespace}.Components;

public class ScrollingButton : Button
{
    public override void Click()
    {
        ScrollToVisible();
        base.Click();
    }
}
```

### Using custom components in Map files

```csharp
using {project.rootNamespace}.Components;

public Toast SuccessToast => App.Components.CreateByCss<Toast>("[data-testid='toast']");
public ScrollingButton PlaceOrderButton => App.Components.CreateById<ScrollingButton>("place_order");
```

Inside a component, locate children with the scoped factories (`this.CreateByCss<T>(…)`, `this.CreateByXpath<T>(".//…")`) or the protected `ComponentCreateService` — never with raw `WrappedDriver.FindElement`.

<!-- lang:sections -->
```csharp
// Sections/HeaderSection/HeaderSection.Map.cs
using Bellatrix.Web;

namespace {project.rootNamespace}.Sections;

public partial class HeaderSection
{
    public Anchor CartLink => App.Components.CreateByCss<Anchor>("[data-testid='header-cart']");
    public Span CartCount => App.Components.CreateByCss<Span>("[data-testid='cart-count']");
}

// Sections/HeaderSection/HeaderSection.Actions.cs
using Bellatrix.Web;

namespace {project.rootNamespace}.Sections;

public partial class HeaderSection : WebSection
{
    public void OpenCart()
    {
        CartLink.Click();
        App.Browser.WaitUntilReady();
    }
}

// Pages/HomePage/HomePage.Map.cs — embed by composition
public HeaderSection Header => new HeaderSection();

// Pages/HomePage/HomePage.Actions.cs — delegate
public void OpenCart() => Header.OpenCart();
```

`WebSection` provides `App` but no `Url`/`Open()`.

<!-- lang:models -->
```csharp
namespace {project.rootNamespace}.Models;

public class BillingInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public bool ShouldCreateAccount { get; set; }
}

public enum ProductSort
{
    Popularity,
    PriceLowToHigh,
    PriceHighToLow,
}
```

Map enum values to visible option texts inside the page object (a `switch` in the action method), not in the model.

<!-- lang:tests -->
### Test Class Template

```csharp
using Bellatrix.Web;
using Bellatrix.Web.NUnit;
using {project.rootNamespace}.Models;
using {project.rootNamespace}.Pages;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]   // default for generated classes — speed optimization
public class CheckoutTests : WebTest
{
    [Test]
    [ManualTestCase("TC-210")]
    [Category("Smoke")]
    public void PlaceOrder_WithValidBillingInfo_ShowsOrderReceived()
    {
        var checkoutPage = App.GoTo<CheckoutPage>();
        checkoutPage.AssertPageLoaded();

        checkoutPage.FillBillingInfo(new BillingInfo
        {
            FirstName = "In",
            LastName = "Deepthought",
            Email = "info@example.com",
            Country = "Bulgaria",
        });
        var orderReceivedPage = checkoutPage.PlaceOrder();

        orderReceivedPage.AssertOrderReceived();
    }
}
```

- `App.GoTo<TPage>()` opens the page; `App.Create<TPage>()` wraps a page already displayed.
- `[ManualTestCase("TC-XXX")]` only when the test automates a SPECTRA case (see `bellatrix-web-automate-spectra-test`).
- Lifecycle: class default `Lifecycle.ReuseIfStarted`; switch a class or a single method to `Lifecycle.RestartEveryTime` when the test logs in, changes account/cart state, or needs a clean browser — and comment why.

<!-- lang:code-rules -->
## Code Rules (MUST FOLLOW)

### Syntax — Avoid Build Errors
1. **File-scoped namespaces**: `namespace X;` (NOT `namespace X { }`)
2. **Partial classes**: all page-object and section files use `public partial class`
3. **`WebPage` base** (non-generic) on the Actions part; sections use `WebSection`. NOT `DesktopPage`, NOT a generic WebPage with type arguments, NOT the obsolete `NavigatablePage`/`AssertedPage`
4. **`override`** on `Url` (`public override string Url => "…";`) and `Open()`
5. **`CreateByXpath`** — lowercase **p** (`CreateByXPath` does not exist in web); `CreateAllByXpath` likewise
6. **No `CreateByDataTestId` / `CreateByName`** — use `CreateByCss("[data-testid='…']")` / `CreateByCss("[name='…']")`
7. **Validate methods**: native `Validate*` (`ValidateIsVisible()`, `ValidateInnerTextIs()`, `ValidateInnerTextContains()`, `ValidateValueIs()`, `ValidateIsChecked()`, …) instead of `Assert.That(...)` — and only on components that support them (`Select`/`Password` have no `ValidateInnerText*`)
8. **`using Bellatrix.Web;`** in every page, section, component, and test file

### Architecture
9. **Page instantiation**: `App.Create<TPage>()` / `App.GoTo<TPage>()` (NOT `new TPage()`)
10. **Lifecycle**: generated test classes default to `[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]`; the global config stays `restart every time`. Override per class or per method with `Lifecycle.RestartEveryTime` when state (auth cookies, cart, local storage) would leak — see the generator agent for the full policy
11. **No direct components in tests**: tests use only page-object and section methods
12. **Generic methods for dynamic elements**: parameters for product names, row ids, menu texts
13. **Navigation**: `App.Navigation.Navigate(url)` (NOT `App.Browser.NavigateToUrl`)
14. **Never raw Selenium** (`WrappedDriver.FindElement(s)`, `By.*`, `WebDriverWait`, `Thread.Sleep`) in test-project code — BELLATRIX factories and waits only

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `foreach`, `while`, `do`, and `using` block MUST use `{ }` braces — even for single-statement bodies. Braces on their own line (Allman style).

```csharp
// WRONG
foreach (var card in ProductCards)
{
    if (!card.IsVisible) continue;
    card.ValidateInnerTextContains("€");
}

// CORRECT
foreach (var card in ProductCards)
{
    if (!card.IsVisible)
    {
        continue;
    }

    card.ValidateInnerTextContains("€");
}
```

Applies everywhere — page objects, actions, assertions, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `App.Components.CreateByXPath<T>()` | `App.Components.CreateByXpath<T>()` (lowercase p) |
| `App.Components.CreateByDataTestId<T>("x")` | `App.Components.CreateByCss<T>("[data-testid='x']")` |
| `App.Browser.NavigateToUrl(url)` | `App.Navigation.Navigate(url)` |
| `new LoginPage()` | `App.Create<LoginPage>()` |
| `public string Url => …` | `public override string Url => …` |
| `Assert.That(el.IsVisible, Is.True)` | `el.ValidateIsVisible()` |
| `countrySelect.ValidateInnerTextIs("X")` | `countrySelect.GetSelected().ValidateInnerTextIs("X")` |
| `Assert.That(App.Browser.Url, Does.Contain("/x"))` | `Assert.That(App.Browser.Url.ToString(), Does.Contain("/x"))` or `page.AssertLandedOnPage("/x")` |
| `//Button[@Name='OK']` (desktop syntax) | `//button[normalize-space()='OK']` |
| `namespace X { }` | `namespace X;` |
| `Thread.Sleep(2000)` | `ToBeVisible()` / `App.Browser.WaitForAjax()` |

<!-- lang:build -->
```bash
{project.buildCommand}
```

Common build errors:
- Missing `using Bellatrix.Web;` (components, factories, `Validate*` all live there)
- `CreateByXPath` / `CreateByName` / `CreateByDataTestId` — these do not exist in web
- `Validate*` on a component without that capability (e.g. `Select.ValidateInnerTextIs`)
- Missing `override` on `Url` / `Open()`, or two parts declaring different base classes
- Namespace mismatch between the page parts or with the test's `using`

The command the user runs afterwards (include it in the hand-off; do NOT run it yourself):

```bash
dotnet test {project.path} --filter "FullyQualifiedName~{TestMethodName}" --no-build --logger "console;verbosity=detailed"
```

<!-- lang:output -->
## Output Format

### Files Created/Modified

```
[CREATED] Pages/{PageName}/{PageName}.Map.cs
[CREATED] Pages/{PageName}/{PageName}.Actions.cs
[CREATED] Pages/{PageName}/{PageName}.Assertions.cs
[EDITED]  Pages/{ExistingPage}/{ExistingPage}.Map.cs — added 3 new properties
[EDITED]  Pages/{ExistingPage}/{ExistingPage}.Actions.cs — added 2 new methods
[CREATED] Sections/{SectionName}/{SectionName}.Map.cs (+ Actions, Assertions)
[CREATED] Models/{ModelName}.cs
[CREATED] Tests/{FeatureArea}/{FeatureArea}Tests.cs
[EDITED]  bellatrix-web-project-structure skill — added {PageName} to the Pages table
```

### Build Verification
```
Build succeeded: {project.buildCommand}
```
