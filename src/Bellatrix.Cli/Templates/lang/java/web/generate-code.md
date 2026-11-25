<!-- lang:page-matrix -->
| Page object package exists for this page | **EDIT** — add new methods to the existing `Map` / page / `Asserts` classes |
| Page object package does NOT exist | **CREATE** — new package with the 3 classes (`{PageName}.java`, `Map.java`, `Asserts.java`) |
| `common/ManualTestCase.java` does NOT exist | **CREATE** it once (code in Step 4) before the first traced test |
<!-- lang:edit-naming -->
- **Keep the 3-class split** — locators only in `Map`, workflows only in the page class, assertions only in `Asserts`; never rename a page's `Map` / `Asserts` (the page's `WebPage<Map, Asserts>` refers to them)
<!-- lang:page-layout -->
Each page (or modal / wizard step) gets a dedicated package with three classes (Java has no partial classes):

```
src/test/java/{project.rootPath}/pages/
  {page}/
    {PageName}.java   — extends WebPage<Map, Asserts>: getUrl(), waitForPageLoad(), workflow methods
    Map.java          — extends PageMap: component methods (validated locators only)
    Asserts.java      — extends PageAsserts<Map>: assertion methods built from validate*
```

`{page}` is the lowercase page name without the `Page` suffix (`cart`, `checkout`, `orderreceived`); `{PageName}` is the class name (`CartPage`). The `package` line must match the folder.
<!-- lang:target -->
**Target project:** the BELLATRIX-Java web test project named in `.bellatrix/config.json` (`domains.web.project`: folder `{project.dir}`, root package `{project.rootNamespace}`).
- **Packages**: `{project.rootNamespace}.pages.<page>`, `{project.rootNamespace}.sections.<section>`, `{project.rootNamespace}.components`, `{project.rootNamespace}.models`, `{project.rootNamespace}.tests.<featurearea>`, `{project.rootNamespace}.common` (sources under `src/test/java/`). The folder names used below (`Pages/`, `Sections/`, `Components/`, `Models/`, `Tests/`) are these lowercase packages.
- **Build command**: `{project.buildCommand}` (from the repository root)
- **Language**: Java 19, JUnit 5 + `solutions.bellatrix.web.infrastructure.junit.WebTest` unless the project already uses TestNG (`…infrastructure.testng.WebTest`)

<!-- lang:page-objects -->
### Map Class (`Map.java`)

```java
package {project.rootNamespace}.pages.{page};

import java.util.List;
import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.pages.PageMap;

public class Map extends PageMap {
    // PREFERRED — direct factories for single-attribute locators
    public TextInput emailField() {
        return create().byCss(TextInput.class, "[data-testid='email']");
    }

    public Select countrySelect() {
        return create().byId(Select.class, "billing_country");
    }

    public TextInput usernameField() {
        return create().byName(TextInput.class, "username");      // Java web HAS byName
    }

    // XPath for text-based / structural locators (capital "P": byXPath)
    public Button submitButton() {
        return create().byXPath(Button.class, "//button[normalize-space()='Place order']").toBeClickable();
    }

    // Dynamic locator — a method with a parameter
    public Anchor removeLinkFor(String productName) {
        return create().byXPath(Anchor.class, String.format("//tr[td[normalize-space()='%s']]//a[contains(@class,'remove')]", productName));
    }

    // Collections — java.util.List, eager, no waiting
    public List<Div> productCards() {
        return create().allByCss(Div.class, "[data-testid='product-card']");
    }

    public Div errorMessage() {
        return create().byCss(Div.class, "[data-testid='checkout-error']");
    }
}
```

**Locator factory rule:** `[data-testid]` / any CSS → `byCss`; static id → `byId`; `name` → `byName`; exact link text → `byLinkText`; text/structural → `byXPath`. Class token first, locator second. See `bellatrix-web-locators-and-waits` for the full mapping from a validated CLI `{ strategy, selector }`.

**Component rule:** pick the class from the element recorded during execution (see the HTML → component table in `bellatrix-web-execute-scenario`). `Div`, `Span`, `Label`, `Heading` have no `click()` — type clickable non-buttons as `Button`.

### Page Class (`{PageName}.java`)

