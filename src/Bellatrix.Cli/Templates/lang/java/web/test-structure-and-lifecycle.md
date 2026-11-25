---
description: Reference skill for BELLATRIX Java web test classes (JUnit 5 / TestNG WebTest), the app() object, the @ExecutionBrowser annotation and browser lifecycle, setup hooks, and naming conventions.
---
# BELLATRIX Web Test Structure and Lifecycle Reference (Java)

## Test Base Classes

### JUnit 5 (default)

```java
package {project.rootNamespace}.tests.login;

import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.junit.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTests extends WebTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensDashboard() {
        // Test code here
    }
}
```

### TestNG

```java
package {project.rootNamespace}.tests.login;

import org.testng.annotations.Test;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.testng.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTestNgTests extends WebTest {
    @Test(groups = "Smoke")
    public void login_WithValidCredentials_OpensDashboard() {
        // Test code here
    }
}
```

`WebTest` exists in both `solutions.bellatrix.web.infrastructure.junit` and `solutions.bellatrix.web.infrastructure.testng` — import exactly ONE of them, and use the matching `@Test` (`org.junit.jupiter.api.Test` vs `org.testng.annotations.Test`). No class-level annotation (like C# `[TestFixture]`) is needed; the JUnit base already declares `@TestInstance(PER_CLASS)`. `ExecutionBrowser`, `Browser`, and `Lifecycle` live in `solutions.bellatrix.web.infrastructure` (each domain has its own `Lifecycle` — import the web one).

---

## The app() Object

`app()` (type `solutions.bellatrix.web.services.App`) is the entry point for all web interactions. It is a **method** of `WebTest` in test classes and of `WebPage` / `WebSection` / `PageMap` / `PageAsserts` in page objects.

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `app().create()` | Create and find components (`byCss`, `byId`, `byXPath`, …) |
| Navigation | `app().navigate()` | `to(url)`, `waitForPartialUrl(…)`, `toLocalPage(…)` |
| Browser | `app().browser()` | `getUrl()`, `getTitle()`, `back/forward/refresh`, page-level waits, frames, tabs, storage |
| JavaScript | `app().script()` | Execute scripts |
| Dialogs | `app().dialogs()` | Native alert / confirm / prompt |
| Cookies | `app().cookies()` | Add / read / delete cookies |
| Waits | `app().waitFor()` | Run a `Wait.to()…` strategy against a component now |
| createPage | `app().createPage(P.class)` | Page object instance (no navigation) |
| goTo | `app().goTo(P.class)` | Page object instance + `open()` |
| createSection | `app().createSection(S.class)` | Section object instance |

There is no Interactions service and no `App.Assert` in Java. See `bellatrix-web-app-services`.

---

## @ExecutionBrowser Annotation and Lifecycle

```java
@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
@ExecutionBrowser(browser = Browser.FIREFOX, lifecycle = Lifecycle.REUSE_IF_STARTED)
@ExecutionBrowser(browser = Browser.EDGE, lifecycle = Lifecycle.RESTART_ON_FAIL)
@ExecutionBrowser(browser = Browser.CHROME_HEADLESS, lifecycle = Lifecycle.RESTART_EVERY_TIME)
@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED, width = 1280, height = 800)   // class level only
```

Rules (compile-time and runtime):
- **`lifecycle` is mandatory** — it has no default; `@ExecutionBrowser(browser = Browser.CHROME)` does not compile.
- Arguments are **named** (`browser = …`, `lifecycle = …`) and must be compile-time constants.
- `Browser`: `CHROME`, `CHROME_HEADLESS`, `CHROME_MOBILE` (+ `deviceName = DeviceName.…`, `import solutions.bellatrix.web.infrastructure.DeviceName;`), `FIREFOX`, `FIREFOX_HEADLESS`, `EDGE`, `EDGE_HEADLESS`, `OPERA`, `SAFARI`, `INTERNET_EXPLORER`.
- **On a method, always repeat `browser = …`**: a method-level annotation replaces the class one entirely, and an omitted `browser` there means `NOT_SET` (it does NOT inherit the class browser or the config default). `width` / `height` are honored only at class level.

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RESTART_EVERY_TIME` | A new browser for every test — fresh cookies, cache, storage (cleanest, slowest) |
| `Lifecycle.REUSE_IF_STARTED` | Reuses the running browser while the configuration (browser, lifecycle, size) is unchanged (fastest; cookies/storage carry over). The browser is always closed after the test class |
| `Lifecycle.RESTART_ON_FAIL` | Restarts only after a failed test |

The annotation on the class applies to all its tests; the annotation on a single test method overrides the class for that test only:

```java
@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)   // class default — fast
public class CatalogTests extends WebTest {
    @Test
    public void sortByPrice_LowToHigh_OrdersProducts() { /* ... */ }

