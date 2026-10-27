---
description: As-is snapshot of the demo-desktop-tests (Java/Maven) project tree. Ground CREATE vs EDIT decisions on this file. Refresh whenever a new pages/, components/, models/, or tests/ package is added.
---

# demo-desktop-tests — Current Project Structure (Java)

This file is the authoritative as-is snapshot of what exists in the test project. Use it to decide whether to CREATE a new file/folder or EDIT an existing one. It is NOT aspirational — only add an entry after the folder/file is actually on disk.

> **Fresh project?** The tree below is the reference layout of the BELLATRIX demo project (ported to Java). If a
> folder or file listed here does not exist in your repo, treat it as the target layout: CREATE it (do not assume it
> exists), then replace this tree with what is really on disk.

> **Maintenance rule:** Whenever you add a new page object package, custom component, model, or tests sub-package as part of a generation workflow, update the corresponding section of this file in the same commit. The agent owns keeping this tree accurate.

---

## Top-Level Layout

Root package: `demo.desktoptests` (folders under `src/test/java/demo/desktoptests/`).

```
demo-desktop-tests/
├─ pom.xml                                          # solutions.bellatrix:bellatrix.desktop:1.0 (test scope), JUnit 5
├─ src/main/resources/application.properties        # environment=dev
├─ src/main/resources/testFrameworkSettings.dev.json # desktopSettings — lifecycle, timeouts, app path, serviceUrl
└─ src/test/java/demo/desktoptests/
   ├─ common/ManualTestCase.java          # @ManualTestCase("TC-XXX") for SPECTRA traceability
   ├─ common/UiaXPathFindStrategy.java    # XPath find strategy (BELLATRIX-Java 1.0 byXPath is broken)
   ├─ common/Constants.java               # compile-time constants for @ExecutionApp(appPath = …)
   ├─ components/                         # Custom BELLATRIX components (overrides / new)
   ├─ models/                             # Enums, records and data classes used by page objects
   ├─ pages/{window}/                     # Page objects — {Window}.java + Map.java + Asserts.java
   ├─ pages/shared/                       # Reusable page-object sections (composition)
   └─ tests/{featurearea}/                # Test classes grouped by feature area
```

### Key facts
- **Target app:** `Microsoft.WindowsCalculator_8wekyb3d8bbwe!App` (set as `defaultAppPath` in config and as `Constants.CALCULATOR_APP_PATH`)
- **Driver endpoint:** `http://127.0.0.1:4723` (`desktopSettings.serviceUrl`; the driver is managed by the user — not by the agent, not by the tests)
- **Global lifecycle (config):** `restart every time`
- **Class-level lifecycle (generated classes default):** `Lifecycle.REUSE_IF_STARTED` — declared speed intent, overrides the config per test class (BELLATRIX-Java 1.0 still restarts per test — see `bellatrix-desktop-test-structure-and-lifecycle`)
- **Build check:** `mvn -q test-compile` (never `mvn test` — the user runs tests)
- **Toolchain:** BELLATRIX-Java 1.0 targets Java 19 — Maven must run on **JDK 19 or later** (`mvn -v`); BELLATRIX-Java is not on Maven Central, install it with `mvn install` from the BELLATRIX-Java repository

### Required project wiring (`pom.xml`)

```xml
<properties>
  <!-- BELLATRIX-Java 1.0 is built for Java 19. Use source/target: the default maven-compiler-plugin of Maven 3.8
       ignores maven.compiler.release and compiles for Java 5 -->
  <maven.compiler.source>19</maven.compiler.source>
  <maven.compiler.target>19</maven.compiler.target>
  <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
</properties>

<dependencies>
  <dependency>
    <groupId>solutions.bellatrix</groupId>
    <artifactId>bellatrix.desktop</artifactId>
    <version>1.0</version>
    <scope>test</scope>
  </dependency>
  <dependency>
    <groupId>org.junit.jupiter</groupId>
    <artifactId>junit-jupiter-engine</artifactId>
    <version>5.9.2</version>
    <scope>test</scope>
  </dependency>
  <dependency>
    <groupId>org.junit.jupiter</groupId>
    <artifactId>junit-jupiter-params</artifactId>   <!-- only for data-driven tests -->
    <version>5.9.2</version>
    <scope>test</scope>
  </dependency>
</dependencies>
```

`bellatrix.core` already brings JUnit 5.9.2 and TestNG 7.7.0 transitively; the explicit JUnit lines keep the test
framework from being an accident of the dependency graph.

---

## `pages/` — Page Objects (3-class pattern)

Each window/view gets its own package with three classes: `Map.java` (component methods / locators), `{Window}.java` (extends `DesktopPage<Map, Asserts>`; workflows), `Asserts.java` (extends `PageAsserts<Map>`; assertion methods using `validate*`).

| Package | Purpose |
|---------|---------|
| `pages/calculatorwindow/` | Main Calculator window — arithmetic pad, memory row, history/memory flyouts, mode switcher |
| `pages/addsubtractdayswindow/` | Date Calculation → "Add or subtract days" sub-mode |
| `pages/datecalculationwindow/` | Date Calculation mode shell (mode selector, date pickers common to both sub-modes) |
| `pages/datedifferencewindow/` | Date Calculation → "Difference between dates" sub-mode |
| `pages/scientificcalculatorwindow/` | Scientific Calculator mode — DEG button, trig flyout (sin/cos/tan), scientific functions |
| `pages/unitconverterwindow/` | Unit Converter mode — category selector, two value fields, two unit ComboBoxes |

**Package:** `demo.desktoptests.pages.<window>`
**Base classes:** `DesktopPage<Map, Asserts>`, `PageMap`, `PageAsserts<Map>` (`solutions.bellatrix.desktop.pages`)

---

