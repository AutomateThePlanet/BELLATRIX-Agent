---
title: BELLATRIX Desktop - Execute Test Scenario
description: Skill for executing desktop test scenarios using the bellatrix desktop CLI to interact with live Windows applications via WinAppDriver.
tags: [BELLATRIX, desktop, CLI, scenario-execution, WinAppDriver]
---

# Execute Desktop Test Scenario

Use the bellatrix desktop CLI (`bellatrix desktop` CLI) to interact with a live Windows application, capture UI snapshots, and validate test steps before generating code. The CLI runs no model and holds no credentials — all locator intelligence is yours, delegated to the isolated locator specialist.

> **MANDATORY — context economy:** `snapshot` returns only a compact reference to a snapshot on disk. NEVER load a full UI tree into this conversation. Delegate locator resolution to the isolated **locator specialist** (pass the `path`), and use `find` for narrow lookups.

---

## Phase 1: Session Setup

### Driver Backend — the User's Job, NOT Yours

The user runs the driver (WinAppDriver, or Appium with NovaWindows) at `http://127.0.0.1:4723` (or the `McpSettings__ServerUrl` environment variable); `McpSettings__DriverType` tells the CLI which one it is. The `bellatrix desktop` CLI only connects to it — it never starts or stops a driver, and **neither do you**: do NOT start, stop, or probe any driver process yourself.

**FORBIDDEN actions:**
- Running `Get-Process -Name "WinAppDriver"` / `Get-Process -Name "Appium"` to check state
- Running `Start-Process "WinAppDriver.exe"` or `appium` from a terminal
- Launching any driver executable via `run_in_terminal`, `execute/runInTerminal`, or any shell tool
- "Warming up" the driver before calling `bellatrix desktop start-session`

**Correct behavior:** Call `bellatrix desktop start-session` / `bellatrix desktop attach-window` directly. If the CLI returns the error code `driver_unreachable` ("Could not reach the desktop driver. Start your driver at http://127.0.0.1:4723 and try again."), the driver is not running — **ask the user to start it**. Do not try to start it yourself. Example message:

> "bellatrix cannot reach the desktop driver at 127.0.0.1:4723. Please start WinAppDriver (or Appium with NovaWindows, per `McpSettings__DriverType`) and let me know when it's up."

Then retry `bellatrix desktop start-session` once the user confirms.

### Start a New Session

**Prefer configured apps** (`McpSettings__Apps__<n>__Name` / `__Path` environment variables). Call `bellatrix desktop list-apps` first to see the configured names; then start by name, choosing a short `--session` name of your own for this run (e.g. `calc`):

```
bellatrix desktop list-apps --json
bellatrix desktop start-session --app calculator --session calc --json
```

If no name is given, the default-marked configured app is used:

```
bellatrix desktop start-session --session calc --json
```

For ad-hoc apps that aren't configured, pass `--app-path` directly:

```
bellatrix desktop start-session --app-path "<appPath>" --session calc --json
```

Or attach to a running window:
```
bellatrix desktop attach-window --window-title "<title>" --session calc --json
```

The session is addressed by the `--session <name>` you chose, not by the real driver `sessionId` the JSON response also reports. Every later CLI invocation is a **separate process** — no daemon — so pass that same `--session <name>` again; the CLI resolves it back to the live driver session for you. You never need to copy the real `sessionId` around yourself.

**Multiple sessions in parallel.** Each `--session` name is one app session. To work with several apps in the same scenario, call `bellatrix desktop start-session --app <name> --session <yourName>` once per app, each under its own `--session` name, and route later calls by that name. There is no "switch app" verb — switching is just routing the next call to the right `--session`.

If either call returns `driver_unreachable`, follow the rule above — ask the user to start the driver; never start it yourself.

### Handle Splash Screens / Loading Windows

Applications with splash screens or delayed loading may cause the session to attach to a temporary window (the splash). When the splash closes and the main window opens, subsequent calls will fail with **"UI no longer available"** or **"the parent window has closed"**.

**Recovery:**
```
bellatrix desktop reattach-session --session calc --window-title "<main window title>" --json
```

This re-derives the same `--session` against the new main window. Continue using the same `--session` name after this call.

**When to use:** After `bellatrix desktop start-session`, if `bellatrix desktop get-title`, `bellatrix desktop snapshot`, `bellatrix desktop take-screenshot`, or any other call returns a stale-window error, call `bellatrix desktop reattach-session` with the expected main window title and retry the failed operation.

### Verify the Window
```
bellatrix desktop get-title --session calc --json
bellatrix desktop maximize --session calc --json
```

---

## Phase 2: Capture UI Snapshots (MANDATORY)

**NEVER skip this step.** Every window or dialog you interact with MUST be snapshot first.

