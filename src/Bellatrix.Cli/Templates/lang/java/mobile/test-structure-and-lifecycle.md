# BELLATRIX Mobile Test Structure and Lifecycle Reference (Java)

## Test Base Classes

| Platform | JUnit 5 base | TestNG base | App annotation |
|----------|--------------|-------------|----------------|
| Android | `solutions.bellatrix.android.infrastructure.junit.AndroidTest` | `solutions.bellatrix.android.infrastructure.testng.AndroidTest` | `@solutions.bellatrix.android.infrastructure.ExecutionApp` |
| iOS | `solutions.bellatrix.ios.infrastructure.junit.IOSTest` | `solutions.bellatrix.ios.infrastructure.testng.IOSTest` | `@solutions.bellatrix.ios.infrastructure.ExecutionApp` |

Each platform has its OWN `ExecutionApp` and `Lifecycle` (`solutions.bellatrix.android.infrastructure.Lifecycle` /
`solutions.bellatrix.ios.infrastructure.Lifecycle`) — import the ones of the test's platform.

### Android (JUnit 5)

```java
package {project.rootNamespace}.android.tests.login;

import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.junit.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH,
        appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY,
        lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class ShopLoginTests extends AndroidTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensHome() {
        // Test code here
    }
}
```

### iOS (JUnit 5)

```java
package {project.rootNamespace}.ios.tests.login;

import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.ios.infrastructure.ExecutionApp;
import solutions.bellatrix.ios.infrastructure.Lifecycle;
import solutions.bellatrix.ios.infrastructure.junit.IOSTest;

@ExecutionApp(appPath = Constants.IOS_APP_PATH,
        iOSVersion = Constants.IOS_VERSION,
        deviceName = Constants.IOS_DEVICE,
        lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class ShopLoginTests extends IOSTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensHome() {
        // Test code here
    }
}
```

### TestNG

```java
package {project.rootNamespace}.android.tests.login;

import {project.rootNamespace}.common.Constants;
import org.testng.annotations.Test;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.testng.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class ShopLoginTestNgTests extends AndroidTest {
    @Test(groups = "Smoke")
    public void login_WithValidCredentials_OpensHome() {
        // Test code here
    }
}
```

No class-level test annotation is needed (the JUnit base carries `@TestInstance(PER_CLASS)`). Test methods must be
`public`. Pick ONE framework per project (match what the project already uses; JUnit 5 for new projects).

---

## The App Object

`app()` is a **method** on the test base class. Android pages also have `app()`; iOS pages do not (they expose
`create()` and `appService()`).

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `app().create()` | Create and find components |
| Pages | `app().createPage(TPage.class)` | Instantiate page objects |
| Sections | `app().createSection(TSection.class)` | Instantiate `AppSection` objects |
| AppService | `app().appService()` | Background / terminate / activate / install app, contexts; `startActivity` (Android) |
| Device | `app().device()` | Orientation, lock, notifications (Android), shake (iOS) |
| Keyboard | `app().keyboard()` | Hide keyboard; Android keys |
| Files | `app().fileSystem()` | Pull / push files |
| Touch | `app().touch()` | Swipe, tap, long press |
| Wait | `app().waitFor()` | `wait(component, Wait.to()…)` |

Details: `bellatrix-mobile-app-services`.

---

## `@ExecutionApp` and Lifecycle

Named arguments only; allowed on the class and on a single test method (the method annotation wins for that test).
**Values must be compile-time constants** — a string literal or a `public static final String` field (e.g.
`Constants.ANDROID_APP_PATH`), never a method call or a non-final field.

| Platform | Attributes (all optional) |
|----------|---------------------------|
| Android | `appPath`, `appPackage`, `appActivity`, `androidVersion`, `deviceName`, `lifecycle` (default `RESTART_EVERY_TIME`), `isMobileWebTest` |
| iOS | `appPath`, `iOSVersion`, `deviceName`, `lifecycle` (default `RESTART_EVERY_TIME`), `isMobileWebTest` (`appPackage` / `appActivity` exist but are ignored) |

