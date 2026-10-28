# BELLATRIX — Live Verification Checklist

> Everything here needs a human: a real IDE or agent, browser, device or driver. Builds, unit tests and golden files
> are automated (see [CONTRIBUTING.md](../../CONTRIBUTING.md)). Tick a box only after you see the stated result. Record
> failures with the version (`bellatrix --version`) and date, and open an issue with the JSON output.
>
> Sections: **A. per AI agent**, **B. per domain** (automated by `scripts/run-e2e.ps1` where the drivers are
> reachable), **C. golden paths** (four end-to-end runs).

---

## A. Per AI agent (`--agent`)

**Common setup.** Use a scratch repo that contains a BELLATRIX test project, or an empty folder. Run
`bellatrix --version`; it must show ≥ 2.2.0 (≥ 3.1.0 for A.8–A.11). The delegation prompt has a snapshot available: start a Calculator
desktop session first (B.1 steps 1–4), or use any snapshot path the agent can produce itself.

**Delegation test prompt (same for every agent):**

> Start a desktop session for Calculator as `s1`, take a snapshot, and find the locator for the **Equals** button.
> Then close the session.

**Pass:** the agent either (a) hands the snapshot path to `bellatrix-desktop-locator-specialist` and gets back one
XPath, or (b) runs `bellatrix desktop find --session s1 --snapshot <path> --name "Equals" --json`.
**Fail:** the agent opens or reads the snapshot JSON file, or runs `get-page-source`, in its main context.

### A.1 Claude Code
- [ ] `bellatrix init --agent claude --domains desktop`, then confirm `CLAUDE.md` has one `@.bellatrix/bellatrix.instructions.md` line
- [ ] `/skills` (or ask "what bellatrix skills do you have?") lists the 12 `bellatrix-desktop-*` skills
- [ ] `/agents` lists `bellatrix-desktop-locator-specialist` and `bellatrix-desktop-system-tests-generator`
- [ ] Delegation test passes. Also try forcing it: `@agent-bellatrix-desktop-locator-specialist …`

### A.2 GitHub Copilot — VS Code
- [ ] `bellatrix init --agent copilot --domains desktop` (without Claude). Check `.github/instructions/bellatrix.instructions.md`, `.github/skills/`, `.github/agents/*.agent.md`
- [ ] Chat → *Configure Instructions* shows `bellatrix.instructions.md` as applied to all files
- [ ] Skills list shows the `bellatrix-desktop-*` skills **once each**
- [ ] The agent picker shows `bellatrix-desktop-system-tests-generator`. With it selected, the delegation test runs the locator specialist as a subagent (allowed by `agents:`)
- [ ] Repeat with `--agent claude,copilot` in a fresh folder. Copilot must still see every skill and agent **once** (it reuses `.claude/skills` and `.claude/agents`, and no `.github/agents` is written)

### A.3 GitHub Copilot — CLI and cloud agent
- [ ] Copilot CLI in the `--agent copilot` repo: `/agent` lists the BELLATRIX agents, and the delegation test passes
- [ ] Copilot CLI in the `--agent claude,copilot` repo: the agents still appear (from `.claude/agents`, documented in cli-plugin-reference)
- [ ] **Cloud agent (UNKNOWN in docs):** in a GitHub repo scaffolded with `--agent claude,copilot`, check whether the cloud agent can select `bellatrix-desktop-locator-specialist`. If not, record it. The remedy is scaffolding Copilot without Claude.

### A.4 Cursor
- [ ] `bellatrix init --agent cursor --domains desktop`. Settings → Rules shows `bellatrix` as **Always**
- [ ] Skills list shows the `bellatrix-desktop-*` skills once each
- [ ] Subagents list shows the two agents. The locator specialist is read-only (`readonly: true`)
- [ ] Delegation test passes, including when forced with `/bellatrix-desktop-locator-specialist`
- [ ] In a fresh folder, `--agent claude,cursor`: there is **no** `.cursor/skills`, skills come once from `.claude/skills`, and the `.cursor/agents` copies win over `.claude/agents` (no duplicate agent names)

