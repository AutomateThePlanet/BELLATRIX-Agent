# Demo.DesktopTests — Current Project Structure

This file is the authoritative as-is snapshot of what exists in the test project. Use it to decide whether to CREATE a new file/folder or EDIT an existing one. It is NOT aspirational — only add an entry after the folder/file is actually on disk.

> **Fresh project?** The tree below is the reference layout of the BELLATRIX demo project. If a folder or file listed
> here does not exist in your repo, treat it as the target layout: CREATE it (do not assume it exists), then replace
> this tree with what is really on disk.

> **Maintenance rule:** Whenever you add a new page object folder, custom component, model, or tests sub-folder as part of a generation workflow, update the corresponding section of this file in the same commit. The agent owns keeping this tree accurate.

---

## Top-Level Layout

```
Demo.DesktopTests/
├─ Demo.DesktopTests.csproj
├─ TestsInitialize.cs                 # NUnit [SetUpFixture] — BELLATRIX bootstrap
├─ ManualTestCaseAttribute.cs         # [ManualTestCase("TC-XXX")] for SPECTRA traceability
├─ testFrameworkSettings.Debug.json   # BELLATRIX config — lifecycle, timeouts, app path
├─ Components/                        # Custom BELLATRIX components (overrides / new)
├─ Models/                            # Enums and data classes used by page objects
├─ Pages/{WindowName}/                # Page objects — 3-file partial class pattern
├─ Pages/Shared/                      # Reusable page-object sections (composition)
└─ Tests/{FeatureArea}/               # Test classes grouped by feature area
```

### Key facts
- **Target app:** `Microsoft.WindowsCalculator_8wekyb3d8bbwe!App` (set as `defaultAppPath` in config)
- **Driver endpoint:** `http://127.0.0.1:4723` (Appium, managed by the driver — not by the agent)
- **Global lifecycle (config):** `restart every time`
- **Class-level lifecycle (generated classes default):** `Lifecycle.ReuseIfStarted` — speed optimization, overrides the config per test class

---

## `Pages/` — Page Objects (3-file partial class pattern)

Each window/view gets its own folder with three partial class files: `.Map.cs` (locators), `.Actions.cs` (workflows), `.Assertions.cs` (`Assert*` methods using `Validate*`).

| Folder | Purpose |
|--------|---------|
| `Pages/CalculatorWindow/` | Main Calculator window — arithmetic pad, memory row, history/memory flyouts, mode switcher |
| `Pages/AddSubtractDaysWindow/` | Date Calculation → "Add or subtract days" sub-mode |
| `Pages/DateCalculationWindow/` | Date Calculation mode shell (mode selector, date pickers common to both sub-modes) |
| `Pages/DateDifferenceWindow/` | Date Calculation → "Difference between dates" sub-mode |
| `Pages/ScientificCalculatorWindow/` | Scientific Calculator mode — DEG button, trig flyout (sin/cos/tan), scientific functions |
| `Pages/UnitConverterWindow/` | Unit Converter mode — category selector, two value fields, two unit ComboBoxes |

**Namespace:** `Demo.DesktopTests.Pages`
**Base class:** `DesktopPage`

---

## `Pages/Shared/` — Reusable Page-Object Sections

Sections are plain classes (NOT `DesktopPage`) that group locators and workflows shared across multiple windows. Windows embed them via composition — a `public SectionName Section { get; } = new();` property on the Map partial — and delegate from their action methods. Each section resolves `App` itself via `ServicesCollection.Current.Resolve<App>()`, so its members are live locators just like on a page.

| File | Used by | Purpose |
|------|---------|---------|
| `Pages/Shared/NumberPadSection.cs` | `CalculatorWindow`, `UnitConverterWindow` | Digits 0–9, decimal separator, and digit-entry helpers (`GetDigitButton`, `TypeCharacter`, `EnterNumber`, `EnterDecimalNumber`) — both windows share the same `numXButton` / `decimalSeparatorButton` AutomationIds |

**Namespace:** `Demo.DesktopTests.Pages.Shared`
**When to CREATE a new section:** when two or more windows expose the same locator set AND the same workflow around it. A single-window duplicate stays on the window.

---

## `Components/` — Custom Components

Custom BELLATRIX components live here when a built-in is missing behavior or needs an override. New component = inherit `Bellatrix.Desktop.Component`; behavior tweak = inherit an existing component and override virtual members.

