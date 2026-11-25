---
name: bellatrix-mobile-generate-code
description: Skill for generating or editing BELLATRIX mobile test code (page objects, sections, models, tests) for Android/iOS using locators validated during scenario execution.
---

# Generate Mobile Test Code

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.mobile.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.mobile.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no mobile project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

Generate production-ready BELLATRIX mobile test code from the findings of scenario execution
(`bellatrix-mobile-execute-scenario`). This skill handles both **creating new files** and
**editing/expanding existing ones**.

**Source of truth:** only locators validated live with the `bellatrix mobile` CLI (Validated Locators
table) and texts actually observed on the device. Never invent a locator, never "fix" one from memory,
never copy one from a sibling test case without re-validating it.

{{> lang:target}}
---

## CRITICAL: Read Reference Documentation First

**BEFORE generating any code**, load these skills:

| Skill | Content |
|-------|---------|
| `bellatrix-mobile-project-structure` | **What already exists — read FIRST** |
| `bellatrix-mobile-components` | Component types per platform, methods, factory methods |
| `bellatrix-mobile-locators-and-waits` | Find strategies, strategy → factory mapping, waits |
| `bellatrix-mobile-validations-and-assertions` | Validate* methods and which components support them |
| `bellatrix-mobile-test-structure-and-lifecycle` | Test bases, app attributes, lifecycle, hooks |
| `bellatrix-mobile-page-objects-and-patterns` | **Page object structure, file naming, sections, models** |
| `bellatrix-mobile-app-services` | **App services (Components, AppService, Device, Keyboard, TouchActions, Create)** |
| `bellatrix-mobile-system-tests-recipes` | Complete test examples |

---

## Step 1: Scan Existing Project Structure

Before generating code, **always check what already exists**: the project-structure skill first, then
the page object folders, test files, models, sections, and custom components on disk.

### Decision Matrix

| Situation | Action |
|-----------|--------|
| Page object folder exists for this screen | **EDIT** — add new members to the existing Map/Actions/Assertions files |
| Page object folder does NOT exist | **CREATE** — new folder with the 3 files |
| Test class exists for this feature area | **EDIT** — add new test methods to the existing class |
| Test class does NOT exist | **CREATE** — new test file |
| Model exists with matching properties | **REUSE** as-is |
| Model exists but needs more properties | **EDIT** — add properties |
| Model does NOT exist | **CREATE** — new model file |
| Same block + workflow already on ≥2 screens | **CREATE** a shared section, embed it by composition |

### IMPORTANT: When Editing Existing Files

- **Read the entire file first** before changing it
- **Preserve all existing code** — never remove or rewrite existing members
- **Follow the same patterns** — naming, formatting, locator style, platform namespace
- **Append** new members after existing ones
{{> lang:edit-split}}

### One Platform per File

Android and iOS use different component types with the same names. A page object, section, or test file
targets exactly ONE platform. If the project covers both, the Android and iOS versions of a screen are
separate classes in separate files/namespaces with the same public member names.

---

## Step 2: Generate Page Objects (3-File Pattern)

{{> lang:page-files}}
{{> lang:page-objects}}
---

## Step 2b: Custom Components (When Built-ins Don't Fit)

If a built-in BELLATRIX component lacks a control-level behavior you need (scroll-then-tap, a swipe-to-
delete row, a picker that needs an extra wait) or behaves incorrectly for one control, add a custom
component in the project's `Components/` folder instead of working around it in page objects or tests.

{{> lang:custom-components}}
### Rules

- **Never modify BELLATRIX source.** Extend in the test project only.
- **Keep components thin.** Control-level behavior only; business workflow stays in Actions files.
- **One file per component**, file name = class name.
- **Reuse** — a custom component defined once can be used from any Map file.

---

## Step 3: Generate Model Classes (If Needed)

Use plain data classes for structured form input (credentials, addresses, card data). Models hold no
BELLATRIX or component types.

{{> lang:models}}
---

## Step 4: Generate Test Classes

### Test File Organization (MANDATORY)

Do NOT put all tests into a single file. Organize test classes by **logical feature area** using the
test case's tags/component. Each distinct feature area gets its own test class file.

**Naming convention:** `{App}{FeatureArea}Tests`, one folder per feature area under `Tests/`.

| Tags on test case | Test class |
|-------------------|------------|
| `login`, `authentication` | `ShopLoginTests` |
| `cart`, `add-to-cart` | `ShopCartTests` |
| `checkout`, `payment` | `ShopCheckoutTests` |
| `search`, `filter` | `ShopSearchTests` |
| `settings`, `notifications` | `ShopSettingsTests` |
| `offline`, `background` | `ShopAppLifecycleTests` |
| `rotation`, `orientation` | `ShopOrientationTests` |

**How to decide:** use the most specific non-technique tag (ignore `EP`, `BVA`, `ST`, `EG`, platform
tags). When a test has both a specific and a broader feature tag, group by the broader one. When the
file already exists, add the new method to it. Aim for 10–30 tests per file; split by sub-feature past
~50.

{{> lang:test-class}}
### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case produces exactly ONE test method. Do NOT combine several test cases into one
parameterized method, and do NOT stack several manual-test-case markers on one method. Two test cases
that look like "the same test with different data" are still two methods.

---

## Code Rules (MUST FOLLOW)

{{> lang:code-rules}}
---

## Step 5: Verify Build

After generating/editing all files, build the test project and fix every BUILD error until it succeeds.

{{> lang:build}}
---

## Step 6: Hand Off to User — Do NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated tests — the device, emulator, and Appium are
the user's, and the user runs tests and returns with feedback.

Post a concise summary that includes:
- Files created/edited
- TC-IDs covered (if applicable)
- The command the user can run to execute the new test(s) (below)
- Assumptions (locator choices, lifecycle picked, platform, custom components added)

{{> lang:run-command}}
Then wait. When the user returns with specific failures, make targeted fixes based on their guidance:
- **"Locator failed for Y"** → new CLI session, fresh snapshot, re-validate, fix the Map file
- **"Assertion expected X got Y"** → fix the assertion text
- **"Timing issue on Z"** → add a fluent wait on that component

After each fix iteration, rebuild, then hand back to the user again. Do NOT speculatively rerun tests.

**Exception:** if the user has explicitly said "run them and fix until green" in this conversation,
follow that instruction for as long as it applies.

---

## Output Format

{{> lang:output}}
