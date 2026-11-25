# Automate SPECTRA Test Case

Bridge between SPECTRA manual test cases and BELLATRIX automated system tests. Takes a SPECTRA test case (or suite) as input, automates it through the BELLATRIX Desktop bellatrix desktop CLI workflow, and maintains full traceability via `[ManualTestCase("TC-XXX")]` attributes and SPECTRA `automated_by` fields.

---

## When to Use This Skill

User says things like:
- "Automate TC-101"
- "Automate the standard calculator suite"
- "Create system tests for these test cases"
- "Turn these manual tests into automated tests"
- "Automate all high-priority tests in checkout"

---

## Phase 0: Load SPECTRA Test Case(s)

### Single Test Case

Read the SPECTRA test case file to extract the scenario:

```
read tests/{suite}/TC-{id}.md
```

Extract from the file:
- **`id`** — e.g., `TC-101` (will become `[ManualTestCase("TC-101")]` attribute)
- **`title`** — test name (will become test method name)
- **`priority`** — maps to `[Category("Smoke")]` for high, `[Category("Regression")]` for medium/low
- **`component`** — maps to page object window name
- **`source_refs`** — documentation origin (for context)
- **`criteria`** — acceptance criteria being verified
- **Steps** — the exact actions to perform
- **Expected Result** — what to assert
- **Preconditions** — setup requirements

### Suite (Multiple Test Cases)

To automate an entire suite:

```bash
spectra list --suite {suite} --output-format json
```

Parse the JSON output to get all test IDs, then process each one.

### Example: SPECTRA Test Case Input

```markdown
---
id: TC-142
priority: high
tags: [standard, arithmetic]
component: standard-calculator
source_refs: [docs/standard-calculator.md]
criteria: [AC-001, AC-003]
---

# Addition of two positive integers

## Preconditions
- Calculator app is open
- Calculator is in Standard mode
- Display shows 0

## Steps
1. Click the "5" button
2. Click the "+" button
3. Click the "3" button
4. Click the "=" button

## Expected Result
- Display shows "8"
```

---

## Phase 1: Execute Scenario via the bellatrix desktop CLI

Use `bellatrix-desktop-execute-scenario` to execute the test steps from the SPECTRA test case against the live application.

**Map SPECTRA steps to bellatrix desktop CLI actions:**

| SPECTRA Step | bellatrix desktop CLI action |
|-------------|------------|
| "Click the X button" | `bellatrix desktop click --session <s> --locator "//Button[@Name='X']" --json` |
| "Enter text in field" | `bellatrix desktop type-text --session <s> --locator "..." --text "..." --json` |
| "Verify display shows Y" | `bellatrix desktop get-text --session <s> --locator "..." --json` → compare with expected |
| "Select option from dropdown" | `bellatrix desktop click` on ComboBox → `bellatrix desktop click` on ListItem |

**CRITICAL:** Always `bellatrix desktop snapshot` before interacting with any new window/dialog. Use the snapshot to find the correct XPath locators — do NOT guess.

**Splash screen handling:** After `bellatrix desktop start-session`, if any tool call returns "UI no longer available" or "the parent window has closed", the app has a splash/loading screen that closed and the main window has appeared. Call `bellatrix desktop reattach-session --session <s> --window-title "<main window title>" --json` to re-attach to the main window, then retry the failed operation. Continue using the same `--session` name.

After executing all steps and validating the expected result, close the session:
```
bellatrix desktop close-session --session <s> --json
```

---

## Phase 2: Generate Code with TC-ID Traceability

Use `bellatrix-desktop-generate-code` with these **additional rules**:

### Test Method Naming

Convert the SPECTRA title to a C# method name:
- "Addition of two positive integers" → `Addition_OfTwoPositiveIntegers_DisplaysCorrectResult`
- "Checkout with expired card" → `Checkout_WithExpiredCard_ShowsError`

### SPECTRA TC-ID Attribute (MANDATORY)