An empty value falls back to the config (`defaultAppPath`, `defaultAppPackage`, `defaultAppActivity`,
`defaultDeviceName`, `defaultAndroidVersion` / `defaultIOSVersion`). Android expands `${…}` system-property
placeholders in `appPath`; iOS uses an annotation `appPath` verbatim.

```java
// Constants — compile-time constants for @ExecutionApp
package {project.rootNamespace}.common;

public final class Constants {
    public static final String ANDROID_APP_PATH = "${user.home}/apps/shop.apk";
    public static final String APP_PACKAGE = "com.example.shop";
    public static final String MAIN_ACTIVITY = ".MainActivity";
    public static final String IOS_APP_PATH = "/path/to/Shop.app";
    public static final String IOS_VERSION = "17.5";
    public static final String IOS_DEVICE = "iPhone 15";

    private Constants() {
    }
}
```

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RESTART_EVERY_TIME` | Fresh app session before every test (cleanest, slowest) |
| `Lifecycle.REUSE_IF_STARTED` | Reuse the running app across the class's tests (fastest, shared-state risk) |
| `Lifecycle.RESTART_ON_FAIL` | Keep the app after a passing test, restart after a failed one |

**BELLATRIX-Java 1.0 reality (from `AppLifecyclePlugin`):**
- The app is always closed after the last test of a class.
- **Android:** `REUSE_IF_STARTED` works (configurations are compared with `equals`). `RESTART_ON_FAIL` probes the
  session after a passing test with `executeScript("window.location")`; when the native context rejects that call
  the app is closed anyway.
- **iOS:** app configurations have no `equals()`, so from the second test of a class on, the app is restarted before
  every test whatever the lifecycle says.

Declare the intended lifecycle anyway, but never let a test depend on state left by a previous test.

```java
@ExecutionApp(appPath = Constants.ANDROID_APP_PATH, appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY, lifecycle = Lifecycle.REUSE_IF_STARTED)
public class ShopCartTests extends AndroidTest {
    @Test
    public void addItem_FromProductPage_IncrementsBadge() { /* reuses the running app */ }

    // RESTART_EVERY_TIME: verifies the empty cart on first launch
    @Test
    @ExecutionApp(appPath = Constants.ANDROID_APP_PATH, appPackage = Constants.APP_PACKAGE,
            appActivity = Constants.MAIN_ACTIVITY, lifecycle = Lifecycle.RESTART_EVERY_TIME)
    public void cart_OnFirstLaunch_IsEmpty() { /* fresh app */ }
}
```

### Config-Based Alternative

A class without `@ExecutionApp` uses `defaultAppPath` + `defaultLifeCycle` (and the other `default…` keys) from
`androidSettings` / `iOSSettings` in `src/main/resources/testFrameworkSettings.<env>.json`:

```json
{
  "androidSettings": {
    "serviceUrl": "http://127.0.0.1:4723",
    "executionType": "regular",
    "defaultLifeCycle": "restart every time",
    "defaultAppPath": "${user.home}/apps/shop.apk",
    "defaultAppPackage": "com.example.shop",
    "defaultAppActivity": ".MainActivity"
  }
}
```

(Full key list, including the required booleans and `timeoutSettings`: `bellatrix-mobile-app-services`.)

---

## Hooks (override, don't annotate)

| C# | Java (`AndroidTest` / `IOSTest`) |
|----|----------------------------------|
| `TestInit` / `[SetUp]` | `@Override protected void beforeEach() throws Exception` — runs after the app is started |
| `TestCleanup` / `[TearDown]` | `@Override protected void afterEach()` |
| `TestsArrange` / class setup | `@Override protected void beforeAll() throws Exception` — **do not use** (see below) |
| class teardown | JUnit `@Override protected void afterClass()`; TestNG `@Override protected void afterAll() throws Exception` |
| plugin registration | `@Override protected void configure()` — call `super.configure()` first |

`beforeAll()` is unusable for UI setup in BELLATRIX-Java 1.0: on iOS the built-in `DownloadDemoAppsPlugin` throws a
`NullPointerException` in its before-class hook and the base class skips `beforeAll()`; on Android the app is only
started in the per-test hook, after `beforeAll()`. Put per-class setup in `beforeEach()` (guard it with a field if it
must run once). There is no assembly bootstrap file (no `TestsInitialize`) — the base class owns start-up.

```java
@Override
protected void beforeEach() throws Exception {
    // Reset to a known screen when the class reuses the app (Android)
    app().appService().startActivity(Constants.APP_PACKAGE, ".LoginActivity");
}
```

On iOS there is no activity jump — reset by tapping through the UI (or rely on the restart).

---

## Test Naming Convention

Use `action_Condition_ExpectedResult` (lower-camel first segment, as in the BELLATRIX-Java docs):

```text
Good
login_WithValidCredentials_OpensHome()
addToCart_FromProductPage_IncrementsBadge()
rotate_ToLandscape_KeepsFormValues()