| File | Extends | Why |
|------|---------|-----|
| `Components/WinUiComboBox.cs` | `ComboBox` | Overrides `SelectByText` to click-expand + XPath-lookup for WinUI3 dropdown items (the default `windows:select` ExecuteScript path is unreliable on WinUI3 flyouts) |

**Namespace:** `Demo.DesktopTests.Components`
**Usage in Map files:** `App.Components.CreateByAutomationId<WinUiComboBox>("...")`.

---

## `Models/` — Data Models and Enums

Plain data classes and enums shared across page objects. No component or BELLATRIX types here — pure data.

| File | Purpose |
|------|---------|
| `Models/UnitConverterEnums.cs` | Aggregated enum definitions (or shared types) for the Unit Converter |
| `Models/UnitEnumExtensions.cs` | Extension methods that map unit enums → display strings for ComboBox selection |
| `Models/ConverterType.cs` | Top-level converter category (Length, Weight, Volume, etc.) |
| `Models/AngleUnit.cs` | Angle units |
| `Models/AreaUnit.cs` | Area units |
| `Models/DataUnit.cs` | Data size units |
| `Models/EnergyUnit.cs` | Energy units |
| `Models/LengthUnit.cs` | Length units |
| `Models/PowerUnit.cs` | Power units |
| `Models/PressureUnit.cs` | Pressure units |
| `Models/SpeedUnit.cs` | Speed units |
| `Models/TemperatureUnit.cs` | Temperature units |
| `Models/TimeUnit.cs` | Time units |
| `Models/WeightUnit.cs` | Weight / mass units |

**Namespace:** `Demo.DesktopTests.Models`

---

## `Tests/` — Test Classes (grouped by feature area)

One file per feature area. One test method per SPECTRA TC. All methods carry `[ManualTestCase("TC-XXX")]`.

| Folder | File | Feature area |
|--------|------|--------------|
| `Tests/AlwaysOnTop/` | `CalculatorAlwaysOnTopTests.cs` | Always-on-top toggle, compact mode |
| `Tests/Arithmetic/` | `CalculatorArithmeticTests.cs` | Basic arithmetic (+, −, ×, ÷) |
| `Tests/ClearAndReset/` | `CalculatorClearAndResetTests.cs` | C, CE, backspace, reset behavior |
| `Tests/DateCalculation/` | `CalculatorDateCalculationTests.cs` | Date calculation mode tests (difference + add/subtract) |
| `Tests/History/` | `CalculatorHistoryTests.cs` | Calculation history pane behavior |
| `Tests/InputControls/` | `CalculatorInputControlsTests.cs` | Keyboard input, button input, mixed input |
| `Tests/Memory/` | `CalculatorMemoryTests.cs` | M+, M−, MR, MC, MS, memory flyout |
| `Tests/Percentage/` | `CalculatorPercentageTests.cs` | Percentage operator |
| `Tests/Scientific/` | `CalculatorScientificTests.cs` | Scientific Calculator — trigonometry (sin/cos/tan), angle units |
| `Tests/SpecialOperations/` | `CalculatorSpecialOperationsTests.cs` | Square root, square, reciprocal, negate |
| `Tests/UnitConverter/` | `UnitConverterConversionTests.cs` | Unit conversion correctness per category |
| `Tests/UnitConverter/` | `UnitConverterInteractionTests.cs` | Unit Converter UI interactions (category/unit selection, field focus) |

**Namespace:** `Demo.DesktopTests.Tests` (or nested by folder if preferred — check the file before deciding)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a window NOT in the Pages table above | **CREATE** new `Pages/{WindowName}/` folder with 3 partial files | `Pages/` |
| Test case targets an existing window | **EDIT** that window's Map/Actions/Assertions; append new members | `Pages/{WindowName}/` |
| Test case's feature area is NOT in the Tests table above | **CREATE** new `Tests/{FeatureArea}/Calculator{FeatureArea}Tests.cs` (or appropriate prefix for non-Calculator targets) | `Tests/` |
| Test case's feature area exists | **EDIT** — add a new `[Test]` method to the existing class | `Tests/{FeatureArea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** custom component inheriting `Component` or existing component | `Components/` |
| Same locator block + workflow already duplicated across ≥2 windows | **CREATE** a section class and embed via composition on each window | `Pages/Shared/` |
| Need a shared enum / data class | **EDIT** existing if it fits; otherwise **CREATE** a new file in `Models/` | `Models/` |

---

## Sync

Your agent's instructions file may reference this skill. When you add a new folder or component to the project, ALSO update this skill's tables in the same change.