### A.5 Devin Desktop (formerly Windsurf)
- [ ] `bellatrix init --agent devin --domains desktop` (also try `--agent windsurf`; stderr should print the rename notice)
- [ ] Rules panel shows `bellatrix` as always-on
- [ ] Skills list shows the `bellatrix-desktop-*` skills once each
- [ ] Subagents (experimental) list the two agents from `.devin/agents`. If subagents are disabled, the delegation test must still pass through `find` (path b)
- [ ] In a fresh folder, `--agent claude,devin`: no duplicate agents appear. This checks whether Devin also imports `.claude/agents`, which the docs leave UNKNOWN.

### A.6 Codex CLI
- [ ] `bellatrix init --agent codex --domains desktop`. `AGENTS.md` contains exactly one `<!-- >>> bellatrix >>> -->` block
- [ ] Ask "what instructions apply to UI automation here?". Codex must cite `.bellatrix/bellatrix.instructions.md` (it reads it via the pointer)
- [ ] Skills from `.agents/skills` are listed
- [ ] Custom agents from `.codex/agents/*.toml` load with no TOML errors. Ask Codex to "have bellatrix-desktop-locator-specialist find the Equals button". **Also check that the hyphenated agent `name` is accepted**; the docs only show underscores.
- [ ] Delegation test passes (subagent or `find`)

### A.7 Gemini CLI — enterprise (Gemini Code Assist)
- [ ] `bellatrix init --agent gemini --domains desktop`. Running `/memory show` includes the fragment text, which proves `@./.bellatrix/...` was imported
- [ ] `/skills` lists the `bellatrix-desktop-*` skills (from `.agents/skills`)
- [ ] `/agents` lists the two agents. The specialist's tools are limited to `read_file`, `grep_search`, `glob` and `run_shell_command`
- [ ] Delegation test passes with `@bellatrix-desktop-locator-specialist …`
- [ ] Note your account tier. Gemini CLI serves only Gemini Code Assist Standard/Enterprise licences and API keys since 2026-06-18 (unpaid and Google One users moved to Antigravity CLI, see A.8). Code Assist itself sells no new subscriptions since 2026-10-09 (renewals end 2027).

### A.8 Antigravity CLI (`agy`)
- [ ] `bellatrix init --agent antigravity --domains desktop` (also try `--agent agy`; no notice is printed). Check `.agents/rules/bellatrix.md` (`trigger: always_on`), `.agents/skills/`, `.agents/agents/<name>/agent.md`
- [ ] Ask "which rules apply in this workspace?". The `bellatrix` rule is listed as always on, and no `AGENTS.md`/`GEMINI.md` was created
- [ ] `/skills` (or the `/bellatrix-desktop-…` slash commands) lists the 12 `bellatrix-desktop-*` skills once each
- [ ] `/agents` lists `bellatrix-desktop-locator-specialist` (subagent only) and `bellatrix-desktop-system-tests-generator` (also selectable as the main agent). **Neither hangs on load**: an unknown tool name would hang it, and only documented names are used
- [ ] The locator specialist works with only `view_file`, `grep_search`, `run_command`
- [ ] **Undocumented, check it:** can the generator *create* new files with `replace_file_content` alone? If not, record the tool name Antigravity reports for file creation
- [ ] Delegation test passes. There is no `@name` syntax, so ask "use the bellatrix-desktop-locator-specialist subagent to …" and accept path (b) `find` as well
- [ ] Headless: `agy -p "<delegation prompt>"` returns the locator (used by `scripts/agent-conformance.ps1`)

### A.9 Kiro CLI (v3)
- [ ] `kiro-cli --version` is 3.x (or start with `--v3`). `bellatrix init --agent kiro --domains desktop`. Check `.kiro/steering/bellatrix.md` (`inclusion: always`), `.kiro/skills/`, `.kiro/agents/*.md`
- [ ] The default agent shows the `bellatrix` steering in its context and lists the `bellatrix-desktop-*` skills
- [ ] `/agent` (or `--agent bellatrix-desktop-system-tests-generator`) loads the generator **without** `/upgrade-agent` prompts; its context includes the steering file and the skill names (from `resources`)
- [ ] The locator specialist is limited to `read` and `shell`: asking it to edit a file is refused
- [ ] Delegation test passes. Ask in plain language: "use the bellatrix-desktop-locator-specialist agent to find the Equals button in <snapshot path>"
- [ ] Headless: `kiro-cli chat --no-interactive --trust-all-tools "<delegation prompt>"` (with `KIRO_API_KEY`) returns the locator
- [ ] On a Kiro CLI 2.x install, record what happens: Markdown agents are v3-only

