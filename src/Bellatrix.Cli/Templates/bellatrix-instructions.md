# BELLATRIX Automation (model-free CLI)

> Scaffolded by `bellatrix init`. This is the single BELLATRIX instruction fragment
> for this repo, loaded by your AI agent from its own instructions location. Edit your
> own automation notes elsewhere — re-running `bellatrix init` may refresh this file
> (it skips files you have edited; use `--regenerate` to force a refresh).

Drive UI automation with the **`bellatrix <domain> <verb>`** CLI. It runs no model and
holds no credentials — all inference is yours. The driver, browser server, or device
is the user's to run; never start, stop, or probe it (if a command returns a
connection error, ask the user to start it). Output is always JSON — the success JSON
is the contract. Sessions persist across invocations by id (no daemon); always
`close-session` when done.

## MANDATORY: delegate UI-state reads (Context Economy)

Never read a full UI tree (`snapshot --inline`, `get-page-source`, or a snapshot file)
into this context. Capture a snapshot — which returns only a compact reference
`{ path, elementCount, summary }` — then resolve each locator in one of two ways:

1. **Delegate (preferred, when your agent supports subagents):** hand the description
   and the snapshot `path` to the domain's isolated **locator-specialist** subagent
   (`bellatrix-<domain>-locator-specialist`); only the returned locator comes back.
2. **Otherwise — and whenever delegation is unavailable — query narrowly:**
   `bellatrix <domain> find --session <s> --snapshot <path> … --json`, which returns a
   handful of matching nodes. Never open the snapshot file yourself.

<!-- bellatrix:domain=desktop -->
## Desktop domain (`bellatrix desktop <verb>`)

Drives Windows apps over WinAppDriver/NovaWindows (default driver
`http://127.0.0.1:4723`). Locator specialist: **`bellatrix-desktop-locator-specialist`**.
Narrow query: `bellatrix desktop find --session <s> --snapshot <path> --name "Login" --json`
(also `--automation-id`, `--control-type`, `--grep <regex>`, `--interactive-only`).

### Canonical flow

1. `bellatrix desktop list-apps --json`
2. `bellatrix desktop start-session --app <name> --session <s> --json` → real sessionId
3. (splash?) `bellatrix desktop reattach-session --session <s> --window-title "<main>" --json`
4. `bellatrix desktop snapshot --session <s> --json` → `{ path, elementCount, summary }`
5. resolve ONE XPath via the locator specialist (or `find` above)
6. interact: `click` / `type-text` / `clear` / `get-text` / `get-attribute` / `is-visible` / `is-enabled`
   (all take `--session <s> --locator "<xpath>"`, legacy `--xpath` also works; `type-text` also `--text "…"`;
   `get-attribute` also `--attribute <name>`)
7. re-snapshot after navigation; then `bellatrix desktop close-session --session <s> --json`

### Verbs

`start-session`, `list-apps`, `attach-window`, `reattach-session`, `close-session`,
`list-sessions`, `get-title`, `maximize`, `get-page-source`, `snapshot`, `find`,
`take-screenshot`, `click`, `type-text`, `clear`, `get-text`, `get-attribute`,
`is-visible`, `is-enabled`.
(`get-view-snapshot`→`snapshot` and `query-snapshot`→`find` remain as aliases.)
`take-screenshot` saves a PNG under `.bellatrix/screenshots/` → `{ path, width, height }` (`--out <file>` picks the file).
Every verb's flags and output: `bellatrix desktop help --json`.

### Locator hierarchy (desktop PascalCase UIA)

1. `//*[@AutomationId='loginBtn']` (most stable)
2. `//Button[@Name='Login']`
3. `//<ControlType>[@Name='…']` using the `ControlType` snapshot key
4. `//<Tag>[@ClassName='…']` (last resort)

Never use dynamic AutomationIds or generated ClassNames.

### Skills & agents

- Skills `bellatrix-desktop-*` — components, locators & waits,
  page objects, test structure & lifecycle, validations & assertions, app services,
  recipes, project structure, code generation, execute-scenario, and the SPECTRA
  bridges (automate-spectra-test, execute-spectra-test).
- Agent `bellatrix-desktop-system-tests-generator` — end-to-end desktop test
  generation with SPECTRA TC-ID traceability.
