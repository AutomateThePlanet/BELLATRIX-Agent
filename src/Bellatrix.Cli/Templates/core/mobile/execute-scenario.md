---
name: bellatrix-mobile-execute-scenario
description: Skill for executing mobile test scenarios using the bellatrix mobile CLI to interact with live Android/iOS apps via Appium, validating every locator before code generation.
---

# Execute Mobile Test Scenario

Use the **`bellatrix mobile <verb>`** CLI to drive a live Android or iOS app (emulator, simulator, or
real device) over Appium, capture UI snapshots, and validate every test step and locator **before**
any code is generated. The CLI runs no model and holds no credentials — all locator intelligence is
yours, delegated to the isolated locator specialist. Every command prints JSON; the success JSON is
the contract. Always pass `--json`.

> **MANDATORY — context economy:** `snapshot` returns only a compact reference to a snapshot on disk.
> NEVER load a full UI tree into this conversation (`snapshot --inline`, `get-page-source`, or reading
> the snapshot file). Delegate locator resolution to the isolated **`bellatrix-mobile-locator-specialist`**
> (pass the `path`), and use `find` for narrow lookups.

---

## Phase 1: Session Setup

### Appium, the Device, and the App Are the User's — NOT Yours

The CLI talks to an Appium server (default `http://127.0.0.1:4723`) that the **user** runs, attached to
an emulator / simulator / device the **user** has booted. **You do NOT start, stop, or probe Appium,
`adb`, `xcrun simctl`, or any emulator yourself.**

**FORBIDDEN actions:**
- Running `appium`, `adb devices`, `adb start-server`, `emulator -avd …`, `xcrun simctl boot …`
- Checking processes (`Get-Process`, `ps`, `tasklist`) to see whether Appium is up
- "Warming up" or health-checking the server before calling `bellatrix mobile start-session`

**Correct behavior:** call `bellatrix mobile start-session` directly. If it returns a connection error
(e.g. it cannot reach `127.0.0.1:4723`) or a device/capability error, **stop and ask the user** to start
Appium / boot the device. Example message:

> "The CLI cannot reach Appium at 127.0.0.1:4723. Please start Appium and boot the Android emulator
> (or iOS simulator), then tell me and I'll retry."

Retry `start-session` once the user confirms.

### Start a New Session

Pick a short session name of your own for this run (e.g. `shop`) and start the app:

```
bellatrix mobile start-session --platform android --app "<path to .apk>" --session shop --json
bellatrix mobile start-session --platform ios --app "<path to .app or .ipa>" --session shop --json
```

| Flag | Required | Meaning |
|------|----------|---------|
| `--platform android\|ios` | yes | Selects UiAutomator2 (Android) or XCUITest (iOS) |
| `--app <path>` | yes | Passed as the Appium `app` capability — normally the absolute path to the `.apk` / `.app` / `.ipa` |
| `--device-name <name>` | no | Defaults to `Android Emulator` / `iPhone Simulator` |
| `--appium-url <url>` | no | Defaults to `http://127.0.0.1:4723` |
| `--session <name>` | yes (by convention) | Your logical name for the session; omitted → `default` |

The response is `{ "sessionId": "<real Appium id>", "platform": "Android|iOS", "appPath": "…" }`.

The session is addressed by the `--session <name>` you chose, not by the real Appium `sessionId`.
Every later CLI invocation is a **separate process** — there is no daemon — so pass the same
`--session <name>` again; the CLI resolves it back to the live Appium session for you.

**Two platforms in one scenario.** Start one session per platform/app, each under its own name
(`--session droid`, `--session ios`), and route each call by name. There is no "switch" verb.

### Verify the App Is Up

```
bellatrix mobile get-context --session shop --json
bellatrix mobile take-screenshot --session shop --out start.png --json
```

`get-context` should report `NATIVE_APP`. If the app shows a WebView, list contexts with
`get-contexts` and switch with `set-context --context <WEBVIEW_…>` (switch back to `NATIVE_APP` before
native interactions).

---

## Phase 2: Capture UI Snapshots (MANDATORY)

**NEVER skip this step.** Every screen, dialog, sheet, or keyboard state you interact with MUST be
snapshot first.

```
bellatrix mobile snapshot --session shop --json
```

This returns only a **compact reference** — `{ "path": "<file>", "elementCount": N, "summary": {…} }`.
The node array is written under `.bellatrix/snapshots/`; it never enters this context.

### Analyze the Snapshot — via delegation, NEVER inline

**MANDATORY:** delegate each locator to the **`bellatrix-mobile-locator-specialist`** subagent, passing
`{ description, snapshotPath, platform }`. It reads the snapshot in its own context and returns ONE
locator as `{ "locatorType": "…", "locatorValue": "…" }`, which you pass to the CLI as
`--strategy <locatorType> --locator <locatorValue>`.