### A.10 OpenCode
- [ ] `bellatrix init --agent opencode --domains desktop`. `AGENTS.md` contains exactly one `<!-- >>> bellatrix >>> -->` block; `.opencode/agents/*.md` exist
- [ ] Ask "what instructions apply to UI automation here?". OpenCode must cite `.bellatrix/bellatrix.instructions.md` (via the pointer; it does not follow `@` imports)
- [ ] Skills are listed once each (from `.opencode/skills`, or from `.claude/skills` / `.agents/skills` when those agents are scaffolded too)
- [ ] Tab/agent list: `bellatrix-desktop-system-tests-generator` (mode `all`) is selectable; `bellatrix-desktop-locator-specialist` (mode `subagent`) is not, and it cannot edit files
- [ ] Delegation test passes with `@bellatrix-desktop-locator-specialist …`
- [ ] In a fresh folder, `--agent codex,opencode`: still one `AGENTS.md` block
- [ ] Headless: `opencode run --auto "<delegation prompt>"` returns the locator

### A.11 All nine in one repo
- [ ] `bellatrix init --agent claude,copilot,cursor,devin,codex,gemini,antigravity,kiro,opencode --domains desktop --dry-run`. Read the `decisions`: expect `.claude/skills`, `.agents/skills` and `.kiro/skills`, plus `warning:` lines for copilot, cursor and opencode
- [ ] Run it for real, then open the repo in two different agents. Each one sees the BELLATRIX skills, and any duplicate listing matches the warning
- [ ] `bellatrix update-skills` with no `--agent` refreshes all nine (`"agents"` lists nine) and reports `emitted: []`

---

## B. Per domain (run each command as a SEPARATE invocation)

Each step is its own `bellatrix …` process, run from the same folder. Re-attach by `--session` is the thing under
test. After every command, record the JSON. **Pass** means each command exits 0 and its JSON matches the note.

### B.1 Desktop regression — Windows Calculator (WinAppDriver on `http://127.0.0.1:4723`)
- [ ] `$env:McpSettings__Apps__0__Name="calculator"; $env:McpSettings__Apps__0__Path="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"`
- [ ] `bellatrix desktop start-session --app calculator --session calc --json` → `sessionId`
- [ ] `bellatrix desktop snapshot --session calc --json` → `{path, elementCount > 0, summary}`
- [ ] `bellatrix desktop find --session calc --snapshot <path> --automation-id num7Button --json` → 1 node
- [ ] `bellatrix desktop click --session calc --locator "//*[@AutomationId='num7Button']" --json` (unified flag)
- [ ] `bellatrix desktop click --session calc --xpath "//*[@AutomationId='plusButton']" --json` (legacy flag still works)
- [ ] `bellatrix desktop get-text --session calc --locator "//*[@AutomationId='CalculatorResults']" --json` → contains `7`
- [ ] `bellatrix desktop get-attribute --session calc --locator "//*[@AutomationId='num7Button']" --attribute Name --json`
- [ ] `bellatrix desktop close-session --session calc --json`. Then `bellatrix desktop list-sessions --json` no longer lists `calc`

