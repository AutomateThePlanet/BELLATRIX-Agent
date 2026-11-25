# Generate Desktop Test Code (Java)

Generate production-ready BELLATRIX-Java desktop test code using validated locators from scenario execution. This skill handles both **creating new files** and **editing/expanding existing ones**.

**Target project:** `{project.dir}` (Maven, JUnit 5, `solutions.bellatrix:bellatrix.desktop:1.0`) — all generated code goes here.
- **Packages**: `{project.rootNamespace}.pages.<window>`, `{project.rootNamespace}.models`, `{project.rootNamespace}.tests.<featurearea>`, `{project.rootNamespace}.components`, `{project.rootNamespace}.common` (demo root package: `{project.rootNamespace}`; sources under `src/test/java/`)
- **Build command**: `{project.buildCommand}` (from the repository root)

---

## CRITICAL: Read Reference Documentation First

**BEFORE generating any code**, load these skills:

| Skill | Content |
|-------|---------|
| `bellatrix-desktop-components` | Component types, methods, factory methods, `UiaXPathFindStrategy` |
| `bellatrix-desktop-locators-and-waits` | Find strategies, XPath patterns, waits, wait-chain trap |
| `bellatrix-desktop-validations-and-assertions` | validate* methods (read-once!), assertions |
| `bellatrix-desktop-test-structure-and-lifecycle` | Test classes, `@ExecutionApp`, lifecycle, hooks |
| `bellatrix-desktop-page-objects-and-patterns` | **Page object structure, file naming, models** |
| `bellatrix-desktop-app-services` | **App services (`create()`, `appService()`, `waitFor()`, `createPage`), configuration** |
| `bellatrix-desktop-system-tests-recipes` | Complete test examples |

---

## Step 1: Scan Existing Project Structure

Before generating code, **always check what already exists**:

```
Search for existing page objects:  src/test/java/**/pages/*/
Search for existing test files:    src/test/java/**/tests/**/*.java
Search for existing models:        src/test/java/**/models/*.java
Search for project infrastructure: src/test/java/**/common/*.java (ManualTestCase, UiaXPathFindStrategy, Constants)
```

### Decision Matrix

| Situation | Action |
|-----------|--------|
| Page object package exists for this window | **EDIT** — add new methods to the existing `Map` / page / `Asserts` classes |
| Page object package does NOT exist | **CREATE** — new package with 3 classes |
| Test class exists for this feature area | **EDIT** — add new test methods to existing class |
| Test class does NOT exist | **CREATE** — new test file |
| Model exists with matching fields | **REUSE** — use existing model as-is |
| Model exists but needs more fields | **EDIT** — add new components/fields to existing model |
| Model does NOT exist | **CREATE** — new model file |
| `common/ManualTestCase.java`, `common/UiaXPathFindStrategy.java`, `common/Constants.java` missing | **CREATE** once (code below) |

### IMPORTANT: When Editing Existing Files

- **Read the entire file first** before making changes
- **Preserve all existing code** — do not remove or modify existing methods
- **Follow the same patterns** — match naming conventions, formatting, and style of existing code
- **Add to the end** — append new methods after existing ones
- **Keep the 3-class split** — locators only in `Map`, workflows only in the page class, assertions only in `Asserts`

---

## Step 1b: Project Infrastructure (create once)

`common/ManualTestCase.java` — SPECTRA traceability (metadata only):

```java
package {project.rootNamespace}.common;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ManualTestCase {
    String value();
}
```

`common/Constants.java` — annotation values must be compile-time constants:

```java
package {project.rootNamespace}.common;

public final class Constants {
    public static final String CALCULATOR_APP_PATH = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    public static final String MY_APP_PATH = "C:\\Program Files\\MyApp\\MyApp.exe";

    private Constants() {
    }
}
```

`common/UiaXPathFindStrategy.java` — the XPath find strategy every XPath locator uses (BELLATRIX-Java 1.0's own
`byXPath` is broken). Copy it verbatim from `bellatrix-desktop-components`.

`src/main/resources/application.properties` (`environment=dev`) and `testFrameworkSettings.dev.json` — see
`bellatrix-desktop-app-services`.

---

## Step 2: Generate Page Objects (3-Class Pattern)