```java
package {project.rootNamespace}.pages.{page};

import {project.rootNamespace}.models.BillingInfo;
import {project.rootNamespace}.pages.orderreceived.OrderReceivedPage;
import solutions.bellatrix.web.pages.WebPage;

public class {PageName} extends WebPage<Map, Asserts> {
    @Override
    protected String getUrl() {
        return "https://example.com/checkout/";
    }

    // Optional — open() calls it after navigating; the default only waits for AJAX
    @Override
    protected void waitForPageLoad() {
        map().submitButton().toBeVisible().waitToBe();
    }

    public void fillBillingInfo(BillingInfo billingInfo) {
        map().emailField().setText(billingInfo.getEmail());
        map().countrySelect().selectByText(billingInfo.getCountry());
    }

    public OrderReceivedPage placeOrder() {
        map().submitButton().click();
        app().navigate().waitForPartialUrl("/order-received");
        return app().createPage(OrderReceivedPage.class);
    }
}
```

Every wait you needed during live execution (spinner, AJAX, URL change) belongs in the action method that triggers it (`browser().waitForAjax()`, `app().navigate().waitForPartialUrl(…)`, `app().waitFor().wait(spinner, Wait.to().notExist())`).

### Asserts Class (`Asserts.java`)

```java
package {project.rootNamespace}.pages.{page};

import org.junit.jupiter.api.Assertions;
import solutions.bellatrix.web.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void pageLoaded() {
        map().submitButton().validateIsVisible();
        map().emailField().validateIsVisible();
    }

    public void errorMessageIs(String expected) {
        map().errorMessage().validateIsVisible();
        map().errorMessage().validateTextIs(expected);
    }

    // Select has no validateText* — read the selected option once and assert it
    public void countryIs(String expected) {
        Assertions.assertEquals(expected, map().countrySelect().getSelected().getText());
    }

    public void onPage() {
        app().browser().assertLandedOnPage("/checkout");   // one-shot: wait for the URL first after a navigating click
    }
}
```

Use the exact texts observed during live execution. Tests call them as `page.asserts().errorMessageIs("…")`.

<!-- lang:custom-components -->
### Option A — New component extending `WebComponent`

```java
package {project.rootNamespace}.components;

import solutions.bellatrix.web.components.Button;
import solutions.bellatrix.web.components.WebComponent;
import solutions.bellatrix.web.components.contracts.ComponentText;

// Implementing ComponentText makes validateTextIs/Contains/NotContains/IsSet available on it
public class Toast extends WebComponent implements ComponentText {
    @Override
    public String getText() {
        return getWrappedElement().getText();
    }

    public boolean isError() {
        return getHtmlClass().contains("toast-error");
    }

    public void dismiss() {
        createByCss(Button.class, "button[aria-label='Close']").click();
    }
}
```

### Option B — Child component overriding a built-in

```java
package {project.rootNamespace}.components;

import solutions.bellatrix.web.components.Button;

public class ScrollingButton extends Button {
    // Sticky headers sometimes cover the button — scroll it into view first
    @Override
    public void click() {
        scrollToVisible();
        super.click();
    }
}
```

### Using custom components in Map classes

```java
import {project.rootNamespace}.components.ScrollingButton;
import {project.rootNamespace}.components.Toast;

public Toast successToast() {
    return create().byCss(Toast.class, "[data-testid='toast']");
}

public ScrollingButton placeOrderButton() {
    return create().byId(ScrollingButton.class, "place_order");
}
```

A custom component needs a public no-argument constructor (the implicit one) — factories create it reflectively. Inside a component, locate children with the scoped factories (`createByCss(T.class, …)`, `createByXPath(T.class, ".//…")`) or the protected `componentCreateService` field for page-wide lookups — never with raw `getWrappedDriver().findElement(…)`. Keyboard chords / drag-and-drop go in a component method using Selenium `Actions` on its own `getWrappedElement()` (BELLATRIX Java has no Interactions service — see `bellatrix-web-app-services`).

