<!-- lang:target -->
## Target Project

All generated mobile tests, page objects, sections, and models go into the BELLATRIX-Java mobile test project
named in `.bellatrix/config.json` (`domains.mobile.project`: folder `{project.dir}`, root package `{project.rootNamespace}`):

```
{project.dir}/
  pom.xml
  src/main/resources/
    application.properties
    testFrameworkSettings.dev.json
  src/test/java/{project.rootPath}/
    common/
      Constants.java
      ManualTestCase.java
    models/
    android/
      pages/{screen}/
        {ScreenName}.java
        Map.java
        Asserts.java
      pages/shared/
      components/
      tests/{featurearea}/
        {TestClassName}.java
    ios/
      … same layout
```

- **Packages**: `{project.rootNamespace}.android.pages.<screen>`, `.android.pages.shared`, `.android.components`,
  `.android.tests.<featurearea>` (and `{project.rootNamespace}.ios.…`), plus `{project.rootNamespace}.models`, `{project.rootNamespace}.common`
- **Build command**: `{project.buildCommand}`
- **Bases**: Android `AndroidPage<Map, Asserts>` / `AndroidTest` / `@ExecutionApp` (`solutions.bellatrix.android.*`);
  iOS `IOSPage<Map, Asserts>` / `IOSTest` / `@ExecutionApp` (`solutions.bellatrix.ios.*`)
- This project depends on `solutions.bellatrix:bellatrix.android` and/or `bellatrix.ios` `1.0` (test scope) — no
  web/desktop module

<!-- lang:po-step -->
3. Generate/edit page objects (3-class package per screen — `{ScreenName}.java`, `Map.java`, `Asserts.java` — one platform per package)
<!-- lang:po-rows -->
| Page object package exists | **Edit** its `Map` / page / `Asserts` classes — append new methods |
| Page object package missing | **Create** a package with the 3 classes |
<!-- lang:lifecycle -->
### Example

```java
@ExecutionApp(appPath = Constants.ANDROID_APP_PATH, appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY, lifecycle = Lifecycle.REUSE_IF_STARTED)  // class default — fast
public class ShopCartTests extends AndroidTest {
    @Override
    protected void beforeEach() throws Exception {
        app().createPage(CartPage.class).open();   // startActivity(...) — reset instead of restarting the app
    }

    @Test
    @ManualTestCase("TC-142")
    public void addToCart_FromProductPage_IncrementsBadge() { /* ... */ }

    // RESTART_EVERY_TIME: this test verifies the empty cart on first launch
    @Test
    @ManualTestCase("TC-100")
    @ExecutionApp(appPath = Constants.ANDROID_APP_PATH, appPackage = Constants.APP_PACKAGE,
            appActivity = Constants.MAIN_ACTIVITY, lifecycle = Lifecycle.RESTART_EVERY_TIME)
    public void cart_OnFirstLaunch_IsEmpty() { /* ... */ }
}
```

- Global config: `defaultLifeCycle` = `"restart every time"` in `androidSettings` / `iOSSettings` of
  `src/main/resources/testFrameworkSettings.<env>.json` — leave it.
- Class default: `Lifecycle.REUSE_IF_STARTED`; per-class or per-method override: `Lifecycle.RESTART_EVERY_TIME` or
  `Lifecycle.RESTART_ON_FAIL`. `@ExecutionApp` values must be compile-time constants (`Constants.X`).
- Per-test reset hook: `@Override protected void beforeEach() throws Exception` (also `afterEach()`); never
  `beforeAll()` (skipped / runs before the app starts in 1.0) and never your own `@BeforeEach` / `@AfterEach`.
- iOS uses `@ExecutionApp(appPath = Constants.IOS_APP_PATH, iOSVersion = …, deviceName = …, lifecycle = …)` and has no
  activity jump — reset by navigating through the UI. BELLATRIX-Java 1.0 restarts the iOS app before every test from
  the second test of a class on, whatever the lifecycle says — keep tests independent.

<!-- lang:brace-rule -->
- **Always brace control flow** — every `if`/`else`/`for`/`while`/`do`/`try` body in braces (Java style: opening brace
  on the same line), no exceptions
<!-- lang:critical-rules -->
### Java / BELLATRIX Rules