Every generated test method **MUST** include a `[ManualTestCase("TC-XXX")]` attribute that references the SPECTRA test case ID. This is a custom metadata attribute (NOT NUnit's `[TestCase]` which is for data-driven parameterization). It enables SPECTRA's automation coverage scanning.

If the project does not have the attribute yet, create it once at the project root
(`ManualTestCaseAttribute.cs`) — it carries metadata only:

```csharp
namespace {project.rootNamespace};

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ManualTestCaseAttribute : Attribute
{
    public ManualTestCaseAttribute(string id) => Id = id;

    public string Id { get; }
}
```

```csharp
[Test]
[ManualTestCase("TC-142")]
[Category("Smoke")]
public void Addition_OfTwoPositiveIntegers_DisplaysCorrectResult()
{
    var calc = App.Create<StandardCalculatorWindow>();
    calc.ClickButton("5");
    calc.ClickButton("+");
    calc.ClickButton("3");
    calc.ClickButton("=");
    calc.AssertDisplayIs("8");
}
```

### Priority → Category Mapping

| SPECTRA Priority | NUnit Category |
|-----------------|----------------|
| `high` | `[Category("Smoke")]` |
| `medium` | `[Category("Regression")]` |
| `low` | `[Category("Regression")]` |

### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into a single test method, even if the steps are similar. Do NOT use `[TestCaseSource]` to merge multiple TCs into a parameterized test.

```csharp
// CORRECT: Each TC gets its own method
[Test]
[ManualTestCase("TC-142")]
[Category("Smoke")]
public void Addition_FivePlusThree_DisplaysEight()
{
    var calc = App.Create<CalculatorWindow>();
    calc.ClickButton("5");
    calc.ClickButton("+");
    calc.ClickButton("3");
    calc.ClickButton("=");
    calc.AssertDisplayIs("8");
}

[Test]
[ManualTestCase("TC-143")]
[Category("Smoke")]
public void Addition_TwoPlusSeven_DisplaysNine()
{
    var calc = App.Create<CalculatorWindow>();
    calc.ClickButton("2");
    calc.ClickButton("+");
    calc.ClickButton("7");
    calc.ClickButton("=");
    calc.AssertDisplayIs("9");
}
```

**BANNED**: Stacking multiple `[ManualTestCase]` attributes on one method. If you feel two TCs are "the same test with different data," they are still two separate methods.

### Comments Linking to SPECTRA

Add a summary comment referencing the source:

```csharp
/// <summary>
/// Automates SPECTRA TC-142: Addition of two positive integers
/// Source: docs/standard-calculator.md
/// Criteria: AC-001, AC-003
/// </summary>
```

---

## Phase 3: Update SPECTRA Coverage

After generating the automated test, update the SPECTRA test case file to link back to the automation code.

### Step 3a: Update `automated_by` in SPECTRA test file

Read the SPECTRA test file:
```
read tests/{suite}/TC-{id}.md
```

Add or update the `automated_by` field in the YAML frontmatter:

**Before:**
```yaml
---
id: TC-142
priority: high
tags: [standard, arithmetic]
component: standard-calculator
source_refs: [docs/standard-calculator.md]
criteria: [AC-001, AC-003]
---
```

**After:**
```yaml
---
id: TC-142
priority: high
tags: [standard, arithmetic]
component: standard-calculator
source_refs: [docs/standard-calculator.md]
criteria: [AC-001, AC-003]
automated_by:
  - {project.dir}/Tests/StandardCalculatorTests.cs
---
```

### Step 3b: Rebuild SPECTRA index

```bash
spectra index --suite {suite}
```

This updates `_index.json` with the new `automated_by` field so the dashboard and coverage reports reflect the automation link.

### Step 3c: Verify coverage (optional)

```bash
spectra ai analyze --coverage --output-format json
```

The automation coverage percentage should increase after linking.

---

## Phase 4: Verify Build

```bash
{project.buildCommand}
```

Fix any build errors until the build succeeds.

---

## Phase 5: Hand Off to User — DO NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated test(s). The user runs tests and returns with feedback.

### What to do

1. Verify the build succeeds with `{project.buildCommand}`
2. Post a concise summary: TC-ID(s) automated, files created/edited, command the user can run to execute the test, and any assumptions you made (e.g., locator choices, lifecycle picked, custom components added)
3. Wait for the user to report results

### What NOT to do

- Do NOT run `dotnet test` yourself
- Do NOT re-open CLI sessions speculatively to "double-check" locators
- Do NOT enter a 2/3/N-attempt fix loop on your own
- Do NOT defer this hand-off and run through more test cases first — ping at natural checkpoints (end of single TC, end of batch, or when the user asks)

### When the user returns with feedback

THEN you fix based on their concrete guidance:
- **"XPath failed for Y"** → new CLI session, fresh snapshot, fix the Map file, rebuild, hand back
- **"Assertion expected X got Y"** → fix the assertion, rebuild, hand back
- **"Timing issue on Z"** → add a wait, rebuild, hand back

Use the same hand-off discipline on each fix iteration: build succeeds → summary → wait.

### Exception

If the user explicitly says "run them and iterate until green" in the current conversation, follow that instruction for as long as it applies.

---

## Output Summary

After completing all phases, report:

```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Title | Test Method | File |
|-------|-------|-------------|------|
| TC-142 | Addition of two positive integers | Addition_OfTwoPositiveIntegers_DisplaysCorrectResult | Tests/StandardCalculatorTests.cs |

### Files Created/Modified
[CREATED] Pages/StandardCalculatorWindow/StandardCalculatorWindow.Map.cs
[CREATED] Pages/StandardCalculatorWindow/StandardCalculatorWindow.Actions.cs
[CREATED] Pages/StandardCalculatorWindow/StandardCalculatorWindow.Assertions.cs
[CREATED] Tests/StandardCalculatorTests.cs
[EDITED]  tests/standard/TC-142.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
✅ dotnet build succeeded
```

---

## Batch Mode: Automate a Suite

### MANDATORY: One-by-One Processing

Suite automation = running the single-test workflow (Phase 0 → 1 → 2 → 3 → 4) in a loop, once per test case. Each test case gets its own complete CLI session.

**BANNED behaviors** (doing any of these is a failure):
- Grouping test cases by component, window, or any other criteria
- Generating code for multiple test cases after a single CLI session
- Batching test cases into ranges (e.g., "TC-777 to TC-870")
- Writing Map/Actions/Assertions without having run the CLI for THAT specific test case first
- Reading all test case files upfront before starting any CLI session
- Skipping the CLI run for a test case because "we already have the XPaths from another test case" — **different TCs produce different error messages, dialog text, confirmation states, and edge conditions; only live CLI execution reveals them**
- Running `dotnet test` yourself — the USER runs tests and returns with feedback. Build-only verification is your job
- Entering speculative fix loops (locator guess → rerun → new guess → rerun). Fixes happen after the user reports what actually failed

### Step 1: List and Filter

```bash
spectra list --suite {suite} --output-format json
```

- **Skip** test cases that already have `automated_by` populated
- **Prioritize** remaining tests:
  1. `priority: high` first
  2. Tests with acceptance criteria (`criteria` field non-empty)
  3. All remaining tests
- Count remaining test cases — call this M

### Step 2: Loop — For EACH Test Case (N = 1 to M)

Run the **complete single-test workflow** for this one test case before moving to the next. The cycle is: **CLI → generate → build → run → fix-if-failing → update coverage → next**.

1. **Phase 0**: Read THIS test case file only (`test-cases/{suite}/TC-{id}.md`)
2. **Phase 1 (MANDATORY — no shortcuts)**: `bellatrix desktop start-session` → `bellatrix desktop snapshot` → execute ALL steps via bellatrix desktop CLI commands → observe actual UI text/state → validate expected result → `bellatrix desktop close-session`. Record the ACTUAL text of any error messages, dialogs, and result displays as observed — do not assume they match a sibling TC
3. **Phase 2**: Generate/edit page objects and test code with `[ManualTestCase("TC-XXX")]`. Create new page object folders when a new window/view is encountered. Add custom components under `{project.dir}/Components/` if BELLATRIX built-ins don't fit. Prefer `CreateByAutomationId` / `CreateByName` direct factories over `CreateByXPath` for single-attribute locators
4. **Phase 4**: `dotnet build` and fix any BUILD errors (not test failures — you don't run tests)
5. **Phase 3**: Update THIS test case's `automated_by` frontmatter
6. Continue to the next test case

After the batch is complete (or at natural checkpoints), **hand off to the user** with a summary of what was generated and the command they can run to verify. Do NOT run tests yourself. See Phase 5 for the full hand-off protocol.

Do NOT proceed to the next test case until the current one has had its CLI execution + code generation + build verification done.

### Step 3: Finalize Suite

After ALL test cases are processed:

```bash
spectra index --suite {suite}
spectra ai analyze --coverage --output-format json
```

Report the final coverage impact.

---

## Scan Pattern Compatibility

SPECTRA's automation coverage scanner uses `scan_patterns` from `spectra.config.json` to detect TC-ID references in code. The `[ManualTestCase("TC-XXX")]` attribute matches the configured pattern:

```json
{
  "coverage": {
    "scan_patterns": [
      "[ManualTestCase(\"{id}\")]"
    ]
  }
}
```

This means as long as you use `[ManualTestCase("TC-142")]`, SPECTRA will automatically detect the link during `spectra ai analyze --coverage` — even without manually editing `automated_by`. The manual edit in Phase 3 provides an explicit link; the scan pattern provides automatic discovery.

> **Important:** Do NOT use NUnit's `[TestCase("TC-XXX")]` for traceability — that attribute is for data-driven test parameterization. Use `[ManualTestCase("TC-XXX")]` instead, which is a custom metadata-only attribute defined in `{project.dir}/ManualTestCaseAttribute.cs`.

---

## Skills Required

| Skill | File | Purpose |
|-------|------|---------|
| Execute Scenario | `bellatrix-desktop-execute-scenario` | CLI interaction with live app |
| Generate Code | `bellatrix-desktop-generate-code` | Code generation with project awareness |
| Components | `bellatrix-desktop-components` | Component types reference |
| Locators | `bellatrix-desktop-locators-and-waits` | XPath patterns |
| Validations | `bellatrix-desktop-validations-and-assertions` | Assert patterns |
| Page Objects | `bellatrix-desktop-page-objects-and-patterns` | 3-file pattern |
| Test Structure | `bellatrix-desktop-test-structure-and-lifecycle` | Test class setup |
| Recipes | `bellatrix-desktop-system-tests-recipes` | Example tests |
