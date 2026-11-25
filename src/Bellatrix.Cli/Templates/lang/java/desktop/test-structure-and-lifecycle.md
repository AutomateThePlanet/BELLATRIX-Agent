# BELLATRIX Desktop Test Structure and Lifecycle Reference (Java)

## Test Base Classes

### JUnit 5 (default)

```java
package {project.rootNamespace}.tests.login;

import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTests extends DesktopTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensMainWindow() {
        // Test code here
    }
}
```

No class-level test annotation is needed (the base class carries `@TestInstance(PER_CLASS)`). Test methods must be
`public`.

### TestNG

```java
package {project.rootNamespace}.tests.login;

import {project.rootNamespace}.common.Constants;
import org.testng.annotations.Test;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.testng.DesktopTest;

@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTestNgTests extends DesktopTest {
    @Test(groups = "Smoke")
    public void login_WithValidCredentials_OpensMainWindow() {
        // Test code here
    }
}
```

Pick ONE framework per project (match what the project already uses; JUnit 5 for new projects).

### Hooks (override, don't annotate)

| C# | Java (`DesktopTest`) |
|----|----------------------|
| `TestInit` / `[SetUp]` | `@Override protected void beforeEach() throws Exception` |
| `TestCleanup` / `[TearDown]` | `@Override protected void afterEach()` |
| `TestsArrange` / class setup | `@Override protected void beforeAll() throws Exception` — **unreliable on desktop 1.0, use `beforeEach()`** |
| class teardown | JUnit `@Override protected void afterClass()`; TestNG `@Override protected void afterAll() throws Exception` |
| plugin registration | `@Override protected void configure()` — call `super.configure()` first |

`beforeAll()` is skipped in BELLATRIX-Java 1.0 desktop: the built-in `DownloadDemoAppsPlugin` throws a
`NullPointerException` in its before-class hook (unset `ThreadLocal<Boolean>`), the base class swallows it and never
reaches `beforeAll()`. Put per-class setup in `beforeEach()` (guard it with a field if it must run once).

---

## The App Object

`app()` is a **method** on the test base class. Desktop page objects do NOT have `app()` — they expose `create()` and
`appService()`.

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `app().create()` | Create and find desktop components |
| AppService | `app().appService()` | Window operations (`getTitle()`, `maximize()`, `back()`, `forward()`) |
| Wait | `app().waitFor()` | Wait for conditions (`wait(component, Wait.to()...)`) |
| Pages | `app().createPage(TPage.class)` | Instantiate page objects |
| Sections | `app().createSection(TSection.class)` | Instantiate `AppSection` objects |

---

## `@ExecutionApp` and Lifecycle

```java
@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.REUSE_IF_STARTED)
```

`solutions.bellatrix.desktop.infrastructure.ExecutionApp` — named arguments only: `appPath` (default `""`),
`lifecycle` (default `RESTART_EVERY_TIME`), `width`, `height`. Allowed on the class and on a method (method wins).
**Annotation values must be compile-time constants** — a string literal or a `public static final String` field
(e.g. `Constants.MY_APP_PATH`), never a method call or a non-final field. The desktop `Lifecycle` is
`solutions.bellatrix.desktop.infrastructure.Lifecycle` (not the web/mobile one). UWP apps take their AUMID as
`appPath` (`"Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"`).

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RESTART_EVERY_TIME` | App restarts before every test (cleanest, slowest) |
| `Lifecycle.REUSE_IF_STARTED` | Reuses running app (fastest, shared state risk) |
| `Lifecycle.RESTART_ON_FAIL` | Restarts only after a failed test |

**BELLATRIX-Java 1.0 reality:** `AppLifecyclePlugin` compares app configurations by reference (`AppConfiguration`
has no `equals()`), so from the second test of a class on, the app is restarted before every test whatever the
lifecycle says, and it is always closed after the class. Declare the intended lifecycle anyway, but never let a test
depend on state left by a previous test.

### Config-Based Alternative

A class without `@ExecutionApp` uses `desktopSettings.defaultAppPath` + `defaultLifeCycle` from
`src/main/resources/testFrameworkSettings.dev.json`:

```json
{
  "desktopSettings": {
    "serviceUrl": "http://127.0.0.1:4723",
    "executionType": "regular",
    "defaultLifeCycle": "restart every time",
    "defaultAppPath": "C:\\Program Files\\MyApp\\MyApp.exe"
  }
}
```

(Full key list, including the required booleans and `timeoutSettings`: `bellatrix-desktop-app-services`.)

---

## Test Naming Convention

Use the pattern: `action_Condition_ExpectedResult` (lower-camel first segment, as in the BELLATRIX-Java docs)

```text
Good
login_WithValidCredentials_OpensMainWindow()
deleteItem_WithConfirmation_RemovesItemFromList()
selectTheme_DarkMode_UpdatesAppearance()

