# Testing `bellatrix` (current release: 3.2.0)

> This guide always describes the latest release. Release history: [CHANGELOG.md](../CHANGELOG.md).

This guide walks you through the model-free `bellatrix` CLI on all three domains: desktop, web and mobile. Every
step uses PowerShell on Windows.

The CLI runs no AI model and starts no driver. **You start the driver, browser server or emulator yourself**, and
`bellatrix` attaches to it. Every command prints a single line of JSON. Errors look like this:

```json
{"error":{"code":"…","message":"…"}}
```

## 1. Install

You need the .NET 8 SDK. Install the current release from NuGet.org (to install from the GitHub release `.nupkg`
instead, see [INSTALL-PREVIEW.md](INSTALL-PREVIEW.md)):

```powershell
# First install:
dotnet tool install -g Bellatrix.Cli
# …or, if an older bellatrix is already installed:
dotnet tool update  -g Bellatrix.Cli

bellatrix --version          # → {"version":"3.2.0"}
```

To try a build from source, pack it and add the folder as a source:

```powershell
git clone https://github.com/AutomateThePlanet/BELLATRIX-Agent
cd BELLATRIX-Agent
dotnet pack src\Bellatrix.Cli\Bellatrix.Cli.csproj -c Release -o .\artifacts\nuget
dotnet tool update -g Bellatrix.Cli --add-source .\artifacts\nuget --version 3.2.0
```

> **Run every command for one test from the same folder.** Sessions are stored in `.\.bellatrix\sessions.db` and
> snapshots in `.\.bellatrix\snapshots\`, both relative to the current directory. Each command is a separate
> process that re-attaches to the session by name (`--session`); there is no daemon.

Optional: to scaffold the AI skills and subagents for your AI agent, run this in an empty folder (or your test repo):

```powershell
bellatrix init --agent claude --domains desktop,web,mobile --json      # or copilot, cursor, devin, codex, gemini, antigravity, kiro, opencode
bellatrix init --agent claude,cursor --dry-run --json                  # several agents; preview only
```

Each agent gets its own native layout (see the provider matrix in the [README](../README.md)). For Claude this writes
`.claude/skills/bellatrix-*`, `.claude/agents/bellatrix-*` and `.bellatrix/bellatrix.instructions.md`, plus one
`@import` line in `CLAUDE.md`. It registers **no** MCP server. Without `--agent`, `init` re-uses the agents already
scaffolded, or detects the single agent present (`.claude/`, `.cursor/`, `.github/agents/`, `.devin/`, `.codex/`,
`.gemini/` …), and otherwise exits 2 and asks.

**Language.** The skills' code examples come in three BELLATRIX flavors:

```powershell
bellatrix init --agent claude --language java --domains desktop,web,mobile --json
bellatrix init --agent claude --language typescript --domains web --json
```

Without `--language`, `init` uses the language recorded in `.bellatrix/manifest.<agent>.json`, else detects it from the
repo root (`*.csproj`/`*.sln` → csharp, `pom.xml`/`build.gradle*` → java, `package.json` + `tsconfig.json` →
typescript), else falls back to csharp with a notice. Several detected → exit 2. **TypeScript is web only:** for
`--domains desktop|mobile` it scaffolds just `execute-scenario`, `execute-spectra-test` and the locator specialist and
prints a warning. To set up the Maven or npm project the generated code builds in, see
https://bellatrix.solutions/agent/languages/.

---

## 2. Desktop — Windows Calculator

**Prerequisites**

- Windows 10/11.
- **WinAppDriver** listening on `http://127.0.0.1:4723`. It needs Developer Mode on; start `WinAppDriver.exe`
  yourself.
- *Alternative:* Appium with the NovaWindows driver on the same URL. Set
  `$env:McpSettings__DriverType = "NovaWindows"`.
- Optional: `$env:McpSettings__ServerUrl` to use a different driver URL.

**Flow.** Set the environment variables in the **same** PowerShell window, then run the commands one by one.

```powershell
$env:McpSettings__Apps__0__Name = "calculator"
$env:McpSettings__Apps__0__Path = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"

bellatrix desktop list-apps --json
bellatrix desktop start-session --app calculator --session calc --json
bellatrix desktop snapshot --session calc --json
#   → {"path":".bellatrix\\snapshots\\…json","elementCount":…,"summary":…}
bellatrix desktop find --session calc --snapshot <path-from-above> --automation-id num7Button --json
bellatrix desktop click --session calc --locator "//*[@AutomationId='num7Button']" --json
bellatrix desktop get-text --session calc --locator "//*[@AutomationId='CalculatorResults']" --json
#   → e.g. {"text":"Display is 7"}
bellatrix desktop get-attribute --session calc --locator "//*[@AutomationId='num7Button']" --attribute Name --json
bellatrix desktop close-session --session calc --json
```

You can also start an app without configuring it first: `start-session --app-path <exe or AUMID>`.

---

## 3. Web — Chrome against the BELLATRIX demo shop

**Prerequisites**

- Google Chrome (or Edge / Firefox). **No Selenium server is needed.** Since 2.3.0, `start-session` without
  `--remote-url` starts the browser driver (chromedriver/msedgedriver/geckodriver) as a separate background
  process. Later commands re-attach to it, and `close-session` stops it. The reply says
  `"driverServer":"local"`.
- Optional: use your own Selenium server (Grid or standalone) with `--remote-url http://localhost:4444`. The reply
  then says `"driverServer":"remote"`, and that server is never stopped by `bellatrix`.

**Flow**