    // RESTART_EVERY_TIME: this test logs in and must not leak the auth cookie
    @Test
    @ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
    public void login_WithValidCredentials_ShowsAccountMenu() { /* ... */ }
}
```

### Config-Based Default

Without an annotation, BELLATRIX uses `webSettings` in `testFrameworkSettings.<env>.json` (env from `application.properties` → `environment=dev`):

```json
{
  "webSettings": {
    "executionType": "regular",
    "defaultBrowser": "chrome",
    "defaultLifeCycle": "restart every time"
  }
}
```

---

## Setup and Teardown Hooks

Override the BELLATRIX hooks (they run inside the plugin pipeline, after the browser is started) — do not add your own `@BeforeEach` / `@BeforeMethod` methods that need the browser:

```java
public class CartTests extends WebTest {
    private CartPage cartPage;

    // Runs before each test, after the browser is ready
    @Override
    protected void beforeEach() {
        cartPage = app().createPage(CartPage.class);
    }

    // Runs after each test
    @Override
    protected void afterEach() {
        app().cookies().deleteAllCookies();
    }
}
```

| Hook | JUnit 5 base | TestNG base |
|------|--------------|-------------|
| once per run (register plugins) | `protected void configure()` — call `super.configure()` first | same |
| once per class, before the first test | `protected void beforeAll() throws Exception` | same |
| before each test | `protected void beforeEach() throws Exception` | same |
| after each test | `protected void afterEach()` | same |
| once per class, after the last test | `protected void afterClass()` | `protected void afterAll() throws Exception` |

Traps:
- **Exceptions thrown from `beforeAll()` are caught and printed by the base class — the tests still run** (against a half-initialized state). Exceptions from `beforeEach()` are printed and then re-thrown by the web browser-lifecycle plugin, so that test fails in setup. Keep setup to page creation and state resets; do assertions inside the test.
- TestNG base: never declare `beforeClass()` / `afterClassCore()` etc. in your test — those names are the framework's own `@BeforeClass`/`@AfterClass` methods; override only the hooks in the table.
- There is no `TestsInitialize` / assembly teardown in Java: the lifecycle plugin closes the browser after each class.

---

## Test Naming Convention

Use the pattern `action_Condition_ExpectedResult` (Java methods start lower-case):

```java
// Good
public void login_WithValidCredentials_OpensDashboard()
public void applyCoupon_WithExpiredCode_ShowsErrorMessage()
public void addToCart_WhenOutOfStock_DisablesButton()

// Bad
public void testLogin()
public void test1()
public void verifyCartWorks()
```

---

## Test Categories

```java
@Tag("Smoke")        // critical path, every build          (JUnit 5)
@Tag("Regression")   // full suite
@Tag("CI")           // fast, stable tests for CI pipelines
```

TestNG: `@Test(groups = "Smoke")`. Run a category with `mvn test -Dgroups=Smoke` (works for both with Surefire).

---

## Complete Test Class Template

```java
package {project.rootNamespace}.tests.checkout;

import {project.rootNamespace}.models.BillingInfo;
import {project.rootNamespace}.pages.cart.CartPage;
import {project.rootNamespace}.pages.checkout.CheckoutPage;
import {project.rootNamespace}.pages.home.HomePage;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.junit.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)
public class CheckoutTests extends WebTest {
    @Test
    @Tag("Smoke")
    public void applyCoupon_WithValidCode_ReducesTotal() {
        var homePage = app().goTo(HomePage.class);
        homePage.addProductById(28);
        homePage.viewCart();

        var cartPage = app().createPage(CartPage.class);
        cartPage.applyCoupon("happybirthday");

        cartPage.asserts().couponApplied();
        cartPage.asserts().totalPrice("54.00");
    }

    @Test
    @Tag("Regression")
    public void placeOrder_WithValidBillingInfo_ShowsOrderReceived() {
        var checkoutPage = app().goTo(CheckoutPage.class);

        var billingInfo = new BillingInfo();
        billingInfo.setFirstName("In");
        billingInfo.setLastName("Deepthought");
        billingInfo.setCountry("Bulgaria");
        billingInfo.setPhone("+00359894646464");
        billingInfo.setEmail("info@example.com");
        checkoutPage.fillBillingInfo(billingInfo);
        var orderReceivedPage = checkoutPage.placeOrder();

        orderReceivedPage.asserts().orderReceived();
    }
}
```

Tests contain only page-object calls (`page.action(…)`, `page.asserts().x(…)`) — no `app().create()` in test methods.

---

## Required Imports

```java
// Test base class (choose ONE)
import solutions.bellatrix.web.infrastructure.junit.WebTest;     // JUnit 5
import solutions.bellatrix.web.infrastructure.testng.WebTest;    // TestNG

// Browser annotation + enums
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;

// Test annotations (choose ONE family)
import org.junit.jupiter.api.Test;          // + org.junit.jupiter.api.Tag, org.junit.jupiter.api.Assertions
import org.testng.annotations.Test;         // + org.testng.Assert

// Components / services (page objects, custom components, recipes)
import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.services.DialogButton;
import solutions.bellatrix.web.waitstrategies.Wait;

// Project
import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.pages.login.LoginPage;
import {project.rootNamespace}.models.BillingInfo;
```

Never `import java.util.*;` in a page package — the page's `Map` class would clash with `java.util.Map`.
