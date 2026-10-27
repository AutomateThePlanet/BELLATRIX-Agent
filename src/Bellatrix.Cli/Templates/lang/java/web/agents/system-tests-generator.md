<!-- lang:page-objects-step -->
3. Generate/edit page objects (3-class package per page: `{PageName}` + `Map` + `Asserts`; `WebSection` packages for blocks shared by ≥2 pages)
<!-- lang:target-project -->
## Target Project (Java / Maven)

All generated web tests, page objects, sections, and models go into the BELLATRIX-Java web test project recorded in `bellatrix-web-project-structure` (written here as `my-project-web-tests` with root package `myproject.webtests` — use the real names):

```
my-project-web-tests/
  pom.xml                                   # solutions.bellatrix:bellatrix.web:1.0, JUnit 5
  src/main/resources/
    application.properties                  # environment=dev
    testFrameworkSettings.dev.json          # webSettings
  src/test/java/myproject/webtests/
    pages/{page}/
      {PageName}.java
      Map.java
      Asserts.java
    sections/{section}/
      {SectionName}.java
      Map.java
      Asserts.java
    components/
      {CustomComponent}.java
    models/
      {ModelName}.java
    tests/{featurearea}/
      {FeatureArea}Tests.java
    common/
      ManualTestCase.java
```

- **Packages**: `myproject.webtests.pages.<page>`, `.sections.<section>`, `.components`, `.models`, `.tests.<featurearea>`, `.common` — the `package` line always matches the folder
- **Build command**: `mvn -q test-compile` (from the folder that holds `pom.xml`)
- **Base classes**: pages `WebPage<Map, Asserts>`, sections `WebSection<Map, Asserts>`, maps `PageMap`, asserts `PageAsserts<Map>` (all `solutions.bellatrix.web.pages`); tests `WebTest` (`solutions.bellatrix.web.infrastructure.junit`; `…infrastructure.testng` only if the project already uses TestNG)
- **SPECTRA traceability annotation**: `@ManualTestCase("TC-XXX")` from `common/ManualTestCase.java`. BELLATRIX-Java has no built-in string version — if it does not exist yet, create it:

```java
package myproject.webtests.common;

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

Usage (one per method — stacking two does not compile):

```java
@Test
@ManualTestCase("TC-210")
@Tag("Smoke")
public void applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() {
    var homePage = app().goTo(HomePage.class);
    homePage.addProductById(28);
    var cartPage = homePage.viewCart();
    cartPage.applyCoupon("happybirthday");
    cartPage.asserts().couponApplied();
    cartPage.asserts().totalPrice("54.00");
}
```

<!-- lang:lifecycle -->
## Browser Lifecycle Policy (Speed Optimization)

Two lifecycle scopes exist and they are intentionally different:

| Scope | Setting | Value | Why |
|-------|---------|-------|-----|
| **Global default** (`testFrameworkSettings.<env>.json`) | `webSettings.defaultLifeCycle` | `"restart every time"` | Safe default for ad-hoc runs and CI isolation |
| **Test class annotation** (default for generated classes) | `@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)` | `REUSE_IF_STARTED` | Keeps one browser across the class's tests for speed |

### Rules

1. **Default for new test classes: `Lifecycle.REUSE_IF_STARTED`.** Generate `@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)` unless the tests need a clean browser. `lifecycle` is mandatory (no default) and both values must be compile-time constants.
2. **Do NOT change the global config.** Leave `defaultLifeCycle` as `"restart every time"`; the class annotation overrides it.
3. **Switch a class (or one method) to `Lifecycle.RESTART_EVERY_TIME` (or `RESTART_ON_FAIL`) when:**
   - the test logs in, logs out, or otherwise changes auth cookies / tokens
   - the test leaves server- or client-side state the next test would see (cart contents, local/session storage, user settings) and cleanup isn't reliable
   - the test verifies first-visit behavior (cookie banner, onboarding, default sort/currency)
   - the test closes windows or crashes the page
4. **Prefer cleanup first:** reset state in `beforeEach()` / `afterEach()` overrides (`app().cookies().deleteAllCookies()`, `app().browser().clearLocalStorage()`, `app().browser().clearSessionStorage()`, emptying the cart via the page object) and keep `REUSE_IF_STARTED`. Every test still opens its own start page (`app().goTo(TPage.class)`). An exception in `beforeEach()` fails that test in setup, while one in `beforeAll()` is only printed and the tests still run — keep the hooks to resets, no assertions.
5. **Per-method override:** `@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)` on a single test method overrides the class annotation for that test only. Always repeat `browser = …` on the method — an omitted browser there is `NOT_SET`, not the class value.
6. **Document the override** with a comment above the annotation.

The browser is always closed after the test class, whatever the lifecycle.

### Example

```java
@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)   // class default — fast
public class CartTests extends WebTest {
    @Override
    protected void afterEach() {
        app().cookies().deleteAllCookies();          // reset state instead of restarting the browser
        app().browser().clearLocalStorage();
        app().browser().clearSessionStorage();
    }

