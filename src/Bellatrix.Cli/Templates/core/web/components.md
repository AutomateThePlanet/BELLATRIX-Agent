---
name: bellatrix-web-components
description: Reference skill for BELLATRIX web component types, the HTML element to component mapping, component methods and properties, and creation patterns.
---

# BELLATRIX Web Components Reference

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.web.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.web.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no web project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

A BELLATRIX web component is a typed wrapper around one HTML element. The component type decides which actions and validations are available, so pick it from the element's tag, `type`, and ARIA role as recorded during scenario execution — never guess it.

{{> lang:catalog}}
## Driving the Same Controls Live with the CLI

During scenario execution (`bellatrix-web-execute-scenario`) the same controls are exercised with `bellatrix web` verbs. Map each intent to the verb that matches the control:

| Intent | CLI verb | Notes |
|--------|----------|-------|
| Click a button / link / tab / checkbox / radio | `click --locator <s>` | Toggles a checkbox |
| Enter text | `type-text --locator <s> --text "…"` | Clears the field first, then types |
| Clear a field | `clear --locator <s>` | |
| Choose a `<select>` option | `select --locator <s> --value "<visible text>"` | `--value` is the option's visible text |
| Read text | `get-text --locator <s>` | The element's visible text |
| Read an attribute / property | `get-attribute --locator <s> --attribute <name>` | e.g. `value`, `checked`, `href`, `disabled`, `aria-expanded` |
| Visibility / enabled state | `is-visible` / `is-enabled` | |
| Wait until present | `wait-for-element --locator <s> [--timeout-ms N]` | Check `found` in the response |

All element verbs accept `--strategy css|xpath|id|class|name|tag|linktext|partiallinktext` (CSS by default; XPath auto-detected when the locator starts with `/` or `(/`).

### Reading the Element Type from a Snapshot

Each snapshot node carries `Tag` (lowercase), `Type`, and `Role` — use them to choose the component:

| Snapshot | Means |
|----------|-------|
| `Tag: input`, `Type: password` | Password field |
| `Tag: input`, `Type: checkbox` | Checkbox |
| `Tag: div`, `Role: button` | Clickable non-button element — type it as a clickable component |
| `Tag: a`, `Href: …` | Link |
| `Tag: select` | Native dropdown — the CLI `select` verb works; a custom dropdown needs click + re-snapshot + click |

`--interactive-only` keeps tags `button`, `input`, `select`, `a`, `textarea`, `option` and roles `button`, `link`, `checkbox`, `radio`, `textbox`, `combobox`, `menuitem`, `tab`, `switch`.

Always resolve a locator from a fresh snapshot, interact, and re-snapshot after the DOM changes. Prefer `data-testid`; fall back per the ladder in `bellatrix-web-locators-and-waits`.

{{> lang:creating}}