Bad
testLogin()
test1()
verifyDeleteWorks()
```

---

## Test Categories

```java
@Tag("Smoke")       // Critical path tests, run on every build
@Tag("Regression")  // Full test suite
@Tag("CI")          // Fast tests suitable for CI pipelines
```

TestNG: `@Test(groups = "Smoke")`. Run a category: `mvn test -Dgroups=Smoke`.

---

## Complete Test Class Template

```java
package {project.rootNamespace}.tests.login;

import {project.rootNamespace}.common.Constants;
import {project.rootNamespace}.common.UiaXPathFindStrategy;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.Label;
import solutions.bellatrix.desktop.components.PasswordField;
import solutions.bellatrix.desktop.components.TextField;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTemplateTests extends DesktopTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensMainWindow() {
        // Arrange
        TextField usernameField = app().create().byAutomationId(TextField.class, "txtUsername");
        PasswordField passwordField = app().create().byAutomationId(PasswordField.class, "txtPassword");
        Button loginButton = app().create().byAutomationId(Button.class, "btnLogin");

        // Act
        usernameField.setText("testuser");
        passwordField.setPassword("SecurePass123");
        loginButton.click();

        // Assert
        Label welcomeLabel = app().create().by(Label.class, new UiaXPathFindStrategy("//Text[@AutomationId='lblWelcome']"));
        welcomeLabel.toBeVisible().waitToBe();
        welcomeLabel.validateTextIs("Welcome");
    }

    @Test
    @Tag("Regression")
    public void login_WithInvalidPassword_ShowsErrorMessage() {
        // Arrange
        TextField usernameField = app().create().byAutomationId(TextField.class, "txtUsername");
        PasswordField passwordField = app().create().byAutomationId(PasswordField.class, "txtPassword");
        Button loginButton = app().create().byAutomationId(Button.class, "btnLogin");

        // Act
        usernameField.setText("testuser");
        passwordField.setPassword("wrongpassword");
        loginButton.click();

        // Assert
        Label errorLabel = app().create().byAutomationId(Label.class, "lblError");
        errorLabel.toBeVisible().waitToBe();
        errorLabel.validateTextIs("Invalid credentials");
    }
}
```

---

## Required Imports

```text
// Components, factories, waits
solutions.bellatrix.desktop.components.*            (Button, TextField, PasswordField, Label, ...)
solutions.bellatrix.desktop.waitstrategies.Wait
solutions.bellatrix.desktop.services.App            (only where app() is not available)

// Page objects
solutions.bellatrix.desktop.pages.DesktopPage / PageMap / PageAsserts

// Test base + annotations (choose one framework)
solutions.bellatrix.desktop.infrastructure.junit.DesktopTest   + org.junit.jupiter.api.Test / Tag / Assertions
solutions.bellatrix.desktop.infrastructure.testng.DesktopTest  + org.testng.annotations.Test / org.testng.Assert
solutions.bellatrix.desktop.infrastructure.ExecutionApp, solutions.bellatrix.desktop.infrastructure.Lifecycle

// Project
{project.rootNamespace}.common.Constants / ManualTestCase / UiaXPathFindStrategy
{project.rootNamespace}.pages.<window>.<Window>
```

Never `import java.util.Map;` in a page package — that import shadows the page's own `Map` class and breaks
`DesktopPage<Map, Asserts>`; import `java.util.List` explicitly and write a JDK map as `java.util.Map<K, V>`.