<!-- lang:sections -->
```java
// sections/header/Map.java
package {project.rootNamespace}.sections.header;

import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.pages.PageMap;

public class Map extends PageMap {
    public Anchor cartLink() {
        return create().byCss(Anchor.class, "[data-testid='header-cart']");
    }

    public Span cartCount() {
        return create().byCss(Span.class, "[data-testid='cart-count']");
    }
}

// sections/header/HeaderSection.java
package {project.rootNamespace}.sections.header;

import solutions.bellatrix.web.pages.WebSection;

public class HeaderSection extends WebSection<Map, Asserts> {
    public void openCart() {
        map().cartLink().click();
        browser().waitUntilPageLoadsCompletely();
    }
}

// sections/header/Asserts.java
package {project.rootNamespace}.sections.header;

import solutions.bellatrix.web.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void cartCountIs(int expected) {
        map().cartCount().validateTextIs(String.valueOf(expected));
    }
}

// pages/home/Map.java — expose the section by composition
public HeaderSection header() {
    return app().createSection(HeaderSection.class);
}

// pages/home/HomePage.java — delegate
public void openCart() {
    map().header().openCart();
}
```

`WebSection<Map, Asserts>` provides `map()`, `asserts()`, `app()`, `create()`, `browser()` but no `getUrl()` / `open()`. A section package lives under `sections/{section}/` (`sections.header` holds `HeaderSection`).

<!-- lang:models -->
```java
// models/BillingInfo.java — a form with many fields: plain class with getters/setters (no Lombok on the classpath)
package {project.rootNamespace}.models;

public class BillingInfo {
    private String firstName;
    private String email;
    private String country;
    private boolean shouldCreateAccount;

    public String getFirstName() {
        return firstName;
    }

    public void setFirstName(String firstName) {
        this.firstName = firstName;
    }

    public String getEmail() {
        return email;
    }

    public void setEmail(String email) {
        this.email = email;
    }

    public String getCountry() {
        return country;
    }

    public void setCountry(String country) {
        this.country = country;
    }

    public boolean getShouldCreateAccount() {
        return shouldCreateAccount;
    }

    public void setShouldCreateAccount(boolean shouldCreateAccount) {
        this.shouldCreateAccount = shouldCreateAccount;
    }
}

// models/LoginCredentials.java — a small immutable value: a record
package {project.rootNamespace}.models;

public record LoginCredentials(String email, String password) {
}

// models/ProductSort.java — a fixed option set: an enum carrying the visible text
package {project.rootNamespace}.models;

public enum ProductSort {
    POPULARITY("Sort by popularity"),
    PRICE_LOW_TO_HIGH("Sort by price: low to high"),
    PRICE_HIGH_TO_LOW("Sort by price: high to low");

    private final String displayText;

    ProductSort(String displayText) {
        this.displayText = displayText;
    }

    public String displayText() {
        return displayText;
    }
}
```

One public top-level type per file. The page passes the enum's text to the component: `map().sortDropDown().selectByText(sort.displayText());`.

<!-- lang:tests -->
**Java file layout:** `Tests/{FeatureArea}/{FeatureArea}Tests` above means `src/test/java/{project.rootPath}/tests/{featurearea}/{FeatureArea}Tests.java` in package `{project.rootNamespace}.tests.{featurearea}` (e.g. `tests/cart/CartTests.java`).

### `common/ManualTestCase.java` (create once)

```java
package {project.rootNamespace}.common;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Links an automated test to a SPECTRA manual test case for traceability.
 * Pure metadata (not JUnit's @ParameterizedTest) — SPECTRA's coverage scanner matches @ManualTestCase("TC-XXX").
 * Deliberately NOT @Repeatable: one SPECTRA test case per test method.
 */
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ManualTestCase {
    String value();
}
```

### Test Class Template

```java
package {project.rootNamespace}.tests.checkout;

import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.models.BillingInfo;
import {project.rootNamespace}.pages.checkout.CheckoutPage;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.junit.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)   // default for generated classes — speed optimization
public class CheckoutTests extends WebTest {
    @Test
    @ManualTestCase("TC-220")
    @Tag("Smoke")
    public void placeOrder_WithValidBillingInfo_ShowsOrderReceived() {
        var checkoutPage = app().goTo(CheckoutPage.class);
        checkoutPage.asserts().pageLoaded();

        var billingInfo = new BillingInfo();
        billingInfo.setFirstName("In");
        billingInfo.setEmail("info@example.com");
        billingInfo.setCountry("Bulgaria");
        checkoutPage.fillBillingInfo(billingInfo);
        var orderReceivedPage = checkoutPage.placeOrder();

        orderReceivedPage.asserts().orderReceived();
    }
}
```