    @Test
    @ManualTestCase("TC-210")
    @Tag("Smoke")
    public void applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }

    // RESTART_EVERY_TIME: verifies the cookie banner shown on the first visit
    @Test
    @ManualTestCase("TC-200")
    @Tag("Regression")
    @ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
    public void firstVisit_ShowsCookieBanner() { /* ... */ }
}
```

<!-- lang:critical-rules -->
## Critical Rules (Java)

- **3-class page objects:** `{PageName} extends WebPage<Map, Asserts>` with `@Override protected String getUrl()` (optional `@Override protected void waitForPageLoad()`), `Map extends PageMap`, `Asserts extends PageAsserts<Map>` — one package per page, `package` = folder, no partial classes. Sections extend `WebSection<Map, Asserts>`. NOT a raw `WebPage`, NOT `DesktopPage`. Every page / asserts class extends the generic base **directly** (`map()` reads the immediate superclass's type arguments)
- **`byXPath` — capital P.** `byXpath` / `allByXpath` do not exist. Class token first: `create().byId(Button.class, "x")`
- **Prefer direct factories over XPath:** `[data-testid]` → `byCss(T.class, "[data-testid='…']")`; static id → `byId(T.class, "…")`; `name` → `byName(T.class, "…")`; exact link text → `byLinkText(Anchor.class, "…")`. Reserve `byXPath` for text-based (`normalize-space()`) or structural locators (label → input, row → cell) and dynamic fragments (`String.format`). There is NO `byDataTestId`
- **Component types from the live element:** `Button`, `TextInput`, `PasswordInput`, `EmailInput`, `PhoneInput`, `NumberInput`, `CheckBox`, `RadioButton`, `Select`, `Option`, `TextArea`, `Anchor`, `Label`, `Heading`, `Span`, `Div`, `Image`, `FileInput`, `Frame`, `Table`. There is no `TextField`, `Password`, `H1`, `ListItem`, or `ComboBox`. `Div`/`Span`/`Label`/`Heading` have no `click()` — type clickable non-buttons as `Button`. No component has `clear()`
- **Validate methods:** `validateIsVisible()` / `validateNotVisible()`, `validateTextIs()` / `validateTextContains()`, `validateValueIs()`, `validateIsChecked()` / `validateIsUnchecked()`, `validateIsDisabled()` / `validateNotDisabled()`, `validateHrefIs()` — only on components that implement them (`Select` → `Assertions.assertEquals(x, select.getSelected().getText())`; `PasswordInput` / `EmailInput` have no `validateText*`). There is no `validateExists` / `validateAttributeIs` / `validateInnerTextIs`. Use JUnit `Assertions` (or TestNG `Assert`) only for URL/title strings, counts, the selected option, and arbitrary attributes. `app().browser().getUrl()` is a `String`; `assertLandedOnPage(…)` asserts once (wait for the URL first)
- **Waits:** fluent `toBeVisible()`, `toBeClickable()`, `toHaveContent()`, `toExist()` (+ `waitToBe()` to force), and `app().browser().waitUntilPageLoadsCompletely()` / `waitForAjax()`, `app().navigate().waitForPartialUrl(…)`. Disappearance: `app().waitFor().wait(c, Wait.to().notExist())` / `Wait.to().notBeVisible()` — NOT `toNotExist().waitToBe()` (throws after a successful wait). Timeouts are in seconds. `x.toBeVisible().click()` does not compile (generic return type) — assign it or end with `waitToBe()`. Never `Thread.sleep`
- **Never drop to raw Selenium** for finding or clicking: no `getWrappedDriver().findElement(s)`, `By.*`, or `WebDriverWait` in test-project code. Use `create().by…` / `allBy…`, scoped `component.createBy…` (`.//` XPaths), and inside custom components the scoped factories or the protected `componentCreateService`. Use `app().browser()` (frames, tabs, storage), `app().dialogs()` (native alerts), `app().script()`, `app().cookies()` for low-level needs; there is NO Interactions service — keyboard chords and drag-and-drop go in a custom component method using Selenium `Actions` on its own element
- **Custom components when built-ins aren't enough:** under `components/`, either a new component extending `solutions.bellatrix.web.components.WebComponent` or a child of an existing component overriding its public methods (e.g. `Button.click()`); public no-argument constructor; use it via `create().byCss(MyComponent.class, …)`. Never modify BELLATRIX source
- **Navigation:** `app().navigate().to(url)` (NOT `app().browser().navigate`); pages via `app().goTo(TPage.class)` (opens) / `app().createPage(TPage.class)` (already open) — never `new TPage()`
- **Tests use page objects only** — no `app().create()` in test methods
- **Never `import java.util.*;`** in a page or section package (the `Map` class clash) — import `java.util.List` explicitly
- **Always brace control flow:** every `if`/`else`/`for`/`while`/`do`/`try` body uses `{ }` (opening brace on the same line), even single statements — no `if (x) continue;`
- **SPECTRA traceability:** `@ManualTestCase("TC-XXX")` (NOT `@ParameterizedTest`) on every automated SPECTRA case, plus `@Tag("Smoke")` for high priority / `@Tag("Regression")` otherwise (TestNG: `@Test(groups = "Smoke")`); update the SPECTRA file's `automated_by` with `my-project-web-tests/src/test/java/myproject/webtests/tests/{featurearea}/{FeatureArea}Tests.java`
- **Build-only:** `mvn -q test-compile` until it succeeds. Never run `mvn test` yourself; give the user `mvn test -Dtest={TestClassName}#{testMethodName}`

