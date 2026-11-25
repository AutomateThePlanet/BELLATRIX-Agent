---
description: Reference skill for BELLATRIX Java web page object structure — the 3-class package pattern (WebPage<Map, Asserts> + Map + Asserts), reusable WebSection sections, models, and design patterns.
---
# BELLATRIX Web Page Objects and Patterns Reference (Java)

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. One Package per Page, Three Classes

A Java page object is **three separate classes in their own package** (there are no partial classes in Java):

| File | Class | Extends |
|------|-------|---------|
| `pages/login/LoginPage.java` | `LoginPage` | `WebPage<Map, Asserts>` |
| `pages/login/Map.java` | `Map` | `PageMap` |
| `pages/login/Asserts.java` | `Asserts` | `PageAsserts<Map>` |

All three base classes are in `solutions.bellatrix.web.pages`. The package is the lowercase page name without the `Page` suffix (`pages.login`, `pages.cart`, `pages.orderreceived`); the `package` line must match the folder; one public top-level class per file, file name = class name. Every page package has its own `Map` and `Asserts` — they never clash because each page imports only its own.

### 2. Elements Are Methods, Not Fields

```java
// CORRECT - a method returning a fresh (lazy) component on each call
public Button loginButton() {
    return create().byXPath(Button.class, "//button[normalize-space()='Log In']");
}
```

```text
WRONG - a field is created once, when the Map is constructed
public Button loginButton = create().byXPath(Button.class, "//button[normalize-space()='Log In']");
```

Callers write `map().loginButton().click()`.

### 3. Extend the Generic `WebPage<Map, Asserts>` and Override `getUrl()`

```java
// CORRECT
public class LoginPage extends WebPage<Map, Asserts> {
    @Override
    protected String getUrl() {
        return "https://example.com/login";
    }
}
```

```text
WRONG
public class LoginPage extends WebPage { }                    // raw type: map() returns Object, map().loginButton() does not compile
public class LoginPage extends DesktopPage<Map, Asserts> { }  // desktop only
public String Url = "https://example.com/login";              // C# Url property — Java pages override getUrl()
```

`WebPage<MapT extends PageMap, AssertsT extends PageAsserts<MapT>>` gives the page:

| Member | What it does |
|--------|--------------|
| `map()` / `asserts()` | Public — a new `Map` / `Asserts` instance (callers: `page.map().x()`, `page.asserts().y()`) |
| `protected String getUrl()` | The page URL — default `""`; override it (keep it `protected`) |
| `public void open()` | `navigate().to(getUrl())`, then `waitForPageLoad()` — called by `app().goTo(P.class)`; do not override it |
| `protected void waitForPageLoad()` | Default `browser().waitForAjax()`; override it to wait for a page-specific element |
| `app()`, `create()`, `browser()`, `navigate()`, `javaScript()` | Shortcuts to the services (`create()` = `app().create()`) |