Each window gets a dedicated package with 3 classes:

### Folder Structure
```
src/test/java/{project.rootPath}/pages/
  {window}/
    {WindowName}.java   — Action methods (extends DesktopPage<Map, Asserts>)
    Map.java            — Component methods (locators)
    Asserts.java        — Assertion methods using validate*
```

`{window}` is the lowercase window name (`calculatorwindow`); `{WindowName}` the class name (`CalculatorWindow`).

### Map Class (`Map.java`)

```java
package {project.rootNamespace}.pages.{window};

import {project.rootNamespace}.common.UiaXPathFindStrategy;
import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.Label;
import solutions.bellatrix.desktop.components.TextField;
import solutions.bellatrix.desktop.pages.PageMap;

public class Map extends PageMap {
    // PREFERRED — direct factory for single-attribute locators
    public Label display() {
        return create().byAutomationId(Label.class, "result");
    }

    public TextField inputField() {
        return create().byAutomationId(TextField.class, "txtInput");
    }

    public Button submitButton() {
        return create().byAutomationId(Button.class, "btnSubmit");
    }

    // Dynamic name — still direct factory when only Name varies
    public Button itemButton(String itemName) {
        return create().byName(Button.class, itemName);
    }

    // XPath is appropriate when you need ControlType + attribute disambiguation
    // or structural traversal — NOT for single-attribute matches
    public Button namedButton(String name) {
        return create().by(Button.class, new UiaXPathFindStrategy("//Button[@Name='" + name + "']"));
    }
}
```

**Locator factory rule:** Single `@AutomationId` → `byAutomationId`. Single `@Name` → `byName`. Single `@ClassName` →
`byClass`. Use `by(T.class, new UiaXPathFindStrategy(...))` ONLY for compound/structural locators — never
`byXPath`/`allByXPath`. See `bellatrix-desktop-locators-and-waits` for the full decision matrix.

### Page Class (`{WindowName}.java`)

```java
package {project.rootNamespace}.pages.{window};

import solutions.bellatrix.desktop.pages.DesktopPage;

public class {WindowName} extends DesktopPage<Map, Asserts> {
    public void doSomething() {
        map().inputField().setText("value");
        map().submitButton().click();
    }
}
```

### Asserts Class (`Asserts.java`)

```java
package {project.rootNamespace}.pages.{window};

import solutions.bellatrix.desktop.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void resultIs(String expected) {
        map().display().toHaveContent().waitToBe();   // validators read once — wait first
        map().display().validateTextIs(expected);
    }

    public void windowLoaded() {
        map().display().toBeVisible().waitToBe();   // there is no validateIsVisible()
    }
}
```

---

## Step 2b: Custom Components (When BELLATRIX Built-ins Don't Fit)

If a built-in BELLATRIX component (`Button`, `Label`, `ComboBox`, …) is missing a method you need, or behaves
incorrectly for a specific control, add a custom component under `src/test/java/{project.rootPath}/components/` instead of
working around it in page objects or tests.

### Folder
```
src/test/java/{project.rootPath}/
  components/
    {CustomComponentName}.java
```

### Option A — New component extending `DesktopComponent`

Use when no BELLATRIX built-in models the control cleanly (e.g., a specialized calendar day, a split-button, a custom
grid cell). `DesktopComponent` has no `click()` — reuse the framework's click pipeline (waits for clickable, logs) via
the inherited `defaultClick`:

```java
package {project.rootNamespace}.components;

import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.DesktopComponent;

public class CalendarDayItem extends DesktopComponent {
    public void select() {
        defaultClick(Button.CLICKING, Button.CLICKED);
    }

    public boolean isToday() {
        String className = getAttribute("ClassName");
        return className != null && className.contains("today");
    }

    public String dayNumber() {
        return getAttribute("Name");
    }
}
```

### Option B — Child component overriding an existing BELLATRIX component

Use when a built-in is *almost* right but you need to tweak behavior (e.g., a `ComboBox` whose `selectByText` needs
extra waits, or a WinUI dropdown that ignores typed text):