Bad
testLogin()
test1()
verifyCartWorks()
```

---

## Test Categories

```java
@Tag("Smoke")       // Critical path, every build
@Tag("Regression")  // Full suite
@Tag("CI")          // Fast tests for CI pipelines
```

TestNG: `@Test(groups = "Smoke")`. Run a category: `mvn test -Dgroups=Smoke`.

---

## Complete Test Class Template (Android)

```java
package {project.rootNamespace}.android.tests.login;

import {project.rootNamespace}.android.pages.home.HomePage;
import {project.rootNamespace}.android.pages.login.LoginPage;
import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.junit.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH,
        appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY,
        lifecycle = Lifecycle.REUSE_IF_STARTED)
public class ShopLoginTests extends AndroidTest {
    @Override
    protected void beforeEach() throws Exception {
        app().createPage(LoginPage.class).open();   // startActivity(...) — reset when the app is reused
    }

    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensHome() {
        LoginPage loginPage = app().createPage(LoginPage.class);
        loginPage.asserts().pageLoaded();

        HomePage homePage = loginPage.loginAndGoHome("demo.user", "Secret123!");

        homePage.asserts().welcomeShown("Welcome, demo.user");
    }

    @Test
    @Tag("Regression")
    public void login_WithWrongPassword_ShowsError() {
        LoginPage loginPage = app().createPage(LoginPage.class);

        loginPage.loginWith("demo.user", "wrong");

        loginPage.asserts().errorDisplayed("Invalid user name or password");
    }
}
```

---

## Required Imports

```text
// Components, factories, waits, validate* — choose ONE platform per file
solutions.bellatrix.android.components.*        (Button, TextField, PasswordField, Label, AndroidComponent, ...)
solutions.bellatrix.ios.components.*            (Button, TextField, PasswordField, Label, IOSComponent, ...)
solutions.bellatrix.android.waitstrategies.Wait / solutions.bellatrix.ios.waitstrategies.Wait
solutions.bellatrix.android.services.App / solutions.bellatrix.ios.services.App   (only where app() is not available)

// Page objects
solutions.bellatrix.android.pages.AndroidPage / PageMap / PageAsserts / AppSection
solutions.bellatrix.ios.pages.IOSPage / PageMap / PageAsserts / AppSection

// Test base + annotations (choose one framework)
solutions.bellatrix.android.infrastructure.junit.AndroidTest  + org.junit.jupiter.api.Test / Tag / Assertions
solutions.bellatrix.android.infrastructure.testng.AndroidTest + org.testng.annotations.Test / org.testng.Assert
solutions.bellatrix.android.infrastructure.ExecutionApp, solutions.bellatrix.android.infrastructure.Lifecycle
(iOS: the same under solutions.bellatrix.ios.infrastructure — IOSTest)

// Other
org.openqa.selenium.ScreenOrientation           (orientation tests)
io.appium.java_client.android.nativekey.AndroidKey  (Android key presses)

// Project
{project.rootNamespace}.common.Constants / ManualTestCase
{project.rootNamespace}.android.pages.<screen>.<Screen>   /   {project.rootNamespace}.ios.pages.<screen>.<Screen>
```

Never `import java.util.Map` in a page package — it hides the page's own `Map` class; import `java.util.List`
explicitly.