There are **no** URL assertion methods on a Java page (C#'s `AssertLandedOnPage` lives on `BrowserService`): call `app().browser().assertLandedOnPage("/login")` from the `Asserts` class.

### 4. `Map` and `Asserts` Are Created Reflectively

`map()` and `asserts()` instantiate the classes named in the **immediate superclass's** type arguments, through their public no-argument constructor, on **every call**:
- keep `Map` and `Asserts` stateless (no fields that should survive between calls) and give them no constructor parameters
- `class BigCartPage extends CartPage` gets `null` from `map()` (its superclass `CartPage` carries no type arguments) — every page extends `WebPage<Map, Asserts>` directly, and every `Asserts` extends `PageAsserts<Map>` directly

### 5. Never `import java.util.*` in a Page Package

The page's `Map` class shadows `java.util.Map`. Import `java.util.List` explicitly when you need it.

### 6. Use validate* Methods in Asserts (NOT JUnit Assertions)

See `bellatrix-web-validations-and-assertions`. Inside `Asserts`, `map()` is `protected` and returns the page's `Map`; `app()` is available for browser-level checks.

---

## Project Structure (3-Class Pattern)

```
src/test/java/{project.rootPath}/
  pages/
    login/
      LoginPage.java     # extends WebPage<Map, Asserts>: getUrl(), waitForPageLoad(), workflow methods
      Map.java           # extends PageMap: component methods (validated locators only)
      Asserts.java       # extends PageAsserts<Map>: assertion methods using validate*
    cart/
      CartPage.java
      Map.java
      Asserts.java
  sections/
    header/
      HeaderSection.java # extends WebSection<Map, Asserts>
      Map.java
      Asserts.java
  components/
    ScrollingButton.java
  models/
    LoginCredentials.java
    BillingInfo.java
  tests/
    login/LoginTests.java
    cart/CartTests.java
  common/
    ManualTestCase.java
```

---

## Complete 3-Class Example: LoginPage

### Map.java

```java
package {project.rootNamespace}.pages.login;

import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.pages.PageMap;

public class Map extends PageMap {
    // ===== FORM FIELDS =====
    public TextInput emailField() {
        return create().byCss(TextInput.class, "[data-testid='email']");
    }

    public PasswordInput passwordField() {
        return create().byId(PasswordInput.class, "password");
    }

    public CheckBox rememberMeCheckBox() {
        return create().byName(CheckBox.class, "remember");
    }

    // ===== BUTTONS / LINKS =====
    public Button loginButton() {
        return create().byXPath(Button.class, "//button[normalize-space()='Log In']").toBeClickable();
    }

    public Anchor forgotPasswordLink() {
        return create().byLinkText(Anchor.class, "Forgot password?");
    }

    // ===== MESSAGES =====
    public Div errorMessage() {
        return create().byCss(Div.class, "[data-testid='login-error']");
    }

    public Heading pageHeading() {
        return create().byTag(Heading.class, "h1");
    }
}
```

(`return create()…toBeClickable();` compiles because the `return` type drives the generic wait method — see `bellatrix-web-locators-and-waits`.)

### LoginPage.java (actions)

```java
package {project.rootNamespace}.pages.login;

import {project.rootNamespace}.models.LoginCredentials;
import {project.rootNamespace}.pages.dashboard.DashboardPage;
import solutions.bellatrix.web.pages.WebPage;

public class LoginPage extends WebPage<Map, Asserts> {
    @Override
    protected String getUrl() {
        return "https://example.com/login";
    }

    // open() = navigate().to(getUrl()) + waitForPageLoad(); the default only waits for AJAX
    @Override
    protected void waitForPageLoad() {
        map().emailField().toBeVisible().waitToBe();
    }

    public void loginWith(String email, String password) {
        map().emailField().setText(email);
        map().passwordField().setPassword(password);
        map().loginButton().click();
    }

    public void loginWith(LoginCredentials credentials) {
        loginWith(credentials.email(), credentials.password());
    }

    public void loginWithRememberMe(String email, String password) {
        map().emailField().setText(email);
        map().passwordField().setPassword(password);
        map().rememberMeCheckBox().check();
        map().loginButton().click();
    }

    public DashboardPage loginAndGoToDashboard(String email, String password) {
        loginWith(email, password);
        app().navigate().waitForPartialUrl("/dashboard");
        return app().createPage(DashboardPage.class);
    }
}
```

### Asserts.java

```java
package {project.rootNamespace}.pages.login;

import solutions.bellatrix.web.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void pageLoaded() {
        map().pageHeading().validateTextIs("Log in");
        map().emailField().validateIsVisible();
        map().passwordField().validateIsVisible();
        map().loginButton().validateNotDisabled();
    }

    public void errorMessageContains(String expectedText) {
        map().errorMessage().validateIsVisible();
        map().errorMessage().validateTextContains(expectedText);
    }

    public void stillOnLoginPage() {
        app().browser().assertLandedOnPage("/login");
    }
}
```

Usage in a test: `var loginPage = app().goTo(LoginPage.class); loginPage.loginWith("u@example.com", "secret"); loginPage.asserts().errorMessageContains("Invalid");`

---

## Design Patterns

### 1. Method-Based Component Definitions (Recommended)

Each element is a method returning a new component — lazy, located on each use, so it survives re-renders:

```java
public Button placeOrderButton() {
    return create().byId(Button.class, "place_order");
}
```

### 2. Parameterized Methods for Dynamic Elements

When the locator depends on runtime data, pass it in and format the locator:

```java
public Anchor addToCartButton(int productId) {
    return create().byAttributeContaining(Anchor.class, "data-product_id", String.valueOf(productId)).toBeClickable();
}

public Anchor removeLinkFor(String productName) {
    return create().byXPath(Anchor.class, String.format("//tr[td[normalize-space()='%s']]//a[contains(@class,'remove')]", productName));
}
```

Prefer a direct factory when only one attribute varies; use `byXPath` only when the dynamic locator is genuinely structural or text-based.

### 3. Collections for Repeated Elements

`allBy…` returns a `java.util.List<T>`, queried once at call time (no waiting):

```java
// Map.java (import java.util.List — never java.util.*)
public List<NumberInput> quantityBoxes() {
    return create().allByClassContaining(NumberInput.class, "input-text qty text");
}

// CartPage.java
public void updateProductQuantity(int productNumber, int newQuantity) {
    var quantityBoxes = map().quantityBoxes();
    if (productNumber > quantityBoxes.size()) {
        throw new IllegalArgumentException("There are fewer items in the cart than the requested product number.");
    }

    quantityBoxes.get(productNumber - 1).setNumber(newQuantity);
    map().updateCartButton().click();
    browser().waitForAjax();
}
```

### 4. Navigation Methods Return Page Objects

```java
public CheckoutPage proceedToCheckout() {
    map().proceedToCheckoutButton().click();
    app().navigate().waitForPartialUrl("/checkout");
    return app().createPage(CheckoutPage.class);
}
```

`app().createPage(…)` returns the per-thread instance — the same object a test gets from `app().createPage(CheckoutPage.class)`. Never `new CheckoutPage()`.

### 5. Fluent Interface for Chaining

```java
public LoginPage withEmail(String email) {
    map().emailField().setText(email);
    return this;
}

public LoginPage withPassword(String password) {
    map().passwordField().setPassword(password);
    return this;
}

// Usage: loginPage.withEmail("user@example.com").withPassword("secret").map().loginButton().click();
```

### 6. Organizing Complex Pages with Regions

Group locators with `// ===== REGION =====` comments inside the `Map`:

```java
public class Map extends PageMap {
    // ===== BILLING DETAILS =====
    public TextInput billingFirstName() {
        return create().byId(TextInput.class, "billing_first_name");
    }

    public Select billingCountry() {
        return create().byId(Select.class, "billing_country");
    }

    public Select billingState() {
        return create().byId(Select.class, "billing_state").toBeVisible().toBeClickable();
    }

    public PhoneInput billingPhone() {
        return create().byId(PhoneInput.class, "billing_phone");
    }

    public EmailInput billingEmail() {
        return create().byId(EmailInput.class, "billing_email");
    }

    // ===== PAYMENT =====
    public RadioButton checkPaymentsRadioButton() {
        return create().byAttributeContaining(RadioButton.class, "for", "payment_method_cheque");
    }

    // ===== ACTIONS =====
    public Button placeOrderButton() {
        return create().byId(Button.class, "place_order");
    }
}
```

### 7. Page URL from Configuration

Hard-coded URLs are fine for a single environment; to follow `webSettings.baseUrl` instead:

```java
import solutions.bellatrix.core.configuration.ConfigurationService;
import solutions.bellatrix.web.configuration.WebSettings;

@Override
protected String getUrl() {
    return ConfigurationService.get(WebSettings.class).getBaseUrl() + "cart/";
}
```

### 8. Reusable Sections Across Pages (Composition with `WebSection`)

Headers, navigation bars, footers, cookie banners, and mini-carts appear on many pages. Model each as a **section** — the same 3-class package, but the action class extends `WebSection<Map, Asserts>` (`solutions.bellatrix.web.pages`), which has `map()`, `asserts()`, `app()`, `create()`, `browser()` and `javaScript()` but **no** `getUrl()` / `open()` / `navigate()`:

```java
// sections/header/Map.java
package {project.rootNamespace}.sections.header;

import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.pages.PageMap;

public class Map extends PageMap {
    public SearchField searchField() {
        return create().byCss(SearchField.class, "[data-testid='header-search']");
    }

    public Button searchButton() {
        return create().byCss(Button.class, "[data-testid='header-search-submit']");
    }

    public Anchor cartLink() {
        return create().byCss(Anchor.class, "[data-testid='header-cart']");
    }

    public Span cartCount() {
        return create().byCss(Span.class, "[data-testid='cart-count']");
    }

    public Anchor menuItem(String text) {
        return create().byLinkText(Anchor.class, text);
    }
}

// sections/header/HeaderSection.java
package {project.rootNamespace}.sections.header;

import solutions.bellatrix.web.pages.WebSection;

public class HeaderSection extends WebSection<Map, Asserts> {
    public void searchFor(String term) {
        map().searchField().setSearch(term);
        map().searchButton().click();
        browser().waitUntilPageLoadsCompletely();
    }

    public void openMenu(String text) {
        map().menuItem(text).click();
        browser().waitUntilPageLoadsCompletely();
    }

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
```

Expose the section from each page's `Map` and delegate from the page's action methods:

```java
// pages/cart/Map.java
import {project.rootNamespace}.sections.header.HeaderSection;

public HeaderSection header() {
    return app().createSection(HeaderSection.class);
}

// pages/cart/CartPage.java
public void openCartFromHeader() {
    map().header().openCart();
}
```

Tests may also create a section directly: `var header = app().createSection(HeaderSection.class); header.asserts().cartCountIs(1);`.

**Rules of thumb:**
- Extract a section only after the duplication is real (≥2 pages). Don't pre-extract for hypothetical reuse.
- Keep the public action names on each page unchanged — the section is an implementation detail.
- A section owns its own locators AND its own workflow methods; a pure locator group stays on the page as a region.
- If the shared block's locators differ per page, do NOT force a section.

---

## Model Classes

Place data models in the `models` package. A small immutable value is a record; a form with many optional fields is a plain class with getters/setters (no Lombok — it is not on a test project's classpath):

```java
package {project.rootNamespace}.models;

public record LoginCredentials(String email, String password) {
}
```

```java
package {project.rootNamespace}.models;

public class BillingInfo {
    private String firstName;
    private String lastName;
    private String email;
    private String country;
    private String phone;
    private boolean shouldCreateAccount;

    public String getFirstName() {
        return firstName;
    }

    public void setFirstName(String firstName) {
        this.firstName = firstName;
    }

    // … the same getter/setter pair for lastName, email, country, phone …

    public boolean getShouldCreateAccount() {
        return shouldCreateAccount;
    }

    public void setShouldCreateAccount(boolean shouldCreateAccount) {
        this.shouldCreateAccount = shouldCreateAccount;
    }
}
```

Usage in a page object:

```java
public void fillBillingInfo(BillingInfo billingInfo) {
    map().billingFirstName().setText(billingInfo.getFirstName());
    map().billingLastName().setText(billingInfo.getLastName());
    map().billingCountry().selectByText(billingInfo.getCountry());
    map().billingPhone().setPhone(billingInfo.getPhone());
    map().billingEmail().setEmail(billingInfo.getEmail());

    if (billingInfo.getShouldCreateAccount()) {
        map().createAccountCheckBox().check();
    }
}
```

Enums for fixed option sets (sort orders, payment methods) also go in `models` and carry their visible text, which the page passes to the component:

```java
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

// HomePage.java
public void sortBy(ProductSort sort) {
    map().sortDropDown().selectByText(sort.displayText());
    browser().waitForAjax();
}
```

---

## Best Practices Summary

1. **One package per page** with 3 classes (`{PageName}`, `Map`, `Asserts`); one package per section under `sections/`
2. **Map**: component-returning methods only — no logic
3. **Page class**: `extends WebPage<Map, Asserts>`, `@Override protected String getUrl()`, optional `waitForPageLoad()`, business workflows — no assertions (a `validate*` used as a synchronization point is acceptable)
4. **Asserts**: assertion methods built from `validate*` — no actions
5. **`app().goTo(TPage.class)` / `app().createPage(TPage.class)`** — never `new` a page
6. **No direct `app().create()`** in test methods — always go through page objects and sections
7. **Descriptive, business-level method names** (`applyCoupon`, `fillBillingInfo`), not UI mechanics (`clickButton3`)
