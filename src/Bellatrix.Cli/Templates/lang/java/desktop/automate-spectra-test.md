# Automate SPECTRA Test Case (Java)

Bridge between SPECTRA manual test cases and BELLATRIX-Java automated system tests. Takes a SPECTRA test case (or suite) as input, automates it through the BELLATRIX Desktop bellatrix desktop CLI workflow, and maintains full traceability via `@ManualTestCase("TC-XXX")` annotations and SPECTRA `automated_by` fields.

**Target project:** `{project.dir}` (Maven, JUnit 5, `solutions.bellatrix:bellatrix.desktop:1.0`), root package
`{project.rootNamespace}`, sources under `src/test/java/{project.rootPath}/`. See `bellatrix-desktop-project-structure`.

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

Read the SPECTRA test case file to extract the scenario: `{testCasesDir}` is the test-case folder from `.bellatrix/config.json` (the same folder as SPECTRA's `tests.dir`; default `test-cases`).

```
read {testCasesDir}/{suite}/TC-{id}.md
```

Extract from the file:
- **`id`** — e.g., `TC-101` (will become the `@ManualTestCase("TC-101")` annotation)
- **`title`** — test name (will become the test method name)
- **`priority`** — maps to `@Tag("Smoke")` for high, `@Tag("Regression")` for medium/low
- **`component`** — maps to the page object window package (`pages/{window}/`)
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

**Context Economy (mandatory):** `snapshot` returns only `{ path, elementCount, summary }`. Never read the snapshot
file or `get-page-source` output into your own context. Delegate each locator to the
`bellatrix-desktop-locator-specialist` subagent (pass the description + snapshot `path`; only the XPath comes back).
If your agent cannot delegate, query narrowly instead:
`bellatrix desktop find --session <s> --snapshot <path> --name "…" --json`.

**Splash screen handling:** After `bellatrix desktop start-session`, if any tool call returns "UI no longer available" or "the parent window has closed", the app has a splash/loading screen that closed and the main window has appeared. Call `bellatrix desktop reattach-session --session <s> --window-title "<main window title>" --json` to re-attach to the main window, then retry the failed operation. Continue using the same `--session` name.

After executing all steps and validating the expected result, close the session:
```
bellatrix desktop close-session --session <s> --json
```

---

## Phase 2: Generate Code with TC-ID Traceability

Use `bellatrix-desktop-generate-code` with these **additional rules**:

### Test Method Naming

Convert the SPECTRA title to a Java method name (`action_Condition_ExpectedResult`, lower-camel first segment):
- "Addition of two positive integers" → `addition_OfTwoPositiveIntegers_DisplaysCorrectResult`
- "Checkout with expired card" → `checkout_WithExpiredCard_ShowsError`

### SPECTRA TC-ID Annotation (MANDATORY)

Every generated test method **MUST** carry a `@ManualTestCase("TC-XXX")` annotation that references the SPECTRA test case ID. This is a project-defined, metadata-only annotation — BELLATRIX-Java has no built-in string `ManualTestCase`, and it is NOT JUnit's `@Tag` (categories), `@DisplayName` (report text) or `@ParameterizedTest` (data-driven parameterization). It enables SPECTRA's automation coverage scanning.

If the project does not have the annotation yet, create it once in the `common` package
(`src/test/java/{project.rootPath}/common/ManualTestCase.java`) — it carries metadata only:

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

```java
package {project.rootNamespace}.tests.arithmetic;

import {project.rootNamespace}.common.Constants;
import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.pages.calculatorwindow.CalculatorWindow;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.CALCULATOR_APP_PATH, lifecycle = Lifecycle.REUSE_IF_STARTED)
public class CalculatorArithmeticTests extends DesktopTest {
    @Test
    @ManualTestCase("TC-142")
    @Tag("Smoke")
    public void addition_OfTwoPositiveIntegers_DisplaysCorrectResult() {
        CalculatorWindow calc = app().createPage(CalculatorWindow.class);
        calc.clickButton("5");
        calc.clickButton("+");
        calc.clickButton("3");
        calc.clickButton("=");
        calc.asserts().displayIs("8");
    }
}
```

`clickButton(String)` lives on the page class (`CalculatorWindow`), its locator on `Map`, and `displayIs(String)` on
`Asserts` (wait with `toHaveContent().waitToBe()` first, then `validateTextIs(...)` — desktop validators read the
value once). See `bellatrix-desktop-page-objects-and-patterns`.

### Priority → Tag Mapping

| SPECTRA Priority | JUnit 5 Tag |
|-----------------|-------------|
| `high` | `@Tag("Smoke")` |
| `medium` | `@Tag("Regression")` |
| `low` | `@Tag("Regression")` |

(TestNG projects: `@Test(groups = "Smoke")` / `@Test(groups = "Regression")`.)

### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into a single test method, even if the steps are similar. Do NOT use `@ParameterizedTest` with `@CsvSource` / `@MethodSource` to merge multiple TCs into a parameterized test.

```java
// CORRECT: Each TC gets its own method
@Test
@ManualTestCase("TC-142")
@Tag("Smoke")
public void addition_FivePlusThree_DisplaysEight() {
    CalculatorWindow calc = app().createPage(CalculatorWindow.class);
    calc.clickButton("5");
    calc.clickButton("+");
    calc.clickButton("3");
    calc.clickButton("=");
    calc.asserts().displayIs("8");
}

@Test
@ManualTestCase("TC-143")
@Tag("Smoke")
public void addition_TwoPlusSeven_DisplaysNine() {
    CalculatorWindow calc = app().createPage(CalculatorWindow.class);
    calc.clickButton("2");
    calc.clickButton("+");
    calc.clickButton("7");
    calc.clickButton("=");
    calc.asserts().displayIs("9");
}
```

**BANNED**: Putting multiple `@ManualTestCase` annotations on one method. The annotation is deliberately not `@Repeatable` (stacking it does not compile) — never make it repeatable. If you feel two TCs are "the same test with different data," they are still two separate methods.

### Comments Linking to SPECTRA

Add a Javadoc comment referencing the source:

```java
/**
 * Automates SPECTRA TC-142: Addition of two positive integers
 * Source: docs/standard-calculator.md
 * Criteria: AC-001, AC-003
 */
```

---

## Phase 3: Update SPECTRA Coverage

After generating the automated test, update the SPECTRA test case file to link back to the automation code.

### Step 3a: Update `automated_by` in SPECTRA test file

Read the SPECTRA test file:
```
read {testCasesDir}/{suite}/TC-{id}.md
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
  - {project.dir}/src/test/java/{project.rootPath}/tests/arithmetic/CalculatorArithmeticTests.java
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

(Run it from the repository root.) Compile only — never `mvn test` /
`mvn verify`. Fix any build errors until the build succeeds.

---

## Phase 5: Hand Off to User — DO NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated test(s). The user runs tests and returns with feedback.

### What to do

1. Verify the build succeeds with `{project.buildCommand}`
2. Post a concise summary: TC-ID(s) automated, files created/edited, command the user can run to execute the test (e.g. `mvn -f {project.path} test -Dtest=CalculatorArithmeticTests#addition_OfTwoPositiveIntegers_DisplaysCorrectResult`), and any assumptions you made (e.g., locator choices, lifecycle picked, custom components added)
3. Wait for the user to report results

### What NOT to do

- Do NOT run `mvn test` yourself
- Do NOT re-open CLI sessions speculatively to "double-check" locators
- Do NOT enter a 2/3/N-attempt fix loop on your own
- Do NOT defer this hand-off and run through more test cases first — ping at natural checkpoints (end of single TC, end of batch, or when the user asks)

### When the user returns with feedback

THEN you fix based on their concrete guidance:
- **"XPath failed for Y"** → new CLI session, fresh snapshot, fix the `Map` class, rebuild, hand back
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
| TC-142 | Addition of two positive integers | addition_OfTwoPositiveIntegers_DisplaysCorrectResult | tests/arithmetic/CalculatorArithmeticTests.java |

### Files Created/Modified
[CREATED] src/test/java/{project.rootPath}/pages/calculatorwindow/Map.java
[CREATED] src/test/java/{project.rootPath}/pages/calculatorwindow/CalculatorWindow.java
[CREATED] src/test/java/{project.rootPath}/pages/calculatorwindow/Asserts.java
[CREATED] src/test/java/{project.rootPath}/tests/arithmetic/CalculatorArithmeticTests.java
[EDITED]  {testCasesDir}/standard/TC-142.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
✅ mvn test-compile succeeded
```

---

## Batch Mode: Automate a Suite

### MANDATORY: One-by-One Processing

Suite automation = running the single-test workflow (Phase 0 → 1 → 2 → 3 → 4) in a loop, once per test case. Each test case gets its own complete CLI session.

**BANNED behaviors** (doing any of these is a failure):
- Grouping test cases by component, window, or any other criteria
- Generating code for multiple test cases after a single CLI session
- Batching test cases into ranges (e.g., "TC-777 to TC-870")
- Writing `Map`/page/`Asserts` classes without having run the CLI for THAT specific test case first
- Reading all test case files upfront before starting any CLI session
- Skipping the CLI run for a test case because "we already have the XPaths from another test case" — **different TCs produce different error messages, dialog text, confirmation states, and edge conditions; only live CLI execution reveals them**
- Running `mvn test` yourself — the USER runs tests and returns with feedback. Build-only verification (`{project.buildCommand}`) is your job
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

Run the **complete single-test workflow** for this one test case before moving to the next. The cycle is: **CLI → generate → build → update coverage → next** (the user runs the tests).

1. **Phase 0**: Read THIS test case file only (`{testCasesDir}/{suite}/TC-{id}.md`)
2. **Phase 1 (MANDATORY — no shortcuts)**: `bellatrix desktop start-session` → `bellatrix desktop snapshot` → execute ALL steps via bellatrix desktop CLI commands → observe actual UI text/state → validate expected result → `bellatrix desktop close-session`. Record the ACTUAL text of any error messages, dialogs, and result displays as observed — do not assume they match a sibling TC
3. **Phase 2**: Generate/edit page objects and test code with `@ManualTestCase("TC-XXX")`. Create a new page object package (`pages/{window}/` with `{Window}.java`, `Map.java`, `Asserts.java`) when a new window/view is encountered. Add custom components under `src/test/java/{project.rootPath}/components/` if BELLATRIX built-ins don't fit. Prefer the direct factories `byAutomationId` / `byName` over `by(T.class, new UiaXPathFindStrategy(...))` for single-attribute locators (never `byXPath` — broken in BELLATRIX-Java 1.0)
4. **Phase 4**: `{project.buildCommand}` and fix any BUILD errors (not test failures — you don't run tests)
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

SPECTRA's automation coverage scanner uses `scan_patterns` from `spectra.config.json` to detect TC-ID references in code. For Java the pattern is the annotation form `@ManualTestCase("{id}")` (NOT the C# attribute form `[ManualTestCase("{id}")]`):

```json
{
  "coverage": {
    "scan_patterns": [
      "@ManualTestCase(\"{id}\")"
    ]
  }
}
```

This means as long as you write `@ManualTestCase("TC-142")` (exactly that spelling — no `value =`, no extra spaces), SPECTRA will automatically detect the link during `spectra ai analyze --coverage` — even without manually editing `automated_by`. The manual edit in Phase 3 provides an explicit link; the scan pattern provides automatic discovery.

> **Important:** Do NOT use JUnit's `@Tag("TC-XXX")`, `@DisplayName("TC-XXX …")` or a `@ParameterizedTest` argument for traceability — `@Tag` is for categories (Smoke/Regression) and `@ParameterizedTest` for data-driven variation. Use `@ManualTestCase("TC-XXX")` instead, the project-defined metadata-only annotation in `src/test/java/{project.rootPath}/common/ManualTestCase.java`.

---

## Skills Required

| Skill | File | Purpose |
|-------|------|---------|
| Execute Scenario | `bellatrix-desktop-execute-scenario` | CLI interaction with live app |
| Generate Code | `bellatrix-desktop-generate-code` | Code generation with project awareness |
| Components | `bellatrix-desktop-components` | Component types reference |
| Locators | `bellatrix-desktop-locators-and-waits` | XPath patterns |
| Validations | `bellatrix-desktop-validations-and-assertions` | Assert patterns |
| Page Objects | `bellatrix-desktop-page-objects-and-patterns` | 3-class pattern |
| Test Structure | `bellatrix-desktop-test-structure-and-lifecycle` | Test class setup |
| Recipes | `bellatrix-desktop-system-tests-recipes` | Example tests |