### B.2 Web — Chrome, **no Selenium server** (new in 2.3.0: detached local driver)
- [ ] `bellatrix web start-session --browser chrome --session w1 --json`. Expect `"driverServer":"local"` and a `serverUrl` like `http://localhost:<port>/`. Task Manager shows a `chromedriver.exe`
- [ ] `bellatrix web navigate --session w1 --url https://demos.bellatrix.solutions/ --json` (separate process). The same browser window navigates
- [ ] `bellatrix web snapshot --session w1 --json` → `{path, elementCount, summary}`
- [ ] `bellatrix web find --session w1 --snapshot <path> --text "Falcon 9" --json`
- [ ] `bellatrix web click --session w1 --locator "[data-product_id*='28']" --json` (CSS auto)
- [ ] `bellatrix web get-text --session w1 --locator "//h2[contains(.,'Falcon 9')]" --json` (XPath auto-detected, no `--strategy`)
- [ ] `bellatrix web close-session --session w1 --json` → `"driverStopped":true`. Both the browser window and `chromedriver.exe` are gone
- [ ] Leak check: start `w2`, wait more than 10 minutes, then run `bellatrix web list-sessions --json`. `w2` is pruned and its chromedriver is gone
- [ ] Repeat the first and last step with `--browser edge` (msedgedriver) and `--browser firefox` (geckodriver)

### B.3 Web — with a Selenium server (`--remote-url`, unchanged behavior)
- [ ] `java -jar selenium-server-4.x.jar standalone`
- [ ] `bellatrix web start-session --browser chrome --remote-url http://localhost:4444 --session w3 --json` → `"driverServer":"remote"`
- [ ] navigate / snapshot / click as in B.2, each a separate invocation
- [ ] `bellatrix web close-session --session w3 --json` → `"driverStopped":false`. The Selenium server keeps running

### B.4 Mobile — Android emulator (Appium 2 + uiautomator2 on `http://127.0.0.1:4723`; stop WinAppDriver first)
- [ ] `bellatrix mobile start-session --platform android --app <path>\ApiDemos-debug.apk --session m1 --json`
- [ ] `bellatrix mobile snapshot --session m1 --json`
- [ ] `bellatrix mobile find --session m1 --snapshot <path> --text "Views" --json`
- [ ] `bellatrix mobile click --session m1 --locator "Views" --json` (unified flag, default `accessibilityid`)
- [ ] `bellatrix mobile get-text --session m1 --locator "//android.widget.TextView[@text='Animation']" --json` (XPath auto)
- [ ] `bellatrix mobile click --session m1 --locator-type accessibilityid --locator-value "Animation" --json` (legacy flags)
- [ ] `bellatrix mobile get-orientation --session m1 --json`
- [ ] `bellatrix mobile close-session --session m1 --json`

### B.5 Cross-domain
- [ ] Use the same `--session x` name in desktop and web at the same time. Neither session disturbs the other
- [ ] `bellatrix sessions --json` lists both, each with its `domain`

---

## C. Golden paths — four end-to-end runs

Each run is one whole user journey: scaffold → the agent automates a test case live through the CLI → the generated
code builds → **you** run the generated test. A run passes only when every row of its results table is ✅. These
four runs are the Level-3 evidence for a matrix cell (`docs/support-matrix.json` → `internal.cells[].evidence.goldenPath`). Every
other cell relies on L1 (E2E) + L2 (conformance) only.