```java
package {project.rootNamespace}.components;

import solutions.bellatrix.desktop.components.ComboBox;

public class ResilientComboBox extends ComboBox {
    @Override
    public void selectByText(String text) {
        toBeClickable().waitToBe();
        super.selectByText(text);
    }
}
```

```java
package {project.rootNamespace}.components;

import {project.rootNamespace}.common.UiaXPathFindStrategy;
import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.ComboBox;

/** WinUI3 ComboBox: built-in selectByText only types the text; expand and click the item instead. */
public class WinUiComboBox extends ComboBox {
    @Override
    public void selectByText(String value) {
        if (getText().equals(value)) {
            return;
        }

        defaultClick(Button.CLICKING, Button.CLICKED);   // expand the flyout
        getComponentCreateService()
                .by(Button.class, new UiaXPathFindStrategy("//ListItem[@Name='" + value + "']"))
                .click();
    }
}
```

### Using custom components in Map classes

Reference them by the custom class token — all factory methods work transparently:

```java
public CalendarDayItem fifteenthOfMonth() {
    return create().byAutomationId(CalendarDayItem.class, "Day_15");
}

public ResilientComboBox countryDropdown() {
    return create().byAutomationId(ResilientComboBox.class, "cmbCountry");
}
```

### Rules

- **Never modify BELLATRIX source.** Extend in `{project.rootNamespace}.components` only.
- **Public no-argument constructor.** Components are created reflectively — don't add constructor parameters.
- **Keep components thin.** Methods on a custom component should be *control-level* behaviors (select day, expand menu). Business workflow stays in the page class.
- **One file per component.** Match filename to class name.
- **Reuse across page objects.** A custom component defined once can be referenced from any `Map`.

---

## Step 3: Generate Model Classes (If Needed)

Place in the `models` package. Use for structured form data — prefer records:

```java
package {project.rootNamespace}.models;

public record {ModelName}(String propertyOne, String propertyTwo) {
}
```

---

## Step 4: Generate Test Classes

### Test File Organization (MANDATORY)

Do NOT put all tests into a single file. Organize test classes by **logical feature area** using the test case's tags. Each distinct feature area gets its own test class file.

**Naming convention:** `{Window}{FeatureArea}Tests.java` in package `{project.rootNamespace}.tests.{featurearea}`

**Examples for standard calculator:**

| Tags on test case | Test class file | Class name |
|-------------------|----------------|------------|
| `arithmetic`, `addition` | `tests/arithmetic/CalculatorArithmeticTests.java` | `CalculatorArithmeticTests` |
| `memory` | `tests/memory/CalculatorMemoryTests.java` | `CalculatorMemoryTests` |
| `history` | `tests/history/CalculatorHistoryTests.java` | `CalculatorHistoryTests` |
| `square-root`, `special-operations` | `tests/specialoperations/CalculatorSpecialOperationsTests.java` | `CalculatorSpecialOperationsTests` |
| `reciprocal`, `special-operations` | `tests/specialoperations/CalculatorSpecialOperationsTests.java` | `CalculatorSpecialOperationsTests` |
| `percentage` | `tests/percentage/CalculatorPercentageTests.java` | `CalculatorPercentageTests` |
| `always-on-top` | `tests/alwaysontop/CalculatorAlwaysOnTopTests.java` | `CalculatorAlwaysOnTopTests` |
| `decimal` | `tests/decimal/CalculatorDecimalTests.java` | `CalculatorDecimalTests` |
| `input-controls`, `backspace` | `tests/inputcontrols/CalculatorInputControlsTests.java` | `CalculatorInputControlsTests` |
| `error`, `negative-test` | `tests/errorhandling/CalculatorErrorHandlingTests.java` | `CalculatorErrorHandlingTests` |
| `display` | `tests/display/CalculatorDisplayTests.java` | `CalculatorDisplayTests` |

**How to decide:** Look at the test case's `tags` array. Use the most specific non-generic tag (ignore `standard-calculator`, `EP`, `BVA`, `ST`, `EG` — these are technique tags, not feature tags). If a test has both a specific tag (e.g., `reciprocal`) and a broader tag (e.g., `special-operations`), prefer the broader one for file grouping. When the file already exists, add the new test method to it.

