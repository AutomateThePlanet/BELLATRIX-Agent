# BELLATRIX Desktop App Services Reference (Java)

The entry point is the **method** `app()` on the test base class (`DesktopTest`), returning
`solutions.bellatrix.desktop.services.App`. Desktop page objects have **no** `app()` — they expose `create()` and
`appService()`; anywhere else (custom components, sections, helpers) construct `new App()` or the service directly.

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `App.Components.CreateByXPath<T>(…)` (C#) | `app().create().byAutomationId(T.class, …)` / `create().by(T.class, new UiaXPathFindStrategy(…))` |
| `app().create().byXPath(T.class, …)` | `app().create().by(T.class, new UiaXPathFindStrategy(…))` (built-in XPath strategy is broken in 1.0) |
| `app().create().byId(T.class, …)` | `app().create().byAutomationId(T.class, …)` (no `byId` on desktop) |
| `app().goTo(LoginWindow.class)` / navigating by URL | `app().createPage(LoginWindow.class)` (no `goTo` on desktop) |
| `new LoginWindow()` | `app().createPage(LoginWindow.class)` |
| `App.AppService.Title` | `app().appService().getTitle()` |
| `button.toBeVisible().click()` | `button.toBeVisible().waitToBe(); button.click();` (wait-chain generics trap) |

---

## ComponentCreateService (`app().create()`)

### Single Component Creation

```java
// By AutomationId (PREFERRED)
app().create().byAutomationId(Button.class, "btnOk");

// By Name
app().create().byName(TextField.class, "Username");

// By XPath (PascalCase required for WinAppDriver) — via the project's UiaXPathFindStrategy
app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@AutomationId='btnOk' or @Name='OK']"));

// By ClassName
app().create().byClass(TextField.class, "TextBox");

// By Tag (ControlType)
app().create().byTag(Label.class, "Text");

// By AccessibilityId / AutomationId fragment
app().create().byAccessibilityId(Button.class, "num8Button");
app().create().byIdContaining(Label.class, "status");
```

### Collection Creation (`java.util.List<T>`)

```java
app().create().allByAutomationId(Button.class, "item");
app().create().allByName(Button.class, "Submit");
app().create().allBy(Label.class, new UiaXPathFindStrategy("//Text[@ClassName='Label']"));
app().create().allByClass(CheckBox.class, "CheckBox");
app().create().allByTag(Button.class, "Button");
```

---

## AppService (`app().appService()`)

| Method | Returns | Description |
|--------|---------|-------------|
| `getTitle()` | `String` | Current window title |
| `maximize()` | `void` | Maximize the application window |
| `back()` | `void` | Navigate back |
| `forward()` | `void` | Navigate forward |
| `getWrappedDriver()` | `WindowsDriver` | Raw Appium driver — low-level operations only, never element lookup |

There is no Java equivalent of C#'s `GetCurrentViewSnapshot()` — UI-tree inspection is the `bellatrix desktop
snapshot` CLI's job during scenario execution, not the test's.

### Example Usage

```java
// Get window title
String title = app().appService().getTitle();
Assertions.assertTrue(title.contains("My Application"));

// Maximize window (the driver already maximizes on start)
app().appService().maximize();
```

---

## Wait Service (`app().waitFor()`)

```java
import solutions.bellatrix.desktop.waitstrategies.Wait;

// Wait for a component with a specific strategy (evaluates only the condition — right for negative waits)
app().waitFor().wait(element, Wait.to().beVisible());
app().waitFor().wait(spinner, Wait.to().notExist());
```

`Wait.to()` offers `exist()`, `notExist()`, `beVisible()`, `notBeVisible()`, `beClickable()`, `haveContent()`, each
also with `(timeoutSeconds, sleepSeconds)`. Outside a test use `new ComponentWaitService().wait(...)`
(`solutions.bellatrix.desktop.services`).

Most commonly, use fluent wait methods on components instead (assign to the concrete type — see the wait-chain trap):

```java
Button button = app().create().byAutomationId(Button.class, "btn").toBeClickable();
Label label = app().create().byName(Label.class, "Status").toHaveContent();
```

---

## Page Object Factory

### `app().createPage(TPage.class)`

Returns the page object (one instance per page class and thread) without any navigation. Use when the window is
already open:

```java
LoginWindow loginWindow = app().createPage(LoginWindow.class);
loginWindow.loginWith("user", "pass");

MainWindow mainWindow = app().createPage(MainWindow.class);
mainWindow.asserts().windowLoaded();
```

Inside a page object (no `app()`), return the next page with `new App().createPage(MainWindow.class)`.
Sections (`AppSection<Map, Asserts>`) come from `app().createSection(X.class)`.

---

## Configuration: `src/main/resources/testFrameworkSettings.dev.json`

`application.properties` (same folder) selects the file: `environment=dev` → `testFrameworkSettings.dev.json`
(override with `-Denvironment=qa`). The section name is `desktopSettings`.

```json
{
  "troubleshootingSettings": {
    "debugInformationEnabled": "true"
  },
  "desktopSettings": {
    "serviceUrl": "http://127.0.0.1:4723",
    "executionType": "regular",
    "defaultLifeCycle": "restart every time",
    "defaultAppPath": "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
    "downloadDemoApps": "false",
    "artificialDelayBeforeAction": "0",
    "automaticallyScrollToVisible": "false",
    "screenshotsOnFailEnabled": "true",
    "screenshotsSaveLocation": "${user.home}/BELLATRIX/screenshots",
    "videosOnFailEnabled": "false",
    "videosSaveLocation": "${user.home}/BELLATRIX/videos",
    "timeoutSettings": {
      "implicitWaitTimeout": "5",
      "elementWaitTimeout": "30",
      "sleepInterval": "1",
      "validationsTimeout": "10",
      "elementToBeVisibleTimeout": "30",
      "elementToExistTimeout": "30",
      "elementToNotExistTimeout": "30",
      "elementToBeClickableTimeout": "30",
      "elementNotToBeVisibleTimeout": "30",
      "elementToHaveContentTimeout": "15"
    }
  }
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `serviceUrl` | URL (e.g. `http://127.0.0.1:4723`) | WinAppDriver / Appium endpoint — set it explicitly (the framework sample uses 4722) |
| `executionType` | `"regular"` (or a `gridSettings[].providerName`) | Required — missing value fails app start |
| `defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | Lifecycle when a class has no `@ExecutionApp`; unknown text → restart every time |
| `defaultAppPath` | Path or App User Model ID | Application to launch when a class has no `@ExecutionApp` |
| `automaticallyScrollToVisible`, `screenshotsOnFailEnabled`, `videosOnFailEnabled`, `downloadDemoApps` | `"true"` / `"false"` | Required booleans — leaving one out fails at runtime |
| `timeoutSettings.*` | seconds | Wait/validation timeouts; `sleepInterval` is the polling interval |

BELLATRIX-Java never starts WinAppDriver/Appium — the driver is the user's to run (there is no
`shouldStartLocalService`). `app().addDriverOptions(key, value)` exists but is not passed to the driver in 1.0 — do
not rely on it.