- **Prefer direct factories over XPath:** Android content-desc → `byDescription(T.class, …)`, resource-id →
  `byId(T.class, "pkg:id/x")`, text → `byText(T.class, …)`, UiSelector → `byAndroidUIAutomator(T.class, …)`; iOS
  accessibility id → `byAccessibilityIdContaining(T.class, …)` (exact match), predicate → `byIOSNsPredicate(T.class, …)`,
  class chain → `byIOSClassChain(T.class, …)`. Use `byXPath(T.class, …)` only for structural locators
- **No `byAccessibilityId` single factory** on either platform; Android has no usable `byName` / `byTag`; iOS has no
  `byText` / `byDescription` / `byIdContaining`
- **Class token first:** `create().byId(TextField.class, "…")`; capital P in `byXPath`
- **Android resource-ids are fully qualified** in `byId` (`com.app:id/login`)
- **One platform per file:** import `solutions.bellatrix.android.*` OR `solutions.bellatrix.ios.*` — never both
  (`reference to Button is ambiguous`)
- **Page bases:** `AndroidPage<Map, Asserts>` (has `app()`) / `IOSPage<Map, Asserts>` (has `create()`,
  `appService()`, no `app()`); `Map extends PageMap`, `Asserts extends PageAsserts<Map>`; never `WebPage` /
  `DesktopPage`; elements are methods, not fields; never `import java.util.Map` in a page package
- **Tappable elements are `Button`:** `Label` has no `click()`; rows/cells/links → `Button` or a custom component
  extending `Button`
- **Known built-in gaps (1.0):** no `isVisible()` / `validateIsVisible()` (use `toBeVisible().waitToBe()`);
  `isDisabled()` / `validateIsDisabled()` do not track the real state (assert `getAttribute("enabled")`); Android
  `ComboBox.selectByText` / `RadioGroup` child lookups crash (custom `SpinnerComboBox`, tap radios as `Button`); iOS
  `CheckBox` / `ToggleButton` state does not work (custom `SwitchButton` reading `value`); `validateNumberIs(3.0)` and
  `SeekBar.set(50.0)` need doubles
- **Wait-chain trap:** `x.toBeVisible().click()` does not compile — end with `waitToBe()` or assign to the concrete type
- **Negative waits:** Android `app().waitFor().wait(c, Wait.to().notExist())`; iOS (and Android) poll
  `app().create().allBy…(…).isEmpty()` with `WebDriverWait` — never `toNotExist().waitToBe()`
- **Never drop to the raw driver:** no `getWrappedAndroidDriver().findElement(...)`, no `AppiumBy.*` / `By.*` in
  project code — use `create().by…` / `allBy…` or a relative-XPath child factory on a parent component
- **Custom components** go in `{project.rootNamespace}.android.components` / `.ios.components`: (a) extend the closest built-in
  (`Button`, `ComboBox`, …) to add behavior, or (b) override its public methods; use them in `Map` classes via
  `create().byDescription(MyComponent.class, "…")`
- **Always brace control flow:** never `if (x) return;` or `if (row == null) continue;` without braces
- **`app().createPage(TPage.class)`** in tests and Android pages, `new App().createPage(TPage.class)` in iOS pages —
  never `new TPage()`
- **Validate methods:** `validateTextIs()`, `validateTextContains()`, `validateIsChecked()`, `validateIsUnchecked()`,
  `validateIsOn()`, `validateIsOff()`, `validateNumberIs()` — they read the value ONCE, so wait first; fall back to
  JUnit `Assertions` for attributes, counts, and app/device state
- **No `Thread.sleep`:** fluent waits (`toBeVisible()`, `toBeClickable()`, `toHaveContent()`) and the wait service
- **Touch:** `app().touch().swipe(...)` executes immediately; queued `tap/press/…` chains need `perform()`
- **Tests use page objects only** — no `app().create()` in test methods
- **SPECTRA traceability:** `@ManualTestCase("TC-XXX")` (project-local string annotation in
  `{project.rootNamespace}.common.ManualTestCase` — create it if missing; NOT `@ParameterizedTest` / `@CsvSource`) and update
  the SPECTRA file's `automated_by`
- **1 TC = 1 test method** — `@ManualTestCase` is not repeatable; never merge TCs via parameterized tests
- **Separate test files by feature** — e.g. `android/tests/login/ShopLoginTests.java`,
  `android/tests/cart/ShopCartTests.java`
- **Build only:** `{project.buildCommand}` until green — never `mvn test` yourself