**Rule of thumb:** A test file should cover one coherent feature area. Aim for roughly 10-30 tests per file. If a file grows beyond ~50 test methods, consider splitting by sub-feature.

### Test Class Template

```java
package {project.rootNamespace}.tests.{featurearea};

import {project.rootNamespace}.common.Constants;
import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.pages.{window}.{WindowName};
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.{APP_PATH_CONSTANT}, lifecycle = Lifecycle.REUSE_IF_STARTED)  // default for generated classes
public class {TestClassName} extends DesktopTest {
    @Test
    @ManualTestCase("TC-XXX")
    @Tag("Smoke")
    public void {testMethodName}() {
        {WindowName} page = app().createPage({WindowName}.class);
        page.asserts().windowLoaded();

        // Actions
        page.doSomething();

        // Assertions
        page.asserts().resultIs("expected");
    }
}
```

### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into a single parameterized test method (`@ParameterizedTest` + `@CsvSource`/`@MethodSource`). Do NOT put several `@ManualTestCase` annotations on one method (the annotation is not repeatable — it won't compile, and it must not be made repeatable).

---

## Code Rules (MUST FOLLOW)

### Syntax — Avoid Build Errors
1. **Package = folder**: the `package` line matches the directory under `src/test/java`; one public top-level class per file; file name = class name
2. **Three classes per page**: `{WindowName} extends DesktopPage<Map, Asserts>`, `Map extends PageMap`, `Asserts extends PageAsserts<Map>` — all in `solutions.bellatrix.desktop.pages` (NOT `WebPage`, NOT the web `PageMap`)
3. **Elements are methods**: `public Button okButton() { return create()...; }` — called as `map().okButton()`
4. **PascalCase XPath**: `//Button[@AutomationId='btn']` (NOT `//button[@automationid='btn']`)
5. **Validate methods**: use BELLATRIX `validateTextIs()`, `validateTextContains()`, `validateIsDisabled()`, `validateNotDisabled()`, `validateIsChecked()`, `validateIsUnchecked()` instead of JUnit `Assertions` — after waiting (they read the value once). There is NO `validateIsVisible()` (use `toBeVisible().waitToBe()`), NO `validateInnerTextIs`, NO `validateIsEnabled`, and `validateIsSelected()` checks visibility — never use it
6. **Wait-chain trap**: `x.toBeVisible().click()` does not compile — end chains with `waitToBe()`, or assign (`Button b = x.toBeVisible();`)
7. **Never `import java.util.Map;` in a page package** — that single-type import shadows the page's own `Map` class (`type argument java.util.Map is not within bounds`); import `java.util.List` explicitly and write a JDK map fully qualified (`java.util.Map<K, V>`)

### Architecture
8. **Page instantiation**: `app().createPage(TPage.class)` in tests, `new App().createPage(TPage.class)` in page navigation methods (NOT `new TPage()`, NOT `goTo`)
8a. **Lifecycle**: Default generated test classes use `@ExecutionApp(appPath = Constants.X, lifecycle = Lifecycle.REUSE_IF_STARTED)`. Global config stays `restart every time`; the class-level annotation overrides it. `appPath` must be a compile-time constant. Only switch a class (or an individual test via method-level `@ExecutionApp`) to `RESTART_EVERY_TIME` when state leakage cannot be cleaned up via `beforeEach()` reset — see the agent file for the full lifecycle decision policy (and the BELLATRIX-Java 1.0 restart caveat)
9. **No direct components in tests**: Tests use ONLY page object methods
10. **XPath through `UiaXPathFindStrategy`**: `create().by(T.class, new UiaXPathFindStrategy(x))` / `create().allBy(...)` — NEVER `byXPath` / `allByXPath` (broken in 1.0); there is no `byId` (use `byAutomationId`)
11. **Parameterized locator methods**: Use parameters for dynamic elements (product names, row IDs)
12. **No URL navigation**: Desktop apps don't navigate by URL; there is no `goTo` on desktop
13. **Setup in `beforeEach()`**, not `beforeAll()` (skipped on desktop 1.0)

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `while`, `do`, and `try` block MUST use `{ }` braces — even for single-statement bodies.
Braces follow Java convention (opening brace on the same line).