- Subagent `bellatrix-desktop-locator-specialist` — isolated single-XPath resolver.
<!-- /bellatrix:domain -->

<!-- bellatrix:domain=web -->
## Web domain (`bellatrix web <verb>`)

Drives browsers over Selenium. Locator specialist: **`bellatrix-web-locator-specialist`**.
Narrow query: `bellatrix web find --session <s> --snapshot <path> --data-testid "…" --json`
(also `--id`, `--role`, `--name`, `--text`, `--grep`, `--interactive-only`).

Flow: `start-session --browser <chrome|firefox|edge>` → `navigate --url` →
`snapshot` → resolve a selector → interact with `click`/`type-text`/`clear`/`select`/
`get-text`/`get-attribute`/`is-visible`/`is-enabled` (`--locator <s> [--strategy css|xpath|id|…]`;
XPath is auto-detected when it starts with `/`; legacy `--selector` also works)
→ `close-session`. Locator ladder: `data-testid` → `#id` → `aria-label` → role+text
(xpath) → css → positional xpath. Web-only verbs: `navigate`/`back`/`forward`/`refresh`,
`select`, `wait-for-element`/`wait-for-page-ready`, `execute-js`. Every session re-attaches
across invocations: without `--remote-url` the CLI starts a detached local browser driver and
`close-session` stops it; with `--remote-url` the Selenium server is yours to run.
Also: `list-sessions`, `get-title`, `get-url`, `get-page-source` (context-heavy; prefer `snapshot`), `take-screenshot`
(saves a PNG under `.bellatrix/screenshots/` → `{ path, width, height }`; `--out <file>` picks the file).
Every verb's flags and output: `bellatrix web help --json`.

### Skills & agents

- Skills `bellatrix-web-*` — components, locators & waits, page
  objects, test structure & lifecycle, validations & assertions, app services, recipes,
  project structure, code generation, execute-scenario, and the SPECTRA bridges
  (automate-spectra-test, execute-spectra-test).
- Agent `bellatrix-web-system-tests-generator` — end-to-end web test generation with
  SPECTRA TC-ID traceability.
- Subagent `bellatrix-web-locator-specialist` — isolated single-selector resolver.
<!-- /bellatrix:domain -->

<!-- bellatrix:domain=mobile -->
## Mobile domain (`bellatrix mobile <verb>`)

Drives Android/iOS over Appium. Locator specialist: **`bellatrix-mobile-locator-specialist`**.
Narrow query: `bellatrix mobile find --session <s> --snapshot <path> --resource-id "…" --json`
(also `--content-desc`, `--class`, `--text`, `--grep`, `--interactive-only`).

Flow: `start-session --platform <android|ios> --app <path>` → `snapshot` → resolve a
locator → interact with `click`/`type-text`/`clear`/`get-text`/`get-attribute`/
`is-visible`/`is-enabled` (`--locator <v> [--strategy <t>]`; default `accessibilityid`, or `xpath`
when the value starts with `/`; legacy `--locator-type`/`--locator-value` also work) → `close-session`.
Locator strategies: `accessibilityid` (most stable) → `id` → `androiduiautomator` /
`iospredicate` / `iosclasschain` → `classname`. Mobile-only verbs: `swipe`,
`scroll-to-element`, `background-app`, `launch-app`/`close-app`/`install-app`/
`remove-app`/`is-app-installed`, `get-context`/`set-context`/`get-contexts`, `get-orientation`/`set-orientation`.
Also: `list-sessions`, `get-page-source` (context-heavy; prefer `snapshot`), `take-screenshot` (saves a PNG under
`.bellatrix/screenshots/` → `{ path, width, height }`; `--out <file>` picks the file); `tap` = `click`.
Every verb's flags and output: `bellatrix mobile help --json`.

### Skills & agents

- Skills `bellatrix-mobile-*` — components, locators & waits,
  page objects, test structure & lifecycle, validations & assertions, app services,
  recipes, project structure, code generation, execute-scenario, and the SPECTRA
  bridges (automate-spectra-test, execute-spectra-test).
- Agent `bellatrix-mobile-system-tests-generator` — end-to-end Android/iOS test generation
  with SPECTRA TC-ID traceability.
- Subagent `bellatrix-mobile-locator-specialist` — isolated single-locator resolver.
<!-- /bellatrix:domain -->