- `app().goTo(TPage.class)` opens the page (`getUrl()` + `waitForPageLoad()`); `app().createPage(TPage.class)` wraps a page already displayed.
- `@ManualTestCase("TC-XXX")` only when the test automates a SPECTRA case (see `bellatrix-web-automate-spectra-test`); `@Tag("Smoke")` for high priority, `@Tag("Regression")` otherwise.
- Lifecycle: class default `Lifecycle.REUSE_IF_STARTED`; switch a class or a single method to `Lifecycle.RESTART_EVERY_TIME` when the test logs in, changes account/cart state, or needs a clean browser — and comment why. A method-level `@ExecutionBrowser` must repeat `browser = …` (an omitted browser there means `NOT_SET`, not the class value).
- No class-level annotation like C#'s `[TestFixture]` is needed; `lifecycle` is mandatory on `@ExecutionBrowser`.

<!-- lang:code-rules -->
## Code Rules (MUST FOLLOW)

### Syntax — Avoid Build Errors
1. **Package = folder**: the `package` line matches the directory under `src/test/java`; one public top-level class per file; file name = class name
2. **Three classes per page**: `{PageName} extends WebPage<Map, Asserts>`, `Map extends PageMap`, `Asserts extends PageAsserts<Map>` — all `solutions.bellatrix.web.pages`; sections use `WebSection<Map, Asserts>`. NOT a raw `WebPage`, NOT `DesktopPage`, NOT the desktop/mobile `PageMap`
3. **`@Override protected String getUrl()`** on every navigable page (and optionally `@Override protected void waitForPageLoad()`); do not override `open()`
4. **Elements are methods**: `public Button okButton() { return create()…; }` — called as `map().okButton()`
5. **`byXPath`** — capital **P** (`byXpath` / `allByXpath` do not exist); class token first: `byId(Button.class, "x")`
6. **No `byDataTestId`** — use `byCss(T.class, "[data-testid='…']")`; `byName` DOES exist in Java web
7. **Wait-chain trap**: `x.toBeVisible().click()` does not compile — end chains with `waitToBe()`, assign (`Button b = x.toBeVisible();`), or `return` it from a Map method
8. **Validate methods**: native `validate*` (`validateIsVisible()`, `validateTextIs()`, `validateTextContains()`, `validateValueIs()`, `validateIsChecked()`, `validateNotDisabled()`, …) instead of `Assertions.*` — and only on components that implement them (`Select` / `PasswordInput` / `EmailInput` / `CheckBox` / `Image` have no `validateText*`). There is no `validateInnerTextIs`, `validateIsNotVisible`, `validateIsEnabled`, `validateExists`
9. **Imports**: `import solutions.bellatrix.web.components.*;` in Map classes and custom components; **never `import java.util.*;` in a page package** (the page's `Map` shadows `java.util.Map`) — import `java.util.List` explicitly
10. **No `clear()`** on any web component — `setText(…)` clears first

### Architecture
11. **Page instantiation**: `app().goTo(TPage.class)` / `app().createPage(TPage.class)` (NOT `new TPage()`, NOT `app().create(TPage.class)`)
12. **Lifecycle**: generated test classes default to `@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)`; the global config stays `restart every time`. Override per class or per method with `Lifecycle.RESTART_EVERY_TIME` when state (auth cookies, cart, local storage) would leak — see the generator agent for the full policy
13. **No direct components in tests**: tests use only page-object and section methods
14. **Parameterized locator methods** for dynamic elements: product names, row ids, menu texts (`String.format`)
15. **Navigation**: `app().navigate().to(url)` (NOT `app().browser().navigate(…)`); after a navigating click, `app().navigate().waitForPartialUrl(…)`
16. **Never raw Selenium** (`getWrappedDriver().findElement(s)`, `By.*`, `WebDriverWait`, `Thread.sleep`) in test-project code — BELLATRIX factories and waits only

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `while`, `do`, and `try` block MUST use `{ }` braces — even for single-statement bodies. Braces follow Java convention (opening brace on the same line).

```text
WRONG — no braces on single-statement if
for (var card : map().productCards()) {
    if (!card.isVisible()) continue;
    card.validateTextContains("€");
}
```

```java
// CORRECT — every control-flow body is braced
for (var card : map().productCards()) {
    if (!card.isVisible()) {
        continue;
    }

    card.validateTextContains("€");
}
```

Applies everywhere — page objects, actions, assertions, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `app().create().byXpath(Button.class, …)` | `app().create().byXPath(Button.class, …)` (capital P) |
| `create().byDataTestId(Button.class, "x")` | `create().byCss(Button.class, "[data-testid='x']")` |
| `create().byId("x", Button.class)` | `create().byId(Button.class, "x")` (class token first) |
| `app().browser().navigate(url)` / `App.Navigation.Navigate(url)` | `app().navigate().to(url)` |
| `new LoginPage()` / `app().create(LoginPage.class)` | `app().createPage(LoginPage.class)` / `app().goTo(LoginPage.class)` |
| `public class LoginPage extends WebPage { }` | `public class LoginPage extends WebPage<Map, Asserts> { }` |
| `public String url = "…";` | `@Override protected String getUrl() { return "…"; }` |
| `Assertions.assertTrue(el.isVisible())` | `el.validateIsVisible()` |
| `countrySelect.validateTextIs("X")` / `countrySelect.getSelected().validateTextIs("X")` | `Assertions.assertEquals("X", countrySelect.getSelected().getText())` |
| `button.toBeVisible().click()` | `button.toBeVisible().waitToBe(); button.click();` (or `Button b = button.toBeVisible(); b.click();`) |
| `div.click()` | type the element as `Button` in the Map |
| `field.clear()` | `field.setText("")` (`setText` clears first) |
| `//Button[@Name='OK']` (desktop syntax) | `//button[normalize-space()='OK']` |
| `import java.util.*;` in a page package | `import java.util.List;` |
| `Thread.sleep(2000)` | `toBeVisible().waitToBe()` / `app().browser().waitForAjax()` |

<!-- lang:build -->
From the repository root (compile only — never `mvn test` / `mvn verify`):

```bash
{project.buildCommand}
```

Common build errors:
- Missing imports (`solutions.bellatrix.web.components.*`, `java.util.List`, `ManualTestCase`, the page / section / model classes)
- `package` line not matching the folder; `java.util.Map` imported into a page package (`Map` clash)
- `byXpath` / `byDataTestId` — these do not exist (`byXPath`, `byCss("[data-testid='…']")`)
- Wait-chain generics (`cannot find symbol: method click()` after `toBeVisible()`)
- `validate*` on a component without that contract (e.g. `Select.validateTextIs`), or `click()` on `Div` / `Span` / `Label` / `Heading`
- `@ExecutionBrowser` without `lifecycle = …` ("missing a default value for the element 'lifecycle'"); two `@ManualTestCase` on one method ("not a repeatable annotation interface")

The command the user runs afterwards (include it in the hand-off; do NOT run it yourself):

```bash
mvn test -Dtest={TestClassName}#{testMethodName}
```

<!-- lang:output -->
## Output Format

### Files Created/Modified

```
[CREATED] src/test/java/{project.rootPath}/pages/{page}/Map.java
[CREATED] src/test/java/{project.rootPath}/pages/{page}/{PageName}.java
[CREATED] src/test/java/{project.rootPath}/pages/{page}/Asserts.java
[EDITED]  src/test/java/{project.rootPath}/pages/{existingpage}/Map.java — added 3 new element methods
[EDITED]  src/test/java/{project.rootPath}/pages/{existingpage}/{ExistingPage}.java — added 2 new methods
[CREATED] src/test/java/{project.rootPath}/sections/{section}/{SectionName}.java (+ Map, Asserts)
[CREATED] src/test/java/{project.rootPath}/models/{ModelName}.java
[CREATED] src/test/java/{project.rootPath}/tests/{featurearea}/{FeatureArea}Tests.java
[EDITED]  bellatrix-web-project-structure skill — added {PageName} to the Pages table
```

### Build Verification
```
Build succeeded: {project.buildCommand}
```
