---
name: bellatrix-web-generate-code
description: Skill for generating or editing BELLATRIX web test code (page objects, sections, models, tests) using locators validated during live scenario execution.
---

# Generate Web Test Code

Generate production-ready BELLATRIX web test code using the locators validated during scenario execution (`bellatrix-web-execute-scenario`). This skill handles both **creating new files** and **editing/expanding existing ones**.

**Input:** the execution output — validated `{ strategy, selector }` pairs, element → component mapping, page transitions, observed texts, and the waits/obstacles you hit. **Never invent a locator.** Every locator in a Map file must trace to one that resolved against a live snapshot and was exercised with a `bellatrix web` verb. If a needed locator is missing, run a new CLI session for it — do not guess.

{{> lang:target}}
---

## CRITICAL: Read Reference Documentation First

**BEFORE generating any code**, load these skills:

| Skill | Content |
|-------|---------|
| `bellatrix-web-project-structure` | **As-is tree of the test project — ground every CREATE vs EDIT decision here** |
| `bellatrix-web-components` | Component types, methods, factory methods |
| `bellatrix-web-locators-and-waits` | Find strategies, locator ladder, waits |
| `bellatrix-web-validations-and-assertions` | Validate* methods and which component supports which |
| `bellatrix-web-test-structure-and-lifecycle` | Test classes, Browser attribute, lifecycle, hooks |
| `bellatrix-web-page-objects-and-patterns` | Page object structure, sections, models |
| `bellatrix-web-app-services` | App services (Components, Navigation, Browser, Dialogs, …) |
| `bellatrix-web-system-tests-recipes` | Complete test examples |

---

## Step 1: Scan Existing Project Structure

Before generating code, **always check what already exists** — start with `bellatrix-web-project-structure`, then confirm on disk:

```
Existing page objects:   Pages/*/
Existing sections:       Sections/*/
Existing tests:          Tests/**/*
Existing models:         Models/*
Existing components:     Components/*
```

### Decision Matrix

| Situation | Action |
|-----------|--------|
{{> lang:page-matrix}}
| A new modal / drawer / wizard step appears | **CREATE** its own page object (or a section when shared) — don't stuff it into an unrelated page |
| Same block + workflow already on ≥2 pages | **CREATE** a section and embed it by composition |
| Test class exists for this feature area | **EDIT** — add new test methods to the existing class |
| Test class does NOT exist | **CREATE** — new test file |
| Model exists with matching properties | **REUSE** as-is |
| Model exists but needs more properties | **EDIT** — add the properties |
| Model does NOT exist | **CREATE** — new model file |

### IMPORTANT: When Editing Existing Files

- **Read the entire file first** before making changes
- **Preserve all existing code** — never remove or rewrite existing members
- **Follow the same patterns** — naming, formatting, locator style, region comments
- **Append** new members after existing ones
{{> lang:edit-naming}}
- If an existing locator conflicts with one you just validated live, keep both visible to the user: update it only when the live run proved the old one broken, and say so in the hand-off summary

After CREATING a page folder, section, component, model, or tests sub-folder, update `bellatrix-web-project-structure` in the same change.

---

## Step 2: Generate Page Objects (3-File Pattern)

{{> lang:page-layout}}

Name pages after what the user sees: `LoginPage`, `CartPage`, `CheckoutPage`, `AddressModal`.

{{> lang:page-objects}}
---

## Step 2b: Custom Components (When Built-ins Don't Fit)

If a built-in component is missing a method you need, or misbehaves for a specific widget (custom dropdown, date picker, toast, rich-text editor), add a custom component under `Components/` instead of working around it in page objects or tests. Never modify BELLATRIX source; keep components control-level (business workflows stay in Actions); one file per component.

{{> lang:custom-components}}
---

## Step 2c: Reusable Sections (Header, Navigation, Footer, Cookie Banner)

When the same locator block AND workflow appear on two or more pages, extract a section under `Sections/{SectionName}/` with the same 3-part split, and embed it in each page by composition. Keep the public action names on the pages unchanged.

{{> lang:sections}}
---

## Step 3: Generate Model Classes (If Needed)

Place data models in `Models/` for structured form data (billing info, credentials, addresses) and enums for fixed option sets (sort orders, payment methods). Models are pure data — no component types.

{{> lang:models}}
---

## Step 4: Generate Test Classes

### Test File Organization (MANDATORY)

Do NOT put all tests into a single file. Organize test classes by **logical feature area** using the test case's tags/component. Each distinct feature area gets its own test class file in its own folder.

**Naming convention:** `Tests/{FeatureArea}/{FeatureArea}Tests.*`

| Tags on the test case | Test class file |
|-----------------------|-----------------|
| `login`, `authentication` | `Tests/Login/LoginTests` |
| `registration`, `sign-up` | `Tests/Registration/RegistrationTests` |
| `cart`, `coupon` | `Tests/Cart/CartTests` |
| `checkout`, `billing`, `payment` | `Tests/Checkout/CheckoutTests` |
| `search`, `filter`, `sorting` | `Tests/Catalog/CatalogTests` |
| `account`, `profile`, `addresses` | `Tests/Account/AccountTests` |
| `navigation`, `header`, `footer` | `Tests/Navigation/NavigationTests` |

**How to decide:** use the most specific *feature* tag (ignore technique tags such as `EP`, `BVA`, `negative-test`, `smoke`). When a test has both a specific tag (`coupon`) and a broader one (`cart`), group by the broader one. When the file already exists, add the new test method to it. Aim for roughly 10–30 tests per file; split by sub-feature beyond ~50.

{{> lang:tests}}
### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into one parameterized method, and do NOT stack multiple TC-ID traceability markers on one method.

### Tests Use Page Objects Only

Test methods call page-object and section methods only — no direct component creation in tests. Every test starts from a known state (open the page, or reset state in setup) so it can run alone and in any order.

---

{{> lang:code-rules}}
---

## Step 5: Verify Build

After generating/editing all files, build the test project and fix BUILD errors until it succeeds.

{{> lang:build}}
---

## Step 6: Hand Off to User — Do NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated tests. The user runs tests and returns with feedback.

Post a concise summary that includes:
- Files created/edited
- TC-IDs covered (if applicable)
- The command the user can run to execute the new test(s) (see Step 5)
- Any assumptions you made (locator choices, lifecycle picked, waits added, custom components or sections added)

Then wait. When the user returns with specific failures, make targeted fixes:
- **"Locator failed for Y"** → new CLI session, fresh snapshot, re-resolve via the locator specialist, fix the Map file
- **"Assertion expected X got Y"** → re-observe the text live if needed, fix the assertion
- **"Timing issue on Z"** → add the appropriate wait

After each fix iteration, rebuild, then hand back. Do NOT speculatively rerun tests on your own.

**Exception:** if the user has explicitly said "run them and fix until green" in this conversation, follow that instruction for as long as it applies.

---

{{> lang:output}}
