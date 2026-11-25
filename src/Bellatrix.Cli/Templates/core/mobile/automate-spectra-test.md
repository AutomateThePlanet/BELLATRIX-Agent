---
name: bellatrix-mobile-automate-spectra-test
description: Skill for automating a SPECTRA manual test case as a BELLATRIX mobile system test — reads TC steps, executes the scenario live via the bellatrix mobile CLI, generates Android/iOS code with TC-ID traceability, and updates SPECTRA coverage.
---

# Automate SPECTRA Test Case (Mobile)

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.mobile.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.mobile.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no mobile project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

Bridge between SPECTRA manual test cases and BELLATRIX automated mobile system tests. Takes a SPECTRA
test case (or suite), executes it live against the Android/iOS app through the `bellatrix mobile` CLI,
generates page objects and a test with a TC-ID traceability marker, and links the test back through the
SPECTRA `automated_by` field.

---

## When to Use This Skill

User says things like:
- "Automate TC-101"
- "Automate the login suite on Android"
- "Create mobile system tests for these test cases"
- "Turn these manual app tests into automated tests"
- "Automate all high-priority checkout tests on iOS"

If the platform is not stated and the project targets both, ask once which platform to automate first.

---

## Phase 0: Load SPECTRA Test Case(s)

### Single Test Case

