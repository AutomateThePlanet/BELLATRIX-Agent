# Generate Desktop Test Code

Generate production-ready BELLATRIX desktop test code using validated locators from scenario execution. This skill handles both **creating new files** and **editing/expanding existing ones**.

**Target project:** `Demo.DesktopTests` — all generated code goes here.
- **Namespaces**: `Demo.DesktopTests.Pages`, `Demo.DesktopTests.Models`, `Demo.DesktopTests.Tests`
- **Build command**: `dotnet build Demo.DesktopTests/Demo.DesktopTests.csproj`

---

## CRITICAL: Read Reference Documentation First

**BEFORE generating any code**, load these skills:

| Skill | Content |
|-------|---------|
| `bellatrix-desktop-components` | Component types, methods, factory methods |
| `bellatrix-desktop-locators-and-waits` | Find strategies, XPath patterns, waits |
| `bellatrix-desktop-validations-and-assertions` | Validate* methods, assertions |
| `bellatrix-desktop-test-structure-and-lifecycle` | Test classes, configuration, lifecycle |
| `bellatrix-desktop-page-objects-and-patterns` | **Page object structure, file naming, models** |
| `bellatrix-desktop-app-services` | **App services (Components, AppService, Wait, Create)** |
| `bellatrix-desktop-system-tests-recipes` | Complete test examples |

---

## Step 1: Scan Existing Project Structure

Before generating code, **always check what already exists**:

```
Search for existing page objects:  Pages/*/
Search for existing test files:    Tests/*.cs
Search for existing models:        Models/*.cs
```

### Decision Matrix

| Situation | Action |
|-----------|--------|
| Page object folder exists for this window | **EDIT** — add new properties/methods to existing partial class files |
| Page object folder does NOT exist | **CREATE** — new folder with 3 partial class files |
| Test class exists for this feature area | **EDIT** — add new test methods to existing class |
| Test class does NOT exist | **CREATE** — new test file |
| Model class exists with matching properties | **REUSE** — use existing model as-is |
| Model class exists but needs more properties | **EDIT** — add new properties to existing model |
| Model class does NOT exist | **CREATE** — new model file |

### IMPORTANT: When Editing Existing Files

- **Read the entire file first** before making changes
- **Preserve all existing code** — do not remove or modify existing methods/properties
- **Follow the same patterns** — match naming conventions, formatting, and style of existing code
- **Add to the end** — append new methods/properties after existing ones
- **Maintain `partial class`** — all page object files for the same window share the same class name

---

## Step 2: Generate Page Objects (3-File Pattern)

Each window gets a dedicated folder with 3 partial class files:

### Folder Structure
```
Pages/
  {WindowName}/
    {WindowName}.Actions.cs      — Action methods
    {WindowName}.Map.cs          — Component properties (XPath locators)
    {WindowName}.Assertions.cs   — Assert* methods using Validate methods
```

### Map File (`{WindowName}.Map.cs`)

This file defines the `DesktopPage` base class and all component properties:

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.PageObjects;

namespace {ProjectNamespace}.Pages;

public partial class {WindowName} : DesktopPage
{
    // PREFERRED — direct factory for single-attribute locators
    public Label Display =>
        App.Components.CreateByAutomationId<Label>("result");

    public Button SubmitButton =>
        App.Components.CreateByAutomationId<Button>("btnSubmit");

    // Dynamic name — still direct factory when only Name varies
    public Button GetItemButton(string itemName) =>
        App.Components.CreateByName<Button>(itemName);

    // XPath is appropriate when you need ControlType + attribute disambiguation
    // or structural traversal — NOT for single-attribute matches
    public Button NamedButton(string name) =>
        App.Components.CreateByXPath<Button>($"//Button[@Name='{name}']");
}
```

**Locator factory rule:** Single `@AutomationId` → `CreateByAutomationId`. Single `@Name` → `CreateByName`. Single `@ClassName` → `CreateByClass`. Use `CreateByXPath` ONLY for compound/structural locators. See `bellatrix-desktop-locators-and-waits` for the full decision matrix.

### Actions File (`{WindowName}.Actions.cs`)

```csharp
namespace {ProjectNamespace}.Pages;

public partial class {WindowName}
{
    public void DoSomething()
    {
        InputField.SetText("value");
        SubmitButton.Click();
    }
}
```

### Assertions File (`{WindowName}.Assertions.cs`)

```csharp
using Bellatrix.Desktop; // Validate* extension methods live here

namespace {ProjectNamespace}.Pages;

public partial class {WindowName}
{
    public void AssertResultIs(string expected)
    {
        ResultLabel.ValidateInnerTextIs(expected);
    }

    public void AssertWindowLoaded()
    {
        HeaderLabel.ValidateIsVisible();
    }
}
```

---

## Step 2b: Custom Components (When BELLATRIX Built-ins Don't Fit)

If a built-in BELLATRIX component (`Button`, `Label`, `ComboBox`, `Calendar`, etc.) is missing a method you need, or behaves incorrectly for a specific control, add a custom component under `Demo.DesktopTests/Components/` instead of working around it in page objects or tests.

### Folder
```
Demo.DesktopTests/
  Components/
    {CustomComponentName}.cs