For your own narrow lookups use `find` against the saved snapshot (driver-free):

```
bellatrix mobile find --session shop --snapshot <path> --resource-id "login" --json
bellatrix mobile find --session shop --snapshot <path> --content-desc "Log in" --json
bellatrix mobile find --session shop --snapshot <path> --text "Remember me" --json
bellatrix mobile find --session shop --snapshot <path> --class "android.widget.EditText" --json
bellatrix mobile find --session shop --snapshot <path> --grep "^loginButton$" --json
bellatrix mobile find --session shop --snapshot <path> --interactive-only --json
```

`find` takes ONE filter (precedence: `--interactive-only` > `--grep` > `--text` > `--resource-id` >
`--content-desc` > `--class`); all matches are case-insensitive substrings except `--grep` (regex).

| Filter | Matches snapshot key(s) | Platform notes |
|--------|------------------------|----------------|
| `--resource-id` | `ResourceId` | Android only |
| `--content-desc` | `ContentDesc` | Android only |
| `--class` | `Class` | Android (`android.widget.*`); iOS nodes carry `Type` instead — use `--grep` |
| `--text` | `Text`, `ContentDesc`, `Label` | both |
| `--grep <regex>` | `ResourceId`, `ContentDesc`, `Text`, `Class`, `Name`, `Label`, `Value`, `Type`, `Tag` | both — the way to query iOS `Name` / `Type` |
| `--interactive-only` | `Class`/`Type`/`Tag` contains Button, EditText, TextField, CheckBox, Switch, Spinner, RadioButton, SeekBar, ImageButton, ToggleButton, SearchField, SecureTextField, Cell | both |

### Snapshot Keys

| Key | Android source | iOS source |
|-----|---------------|-----------|
| `ResourceId` | `resource-id` (`com.app:id/login`) | — |
| `ContentDesc` | `content-desc` (= Appium accessibility id) | — |
| `Text` | `text` | — |
| `Name` | — | `name` (= accessibility identifier / accessibility id) |
| `Label` | — | `label` (visible / spoken text) |
| `Value` | — | `value` (field contents, switch state) |
| `Class` | `class` (`android.widget.Button`) | — |
| `Type` | — | `type` (`XCUIElementTypeButton`) |
| `Tag` | element tag — **lower-cased** by the snapshot parser | same |

Nodes with no locator identity (no resource-id / name / text / content-desc / label) are dropped from
the snapshot. Build XPath element names from `Class` (Android) or `Type` (iOS) — never from `Tag`,
which is lower-cased and will not match.

### Locator Priority (the specialist follows this)

| Priority | Android (UiAutomator2) | iOS (XCUITest) |
|----------|------------------------|----------------|
| 1 (best) | `accessibilityid` ← `content-desc` | `accessibilityid` ← `name` |
| 2 | `id` ← full `resource-id` (`com.app:id/login`) | `iospredicate` ← `label == "Log in"` / `type == "…" AND name == "…"` |
| 3 | `androiduiautomator` ← `new UiSelector().text("Log in")` | `iosclasschain` ← `` **/XCUIElementTypeButton[`name == "login"`] `` |
| 4 (last) | `classname` / `xpath` | `classname` / `xpath` |

### Optional: Screenshot for Visual Context

```
bellatrix mobile take-screenshot --session shop --out step3.png --json
```

Always pass `--out`; without it the base64 image is returned inline and floods the context.

---

## Phase 3: Execute Test Steps

Every element verb takes `--locator <value>` and an optional `--strategy`:
`accessibilityid` | `id` | `xpath` | `classname` | `androiduiautomator` (Android only) |
`iospredicate` (iOS only) | `iosclasschain` (iOS only). Without `--strategy`, a value starting with `/`
or `(/` is treated as `xpath`, anything else as `accessibilityid`. (Legacy `--locator-type` /
`--locator-value` still work.)

### Tap / Click
```
bellatrix mobile click --session shop --strategy accessibilityid --locator "Log in" --json
bellatrix mobile tap --session shop --strategy id --locator "com.example.shop:id/login" --json
```

### Type Text
```
bellatrix mobile type-text --session shop --strategy id --locator "com.example.shop:id/username" --text "demo.user" --json
bellatrix mobile type-text --session shop --strategy iospredicate --locator "type == \"XCUIElementTypeSecureTextField\"" --text "Secret123!" --json
```

### Clear Fields
```
bellatrix mobile clear --session shop --strategy id --locator "com.example.shop:id/username" --json
```

### Read Values
```
bellatrix mobile get-text --session shop --strategy id --locator "com.example.shop:id/welcome" --json
bellatrix mobile get-attribute --session shop --strategy accessibilityid --locator "Remember me" --attribute checked --json
```
Useful attributes — Android: `checked`, `enabled`, `selected`, `text`, `content-desc`, `resource-id`;
iOS: `value`, `label`, `name`, `enabled`, `visible`.

