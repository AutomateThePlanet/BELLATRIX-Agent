<!-- lang:target -->
**Target project:** the BELLATRIX-Java mobile test project listed in `bellatrix-mobile-project-structure` (Maven,
JUnit 5, `solutions.bellatrix:bellatrix.android:1.0` and/or `bellatrix.ios:1.0`; folder `{project.dir}` and
root package `{project.rootNamespace}` from `.bellatrix/config.json`).
- **Packages**: `{project.rootNamespace}.android.pages.<screen>`, `.android.pages.shared`, `.android.components`,
  `.android.tests.<featurearea>` (the same under `{project.rootNamespace}.ios.…`), plus the shared `{project.rootNamespace}.models` and
  `{project.rootNamespace}.common`; sources under `src/test/java/`
- **Platform imports**: `solutions.bellatrix.android.*` **or** `solutions.bellatrix.ios.*` per file
- **Build command**: `{project.buildCommand}` (from the repository root)

<!-- lang:edit-split -->
- **Keep the 3-class split** — locators only in `Map`, workflows only in the page class (the "Actions file"),
  assertions only in `Asserts` (the "Assertions file")
<!-- lang:page-files -->
Each screen gets a dedicated package with three classes (Java has no partial classes):

| File | Contains | Never contains |
|------|----------|----------------|
| **`Map.java`** (`extends PageMap`) | one component-returning method per validated locator | logic, assertions |
| **`{ScreenName}.java`** (`extends AndroidPage<Map, Asserts>` / `IOSPage<Map, Asserts>`) | business workflow methods; navigation methods return the next page object | assertions |
| **`Asserts.java`** (`extends PageAsserts<Map>`) | assertion methods built on `validate…` (after waiting) | actions |

<!-- lang:page-objects -->
### Folder Structure
```
src/test/java/{project.rootPath}/android/pages/      (or …/ios/pages/)
  {screen}/
    {ScreenName}.java   — action methods (extends AndroidPage<Map, Asserts> / IOSPage<Map, Asserts>)
    Map.java            — component methods (locators)
    Asserts.java        — assertion methods using validate*
```

`{screen}` is the lowercase screen name (`login`); `{ScreenName}` the class name (`LoginPage`).

### Map Class (`Map.java`) — Android

```java
package {project.rootNamespace}.android.pages.{screen};

import solutions.bellatrix.android.components.Button;
import solutions.bellatrix.android.components.Label;
import solutions.bellatrix.android.components.TextField;
import solutions.bellatrix.android.pages.PageMap;

public class Map extends PageMap {
    // accessibilityid (content-desc) → byDescription
    public Button submitButton() {
        return create().byDescription(Button.class, "Submit");
    }

    // id (full resource-id) → byId
    public TextField emailField() {
        return create().byId(TextField.class, "com.example.shop:id/email");
    }

    public Label statusLabel() {
        return create().byId(Label.class, "com.example.shop:id/status");
    }

    // dynamic text → method with a parameter, still a direct factory (byText also scrolls into view)
    public Button productRow(String productName) {
        return create().byText(Button.class, productName);
    }

    // androiduiautomator → byAndroidUIAutomator
    public Button logoutButton() {
        return create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log out\").clickable(true)");
    }
}
```

### Map Class (`Map.java`) — iOS

```java
package {project.rootNamespace}.ios.pages.{screen};

import solutions.bellatrix.ios.components.Button;
import solutions.bellatrix.ios.components.Label;
import solutions.bellatrix.ios.components.TextField;
import solutions.bellatrix.ios.pages.PageMap;

public class Map extends PageMap {
    // accessibilityid (name) → byAccessibilityIdContaining (exact match despite the name)
    public Button submitButton() {
        return create().byAccessibilityIdContaining(Button.class, "submitButton");
    }

    public TextField emailField() {
        return create().byAccessibilityIdContaining(TextField.class, "emailField");
    }

    // iospredicate → byIOSNsPredicate
    public Label statusLabel() {
        return create().byIOSNsPredicate(Label.class, "type == \"XCUIElementTypeStaticText\" AND name == \"status\"");
    }

    // dynamic text → concatenated predicate
    public Button productRow(String productName) {
        return create().byIOSNsPredicate(Button.class, "type == \"XCUIElementTypeCell\" AND label CONTAINS \"" + productName + "\"");
    }
}
```