```

### Option A — New component inheriting from `Component`

Use when no BELLATRIX built-in models the control cleanly (e.g., a specialized calendar day, a split-button, a custom grid cell):

```csharp
using Bellatrix.Desktop;

namespace Demo.DesktopTests.Components;

public class CalendarDayItem : Component
{
    public void Select() => WrappedElement.Click();

    public bool IsToday => GetAttribute("ClassName")?.Contains("today") == true;

    public string DayNumber => GetAttribute("Name");
}
```

### Option B — Child component overriding an existing BELLATRIX component

Use when a built-in is *almost* right but you need to tweak behavior (e.g., a `ComboBox` whose `SelectByText` needs extra waits, or a `TextField` with custom clear semantics):

```csharp
using Bellatrix.Desktop;

namespace Demo.DesktopTests.Components;

public class ResilientComboBox : ComboBox
{
    public override void SelectByText(string text)
    {
        this.ToBeClickable().WaitToBe(); // extension method: `this.` is required
        base.SelectByText(text);
    }
}
```

### Using custom components in Map files

Reference them by the custom type in the factory generic — all factory methods work transparently:

```csharp
using Demo.DesktopTests.Components;

public CalendarDayItem FifteenthOfMonth =>
    App.Components.CreateByAutomationId<CalendarDayItem>("Day_15");

public ResilientComboBox CountryDropdown =>
    App.Components.CreateByAutomationId<ResilientComboBox>("cmbCountry");
```

### Rules

- **Never modify BELLATRIX source.** Extend in `Demo.DesktopTests/Components/` only.
- **Keep components thin.** Methods on a custom component should be *control-level* behaviors (select day, expand menu). Business workflow stays in `.Actions.cs` page files.
- **One file per component.** Match filename to class name.
- **Reuse across page objects.** A custom component defined once can be referenced from any Map file.

---

## Step 3: Generate Model Classes (If Needed)

Place in `Models/` folder. Use for structured form data:

```csharp
namespace {ProjectNamespace}.Models;

public class {ModelName}
{
    public string PropertyOne { get; set; }
    public string PropertyTwo { get; set; }
}
```

---

## Step 4: Generate Test Classes

### Test File Organization (MANDATORY)

Do NOT put all tests into a single file. Organize test classes by **logical feature area** using the test case's tags. Each distinct feature area gets its own test class file.

**Naming convention:** `{Window}{FeatureArea}Tests.cs`

**Examples for standard calculator:**

| Tags on test case | Test class file | Class name |
|-------------------|----------------|------------|
| `arithmetic`, `addition` | `Tests/CalculatorArithmeticTests.cs` | `CalculatorArithmeticTests` |
| `memory` | `Tests/CalculatorMemoryTests.cs` | `CalculatorMemoryTests` |
| `history` | `Tests/CalculatorHistoryTests.cs` | `CalculatorHistoryTests` |
| `square-root`, `special-operations` | `Tests/CalculatorSpecialOperationsTests.cs` | `CalculatorSpecialOperationsTests` |
| `reciprocal`, `special-operations` | `Tests/CalculatorSpecialOperationsTests.cs` | `CalculatorSpecialOperationsTests` |
| `percentage` | `Tests/CalculatorPercentageTests.cs` | `CalculatorPercentageTests` |
| `always-on-top` | `Tests/CalculatorAlwaysOnTopTests.cs` | `CalculatorAlwaysOnTopTests` |
| `decimal` | `Tests/CalculatorDecimalTests.cs` | `CalculatorDecimalTests` |
| `input-controls`, `backspace` | `Tests/CalculatorInputControlsTests.cs` | `CalculatorInputControlsTests` |
| `error`, `negative-test` | `Tests/CalculatorErrorHandlingTests.cs` | `CalculatorErrorHandlingTests` |
| `display` | `Tests/CalculatorDisplayTests.cs` | `CalculatorDisplayTests` |

**How to decide:** Look at the test case's `tags` array. Use the most specific non-generic tag (ignore `standard-calculator`, `EP`, `BVA`, `ST`, `EG` — these are technique tags, not feature tags). If a test has both a specific tag (e.g., `reciprocal`) and a broader tag (e.g., `special-operations`), prefer the broader one for file grouping. When the file already exists, add the new test method to it.

**Rule of thumb:** A test file should cover one coherent feature area. Aim for roughly 10-30 tests per file. If a file grows beyond ~50 test methods, consider splitting by sub-feature.

### Test Class Template

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.NUnit;
using {ProjectNamespace}.Pages;
using NUnit.Framework;

namespace {ProjectNamespace}.Tests;

[TestFixture]
[App("{appPath}", Lifecycle.ReuseIfStarted)]  // default for generated classes — speed optimization
public class {TestClassName} : DesktopTest
{
    [Test]
    [Category("Smoke")]
    public void {TestMethodName}()
    {
        var page = App.Create<{WindowName}>();
        page.AssertWindowLoaded();

        // Actions
        page.DoSomething();

        // Assertions
        page.AssertResultIs("expected");
    }
}
```

### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into a single parameterized test method. Do NOT stack multiple `[ManualTestCase]` attributes on one method.

---

## Code Rules (MUST FOLLOW)

### Syntax — Avoid Build Errors
1. **File-scoped namespaces**: `namespace X;` (NOT `namespace X { }`)
2. **Partial classes**: All page object files use `partial class`
3. **DesktopPage base**: Map file extends `DesktopPage` (NOT `WebPage`)
4. **PascalCase XPath**: `//Button[@AutomationId='btn']` (NOT `//button[@automationid='btn']`)
5. **Validate methods**: Use native BELLATRIX Validate methods (`ValidateIsVisible()`, `ValidateInnerTextIs()`, `ValidateIsDisabled()`, etc.) instead of `Assert.That(...)`. Note: Desktop module has `ValidateInnerTextIs` (exact match) — there is NO `ValidateInnerTextContains` in Desktop

### Architecture
6. **Page instantiation**: `App.Create<TPage>()` (NOT `new TPage()`)
6a. **Lifecycle**: Default generated test classes use `[App(appPath, Lifecycle.ReuseIfStarted)]` for speed. Global config stays `RestartEveryTime`; the class-level attribute overrides it. Only switch a class (or an individual test via method-level `[App]`) to `RestartEveryTime` when state leakage cannot be cleaned up via teardown/reset — see the agent file for the full lifecycle decision policy
7. **No direct components in tests**: Tests use ONLY page object methods
8. **XPath capital P**: `CreateByXPath` (NOT `CreateByXpath`)
9. **Generic methods**: Use parameters for dynamic elements (product names, row IDs)
10. **No URL navigation**: Desktop apps don't use `App.Navigation.Navigate(url)`

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `foreach`, `while`, `do`, and `using` block MUST use `{ }` braces — even for single-statement bodies. Braces on their own line (Allman style).

```csharp
// WRONG — no braces on single-statement if
foreach (var handle in driver.WindowHandles)
{
    if (handle == mainHandle) continue;
    driver.SwitchTo().Window(handle);
    var item = App.Components.CreateByXPath<Button>($"//ListItem[@Name='{modeName}']");
    if (item.IsPresent)
    {
        item.Click();
        break;
    }
}

// CORRECT — every control-flow body is braced
foreach (var handle in driver.WindowHandles)
{
    if (handle == mainHandle)
    {
        continue;
    }

    driver.SwitchTo().Window(handle);
    var item = App.Components.CreateByXPath<Button>($"//ListItem[@Name='{modeName}']");
    if (item.IsPresent)
    {
        item.Click();
        break;
    }
}
```

Applies everywhere — page objects, actions, assertions, tests, custom components, helpers. No exceptions for "short" or "obvious" branches.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `App.Navigation.Navigate(url)` | Desktop apps don't use URLs — use `App.Create<TPage>()` |
| `App.Components.CreateByXpath<T>()` | `App.Components.CreateByXPath<T>()` (capital P) |
| `new LoginWindow()` | `App.Create<LoginWindow>()` |
| `//button[@automationid='btn']` | `//Button[@AutomationId='btn']` (PascalCase!) |
| `namespace X { }` | `namespace X;` (file-scoped) |

---

## Step 5: Verify Build

After generating/editing all files:

```bash
dotnet build <ProjectPath>
```

Fix any build errors until the build succeeds. Common issues:
- Missing `using` statements
- Namespace mismatches
- Incorrect component type for the element
- Typo in XPath attribute names (case-sensitive!)

---

## Step 6: Hand Off to User — Do NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated tests. The user runs tests and returns with feedback.

Post a concise summary that includes:
- Files created/edited
- TC-IDs covered (if applicable)
- The command the user can run to execute the new test(s):
  ```bash
  dotnet test <ProjectPath> --filter "FullyQualifiedName~{TestMethodName}" --no-build --logger "console;verbosity=detailed"
  ```
- Any assumptions you made (e.g., locator choice, lifecycle picked, custom components added)

Then wait. When the user returns with specific failures, make targeted fixes based on their guidance:
- **"XPath failed for Y"** → new CLI session, fresh snapshot, fix Map file
- **"Assertion expected X got Y"** → fix assertion text
- **"Timing issue on Z"** → add a wait

After each fix iteration, rebuild, then hand back to the user again. Do NOT speculatively rerun tests on your own.

**Exception:** if the user has explicitly said "run them and fix until green" in this conversation, follow that instruction for as long as it applies.

---

## Output Format

### Files Created/Modified

```
[CREATED] Pages/{WindowName}/{WindowName}.Map.cs
[CREATED] Pages/{WindowName}/{WindowName}.Actions.cs
[CREATED] Pages/{WindowName}/{WindowName}.Assertions.cs
[EDITED]  Pages/{ExistingWindow}/{ExistingWindow}.Map.cs — added 3 new properties
[EDITED]  Pages/{ExistingWindow}/{ExistingWindow}.Actions.cs — added 2 new methods
[CREATED] Models/{ModelName}.cs
[CREATED] Tests/{TestClassName}.cs
```

### Build Verification
```
Build succeeded: dotnet build <ProjectPath>
```
