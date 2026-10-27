---
name: bellatrix-desktop-execute-spectra-test
description: Execute SPECTRA manual test cases automatically via the bellatrix desktop CLI — reads test steps, performs them through UI automation, validates expected results, reports pass/fail back to SPECTRA.
---

# Execute SPECTRA Test Cases via the bellatrix desktop CLI

Automatically execute SPECTRA manual test cases by driving the desktop
application through the bellatrix desktop CLI. Each test step is performed via UI
automation and the expected result is validated. Results are reported back
to SPECTRA the CLI for tracking.

**Two driver servers used:**
- `bellatrix desktop` CLI — UI interaction (click, type, read, snapshot)
- `spectra/*` — test execution state (start run, advance test, finalize)

---

## Trigger Phrases

- "execute TC-205 via the bellatrix desktop CLI"
- "run TC-100 through TC-110 automatically"
- "auto-execute the standard calculator suite"
- "run this test case through the app"
- "auto-execute the smoke tests"

---

## Workflow

### Step 1: Start SPECTRA Execution Run

Start a SPECTRA execution run so results are tracked:

```
Tool: start_execution_run
Params: { "suite": "{suite_name}" } or { "test_ids": ["TC-205", "TC-206"] }
```

Record the `run_id` and `test_handles` from the response.

### Step 2: Start or Attach BELLATRIX Session

