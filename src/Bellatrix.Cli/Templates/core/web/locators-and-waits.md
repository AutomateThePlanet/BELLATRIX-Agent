---
name: bellatrix-web-locators-and-waits
description: Reference skill for BELLATRIX web find strategies, the CSS/XPath locator ladder, HTML XPath rules, CLI selector strategies, and wait strategies.
---

# BELLATRIX Web Locators and Waits Reference

Locators are validated live with the `bellatrix web` CLI first, then written into page objects. A locator that has not resolved against a live snapshot and been exercised with a CLI verb does not go into code.

---

## Reading UI State (Context Economy — MANDATORY)

Never read the full DOM into context (`snapshot --inline`, `get-page-source`, or the snapshot file). Capture a snapshot — it returns only `{ path, elementCount, summary }` — then delegate to the **`bellatrix-web-locator-specialist`** subagent (pass a description + the `path`; it returns one `{ strategy, selector }`) or query narrowly:

```
bellatrix web snapshot --session s --json
bellatrix web find --session s --snapshot <path> --data-testid "login" --json
bellatrix web find --session s --snapshot <path> --id "email" --json
bellatrix web find --session s --snapshot <path> --name "username" --json
bellatrix web find --session s --snapshot <path> --role "button" --json
bellatrix web find --session s --snapshot <path> --text "Sign in" --json
bellatrix web find --session s --snapshot <path> --grep "submit|continue" --json
bellatrix web find --session s --snapshot <path> --interactive-only --json
```

Snapshot keys: `Tag`, `Id`, `DataTestid`, `Name`, `Role`, `AriaLabel`, `Text`, `Type`, `Placeholder`, `Href`, `Class`, `Value`. `find` uses contains-matching (`--role` is exact), so a hit is a candidate — confirm uniqueness by exercising the selector live.

## CLI Selector Strategies (`--strategy`)

| `--strategy` | Selenium `By` | Example `--locator` |
|--------------|---------------|---------------------|
| `css` (default) | CSS selector | `[data-testid='login']`, `#email`, `form button[type='submit']` |
| `xpath` | XPath | `//button[normalize-space()='Log In']` |
| `id` | id attribute | `email` |
| `name` | name attribute | `username` |
| `class` | single class name | `btn-primary` |
| `tag` | tag name | `h1` |
| `linktext` | exact link text | `Sign in` |
| `partiallinktext` | partial link text | `Sign` |

A locator starting with `/` or `(/` is auto-detected as XPath; anything else defaults to CSS unless `--strategy` says otherwise.

{{> lang:find}}
## Locator Hierarchy (stable → fragile)

| Priority | Locator | Why |
|----------|---------|-----|
| 1 | `data-testid` (or `data-test`, `data-qa`) | Added for testing; survives restyling and copy changes |
| 2 | Static `id` | Unique by contract — but only if it is NOT generated |
| 3 | `name` attribute | Stable for form fields (posted to the server) |
| 4 | `aria-label` / accessible role + name | Stable while accessibility is maintained |
| 5 | Label text → input, placeholder | Ties the field to what the user sees |
| 6 | Tag + visible text (`normalize-space()`) | Fine for buttons/links; breaks on copy changes and i18n |
| 7 | Stable structural CSS (scoped under a stable anchor) | Acceptable when nothing above exists |
| 8 | Positional XPath | LAST resort; document why |

### How to Recognize Unstable Attributes

| Unstable | Example | Why |
|----------|---------|-----|
| Generated / hashed classes | `css-1a2b3c4d`, `sc-AxjAm`, `jss42`, `MuiButton-root-123` | Rebuilt on every deploy |
| Dynamic ids | `quantity_4e732ced`, `react-select-7-input`, `:r3:`, `mat-input-12`, `ember1234` | Counter/GUID per render |
| Framework internals | `_ngcontent-c12`, `ng-star-inserted`, `data-v-7ba5bd90` | Compiler artifacts |
| Absolute paths | `/html/body/div[2]/div[3]/form/input[1]` | Any layout change breaks it |
| Style-only classes | `mt-4`, `text-red-500`, `col-md-6` | Change with the design |

If the only identity is unstable, use a relationship to a stable anchor instead (label → input, row text → button in the same row), and suggest to the user that a `data-testid` be added.

---

## HTML XPath Rules

| Rule | CORRECT | WRONG |
|------|---------|-------|
| Lowercase tags and attributes | `//input[@name='email']` | `//Input[@Name='email']` (desktop UIA style) |
| Normalize visible text | `//button[normalize-space()='Log In']` | `//button[text()=' Log In ']` |
| Single quotes for values | `//a[@href='/cart']` | `//a[@href="/cart"]` inside a quoted string |
| Class tokens via `contains` only for semantic tokens | `//div[contains(@class,'alert-danger')]` | `//div[@class='alert alert-danger fade show']` |
| Relative to a stable anchor | `//tr[td[normalize-space()='Order 42']]//button[normalize-space()='View']` | `(//button)[17]` |

### Useful HTML XPath Patterns

| Need | XPath |
|------|-------|
| Input by its label | `//label[normalize-space()='Email']/following-sibling::input` |
| Input by `for` link | `//input[@id=//label[normalize-space()='Email']/@for]` |
| Button by text | `//button[normalize-space()='Save changes']` |
| Link by text | `//a[normalize-space()='Forgot password?']` |
| Element in a table row | `//tr[td[normalize-space()='Product A']]//a[normalize-space()='Edit']` |
| Element inside a named region | `//section[@aria-label='Shopping cart']//button[normalize-space()='Checkout']` |
| Validation message after a field | `//input[@name='email']/following-sibling::*[contains(@class,'error')]` |
| Nth match (last resort) | `(//div[@data-testid='product-card'])[3]` |

---

## Anti-Patterns (NEVER Use)

| Anti-Pattern | Example | Why |
|--------------|---------|-----|
| Absolute XPath | `/html/body/div[1]/main/form/input[2]` | Breaks on any layout change |
| Bare positional index | `//div[5]`, `(//button)[12]` | Order changes silently |
| Generated classes / dynamic ids | `.css-1q2w3e`, `#input-4e732ced` | Change per build/render |
| XPath where an id/css factory suffices | `//*[@id='email']` | Slower, noisier — use the id strategy |
| Text-only locator for non-unique text | `//*[text()='Edit']` | Matches every row |
| Raw `find`-guessed selectors never exercised live | — | Only validated locators go into code |

---

## CLI Waits (during scenario execution)

| Need | Command |
|------|---------|
| Element appears | `bellatrix web wait-for-element --session s --locator "<sel>" [--strategy css] [--timeout-ms 30000] --json` (check `found` in the response) |
| Page finished loading | `bellatrix web wait-for-page-ready --session s --json` (`document.readyState === 'complete'`) |
| AJAX re-render settled | `wait-for-element` on the element the re-render produces, then re-`snapshot` |

Every wait you needed during execution is a wait the generated code needs too — record it.

{{> lang:decision-and-waits}}