## `pages/shared/` — Reusable Page-Object Sections

Sections are plain classes (NOT `DesktopPage`) that group locators and workflows shared across multiple windows. Windows expose them via composition — a `public NumberPadSection numberPad() { return new NumberPadSection(); }` method on the window's `Map` — and delegate from their action methods. Each section creates its components through `new ComponentCreateService()`, so its members are live locators just like on a page.

| File | Used by | Purpose |
|------|---------|---------|
| `pages/shared/NumberPadSection.java` | `CalculatorWindow`, `UnitConverterWindow` | Digits 0–9, decimal separator, and digit-entry helpers (`digitButton`, `typeCharacter`, `enterNumber`, `enterDecimalNumber`) — both windows share the same `numXButton` / `decimalSeparatorButton` AutomationIds |

**Package:** `demo.desktoptests.pages.shared`
**When to CREATE a new section:** when two or more windows expose the same locator set AND the same workflow around it. A single-window duplicate stays on the window.

---

## `components/` — Custom Components

Custom BELLATRIX components live here when a built-in is missing behavior or needs an override. New component = extend `solutions.bellatrix.desktop.components.DesktopComponent`; behavior tweak = extend an existing component (`ComboBox`, `Button`, …) and override its public methods.

| File | Extends | Why |
|------|---------|-----|
| `components/WinUiComboBox.java` | `ComboBox` | Overrides `selectByText` to click-expand + XPath-lookup for WinUI3 dropdown items (the built-in `selectByText` only types the text, which WinUI3 flyouts ignore) |

**Package:** `demo.desktoptests.components`
**Usage in Map classes:** `create().byAutomationId(WinUiComboBox.class, "...")`.

---

## `models/` — Data Models and Enums

Plain records, data classes and enums shared across page objects. No component or BELLATRIX types here — pure data.

| File | Purpose |
|------|---------|
| `models/ConverterType.java` | Top-level converter category (Length, Weight, Volume, etc.) — enum with a `displayName()` used for ComboBox selection |
| `models/AngleUnit.java` | Angle units |
| `models/AreaUnit.java` | Area units |
| `models/DataUnit.java` | Data size units |
| `models/EnergyUnit.java` | Energy units |
| `models/LengthUnit.java` | Length units |
| `models/PowerUnit.java` | Power units |
| `models/PressureUnit.java` | Pressure units |
| `models/SpeedUnit.java` | Speed units |
| `models/TemperatureUnit.java` | Temperature units |
| `models/TimeUnit.java` | Time units |
| `models/WeightUnit.java` | Weight / mass units |

Each unit enum carries its display string (`LengthUnit.CENTIMETERS.displayName()` → `"Centimeters"`); Java enums replace C#'s separate extension-method class.

**Package:** `demo.desktoptests.models`

---

## `tests/` — Test Classes (grouped by feature area)

One file per feature area. One test method per SPECTRA TC. All methods carry `@ManualTestCase("TC-XXX")`.

| Package | File | Feature area |
|---------|------|--------------|
| `tests/alwaysontop/` | `CalculatorAlwaysOnTopTests.java` | Always-on-top toggle, compact mode |
| `tests/arithmetic/` | `CalculatorArithmeticTests.java` | Basic arithmetic (+, −, ×, ÷) |
| `tests/clearandreset/` | `CalculatorClearAndResetTests.java` | C, CE, backspace, reset behavior |
| `tests/datecalculation/` | `CalculatorDateCalculationTests.java` | Date calculation mode tests (difference + add/subtract) |
| `tests/history/` | `CalculatorHistoryTests.java` | Calculation history pane behavior |
| `tests/inputcontrols/` | `CalculatorInputControlsTests.java` | Keyboard input, button input, mixed input |
| `tests/memory/` | `CalculatorMemoryTests.java` | M+, M−, MR, MC, MS, memory flyout |
| `tests/percentage/` | `CalculatorPercentageTests.java` | Percentage operator |
| `tests/scientific/` | `CalculatorScientificTests.java` | Scientific Calculator — trigonometry (sin/cos/tan), angle units |
| `tests/specialoperations/` | `CalculatorSpecialOperationsTests.java` | Square root, square, reciprocal, negate |
| `tests/unitconverter/` | `UnitConverterConversionTests.java` | Unit conversion correctness per category |
| `tests/unitconverter/` | `UnitConverterInteractionTests.java` | Unit Converter UI interactions (category/unit selection, field focus) |

**Package:** `demo.desktoptests.tests.<featurearea>` (the `package` line must match the folder)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a window NOT in the Pages table above | **CREATE** new `pages/{window}/` package with `{Window}.java`, `Map.java`, `Asserts.java` | `pages/` |
| Test case targets an existing window | **EDIT** that window's Map/page/Asserts classes; append new methods | `pages/{window}/` |
| Test case's feature area is NOT in the Tests table above | **CREATE** new `tests/{featurearea}/Calculator{FeatureArea}Tests.java` (or appropriate prefix for non-Calculator targets) | `tests/` |
| Test case's feature area exists | **EDIT** — add a new `@Test` method to the existing class | `tests/{featurearea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** custom component extending `DesktopComponent` or an existing component | `components/` |
| Same locator block + workflow already duplicated across ≥2 windows | **CREATE** a section class and expose it from each window's `Map` | `pages/shared/` |
| Need a shared enum / record / data class | **EDIT** existing if it fits; otherwise **CREATE** a new file in `models/` | `models/` |
| `common/ManualTestCase.java`, `common/UiaXPathFindStrategy.java` or `common/Constants.java` missing | **CREATE** once (code in `bellatrix-desktop-generate-code`) | `common/` |

---

## Sync

Your agent's instructions file may reference this skill. When you add a new package or component to the project, ALSO update this skill's tables in the same change.