Prefer a name from `bellatrix desktop list-apps` (apps configured through the `McpSettings__Apps__<n>__Name` /
`__Path` environment variables in the user's shell):

```
bellatrix desktop list-apps --json
bellatrix desktop start-session --app calculator --session tc --json
```

If the test case targets an app that is not configured, fall back to its path (e.g. the `defaultAppPath` in the test
project's `testFrameworkSettings.Debug.json`):

```
bellatrix desktop start-session --app-path "<path or AUMID>" --session tc --json
```

Or attach to an already running application:

```
bellatrix desktop attach-window --session tc --window-title "<title>" --json
```

### Step 3: Capture Initial UI Snapshot

**MANDATORY** before any interaction:

```
bellatrix desktop snapshot --session tc --json
```

This returns only `{ path, elementCount, summary }`. Never read the snapshot file into your context: resolve each
locator through the `bellatrix-desktop-locator-specialist` subagent (pass the description + `path`), or query it
narrowly with `bellatrix desktop find --session tc --snapshot <path> --name "…" --json`.

### Step 4: For Each Test Case

Loop through each test in the execution run:

#### 4a. Read Test Details from SPECTRA

```
Tool: get_test_case_details
Params: { "test_handle": "{handle}" }
```

Parse the response to extract:
- **Preconditions** — setup steps before the test
- **Steps** — ordered actions to perform
- **Expected Result** — what to validate

#### 4b. Execute Preconditions

If preconditions require UI actions (e.g., "navigate to Settings"),
perform them via the bellatrix desktop CLI. Capture a snapshot after setup
to confirm the app is in the expected state.

#### 4c. Execute Each Step

For each step, interpret the natural language instruction and map to
BELLATRIX bellatrix desktop CLI calls:

| Step pattern | bellatrix desktop CLI command | Example |
|-------------|----------|---------|
| "Click {button}" | `click` | `bellatrix desktop click --session tc --locator "//Button[@Name='5']" --json` |
| "Enter/Type {text}" | `type-text` | `bellatrix desktop type-text --session tc --locator "//Edit[@AutomationId='input']" --text "42" --json` |
| "Select {option}" | `click` ×2 | `click` the ComboBox, re-`snapshot`, then `click` the ListItem (there is no select verb) |
| "Press {key}" | — | **Unsupported** — the CLI has no key-press verb; click the equivalent button, or mark the step BLOCKED |
| "Verify/Check {text}" | `get-text` | `bellatrix desktop get-text --session tc --locator "//Text[@AutomationId='result']" --json` |
| "Navigate to {screen}" | `click` | Click the navigation element, then re-`snapshot` |
| "Wait for {condition}" | `snapshot` + `is-visible` | Re-`snapshot`, then `bellatrix desktop is-visible --session tc --locator "…" --json` (no wait verb) |
| "Clear {field}" | `clear` | `bellatrix desktop clear --session tc --locator "…" --json` |

**After each step**: capture a snapshot if the window changed or if the
next step targets a different area of the UI. This ensures XPath locators
are fresh and valid.

**If a step fails** (element not found, click doesn't work): capture a
snapshot, log the error, mark the test as FAILED with the specific step
that failed, and move to the next test.

#### 4d. Validate Expected Result

After executing all steps, validate the expected result:

1. Read the relevant UI element(s) that should reflect the expected outcome
2. Compare actual text/state with the expected result from the test case
3. Determine verdict:

| Comparison | Verdict |
|-----------|---------|
| Actual matches expected | PASS |
| Actual differs from expected | FAIL |
| Element not found | FAIL |
| App crashed or window closed | BLOCKED |

#### 4e. Report Result to SPECTRA

```
Tool: advance_test_case
Params: {
  "test_handle": "{handle}",
  "status": "PASSED|FAILED|BLOCKED",
  "notes": "Step 3: Expected '8' but got '7'. Screenshot captured."
}
```

For FAILED tests, include:
- Which step failed
- Expected vs actual values
- Screenshot reference if captured

#### 4f. Reset App State

Before the next test, reset the app to a clean state:
- Clear any input fields
- Navigate back to the starting screen
- Or restart the session if needed (`close-session`, then `start-session` with the same `--session` name)

### Step 5: Finalize

After all tests are executed:

```
Tool: finalize_execution_run
Params: { "run_id": "{run_id}" }
```

Then open the HTML report:

```
show preview .execution/reports/{html_filename}
```

### Step 6: Present Summary

Report the execution results:

```
Execution Complete
══════════════════════════════════

  Suite: Standard Calculator
  Tests: 20
  Passed: 17
  Failed: 2
  Blocked: 1

  Failed tests:
    TC-205: Step 3 — Expected "8" but display showed "7"
    TC-212: Element //Button[@Name='MC'] not found

  Blocked tests:
    TC-218: App crashed after memory overflow input

  Report: .execution/reports/run_2026-04-12_001.html
```

---

## Error Handling

| Error | Action |
|-------|--------|
| Element not found | Capture snapshot, try alternative XPath, if still not found → FAIL with "Element not found: {xpath}" |
| App not responding | Wait 10s, retry. If still unresponsive → BLOCKED with "App not responding" |
| App crashed | Log crash, BLOCKED for current test, restart session for remaining tests |
| Unexpected dialog/popup | Capture snapshot, dismiss if possible, continue. If blocking → BLOCKED |
| Wrong window active | Capture snapshot, try to switch to expected window. If not found → BLOCKED |

---

## Mode Selection

When the user asks to execute tests, ask which mode:

```
How should I execute these tests?

1. Auto-execute via the bellatrix desktop CLI — I'll drive the app automatically,
   validate results, and report pass/fail (hands-free)

2. Assisted execution — I'll show each step and wait for you
   to confirm the result manually (you watch, I track)
```

**Mode 1 (Auto)**: Full autonomous execution as described above.

**Mode 2 (Assisted)**: Show each step, wait for user to say pass/fail.
Uses only SPECTRA (no bellatrix desktop CLI needed). This is the existing
`spectra-execution` agent behavior.

---

## Tips

- **Batch execution**: For large suites, execute in batches of 10–20 to
  avoid long unattended runs where an early failure cascades
- **Screenshots**: Capture a screenshot on every FAIL — invaluable for
  debugging
- **Flaky tests**: If a test fails on first attempt, offer to retry once
  before marking as FAILED
- **Multi-window**: When a test involves multiple windows (e.g., dialog
  opens), capture snapshot of each new window before interacting