```
bellatrix desktop snapshot --session calc --json
```

This returns only a **compact reference** — `{ "path": "<path>", "elementCount": N, "summary": {...} }` — never the full element tree in this context.

### Analyze the Snapshot — via delegation, NEVER inline

**MANDATORY:** Delegate locator resolution to the isolated **`bellatrix-desktop-locator-specialist`** subagent, passing it the snapshot `path` (it reads the tree in its own context and returns a single XPath). For your own narrow lookups instead of a full delegated resolution, use `find` — never dump the whole tree into this conversation:

```
bellatrix desktop find --session calc --snapshot <path> --name "Login" --json
bellatrix desktop find --session calc --snapshot <path> --interactive-only --json
```
(also `--automation-id`, `--control-type`, `--grep <regex>`)

Locator priority (the specialist follows this):

| Priority | Attribute | Usage |
|----------|-----------|-------|
| 1 (best) | `AutomationId` | Most stable locator — always prefer |
| 2 | `Name` | Visible label text |
| 3 | `ControlType` + `Name` | Combine for disambiguation |
| 4 (last) | `ClassName` | Fallback only |

Also note:
- `IsEnabled` — element interactability
- `IsOffscreen` — element visibility
- `ControlType` — maps to BELLATRIX component type (see component mapping below)

### Optional: Screenshot for Visual Context
```
bellatrix desktop take-screenshot --session calc --out shot.png --json
```

---

## Phase 3: Execute Test Steps

Interact with the application using bellatrix desktop CLI commands, following the test scenario:

### Type Text
```
bellatrix desktop type-text --session calc --locator "//Edit[@AutomationId='txtInput']" --text "value" --json
```

### Click Elements
```
bellatrix desktop click --session calc --locator "//Button[@AutomationId='btnSubmit']" --json
```

### Clear Fields
```
bellatrix desktop clear --session calc --locator "//Edit[@AutomationId='txtInput']" --json
```

### Read Values
```
bellatrix desktop get-text --session calc --locator "//Text[@AutomationId='lblResult']" --json
bellatrix desktop get-attribute --session calc --locator "//Button[@AutomationId='btn']" --attribute IsEnabled --json
```

### Check State
```
bellatrix desktop is-visible --session calc --locator "//Text[@AutomationId='lblError']" --json
bellatrix desktop is-enabled --session calc --locator "//Button[@AutomationId='btnNext']" --json
```

### IMPORTANT: Re-snapshot After Navigation

After any action that changes the window (clicking a tab, opening a dialog, navigating to a new view):
```
bellatrix desktop snapshot --session calc --json
```

---

## Phase 4: Close Session (MANDATORY)

**You MUST close the session before proceeding to code generation.** The execution phase ends here — no bellatrix desktop CLI commands should be called after this point.

```
bellatrix desktop close-session --session calc --json
```

Verify the session is closed:
```
bellatrix desktop list-sessions --json
```
The closed session should no longer appear in the list. If it does, call `bellatrix desktop close-session` again.

**DO NOT proceed to code generation until the session is closed.**

---

## Phase 5: Record Findings

After closing the session, compile the results from your execution:

1. **Validated XPath locators** — only XPaths confirmed working via the bellatrix desktop CLI interaction
2. **Element-to-component mapping** — which ControlType maps to which BELLATRIX component
3. **Window transitions** — which actions navigate between windows/dialogs
4. **Test data used** — input values, expected outputs
5. **Assertions needed** — what should be validated in the generated test

---

## XPath Rules for Desktop (WinAppDriver)

### PascalCase is REQUIRED
WinAppDriver uses PascalCase for all element tags and attributes:

| CORRECT | WRONG |
|---------|-------|
| `//Button[@AutomationId='btn']` | `//button[@automationid='btn']` |
| `//Edit[@Name='Username']` | `//edit[@name='Username']` |
| `//CheckBox[@AutomationId='chk']` | `//checkbox[@AutomationId='chk']` |

### NEVER Use
- Position-based: `//Button[1]`
- Complex functions: `contains()`, `normalize-space()`
- Lowercase tags or attributes

---

{{> lang:component-mapping}}
## Output

After completing scenario execution, provide:

```
## Execution Log
- Started session: <name> (sessionId <id>, app: <appPath>)
- Captured snapshot: <windowName> (<N> elements; locators via specialist)
- Executed: <action description>
- Captured snapshot: <nextWindow> (<N> elements)
- ...
- Closed session

## Validated Locators
| Element | XPath | Component Type | Purpose |
|---------|-------|---------------|---------|
| ... | ... | ... | ... |

## Windows/Views Visited
1. <WindowName> — <description>
2. ...

## Test Data
- Input: ...
- Expected: ...
```

This output feeds directly into the **Generate Desktop Test Code** skill.