```powershell
bellatrix web start-session --browser chrome --session w1 --json
bellatrix web navigate --session w1 --url https://demos.bellatrix.solutions/ --json
bellatrix web snapshot --session w1 --json
bellatrix web find --session w1 --snapshot <path-from-above> --text "Falcon 9" --json
bellatrix web click --session w1 --locator "[data-product_id*='28']" --json        # CSS
bellatrix web get-title --session w1 --json
bellatrix web get-text --session w1 --locator "//h2[contains(.,'Falcon 9')]" --json  # XPath, auto-detected
bellatrix web close-session --session w1 --json                                      # → "driverStopped":true
```

`--strategy` accepts `css`, `xpath`, `id`, `class`, `name`, `tag`, `linktext` and `partiallinktext`. When it is
omitted, a locator starting with `/` or `(/` is treated as XPath and anything else as CSS. The older `--selector`
flag still works.

---

## 4. Mobile — Android emulator

**Prerequisites**

- **Appium 2** server on `http://127.0.0.1:4723` with the `uiautomator2` driver installed:

  ```powershell
  appium driver install uiautomator2
  appium
  ```

- An Android emulator that is booted and visible in `adb devices`.
- An APK on disk. Example: Appium's `ApiDemos-debug.apk`.
- WinAppDriver also uses port 4723. Stop it first, or run Appium on another port and pass `--appium-url`.

**Flow**

```powershell
bellatrix mobile start-session --platform android --app C:\apks\ApiDemos-debug.apk --session m1 --json
bellatrix mobile snapshot --session m1 --json
bellatrix mobile find --session m1 --snapshot <path-from-above> --text "Views" --json
bellatrix mobile click --session m1 --locator "Views" --json                     # default strategy: accessibilityid
bellatrix mobile get-orientation --session m1 --json
bellatrix mobile close-session --session m1 --json
```

Supported values for `--strategy` (the older `--locator-type`/`--locator-value` flags still work). Without
`--strategy`, a value starting with `/` is XPath and anything else is `accessibilityid`:

- On every platform: `accessibilityid`, `id`, `xpath`, `classname`.
- Android only: `androiduiautomator`.
- iOS only: `iospredicate`, `iosclasschain`.

For iOS use `--platform ios --app <.app | bundleId>`; this needs macOS and XCUITest.

---

## 5. Session housekeeping (any domain)

```powershell
bellatrix sessions --json            # every stored session, with its domain
bellatrix sessions --prune --json    # drop rows idle longer than the keep-alive
bellatrix web list-sessions --json   # only web sessions (desktop/mobile likewise)
```

Since 2.1.0, sessions are stored per domain. The same `--session` name can be used in web and desktop at the same
time, and pruning one domain never removes another domain's sessions.

### Reference and tracing

```powershell
bellatrix help                       # every command, flag, default and output shape
bellatrix web help --json            # one domain, machine-readable
$env:BELLATRIX_TRACE = "$PWD\trace.jsonl"   # one JSON line per invocation (--text values redacted)
bellatrix web list-sessions --json; Get-Content .\trace.jsonl; Remove-Item Env:\BELLATRIX_TRACE
```

---

## 6. Known limits

| Limit | Workaround |
|---|---|
| Desktop (WinAppDriver) and mobile (Appium) both default to port 4723. | Run one at a time, or move one of them with `McpSettings__ServerUrl` or `--appium-url`. |
| The Copilot **cloud** agent and Devin may not read `.claude/agents`; with Claude + Codex/Gemini/Antigravity, Copilot, Cursor and OpenCode may list each skill twice. Antigravity's file-creating tool name is undocumented (its generator lists only documented tools); Kiro agents need Kiro CLI 3.x (Markdown agents). Gemini CLI works only with Gemini Code Assist licences or API keys. | See [Known limits](../README.md#known-limits) in the README for each agent. |
| **TypeScript has no desktop/mobile skills** (BELLATRIX-JavaScript has no component/page layer there) — use C# or Java. The skills document several BELLATRIX framework bugs found in the source (C# mobile `GetText()` recursion, Java desktop `byXPath`, Java lifecycle `equals()`), with workarounds. | Use C# or Java for desktop and mobile. For a framework bug, follow the workaround in the skill. |
| `init` **removes** `bellatrix-desktop-mcp`, `bellatrix-web-mcp` and `bellatrix-mobile-mcp` entries from `.mcp.json`, `.vscode/mcp.json`, `.claude/settings.json` and the selected agents' own MCP files (`.cursor/mcp.json`, `.devin/mcp_config.json`, `.codex/config.toml`, `.gemini/settings.json`, `.github/mcp.json`, `.agents/mcp_config.json`, `.kiro/settings/mcp.json`, `opencode.json(c)`). Other servers' entries are kept. | Don't run `init` in a repo that still depends on a BELLATRIX MCP server until it has moved to the CLI. |
| The first 2.1.0+ command converts an existing `.bellatrix/sessions.db` to the per-domain format. Older `bellatrix` versions can no longer write sessions to it. | Upgrade everyone to ≥ 2.1.0, or delete `.bellatrix/sessions.db`. |
| A 2.2 re-run renames `.bellatrix/manifest.json` to `manifest.claude.json`. Older `bellatrix` versions will then treat every scaffolded file as unmanaged. | Upgrade everyone to ≥ 2.2.0. |

**Found a problem?** Open an issue at https://github.com/AutomateThePlanet/BELLATRIX-Agent/issues with the commands
you ran, the JSON each one printed (`bellatrix --version` included), and anything that differed from this guide.
