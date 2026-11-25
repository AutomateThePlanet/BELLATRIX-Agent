---
name: bellatrix-web-automate-spectra-test
description: Skill for automating a SPECTRA manual test case as a BELLATRIX web system test — reads TC steps, executes the scenario live via the bellatrix web CLI, generates code with TC-ID traceability, and updates SPECTRA coverage.
---

# Automate SPECTRA Test Case (Web)

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.web.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.web.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no web project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

Bridge between SPECTRA manual test cases and BELLATRIX automated web system tests. Takes a SPECTRA test case (or suite) as input, automates it through the `bellatrix web` CLI workflow, and maintains full traceability via a TC-ID marker on every generated test and the SPECTRA `automated_by` field.

---

## When to Use This Skill

User says things like:
- "Automate TC-101"
- "Automate the checkout suite"
- "Create web system tests for these test cases"
- "Turn these manual tests into automated tests"
- "Automate all high-priority tests in login"

---

## Phase 0: Load SPECTRA Test Case(s)

### Single Test Case

Read the SPECTRA test case file:

```
read tests/{suite}/TC-{id}.md
```

Extract:
- **`id`** — e.g. `TC-101` (becomes the test's TC-ID traceability marker)
- **`title`** — becomes the test method name
- **`priority`** — `high` → Smoke category, `medium`/`low` → Regression category
- **`component`** — maps to the page object (e.g. `checkout` → `CheckoutPage`)
- **`tags`** — decide the test file / feature area
- **`source_refs`** — documentation origin (for context)
- **`criteria`** — acceptance criteria being verified
- **Preconditions**, **Steps**, **Expected Result**

### Suite (Multiple Test Cases)

```bash
spectra list --suite {suite} --output-format json
```

Parse the JSON to get the test IDs, then process them **one at a time** (see Batch Mode).

### Example: SPECTRA Test Case Input

```markdown
---
id: TC-210
priority: high
tags: [checkout, coupon]
component: cart
source_refs: [docs/cart.md]
criteria: [AC-004]
---

# Apply a valid coupon in the cart

## Preconditions
- The cart contains "Falcon 9"
- The user is on https://demos.bellatrix.solutions/cart/

## Steps
1. Enter "happybirthday" in the coupon field
2. Click "Apply coupon"

## Expected Result
- The message "Coupon code applied successfully." is shown
- The order total is "54.00€"
```

---

## Phase 1: Execute Scenario via the bellatrix web CLI

Use `bellatrix-web-execute-scenario` to perform the preconditions and steps against the live site — **before** writing any code.

**Map SPECTRA steps to CLI actions:**

| SPECTRA step | `bellatrix web` action |
|--------------|------------------------|
| "Open / go to {url}" | `navigate --session <s> --url {url}` then `wait-for-page-ready` |
| "Click {x}" | `click --session <s> --locator <validated selector>` |
| "Enter {text} in {field}" | `type-text --session <s> --locator <…> --text "{text}"` |
| "Clear {field}" | `clear --session <s> --locator <…>` |
| "Choose {option} in {dropdown}" | `select --session <s> --locator <…> --value "{visible text}"` (native `<select>`); custom dropdowns: `click` trigger → `snapshot` → `click` option |
| "Check / tick {checkbox}" | `click` (then `get-attribute --attribute checked` to confirm) |
| "Verify {text} is shown" | `get-text --session <s> --locator <…>` → compare with the expected text |
| "Verify {element} is visible / disabled" | `is-visible` / `is-enabled` |
| "Wait for {x}" | `wait-for-element --locator <…> [--timeout-ms N]` |
| "Go back" | `back` |

**CRITICAL:**
- Always `snapshot` before interacting with any new page or state. Resolve every locator through the **`bellatrix-web-locator-specialist`** subagent (pass the description + snapshot `path`) or `find --snapshot <path>` — never guess, never read the snapshot file or `get-page-source` yourself.
- Record the ACTUAL texts the site shows (messages, totals, headings) — they become the assertions.
- Record every wait, cookie banner, modal, iframe, or dialog you had to handle.

Close the session when the steps and expected result are validated:

```
bellatrix web close-session --session <s> --json
```

---

## Phase 2: Generate Code with TC-ID Traceability

Use `bellatrix-web-generate-code` with these **additional rules**.

### Test Method Naming

Convert the SPECTRA title to `Action_Condition_ExpectedResult`:
- "Apply a valid coupon in the cart" → `ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal`
- "Checkout with expired card" → `Checkout_WithExpiredCard_ShowsPaymentError`

### SPECTRA TC-ID Marker (MANDATORY)

Every generated test method **MUST** carry the project's TC-ID traceability marker referencing the SPECTRA test case ID. It is a pure metadata marker — NOT the test framework's data-driven parameterization feature. SPECTRA's coverage scanner detects it.

### Priority → Category Mapping

| SPECTRA priority | Test category |
|------------------|---------------|
| `high` | `Smoke` |
| `medium` | `Regression` |
| `low` | `Regression` |

{{> lang:traceability}}
### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method — even when two TCs differ only in data. Do NOT merge TCs into one parameterized test, and do NOT put two TC-IDs on one method.

---

## Phase 3: Update SPECTRA Coverage

### Step 3a: Update `automated_by` in the SPECTRA test file

Read `tests/{suite}/TC-{id}.md` and add or update `automated_by` in its YAML frontmatter with the path of the generated test file:

{{> lang:automated-by}}
### Step 3b: Rebuild the SPECTRA index

```bash
spectra index --suite {suite}
```

### Step 3c: Verify coverage (optional)

```bash
spectra ai analyze --coverage --output-format json
```

The automation coverage percentage should increase after linking.

---

## Phase 4: Verify Build

{{> lang:build}}
---

## Phase 5: Hand Off to User — DO NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated test(s). The user runs tests and returns with feedback.

### What to do

1. Verify the build succeeds (Phase 4)
2. Post a concise summary: TC-ID(s) automated, files created/edited, the command to run the test, and any assumptions (locator choices, lifecycle picked, waits added, custom components or sections added)
3. Wait for the user to report results

### What NOT to do

- Do NOT run the tests yourself
- Do NOT re-open CLI sessions speculatively to "double-check" locators
- Do NOT enter a 2/3/N-attempt fix loop on your own
- Do NOT defer the hand-off and run through more test cases first — ping at natural checkpoints (end of a single TC, end of a batch, or when the user asks)

### When the user returns with feedback

Fix based on their concrete guidance:
- **"Locator failed for Y"** → new CLI session, fresh snapshot, re-resolve via the locator specialist, fix the Map file, rebuild, hand back
- **"Assertion expected X got Y"** → fix the assertion (re-observe live if unsure), rebuild, hand back
- **"Timing issue on Z"** → add the right wait, rebuild, hand back

### Exception

If the user explicitly says "run them and iterate until green" in the current conversation, follow that instruction for as long as it applies.

---

## Output Summary

{{> lang:summary}}
---

## Batch Mode: Automate a Suite

### MANDATORY: One-by-One Processing

Suite automation = the single-test workflow (Phase 0 → 1 → 2 → 4 → 3) in a loop, once per test case. Each test case gets its own complete CLI session.

**BANNED behaviors** (any of these is a failure):
- Grouping test cases by component, page, or any other criteria
- Generating code for several test cases after a single CLI session
- Batching test cases into ranges (e.g. "TC-777 to TC-870")
- Writing Map/Actions/Assertions without having run the CLI for THAT test case first
- Reading all test case files upfront before starting any CLI session
- Skipping the CLI run because "we already have the selectors from another test case" — **different TCs produce different messages, validation errors, redirects, and edge states; only live execution reveals them**
- Running the tests yourself — the USER runs tests and returns with feedback; build-only verification is your job
- Speculative fix loops (selector guess → rerun → new guess → rerun)

### Step 1: List and Filter

```bash
spectra list --suite {suite} --output-format json
```

- **Skip** test cases that already have `automated_by` populated
- **Prioritize**: `priority: high` first, then cases with non-empty `criteria`, then the rest
- Count the remaining cases — call this M

### Step 2: Loop — For EACH Test Case (N = 1 to M)

1. **Phase 0**: read THIS test case file only
2. **Phase 1 (MANDATORY — no shortcuts)**: `start-session` → `navigate` → `snapshot` → execute ALL steps via `bellatrix web` verbs → observe the actual texts/states → validate the expected result → `close-session`
3. **Phase 2**: generate/edit page objects and the test with the TC-ID marker. Create a new page object for every new page/modal; add sections or custom components when needed
4. **Phase 4**: build and fix BUILD errors (not test failures — you don't run tests)
5. **Phase 3**: update THIS test case's `automated_by`
6. Continue with the next test case

Do NOT start the next test case until the current one has had its CLI execution, code generation, and build verification. After the batch (or at natural checkpoints) hand off to the user (Phase 5).

### Step 3: Finalize Suite

```bash
spectra index --suite {suite}
spectra ai analyze --coverage --output-format json
```

Report the final coverage impact.

---

## Scan Pattern Compatibility

SPECTRA's coverage scanner uses `scan_patterns` from `spectra.config.json` to detect TC-ID references in code. As long as every generated test carries the TC-ID marker, SPECTRA discovers the link during `spectra ai analyze --coverage` even without the manual `automated_by` edit — the Phase 3 edit gives an explicit link, the scan pattern gives automatic discovery.

{{> lang:scan-pattern}}
---

## Skills Required

| Skill | Purpose |
|-------|---------|
| `bellatrix-web-execute-scenario` | Live CLI execution and locator validation |
| `bellatrix-web-generate-code` | Code generation with project awareness |
| `bellatrix-web-project-structure` | CREATE vs EDIT decisions |
| `bellatrix-web-components` | Component types |
| `bellatrix-web-locators-and-waits` | Locator ladder and waits |
| `bellatrix-web-validations-and-assertions` | Validate* patterns |
| `bellatrix-web-page-objects-and-patterns` | 3-file pattern, sections |
| `bellatrix-web-test-structure-and-lifecycle` | Test class setup and lifecycle |
| `bellatrix-web-system-tests-recipes` | Example tests |