Keep the public method names identical on both platforms so the page and `Asserts` classes differ only in their
imports (and in how they reach the next page — see below).

**Locator factory rule** (validated CLI strategy → factory):

| Strategy | Android | iOS |
|----------|---------|-----|
| `accessibilityid` | `byDescription(T.class, …)` | `byAccessibilityIdContaining(T.class, …)` |
| `id` | `byId(T.class, "pkg:id/x")` (full resource-id) | `byId(T.class, …)` |
| `androiduiautomator` / `iospredicate` | `byAndroidUIAutomator(T.class, …)` | `byIOSNsPredicate(T.class, …)` |
| `iosclasschain` | — | `byIOSClassChain(T.class, …)` |
| `classname` | `byClass(T.class, …)` | `byClass(T.class, …)` |
| `xpath` | `byXPath(T.class, …)` | `byXPath(T.class, …)` |

Use `byXPath` ONLY for structural locators. There is no `byAccessibilityId` single factory on either platform — see
`bellatrix-mobile-locators-and-waits`.

### Page Class (`{ScreenName}.java`)

```java
package {project.rootNamespace}.android.pages.{screen};

import {project.rootNamespace}.android.pages.{nextscreen}.{NextScreen};
import {project.rootNamespace}.common.Constants;
import solutions.bellatrix.android.pages.AndroidPage;

public class {ScreenName} extends AndroidPage<Map, Asserts> {
    // Android only — jump to this screen directly (AndroidPage has no GoTo())
    public void open() {
        app().appService().startActivity(Constants.APP_PACKAGE, ".{ActivityName}");
    }

    public void submitEmail(String email) {
        map().emailField().setText(email);
        map().submitButton().click();
    }

    public void openProduct(String productName) {
        map().productRow(productName).click();
    }

    public {NextScreen} goToNext() {
        map().submitButton().click();
        return app().createPage({NextScreen}.class);
    }
}
```

On iOS: `extends IOSPage<Map, Asserts>`, no `open()` (no activities), and `IOSPage` has no `app()` — return the next
page with `new App().createPage({NextScreen}.class)` (`solutions.bellatrix.ios.services.App`).

### Asserts Class (`Asserts.java`)

```java
package {project.rootNamespace}.android.pages.{screen};

import solutions.bellatrix.android.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void pageLoaded() {
        map().submitButton().toBeVisible().waitToBe();   // there is no validateIsVisible()
    }

    public void statusIs(String expected) {
        map().statusLabel().toHaveContent().waitToBe();   // validators read once — wait first
        map().statusLabel().validateTextIs(expected);
    }
}
```

Swap `solutions.bellatrix.android.pages.PageAsserts` for `solutions.bellatrix.ios.pages.PageAsserts` in iOS files.

<!-- lang:custom-components -->
### Folder
```
src/test/java/{project.rootPath}/
  android/components/        (or ios/components/)
    {CustomComponentName}.java
```

### Option A — Extend the closest built-in with new behavior

`Label` and the bare `AndroidComponent` / `IOSComponent` have no `click()`; derive tappable custom components from
`Button`:

```java
package {project.rootNamespace}.android.components;

import solutions.bellatrix.android.components.Button;

public class TabButton extends Button {
    public boolean isSelectedTab() {
        return "true".equals(getAttribute("selected"));
    }

    public void open() {
        if (!isSelectedTab()) {
            click();
        }
    }
}
```

iOS switches: the built-in `ToggleButton` / `CheckBox` state reads an attribute XCUITest does not expose — model
`XCUIElementTypeSwitch` with a `Button` subclass that reads `value`:

```java
package {project.rootNamespace}.ios.components;

import solutions.bellatrix.ios.components.Button;

public class SwitchButton extends Button {
    public boolean isOn() {
        return "1".equals(getAttribute("value"));
    }

    public void turnOn() {
        if (!isOn()) {
            click();
        }
    }

    public void turnOff() {
        if (isOn()) {
            click();
        }
    }
}
```

