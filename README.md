# BELLATRIX Agent — the Agentic Test Automation Framework

[![CI](https://github.com/AutomateThePlanet/BELLATRIX-Agent/actions/workflows/ci.yml/badge.svg)](https://github.com/AutomateThePlanet/BELLATRIX-Agent/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Bellatrix.Cli.svg)](https://www.nuget.org/packages/Bellatrix.Cli)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

BELLATRIX Agent lets your AI coding agent (Claude Code, GitHub Copilot, Cursor, Devin, Codex CLI, Gemini CLI,
Antigravity CLI, Kiro CLI or OpenCode) write and run [BELLATRIX](https://github.com/AutomateThePlanet/BELLATRIX)
tests for **desktop**, **web** and **mobile** apps in C#, Java or TypeScript.

It has two parts:

- **`bellatrix`**, a .NET global tool that drives desktop (WinAppDriver), web (Selenium) and mobile (Appium, Android
  and iOS) automation one shell command at a time. It runs no AI model and holds no credentials: your agent does the
  thinking, the CLI does the driving, and every command prints one line of JSON.
- **`bellatrix init`**, which writes skills, subagents and an instruction file into your test repository, in your
  agent's **native** layout. They teach the agent the BELLATRIX components, page objects and test structure, and how
  to find stable locators.

## How it works

1. **Sessions by name, no daemon.** `bellatrix web start-session --session w1` stores the driver session in
   `.bellatrix/sessions.db`; every later command (`click`, `get-text`, …) is a separate process that re-attaches by
   name.
2. **Snapshots go to disk.** `bellatrix <domain> snapshot` saves the UI tree under `.bellatrix/snapshots/` and returns
   only `{ path, elementCount, summary }`. The full tree never enters the agent's context.
3. **Locators are resolved in isolation.** The agent hands the snapshot path to the domain's
   `bellatrix-<domain>-locator-specialist` subagent, which returns one locator. Where an agent has no subagents, it
   queries narrowly with `bellatrix <domain> find --snapshot <path> …` instead. Reading a whole snapshot or page source
   is never allowed.
4. **Then it writes the code.** Using the validated locators, the agent generates page objects and tests in your
   language's BELLATRIX style, optionally traced back to [SPECTRA](https://github.com/AutomateThePlanet/Spectra) test
   cases.

More: [docs/architecture.md](docs/architecture.md).

## Install

You need the **.NET 8 SDK** (also for Java and TypeScript projects: the CLI is a .NET tool).

The current release, 3.1.4, is on
[GitHub Releases](https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.4): download the `.nupkg`
into a folder and install the tool from it (needs the [GitHub CLI](https://cli.github.com/), or download the file in
the browser).

```bash
gh release download v3.1.4 -R AutomateThePlanet/BELLATRIX-Agent -p "*.nupkg" -D ./bellatrix-pkg
dotnet tool install -g Bellatrix.Cli --add-source ./bellatrix-pkg --version 3.1.4   # or: dotnet tool update -g …
bellatrix --version
bellatrix help
```

Update, uninstall, integrity check and troubleshooting: [docs/INSTALL-PREVIEW.md](docs/INSTALL-PREVIEW.md).
NuGet.org package coming soon.

What each domain needs (you run it; the CLI never starts or stops it):

| Domain | Prerequisite |
|---|---|
| Desktop | Windows with **WinAppDriver** (or NovaWindows) on `http://127.0.0.1:4723` |
| Web | Chrome, Edge or Firefox. The CLI starts a matching local browser driver itself, or use `--remote-url` for your own Selenium server |
| Mobile | **Appium 2** on `http://127.0.0.1:4723` plus an Android emulator/device (uiautomator2) or, on macOS, iOS (XCUITest) |

Step-by-step flows per domain: [docs/TESTING.md](docs/TESTING.md).

## Quickstart per agent

Run `bellatrix init` in the root of your test repository, then open the repository in your agent and ask it to
automate a test case. `--language` is detected from your build files when omitted (`*.csproj` → csharp, `pom.xml` →
java, `package.json` + `tsconfig.json` → typescript).

| Agent | Scaffold | Then |
|---|---|---|
| Claude Code | `bellatrix init --agent claude --language csharp --domains desktop,web,mobile` | `claude`; delegation: `@agent-bellatrix-web-locator-specialist` |
| GitHub Copilot | `bellatrix init --agent copilot --language csharp --domains web` | VS Code Agent mode or `copilot`; pick `bellatrix-web-system-tests-generator` |
| Cursor | `bellatrix init --agent cursor --language java --domains web` | Agent chat or `agent`; delegation: `/bellatrix-web-locator-specialist` |
| Devin Desktop | `bellatrix init --agent devin --language csharp --domains desktop` | name the subagent in the prompt |
| Codex CLI | `bellatrix init --agent codex --language java --domains web,mobile` | `codex`; name the subagent in the prompt |
| Gemini CLI (enterprise) | `bellatrix init --agent gemini --language csharp --domains web` | `gemini`; delegation: `@bellatrix-web-locator-specialist` |
| Antigravity CLI | `bellatrix init --agent antigravity --language csharp --domains desktop` | `agy`; the main agent picks the subagent |
| Kiro CLI | `bellatrix init --agent kiro --language typescript --domains web` | `kiro-cli chat --agent bellatrix-web-system-tests-generator` |
| OpenCode | `bellatrix init --agent opencode --language java --domains web` | `opencode`; delegation: `@bellatrix-web-locator-specialist` |

Several agents can share one repository (`--agent claude,cursor,opencode`); `--dry-run` previews every file.

```bash
bellatrix init --agent claude                        # all domains (desktop, web, mobile)
bellatrix init --agent copilot,cursor --domains web  # several agents, one domain
bellatrix init --dry-run                             # auto-detect the agent, show the plan, write nothing
bellatrix update-skills                              # refresh what is already scaffolded
```

### `init` options

| Option | Values | Default |
|---|---|---|
| `--agent` | `claude`, `copilot`, `cursor`, `devin`, `codex`, `gemini`, `antigravity`, `kiro`, `opencode`, comma-separated. `windsurf` is an alias for `devin`, `agy` for `antigravity` | Agents already scaffolded (`.bellatrix/manifest.<agent>.json`), else the single agent found by its repo markers. If none or several are found, it exits 2 |
| `--domains` | `desktop`, `web`, `mobile` | all |
| `--language` | `csharp`, `java`, `typescript` (`cs`, `c#`, `ts` accepted) | Language recorded in the manifests, else detected (`*.csproj`/`*.sln` → csharp, `pom.xml`/`build.gradle*` → java, `package.json` + `tsconfig.json` → typescript), else csharp. Several detected → exit 2 |
| `--out <dir>` | target repo root | current directory |
| `--regenerate` / `--force` | overwrite files you edited | edited files are skipped |
| `--dry-run` | report `emitted`/`skipped`/`removed` per agent, write nothing | off |
| `--project` | your test project file (`.csproj`, `pom.xml`, `build.gradle`, `package.json`) for every domain, or per domain: `desktop=<path>,web=<path>` | detected (projects that reference BELLATRIX); in a terminal you confirm it. Several and no terminal → exit 2 |
| `--test-cases-dir` | folder of your test-case files, or per domain (`web=<dir>`) | `spectra.config.json` `tests.dir`, else `test-cases` |

`--target` still works as a deprecated alias for `--agent`.

### Project settings (`.bellatrix/config.json`)

`init` records where your test project is, so the skills never guess a project name:

```json
{
  "$schema": "https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/main/schemas/bellatrix-config.v1.schema.json",
  "version": 1,
  "testCasesDir": "test-cases",
  "domains": {
    "desktop": {
      "project": {
        "path": "src/Acme.UiTests/Acme.UiTests.csproj",
        "name": "Acme.UiTests",
        "rootNamespace": "Acme.UiTests",
        "buildCommand": "dotnet build src/Acme.UiTests/Acme.UiTests.csproj"
      }
    }
  }
}
```

- **Commit this file.** It is meant to be shared: your agent and your team use the same project and test-case folder.
  Keep `.bellatrix/sessions.db`, `.bellatrix/snapshots/` and `.bellatrix/screenshots/` out of Git.
- **Where the values come from:** `--project` / `--test-cases-dir`, else detection (see the options above). A
  domain without a project means "ask me": the skills stop and ask you, then record the answer.
- **Re-runs never overwrite it.** `init` and `update-skills` only add missing values (a new domain, a missing
  project); use `--regenerate` to rebuild it. Edit it by hand any time.
- **Check it:** `bellatrix config validate` (exit 1 with the problems) and `bellatrix config show` (the values per
  domain, with the project folder and test-case folder resolved). The skills run `validate` before they build or
  write code. Format: [schemas/bellatrix-config.v1.schema.json](schemas/bellatrix-config.v1.schema.json).

### Your own templates (`--template-source`)

```bash
bellatrix init --agent claude --template-source ./my-templates --prefix acme
```

The same contract, applied to a template folder that you own:
- **Folder layout:** `instructions.md`, `skills/<name>/SKILL.md`, `agents/<name>.md`.
- **Your own namespace:** `.bellatrix/acme.instructions.md`, `.bellatrix/acme-manifest.<agent>.json`, and skills and
  agents named `acme-…` (a name without the prefix gets it).
- **Per-agent native layout and edit protection** work as in a normal `init`.
- **Isolation:** it never touches BELLATRIX's own files and leaves MCP configuration alone.

`--prefix` defaults to the common first segment of the folder's skill and agent names. `bellatrix` is reserved.

## Supported agents

What `bellatrix init --agent <name>` writes for each of the nine supported agents:

<!-- provider-matrix:begin -->
<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->
| Agent | `--agent` | Instructions | Skills | Subagents (locator specialist + generator) |
|---|---|---|---|---|
| **Claude Code** | `claude` | `.bellatrix/bellatrix.instructions.md` + one `@import` line in `CLAUDE.md` | `.claude/skills/` | `.claude/agents/*.md` |
| **GitHub Copilot** | `copilot` | `.github/instructions/bellatrix.instructions.md` (`applyTo: "**"`) | `.github/skills/` (or reuses `.claude/skills/`) | `.github/agents/*.agent.md`, with `agents:` allow-list on generators. Reuses `.claude/agents/` when Claude is scaffolded |
| **Cursor** | `cursor` | `.cursor/rules/bellatrix.mdc` (`alwaysApply: true`) | `.cursor/skills/` (or reuses `.claude/skills/`) | `.cursor/agents/*.md` (`readonly: true` on specialists) |
| **Devin Desktop** (formerly Windsurf) | `devin` | `.devin/rules/bellatrix.md` (`trigger: always_on`) | `.devin/skills/` (or reuses `.claude/skills/`) | `.devin/agents/*.md` (`allowed-tools`) |
| **Codex CLI** | `codex` | One marker block in `AGENTS.md` pointing at `.bellatrix/bellatrix.instructions.md` | `.agents/skills/` | `.codex/agents/*.toml` |
| **Gemini CLI** (enterprise) | `gemini` | One marker block in `GEMINI.md` importing `@./.bellatrix/bellatrix.instructions.md` | `.agents/skills/` | `.gemini/agents/*.md` (`kind: local`, Gemini tool names) |
| **Antigravity CLI** (agy) | `antigravity` | `.agents/rules/bellatrix.md` (`trigger: always_on`) | `.agents/skills/` | `.agents/agents/<name>/agent.md` (documented tool names; `mainAgent`/`subagent`) |
| **Kiro CLI** | `kiro` | `.kiro/steering/bellatrix.md` (`inclusion: always`) | `.kiro/skills/` | `.kiro/agents/*.md` (v3 Markdown; `resources` list the steering file and skills) |
| **OpenCode** | `opencode` | One marker block in `AGENTS.md` pointing at `.bellatrix/bellatrix.instructions.md` (shared with Codex) | `.opencode/skills/` (or reuses `.claude/skills/` / `.agents/skills/`) | `.opencode/agents/*.md` (`mode: subagent`, `edit: deny` on specialists) |
<!-- provider-matrix:end -->

Rules every agent gets:

- **Human-owned files are never rewritten.** `CLAUDE.md`, `AGENTS.md` and `GEMINI.md` get exactly one line or one
  `<!-- >>> bellatrix >>> -->` block. `.github/copilot-instructions.md` is not touched.
- **Skills are written once per directory set.** When several agents read the same directory, BELLATRIX writes to it
  once. Example: Copilot, Cursor and Devin all read `.claude/skills` when Claude is selected. `--dry-run` prints these
  decisions.
- **One manifest per agent.** Re-running for one agent never touches another agent's files. A file you edited is
  skipped unless you pass `--regenerate`.
- **Delegation fallback is mandatory.** Every fragment says to delegate locator work to the locator specialist, or,
  when delegation is unavailable, to use only `bellatrix <domain> find --snapshot <path> …`. The agent must never read
  a snapshot file or `get-page-source` into its main context.
- **No MCP registration.** `init` only removes BELLATRIX's own stale MCP keys. Foreign keys are kept.

## Supported languages and domains

Every supported agent gets the same skills for each language and domain below.

<!-- support-matrix:begin -->
<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->
| Language | Desktop | Web | Mobile (Android + iOS) |
|---|---|---|---|
| C# | ✓ | ✓ | ✓ |
| Java | ✓ | ✓ | ✓ |
| TypeScript | — | ✓ | — |

TypeScript: web only (the BELLATRIX TypeScript framework has no desktop or mobile component layer).
<!-- support-matrix:end -->

## Known limits

- **Copilot cloud agent:** reading `.claude/agents` is undocumented. To give the cloud agent native agents, scaffold
  Copilot without Claude.
- **Devin:** subagents are experimental, and `.claude/agents` import is undocumented, so Devin always gets native
  copies.
- **Codex:** has no per-agent read-only setting. The specialist's read-only rule is instruction-only.
- **Duplicate skill copies:** with Claude plus Codex, Gemini or Antigravity, Copilot, Cursor and OpenCode read both
  `.claude/skills` and `.agents/skills`. No layout avoids that. The run reports it as a `warning:` decision.
- **Gemini CLI is enterprise-only:** since 2026-06-18 it serves Gemini Code Assist (Standard/Enterprise) licences and
  API keys only; unpaid and Google One users moved to Antigravity CLI (`--agent antigravity`). Google sells no new
  Code Assist subscriptions since 2026-10-09 (renewals end in 2027).
- **Antigravity CLI:** subagents have no `@name` syntax (the main agent picks them), and the name of its file-creating
  tool is undocumented, so the generator lists only the documented `view_file`, `grep_search`, `replace_file_content`
  and `run_command`.
- **Kiro CLI:** the agents use the v3 Markdown format (Kiro CLI 3.0); Kiro CLI 2.x reads only JSON agents.
- **OpenCode:** it ignores `@` imports, so like Codex it reads the fragment through a one-line pointer in `AGENTS.md`.
- **TypeScript is web only.** BELLATRIX-JavaScript has no desktop/mobile component or page-object layer, so
  `--language typescript --domains desktop|mobile` scaffolds only the language-agnostic CLI skills (execute-scenario,
  execute-spectra-test, locator specialist) and warns.
- **Framework bugs found in the source** (documented in the skills, with workarounds; to be fixed upstream): C# mobile
  `GetText()` is self-recursive on most components; Java desktop `byXPath` uses `By.tagName`; Java `AppConfiguration`
  lacks `equals()` (lifecycle restarts every test); BELLATRIX-JavaScript `WebComponent.ts` TS2313 depends on check order.

## Drive the CLI

```bash
bellatrix desktop list-apps --json
bellatrix desktop start-session --app Calculator --session s1 --json
bellatrix desktop snapshot --session s1 --json          # → { path, elementCount, summary }
bellatrix desktop find --session s1 --snapshot <path> --name "Equals" --json
bellatrix desktop click --session s1 --locator "//Button[@Name='Equals']" --json
bellatrix desktop close-session --session s1 --json
```

`web` and `mobile` follow the same shape (`start-session` → `snapshot` → `find` → interact → `close-session`).
Elements are addressed the same way everywhere: `--locator <expr> [--strategy <s>]`. Desktop accepts XPath only. Web
treats a locator starting with `/` as XPath and anything else as CSS. Mobile treats `/` as XPath and anything else as
`accessibilityid`. The WinAppDriver/Appium server and the device are yours to run. For web, the CLI starts and stops
a local browser driver itself unless you pass `--remote-url` to your own Selenium server.

Every command, flag, default and success-JSON shape: `bellatrix help` (or `bellatrix <domain> help`, or `--help` on any command); add `--json`
for the machine-readable form. Exit codes: `0` success, `1` error (`{"error":{"code","message"}}` on stdout), `2`
invalid `--agent`/`--language` selection. Set `BELLATRIX_TRACE=<file>` to append one JSON line per invocation
(verb, args with `--text` redacted, exit code, stdout size, context-heavy flags).

## Documentation

- Guide, CLI reference and what is supported: https://bellatrix.solutions/agent/
- [docs/TESTING.md](docs/TESTING.md) — install and one copy-paste flow per domain
- [docs/architecture.md](docs/architecture.md) — sessions, context economy, the `init` contract, provider emitters
- [CHANGELOG.md](CHANGELOG.md)

## Related projects

- [BELLATRIX](https://github.com/AutomateThePlanet/BELLATRIX) — the C# (.NET) framework
- [BELLATRIX-Java](https://github.com/AutomateThePlanet/BELLATRIX-Java) — the Java framework
- [BELLATRIX-JavaScript](https://github.com/AutomateThePlanet/BELLATRIX-JavaScript) — the TypeScript framework
- [SPECTRA](https://github.com/AutomateThePlanet/Spectra) — AI-native test case generation and execution; the
  `*-spectra-test` skills automate SPECTRA test cases with traceability
- Framework documentation: https://docs.bellatrix.solutions/

## Contributing

Build and test:

```bash
dotnet build BELLATRIX-Agent.sln
dotnet test tests/Bellatrix.Cli.Tests
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for golden files, the live E2E suite, the drift and public-safety guard tests,
and how to add an agent or a language. Please read the [Code of Conduct](CODE_OF_CONDUCT.md); report security issues
as described in [SECURITY.md](SECURITY.md).

## License

[Apache License 2.0](LICENSE). Copyright 2026 Automate The Planet Ltd.