**Common setup (all runs)**
- `bellatrix --version` ≥ 3.0.1. Copy the version and the date into each results table.
- Work in a **scratch copy** of a BELLATRIX test repository, never in a repo you care about:
  `git clone <your-test-repo> <scratch>\<run>`.
  - It needs a desktop test project (`Demo.DesktopTests` below) and a web test project (`Demo.WebTests` below), for
    example created from the BELLATRIX templates (https://docs.bellatrix.solutions/), and a `test-cases/` folder for
    the SPECTRA-style test cases.
  - If the copy registers a `bellatrix-*-mcp` server in an MCP config, `bellatrix init` removes that key, which is
    intended.
- In the terminal you start the agent from, set `$env:BELLATRIX_TRACE="<scratch>\<run>\trace.jsonl"`.
  - After the run, `Select-String '"contextHeavy":true' trace.jsonl` must be empty.
  - That proves no `--inline` snapshot and no `get-page-source` entered the agent's context.
- Do not help the agent. If it asks a question, answer it and note the question in the table.

**Desktop test case for runs 1 and 4.** Save this as `test-cases/unit-converter/TC-1001.md`:

```markdown
---
id: TC-1001
priority: high
component: unit-converter
---
# Convert 1 meter to feet in the Calculator unit converter
## Preconditions
Windows Calculator is open in Standard mode.
## Steps
1. Open the navigation menu and choose Length
2. Set the input unit to Meters and the output unit to Feet
3. Enter 1
## Expected Result
The result shows approximately 3.28084 feet.
```

**Web test case for runs 2 and 3.** Save this as `test-cases/web/TC-2001.md`:

```markdown
---
id: TC-2001
priority: high
component: shop
---
# Add Falcon 9 to the cart and verify the cart total
## Preconditions
The demo shop https://demos.bellatrix.solutions/ is open; the cart is empty.
## Steps
1. On the shop home page, click "Add to cart" for Falcon 9
2. Click the mini-cart in the header (it shows the total and item count)
3. On the Cart page, check the line item and the totals
## Expected Result
The cart contains one Falcon 9 at 50.00€; the cart total is 50.00€.
```

### C.1 Claude Code × C# × desktop — TC-1001 on Calculator

**Setup**
- WinAppDriver running on `http://127.0.0.1:4723`. You start it; the CLI never does.
- In `<scratch>\c1`, add `test-cases/unit-converter/TC-1001.md` (above). If the project already has a page object for
  the unit converter, keep it: the agent should reuse or extend it, not duplicate it.
- Set the app definition:
  `$env:McpSettings__Apps__0__Name="calculator"; $env:McpSettings__Apps__0__Path="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"`
- Run `bellatrix init --agent claude --language csharp --domains desktop --regenerate`, then start `claude` in
  that folder.

**Prompt (exact)**
> Use the bellatrix-desktop-system-tests-generator agent to automate test case TC-1001
> (`test-cases/unit-converter/TC-1001.md`) against Windows Calculator. Execute the steps live through the bellatrix
> desktop CLI first, delegate every locator to the locator specialist, then generate the page object and the test with
> `[ManualTestCase("TC-1001")]` following the existing Demo.DesktopTests structure. Build the project. Do not run the
> tests.

**Expected artifacts**
- The trace shows:
  - `start-session`, `snapshot`, locator resolution through `find` or the subagent, the interactions, and
    `close-session`;
  - no context-heavy line.
- The unit-converter page object follows the Actions/Map/Assertions partials (an existing one is extended, not
  duplicated).
- `Tests\UnitConverter\*.cs` contains a `[ManualTestCase("TC-1001")]` test that asserts ~3.28084.
- `dotnet build Demo.DesktopTests` reports 0 errors.
- Locators follow the AutomationId → Name → ControlType ladder.

**Run the generated test**
`dotnet test Demo.DesktopTests --filter "FullyQualifiedName~UnitConverter"` with WinAppDriver running.

| Check | Result (✅/❌ + note) |
|---|---|
| bellatrix version / date | |
| Agent used the generator + locator specialist (or `find`) | |
| Trace: no context-heavy call | |
| Live execution reached "~3.28084 feet" | |
| Page object reused/extended, 3-file pattern | |
| `[ManualTestCase("TC-1001")]` present | |
| `dotnet build` 0 errors | |
| Generated test passes when **you** run it | |
| Questions the agent asked / manual fixes needed | |

### C.2 Claude Code × C# × web — TC-2001 on the demo shop

**Setup**
- Chrome installed. No Selenium server is needed: the CLI starts a matching chromedriver itself.
- In `<scratch>\c2`, add `test-cases/web/TC-2001.md` (above).
- Run `bellatrix init --agent claude --language csharp --domains web --regenerate`, then start `claude`.

**Prompt (exact)**
> Use the bellatrix-web-system-tests-generator agent to automate test case TC-2001 (`test-cases/web/TC-2001.md`)
> against https://demos.bellatrix.solutions/. Execute it live through the bellatrix web CLI first, delegate every
> locator to the locator specialist, then generate page objects (Map/Actions/Assertions) and a test with
> `[ManualTestCase("TC-2001")]` in Demo.WebTests. Build the project. Do not run the tests.

**Expected artifacts**
- The trace shows:
  - `web start-session` with `driverServer:"local"`;
  - navigate, snapshot, find and click;
  - `close-session`;
  - no context-heavy line.
- The new page(s) under `Demo.WebTests\Pages\`:
  - use `WebPage` with `override Url` and `override Open()`;
  - use `CreateByXpath` (lowercase `p`).
- The test is marked `[ManualTestCase("TC-2001")]` and asserts the 50.00€ total.
- `dotnet build Demo.WebTests` reports 0 errors.

**Run the generated test**
`dotnet test Demo.WebTests --filter "FullyQualifiedName~TC2001|FullyQualifiedName~Cart"`. Adjust the filter to the
generated class name.

| Check | Result (✅/❌ + note) |
|---|---|
| bellatrix version / date | |
| Generator + locator specialist used | |
| Trace: no context-heavy call; chromedriver gone after close | |
| Page objects follow 3-file pattern, `CreateByXpath` | |
| `[ManualTestCase("TC-2001")]` present | |
| `dotnet build` 0 errors | |
| Generated test passes when **you** run it | |
| Questions / manual fixes | |

### C.3 Claude Code × Java × web — TC-2001 on the demo shop

**Setup**
- A Maven project with the BELLATRIX-Java web dependency (`solutions.bellatrix:bellatrix.web`), JDK 19 and Maven.
  [LANGUAGE-VERIFICATION-CHECKLIST.md](LANGUAGE-VERIFICATION-CHECKLIST.md) §1 has a ready sample.
- Copy `test-cases/web/TC-2001.md` into the project.
- Run `bellatrix init --agent claude --language java --domains web`. The auto-detect must also report java.
- Start `claude`.

**Prompt (exact)**
> Use the bellatrix-web-system-tests-generator agent to automate test case TC-2001 (`test-cases/web/TC-2001.md`)
> against https://demos.bellatrix.solutions/. Execute it live through the bellatrix web CLI first, delegate every
> locator to the locator specialist, then generate the page (Map + Asserts) and a JUnit test annotated
> `@ManualTestCase("TC-2001")`. Run `mvn test-compile`. Do not run the tests.

**Expected artifacts**
- The trace matches C.2.
- The Java page follows the skill's Map/Asserts layout and package rules.
- `@ManualTestCase("TC-2001")` is the String-valued annotation that the skill defines.
- `mvn -q test-compile` reports 0 errors.

**Run the generated test**
`mvn test -Dtest=<GeneratedTestClass>`

| Check | Result (✅/❌ + note) |
|---|---|
| bellatrix version / date | |
| Generator + locator specialist used | |
| Trace: no context-heavy call | |
| Map/Asserts layout + `@ManualTestCase("TC-2001")` | |
| `mvn test-compile` 0 errors | |
| Generated test passes when **you** run it | |
| Questions / manual fixes | |

### C.4 GitHub Copilot × C# × desktop — TC-1001 on Calculator

Same as C.1, with these differences:
- Use a fresh scratch copy, `<scratch>\c4`.
- If the clone still has a `.claude/` folder, delete it **before** `init`. This run tests Copilot's native layout:
  `.github/instructions`, `.github/skills` and `.github/agents/*.agent.md`.
- Scaffold for Copilot only: `bellatrix init --agent copilot --language csharp --domains desktop --regenerate`.
- Use VS Code Copilot Chat in Agent mode, or the Copilot CLI, and pick the `bellatrix-desktop-system-tests-generator`
  agent.

**Prompt (exact):** the same as C.1.

| Check | Result (✅/❌ + note) |
|---|---|
| bellatrix version / Copilot surface (VS Code / CLI) + version / date | |
| Copilot listed the BELLATRIX skills and agents once each | |
| Locator specialist ran as a subagent (or `find` was used) | |
| Trace: no context-heavy call | |
| Page object reused/extended; `[ManualTestCase("TC-1001")]` | |
| `dotnet build` 0 errors | |
| Generated test passes when **you** run it | |
| Questions / manual fixes | |

> The former per-language live items (Java desktop/mobile, TypeScript web, C# mobile) now live in
> [LANGUAGE-VERIFICATION-CHECKLIST.md](LANGUAGE-VERIFICATION-CHECKLIST.md) §4. They are useful checks, but they are not golden paths.