### Option B — Override a built-in's public method

The Android `ComboBox.selectByText` looks the option up inside the spinner with a strategy that cannot search inside
an element (`NullPointerException`). Override it to open the spinner and tap the option as a top-level element
(confirm the option text live with the CLI):

```java
package {project.rootNamespace}.android.components;

import solutions.bellatrix.android.components.Button;
import solutions.bellatrix.android.components.ComboBox;

public class SpinnerComboBox extends ComboBox {
    @Override
    public void selectByText(String value) {
        if (defaultGetText().equals(value)) {
            return;
        }

        defaultClick(Button.CLICKING, Button.CLICKED);   // open the spinner (waits for clickable, logs)
        getComponentCreateService().byText(Button.class, value).click();
    }
}
```

`defaultClick`, `defaultGetText`, `defaultSetText`, `defaultCheck` are `protected` helpers of
`AndroidComponent` / `IOSComponent`; `getComponentCreateService()` / `getAppService()` are public getters. Built-in
methods (`click()`, `getText()`, `setText()`, `selectByText()`, `check()`, `turnOn()`, …) are ordinary public
methods you can override.

### Using custom components in Map classes

```java
import {project.rootNamespace}.android.components.SpinnerComboBox;
import {project.rootNamespace}.android.components.TabButton;

public TabButton dealsTab() {
    return create().byDescription(TabButton.class, "Deals");
}

public SpinnerComboBox countrySpinner() {
    return create().byId(SpinnerComboBox.class, "com.example.shop:id/country");
}
```

Components are created reflectively — keep the implicit public no-argument constructor.

<!-- lang:models -->
Place them in the shared `{project.rootNamespace}.models` package — prefer records:

```java
package {project.rootNamespace}.models;

public record {ModelName}(String propertyOne, String propertyTwo) {
}
```

<!-- lang:test-class -->
**Java naming:** `{App}{FeatureArea}Tests.java` in package `{project.rootNamespace}.android.tests.{featurearea}` (or
`….ios.tests.{featurearea}`) — e.g. `android/tests/login/ShopLoginTests.java`. Test methods use
`action_Condition_ExpectedResult`.

### Test Class Template (Android)

```java
package {project.rootNamespace}.android.tests.{featurearea};

import {project.rootNamespace}.android.pages.{screen}.{ScreenName};
import {project.rootNamespace}.common.Constants;
import {project.rootNamespace}.common.ManualTestCase;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.junit.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH,
        appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY,
        lifecycle = Lifecycle.REUSE_IF_STARTED)  // default for generated classes — speed optimization
public class {TestClassName} extends AndroidTest {
    @Override
    protected void beforeEach() throws Exception {
        app().createPage({ScreenName}.class).open();   // reset to a known screen when the app is reused
    }

    @Test
    @ManualTestCase("TC-XXX")
    @Tag("Smoke")
    public void {testMethodName}() {
        {ScreenName} page = app().createPage({ScreenName}.class);
        page.asserts().pageLoaded();

        // Actions
        page.submitEmail("demo@example.com");

        // Assertions
        page.asserts().statusIs("Check your inbox");
    }
}
```

### Test Class Template (iOS)

```java
package {project.rootNamespace}.ios.tests.{featurearea};

import {project.rootNamespace}.common.Constants;
import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.ios.pages.{screen}.{ScreenName};
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.ios.infrastructure.ExecutionApp;
import solutions.bellatrix.ios.infrastructure.Lifecycle;
import solutions.bellatrix.ios.infrastructure.junit.IOSTest;

@ExecutionApp(appPath = Constants.IOS_APP_PATH,
        iOSVersion = Constants.IOS_VERSION,
        deviceName = Constants.IOS_DEVICE,
        lifecycle = Lifecycle.REUSE_IF_STARTED)
public class {TestClassName} extends IOSTest {
    @Test
    @ManualTestCase("TC-XXX")
    @Tag("Smoke")
    public void {testMethodName}() {
        {ScreenName} page = app().createPage({ScreenName}.class);
        page.asserts().pageLoaded();
        page.submitEmail("demo@example.com");
        page.asserts().statusIs("Check your inbox");
    }
}
```