Read the SPECTRA test case file: `{testCasesDir}` is the test-case folder from `.bellatrix/config.json` (the same folder as SPECTRA's `tests.dir`; default `test-cases`).

```
read {testCasesDir}/{suite}/TC-{id}.md
```

Extract:
- **`id`** — e.g. `TC-101` (becomes the traceability marker on the test method)
- **`title`** — becomes the test method name
- **`priority`** — maps to the Smoke / Regression category
- **`component`** — maps to the screen / page object name
- **`tags`** — decide the test file (feature area) and, if present, the platform
- **`source_refs`**, **`criteria`** — documentation origin and acceptance criteria (for the summary comment)
- **Preconditions**, **Steps**, **Expected Result**

### Suite (Multiple Test Cases)

```bash
spectra list --suite {suite} --output-format json
```

Parse the JSON to get the test IDs, then process them **one at a time** (see Batch Mode).

### Example: SPECTRA Test Case Input

```markdown
---
id: TC-101
priority: high
tags: [login, authentication, android]
component: login-screen
source_refs: [docs/mobile-login.md]
criteria: [AC-001, AC-002]
---

# Login with valid credentials opens the home screen

## Preconditions
- App is freshly launched
- User "demo.user" exists

## Steps
1. Enter "demo.user" in the User name field
2. Enter "Secret123!" in the Password field
3. Tap "Log in"

## Expected Result
- Home screen shows "Welcome, demo.user"
```

---

## Phase 1: Execute Scenario via the bellatrix mobile CLI

Use `bellatrix-mobile-execute-scenario` to run the steps from the test case against the live app.

**Map SPECTRA steps to CLI actions:**

| SPECTRA Step | `bellatrix mobile` CLI action |
|-------------|-------------------------------|
| "Tap / Press the X button" | `click --strategy <t> --locator <v>` |
| "Enter / Type T in field F" | `type-text --strategy <t> --locator <v> --text "T"` |
| "Clear field F" | `clear --strategy <t> --locator <v>` |
| "Verify Y is shown / reads Z" | `is-visible` / `get-text` → compare with the expected result |
| "Verify option is checked / switched on" | `get-attribute --attribute checked` (Android) / `value` (iOS) |
| "Select option O from the drop-down" | `click` the spinner/picker → re-`snapshot` → `click` the option |
| "Scroll to Z" | `scroll-to-element` (or a scrolling `androiduiautomator` selector) |
| "Swipe left / up" | `swipe --start-x … --start-y … --end-x … --end-y …` |
| "Send the app to background / reopen" | `background-app --seconds N` |
| "Rotate the device" | `set-orientation --orientation landscape` |

**CRITICAL:**
- Always `snapshot` before interacting with any new screen, dialog, or sheet, and resolve every
  locator from that snapshot via the **`bellatrix-mobile-locator-specialist`** (or a narrow `find`) — do
  NOT guess.
- Never read the snapshot file or `get-page-source` output into your own context.
- Appium and the device are the user's: on a connection error, ask the user to start them.

After executing all steps and validating the expected result, close the session:

```
bellatrix mobile close-session --session <name> --json
```

Record the ACTUAL texts observed (error messages, labels, toasts) — the generated assertions use what
the app really shows, not the wording of the manual test.

---

## Phase 2: Generate Code with TC-ID Traceability

Use `bellatrix-mobile-generate-code` with these **additional rules**:

### Test Method Naming

{{> lang:test-naming}}
{{> lang:traceability}}
### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine several test cases into one
method, even if the steps are similar, and do NOT use data-driven parameterization to merge test cases.
Two test cases that are "the same test with different data" are still two methods.

{{> lang:one-per-method}}
**BANNED:** stacking multiple TC-ID markers on one method.

---

## Phase 3: Update SPECTRA Coverage

After generating the automated test, link the SPECTRA test case back to the code.

### Step 3a: Update `automated_by` in the SPECTRA test file

Read the SPECTRA test file (`{testCasesDir}/{suite}/TC-{id}.md`) and add or update `automated_by` in its YAML
frontmatter with the path of the generated test file:

**Before:**
```yaml
---
id: TC-101
priority: high
tags: [login, authentication, android]
component: login-screen
source_refs: [docs/mobile-login.md]
criteria: [AC-001, AC-002]
---
```

**After:**
```yaml
---
id: TC-101
priority: high
tags: [login, authentication, android]
component: login-screen
source_refs: [docs/mobile-login.md]
criteria: [AC-001, AC-002]
automated_by:
  - {path to the generated test file}
---
```

{{> lang:automated-by}}
### Step 3b: Rebuild the SPECTRA index

```bash
spectra index --suite {suite}
```

This updates `_index.json` with the new `automated_by` field so the dashboard and coverage reports
reflect the link.

### Step 3c: Verify coverage (optional)

```bash
spectra ai analyze --coverage --output-format json
```

Automation coverage should increase after linking.

---

## Phase 4: Verify Build

Build the test project and fix every BUILD error until it succeeds.

{{> lang:build}}
---

## Phase 5: Hand Off to User — DO NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated test(s) — the user owns Appium, the
device, and the test run, and returns with feedback.

### What to do

1. Verify the build succeeds (Phase 4)
2. Post a concise summary: TC-ID(s) automated, platform, files created/edited, the command the user can
   run, and any assumptions (locator choices, lifecycle, custom components)
3. Wait for the user to report results

### What NOT to do

- Do NOT run the tests yourself
- Do NOT re-open CLI sessions speculatively to "double-check" locators
- Do NOT enter a 2/3/N-attempt fix loop on your own
- Do NOT defer the hand-off to run through more test cases first — ping at natural checkpoints (end of a
  single TC, end of a batch, or when the user asks)

### When the user returns with feedback

Fix based on their concrete guidance:
- **"Locator failed for Y"** → new CLI session, fresh snapshot, re-validate, fix the Map file, rebuild, hand back
- **"Assertion expected X got Y"** → fix the assertion, rebuild, hand back
- **"Timing issue on Z"** → add a wait on that component, rebuild, hand back

Use the same hand-off discipline on every fix iteration: build succeeds → summary → wait.

### Exception

If the user explicitly says "run them and iterate until green" in the current conversation, follow that
instruction for as long as it applies.

---

## Output Summary

After completing all phases, report:

{{> lang:summary}}
---

## Batch Mode: Automate a Suite

### MANDATORY: One-by-One Processing

Suite automation = the single-test workflow (Phase 0 → 1 → 2 → 4 → 3) in a loop, once per test case.
Each test case gets its own complete CLI session.

**BANNED behaviors** (any of these is a failure):
- Grouping test cases by component, screen, or any other criteria
- Generating code for several test cases after a single CLI session
- Batching test cases into ranges (e.g. "TC-777 to TC-870")
- Writing Map/Actions/Assertions without having run the CLI for THAT specific test case first
- Reading all test case files upfront before starting any CLI session
- Skipping the CLI run because "we already have the locators from another test case" — **different TCs
  produce different messages, dialogs, keyboard states, permission prompts, and edge conditions; only
  live CLI execution reveals them**
- Running the tests yourself — the USER runs tests and returns with feedback; build-only verification is
  your job
- Speculative fix loops (locator guess → rerun → new guess → rerun)

### Step 1: List and Filter

```bash
spectra list --suite {suite} --output-format json
```

- **Skip** test cases that already have `automated_by`
- **Prioritize** the rest: `priority: high` first, then test cases with non-empty `criteria`, then the
  remainder
- Count the remaining test cases — call it M

### Step 2: Loop — For EACH Test Case (N = 1 to M)

Run the **complete single-test workflow** for this one test case before moving to the next:

1. **Phase 0** — read THIS test case file only
2. **Phase 1 (MANDATORY — no shortcuts)** — `start-session` → `snapshot` → execute ALL steps via the CLI →
   observe the actual UI text/state → validate the expected result → `close-session`
3. **Phase 2** — generate/edit page objects and the test with its TC-ID marker; create a new page object
   folder when a new screen appears; add custom components when built-ins don't fit
4. **Phase 4** — build and fix BUILD errors (not test failures — you don't run tests)
5. **Phase 3** — update THIS test case's `automated_by`
6. Continue with the next test case

After the batch (or at natural checkpoints), **hand off to the user** (Phase 5). Do NOT proceed to the
next test case until the current one has had CLI execution + code generation + a green build.

### Step 3: Finalize Suite

```bash
spectra index --suite {suite}
spectra ai analyze --coverage --output-format json
```

Report the final coverage impact.

---

## Scan Pattern Compatibility

SPECTRA's automation coverage scanner uses `scan_patterns` from `spectra.config.json` to detect TC-ID
references in code.

{{> lang:scan-pattern}}
---

## Skills Required

| Skill | Purpose |
|-------|---------|
| `bellatrix-mobile-execute-scenario` | CLI interaction with the live app |
| `bellatrix-mobile-generate-code` | Code generation with project awareness |
| `bellatrix-mobile-project-structure` | What already exists (CREATE vs EDIT) |
| `bellatrix-mobile-components` | Component types per platform |
| `bellatrix-mobile-locators-and-waits` | Strategies, strategy → factory mapping, waits |
| `bellatrix-mobile-validations-and-assertions` | Validate methods |
| `bellatrix-mobile-page-objects-and-patterns` | 3-file pattern, sections |
| `bellatrix-mobile-test-structure-and-lifecycle` | Test class setup, lifecycle |
| `bellatrix-mobile-system-tests-recipes` | Example tests |