### Check State
```
bellatrix mobile is-visible --session shop --strategy id --locator "com.example.shop:id/error" --json
bellatrix mobile is-enabled --session shop --strategy accessibilityid --locator "Log in" --json
```

### Gestures and Scrolling
```
bellatrix mobile swipe --session shop --start-x 900 --start-y 1400 --end-x 900 --end-y 400 --json
bellatrix mobile scroll-to-element --session shop --strategy accessibilityid --locator "Checkout" --json
```
`scroll-to-element` first locates the element, so it must already exist in the hierarchy. For an
Android list item that is not rendered yet, locate it with a scrolling UiAutomator selector instead:
`--strategy androiduiautomator --locator 'new UiScrollable(new UiSelector().scrollable(true)).scrollIntoView(new UiSelector().text("Checkout"))'`.

### App and Device Control
```
bellatrix mobile background-app --session shop --seconds 3 --json
bellatrix mobile close-app --session shop --app-id com.example.shop --json
bellatrix mobile launch-app --session shop --app-id com.example.shop --json
bellatrix mobile is-app-installed --session shop --bundle-id com.example.shop --json
bellatrix mobile install-app --session shop --app-path "<path>" --json
bellatrix mobile remove-app --session shop --app-id com.example.shop --json
bellatrix mobile get-orientation --session shop --json
bellatrix mobile set-orientation --session shop --orientation landscape --json
bellatrix mobile get-contexts --session shop --json
bellatrix mobile set-context --session shop --context NATIVE_APP --json
```

### IMPORTANT: Re-snapshot After Every Transition

After any action that changes the screen (tap that navigates, dialog/sheet opens, keyboard appears,
list scrolls, orientation changes, app returns from background):

```
bellatrix mobile snapshot --session shop --json
```

Resolve the next locator from the NEW snapshot `path` — never from an old one.

---

## Phase 4: Close Session (MANDATORY)

**You MUST close the session before proceeding to code generation.** The execution phase ends here —
no `bellatrix mobile` commands are called after this point.

```
bellatrix mobile close-session --session shop --json
```

Verify it is gone:

```
bellatrix mobile list-sessions --json
```

The closed session's id should no longer appear (`list-sessions` also prunes sessions idle for more than
10 minutes). If it still appears, call `close-session` again.

**DO NOT proceed to code generation until the session is closed.** Close it even when a step fails or
the run is interrupted.

---

## Phase 5: Record Findings

After closing the session, compile:

1. **Validated locators** — only `{strategy, value}` pairs that worked against the live app, per platform
2. **Element-to-component mapping** — which native class (`Class` / `Type`) maps to which BELLATRIX
   component (see the mapping below)
3. **Screen transitions** — which actions navigate between screens, dialogs, sheets
4. **Test data used** — inputs, expected outputs, and the ACTUAL texts observed (error messages, toasts,
   labels) — never assume they match the manual test case wording
5. **Assertions needed** — what the generated test must validate

---

## Mobile Locator Rules

### Use the platform's native strategies first
- Android: `accessibilityid` (content-desc) and the **fully-qualified** `resource-id`
  (`com.example.shop:id/login`, not `login`).
- iOS: `accessibilityid` (`name`) and NSPredicate (`iospredicate`).
- Values are exact and case-sensitive. Copy them from the snapshot, never retype from memory.

### XPath (last resort)
- Android element names are fully-qualified classes: `//android.widget.Button[@content-desc='Log in']`.
- iOS element names are XCUI types: `//XCUIElementTypeButton[@name='Log in']`.
- Attribute names are the raw platform names (`@resource-id`, `@content-desc`, `@text`, `@name`,
  `@label`, `@value`), single-quoted values.

### NEVER Use
- Index-based or absolute paths: `//android.widget.Button[3]`, `/hierarchy/android.widget.FrameLayout/…`
- Dynamic ids (`com.app:id/row_8f3a…`), generated names, coordinates baked into steps
- Text that changes with data, locale, or time as the ONLY identity of an element

---

{{> lang:component-mapping}}
## Output

After completing scenario execution, provide:

```
## Execution Log
- Started session: <name> (sessionId <id>, platform: <Android|iOS>, app: <path>)
- Captured snapshot: <ScreenName> (<N> elements; locators via specialist)
- Executed: <action description>
- Captured snapshot: <NextScreen> (<N> elements)
- ...
- Closed session

## Validated Locators
| Element | Platform | Strategy | Value | Native class | Component Type | Purpose |
|---------|----------|----------|-------|--------------|----------------|---------|
| ... | ... | ... | ... | ... | ... | ... |

## Screens Visited
1. <ScreenName> — <description>
2. ...

## Test Data
- Input: ...
- Expected: ...
- Observed texts: ...
```

This output feeds directly into the **Generate Mobile Test Code** skill (`bellatrix-mobile-generate-code`).