`@ManualTestCase("TC-XXX")` is the project-local annotation in `{project.rootNamespace}.common.ManualTestCase` (create it if
missing — see `bellatrix-mobile-project-structure`). It is not `@Repeatable`. Never use `@ParameterizedTest` /
`@CsvSource` for traceability.

<!-- lang:code-rules -->
### Syntax — Avoid Build Errors
1. **Package = folder**: the `package` line matches the directory under `src/test/java`; one public top-level class per file; file name = class name
2. **Three classes per screen**: `{ScreenName} extends AndroidPage<Map, Asserts>` (or `IOSPage<Map, Asserts>`), `Map extends PageMap`, `Asserts extends PageAsserts<Map>` — all from the SAME platform's `…pages` package (NOT `WebPage` / `DesktopPage`)
3. **One platform per file**: import `solutions.bellatrix.android.*` **or** `solutions.bellatrix.ios.*` — both make `Button`, `PageMap`, `PageAsserts` ambiguous
4. **Elements are methods**: `public Button okButton() { return create()...; }` — called as `map().okButton()`
5. **Class token first, capital P**: `create().byXPath(Button.class, "…")` (NOT `byXpath`, NOT `byXPath("…", Button.class)`)
6. **No `byAccessibilityId`**: Android content-desc → `byDescription`; iOS accessibility id → `byAccessibilityIdContaining` (exact match)
7. **Android resource-ids are fully qualified**: `byId(T.class, "com.app:id/login")` (or `byIdContaining(T.class, "login")`)
8. **Validate methods**: `validateTextIs()`, `validateTextContains()`, `validateIsChecked()`, `validateIsUnchecked()`, `validateIsOn()`, `validateIsOff()`, `validateNumberIs(3.0)` — after waiting (they read the value once). There is NO `validateIsVisible()` (use `toBeVisible().waitToBe()`), NO `validateInnerTextIs`, NO `validateIsEnabled`; `validateIsDisabled()` / `validateNotDisabled()` read a `disabled` attribute the drivers do not expose — assert `getAttribute("enabled")` instead
9. **Tappable = `Button`**: `Label` has no `click()`; rows/cells/links you tap are `Button` (or a custom component extending it)
10. **Wait-chain trap**: `x.toBeVisible().click()` does not compile — end chains with `waitToBe()`, or assign (`Button b = x.toBeVisible();`)
11. **Never import `java.util.Map` in a page package** (it hides the page's `Map`); import `java.util.List` explicitly rather than `java.util.*`
12. **`@ExecutionApp` values are compile-time constants** (`Constants.X`), and the annotation/`Lifecycle` come from the test's platform

### Architecture
13. **Page instantiation**: `app().createPage(TPage.class)` in tests and Android pages, `new App().createPage(TPage.class)` in iOS pages (NOT `new TPage()`, NOT `goTo`)
14. **Lifecycle**: generated test classes default to `@ExecutionApp(…, lifecycle = Lifecycle.REUSE_IF_STARTED)`; the global config stays `restart every time`. Switch a class — or one method via a method-level `@ExecutionApp` — to `RESTART_EVERY_TIME` only when state cannot be reset in `beforeEach()` (see the generator agent's lifecycle policy and the BELLATRIX-Java 1.0 iOS restart caveat)
15. **No direct components in tests**: tests call page object methods only
16. **Parameterized locator methods** for dynamic elements (product names, row titles)
17. **No URL navigation** — `app().createPage(...)`, Android `startActivity(...)`, or tap through the UI
18. **No raw driver lookups** — never `getWrappedAndroidDriver().findElement(...)` / `AppiumBy.*` / `By.*` in project code
19. **No `Thread.sleep`** — fluent waits (`toBeVisible()`, `toBeClickable()`, `toHaveContent()`), `app().waitFor().wait(c, Wait.to().notExist())` (Android) or the `allBy…`-is-empty poll for "gone"
20. **Setup in `beforeEach()`**, not `beforeAll()` (skipped / runs before the app starts in 1.0)

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `while`, `do`, and `try` block MUST use `{ }` braces — even for single-statement bodies.
Braces follow Java convention (opening brace on the same line).

```text
WRONG — no braces on single-statement if
for (Label row : rows) {
    if (row.getText().isEmpty()) continue;
    row.validateTextContains("Pixel");
}
```

```java
// CORRECT — every control-flow body is braced
for (Label row : rows) {
    if (row.getText().isEmpty()) {
        continue;
    }

    row.validateTextContains("Pixel");
}
```

Applies everywhere — page objects, actions, assertions, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `create().byAccessibilityId(Button.class, "x")` | Android `create().byDescription(Button.class, "x")`; iOS `create().byAccessibilityIdContaining(Button.class, "x")` |
| `create().byId(Button.class, "login")` (Android) | `create().byId(Button.class, "com.app:id/login")` or `create().byIdContaining(Button.class, "login")` |
| `create().byXpath(…)` / `create().byXPath("//…", Button.class)` | `create().byXPath(Button.class, "//…")` |
| `label.click()` | Create the element as `Button` |
| `element.validateIsVisible()` | `element.toBeVisible().waitToBe()` |
| `button.validateIsDisabled()` | `Assertions.assertEquals("false", button.getAttribute("enabled"))` |
| `new LoginPage()` | `app().createPage(LoginPage.class)` |
| `appService()` in an Android page / `app()` in an iOS page | Android page → `app().appService()`; iOS page → `appService()`, `new App().createPage(...)` |
| `comboBox.selectByText("x")` (Android spinner) | custom `SpinnerComboBox` (Option B above) |
| `toggle.turnOn()` / `checkBox.check()` (iOS) | `SwitchButton.turnOn()` reading `value` (Option A above) |
| `quantity.validateNumberIs(3)` / `seekBar.set(50)` | `validateNumberIs(3.0)` / `set(50.0)` |
| `@ExecutionApp(appPath = getPath(), …)` | `@ExecutionApp(appPath = Constants.ANDROID_APP_PATH, …)` |

<!-- lang:build -->
```bash
{project.buildCommand}
```

Compile only — never `mvn test` / `mvn verify`. Common build errors:
- **`reference to Button is ambiguous`** → the file imports both `solutions.bellatrix.android.components.*` and `solutions.bellatrix.ios.components.*`
- **`cannot find symbol: method byAccessibilityId` / `byText` (iOS) / `byIOSNsPredicate` (Android)** → use the platform's factory
- **`cannot find symbol: method click()` after `toBeVisible()`** → wait-chain generics trap
- **`cannot find symbol: method app()` (iOS page) / `appService()` (Android page)** → see the page-base table in `bellatrix-mobile-page-objects-and-patterns`
- **`element value must be a constant expression`** → non-constant `@ExecutionApp` argument
- **`type argument java.util.Map is not within bounds`** → `java.util.Map` imported into a page package (it hides the page's `Map`)
- `package` line not matching the folder; missing imports (`List`, components, `ManualTestCase`, `Constants`, `Wait`, `ScreenOrientation`)

<!-- lang:run-command -->
```bash
mvn test -Dtest={TestClassName}#{testMethodName}
```

(The user starts Appium and the device / emulator / simulator before running it.)

<!-- lang:output -->
### Files Created/Modified

```
[CREATED] src/test/java/{project.rootPath}/android/pages/{screen}/Map.java
[CREATED] src/test/java/{project.rootPath}/android/pages/{screen}/{ScreenName}.java
[CREATED] src/test/java/{project.rootPath}/android/pages/{screen}/Asserts.java
[EDITED]  src/test/java/{project.rootPath}/android/pages/{existingscreen}/Map.java — added 3 new element methods
[EDITED]  src/test/java/{project.rootPath}/android/pages/{existingscreen}/{ExistingScreen}.java — added 2 new methods
[CREATED] src/test/java/{project.rootPath}/models/{ModelName}.java
[CREATED] src/test/java/{project.rootPath}/android/tests/{featurearea}/{TestClassName}.java
[EDITED]  bellatrix-mobile-project-structure skill — added android/pages/{screen}
```

### Build Verification
```
Build succeeded: {project.buildCommand}
```