```text
WRONG — no braces on single-statement if
for (String handle : driver.getWindowHandles()) {
    if (handle.equals(mainHandle)) continue;
    driver.switchTo().window(handle);
    ...
}
```

```java
// CORRECT — every control-flow body is braced
for (String handle : driver.getWindowHandles()) {
    if (handle.equals(mainHandle)) {
        continue;
    }

    driver.switchTo().window(handle);
    List<Button> items = app().create().allBy(Button.class, new UiaXPathFindStrategy("//ListItem[@Name='" + modeName + "']"));
    if (!items.isEmpty()) {
        items.get(0).click();
        break;
    }
}
```

Applies everywhere — page objects, actions, assertions, tests, custom components, helpers. No exceptions for "short" or "obvious" branches.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `app().goTo(LoginWindow.class)` | `app().createPage(LoginWindow.class)` (no goTo on desktop) |
| `create().byXPath(Button.class, "//Button[@Name='OK']")` | `create().by(Button.class, new UiaXPathFindStrategy("//Button[@Name='OK']"))` |
| `create().byId(Button.class, "btnOk")` | `create().byAutomationId(Button.class, "btnOk")` |
| `create().byAutomationId("btnOk", Button.class)` | `create().byAutomationId(Button.class, "btnOk")` (class token first) |
| `new LoginWindow()` | `app().createPage(LoginWindow.class)` |
| `//button[@automationid='btn']` | `//Button[@AutomationId='btn']` (PascalCase!) |
| `@ExecutionApp(appPath = getPath(), …)` | `@ExecutionApp(appPath = Constants.MY_APP_PATH, …)` (compile-time constant) |
| `label.validateIsVisible()` | `label.toBeVisible().waitToBe()` |
| `field.clear()` | `field.setText("")` (no `clear()`; `setText` clears first) |

---

## Step 5: Verify Build

After generating/editing all files:

```bash
{project.buildCommand}
```

Compile only — never `mvn test`/`mvn verify`. Fix any build errors until the build succeeds. Common issues:
- Missing imports (`List`, components, `UiaXPathFindStrategy`, `ManualTestCase`)
- `package` line not matching the folder
- Wrong `Map`/`Asserts` (e.g. `java.util.Map` imported into a page package)
- Wait-chain generics (`cannot find symbol: method click()` after `toBeVisible()`)
- Non-constant `@ExecutionApp` argument ("element value must be a constant expression")
- Incorrect component type for the element; typo in XPath attribute names (case-sensitive!)

---

## Step 6: Hand Off to User — Do NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated tests. The user runs tests and returns with feedback.

Post a concise summary that includes:
- Files created/edited
- TC-IDs covered (if applicable)
- The command the user can run to execute the new test(s):
  ```bash
  mvn test -Dtest={TestClassName}#{testMethodName}
  ```
- Any assumptions you made (e.g., locator choice, lifecycle picked, custom components added)

Then wait. When the user returns with specific failures, make targeted fixes based on their guidance:
- **"XPath failed for Y"** → new CLI session, fresh snapshot, fix the `Map` class
- **"Assertion expected X got Y"** → fix assertion text
- **"Timing issue on Z"** → add a wait

After each fix iteration, rebuild (`{project.buildCommand}`), then hand back to the user again. Do NOT speculatively rerun tests on your own.

**Exception:** if the user has explicitly said "run them and fix until green" in this conversation, follow that instruction for as long as it applies.

---

## Output Format

### Files Created/Modified

```
[CREATED] src/test/java/{project.rootPath}/pages/{window}/Map.java
[CREATED] src/test/java/{project.rootPath}/pages/{window}/{WindowName}.java
[CREATED] src/test/java/{project.rootPath}/pages/{window}/Asserts.java
[EDITED]  src/test/java/{project.rootPath}/pages/{existingwindow}/Map.java — added 3 new element methods
[EDITED]  src/test/java/{project.rootPath}/pages/{existingwindow}/{ExistingWindow}.java — added 2 new methods
[CREATED] src/test/java/{project.rootPath}/models/{ModelName}.java
[CREATED] src/test/java/{project.rootPath}/tests/{featurearea}/{TestClassName}.java
```

### Build Verification
```
Build succeeded: {project.buildCommand}
```
