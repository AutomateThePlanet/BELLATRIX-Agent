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

```bash
dotnet tool install -g Bellatrix.Cli   # or: dotnet tool update -g Bellatrix.Cli
bellatrix --version
bellatrix help
```

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

`--target` still works as a deprecated alias for `--agent`.

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

## Provider matrix — where each agent's files go

<!-- provider-matrix:begin -->
<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->
| Agent | Instructions fragment | Skills | Subagents (locator specialist + generator) | Delegation mode |
|---|---|---|---|---|
| **Claude Code** | `.bellatrix/bellatrix.instructions.md` + one `@import` line in `CLAUDE.md` | `.claude/skills/` | `.claude/agents/*.md` | Isolated subagent (`@agent-<name>`) |
| **GitHub Copilot** | `.github/instructions/bellatrix.instructions.md` (`applyTo: "**"`) | `.github/skills/` (or reuses `.claude/skills/`) | `.github/agents/*.agent.md`, with `agents:` allow-list on generators. Reuses `.claude/agents/` when Claude is scaffolded | Subagent (VS Code `runSubagent`, CLI). Cloud agent: see limits |
| **Cursor** | `.cursor/rules/bellatrix.mdc` (`alwaysApply: true`) | `.cursor/skills/` (or reuses `.claude/skills/`) | `.cursor/agents/*.md` (`readonly: true` on specialists) | Isolated subagent (`/name`) |
| **Devin Desktop** (formerly Windsurf) | `.devin/rules/bellatrix.md` (`trigger: always_on`) | `.devin/skills/` (or reuses `.claude/skills/`) | `.devin/agents/*.md` (`allowed-tools`) | Subagent (experimental). Fallback: `bellatrix <domain> find` |
| **Codex CLI** | One marker block in `AGENTS.md` pointing at `.bellatrix/bellatrix.instructions.md` | `.agents/skills/` | `.codex/agents/*.toml` | Name the subagent in the prompt. Fallback: `find` |
| **Gemini CLI** (enterprise — Gemini Code Assist) | One marker block in `GEMINI.md` importing `@./.bellatrix/bellatrix.instructions.md` | `.agents/skills/` | `.gemini/agents/*.md` (`kind: local`, Gemini tool names) | Isolated subagent (`@name`) |
| **Antigravity CLI** (agy) | `.agents/rules/bellatrix.md` (`trigger: always_on`) | `.agents/skills/` | `.agents/agents/<name>/agent.md` (documented tool names; `mainAgent`/`subagent`) | Subagent chosen by the main agent (no `@name` syntax). Fallback: `bellatrix <domain> find` |
| **Kiro CLI** | `.kiro/steering/bellatrix.md` (`inclusion: always`) | `.kiro/skills/` | `.kiro/agents/*.md` (v3 Markdown; `resources` list the steering file and skills) | Subagent named in plain language. Fallback: `find` |
| **OpenCode** | One marker block in `AGENTS.md` pointing at `.bellatrix/bellatrix.instructions.md` (shared with Codex) | `.opencode/skills/` (or reuses `.claude/skills/` / `.agents/skills/`) | `.opencode/agents/*.md` (`mode: subagent`, `edit: deny` on specialists) | Subagent (`@name`) |
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

## Support matrix — language × domain × agent

<!-- support-matrix:begin -->
<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->
| Language × domain | Claude Code | GitHub Copilot | Cursor | Devin | Codex CLI | Gemini CLI (enterprise) | Antigravity CLI | Kiro CLI | OpenCode | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| C# × desktop | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | generated project builds against BELLATRIX `master` |
| C# × web | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | same |
| C# × mobile (Android + iOS) | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | same |
| Java × desktop | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | `mvn test-compile` against BELLATRIX-Java source |
| Java × web | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | same |
| Java × mobile (Android + iOS) | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | same |
| TypeScript × web | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | `tsc --noEmit` against BELLATRIX-JavaScript source |
| TypeScript × desktop | MISSING (framework) | ← | ← | ← | ← | ← | ← | ← | ← | BELLATRIX-JavaScript has no desktop component/page layer; only the CLI skills are scaffolded |
| TypeScript × mobile | MISSING (framework) | ← | ← | ← | ← | ← | ← | ← | ← | raw Android driver only, no iOS; only the CLI skills are scaffolded |

- **READY:** a project generated from the scaffolded skills compiles. Every framework API claim in those skills was checked against the current framework source, and the agent's layout is the one already in live use (Claude Code).
- **READY (synthetic fixtures):** READY evidence plus agent conformance (discovery + delegation) passed, but delegation was only exercised on synthetic snapshot fixtures (desktop, mobile) — not yet on a live recording.
- **PARTIAL:** the same skills, placed in the agent's documented native layout. The layout is pinned by golden-file tests but has not yet been confirmed live in that IDE/CLI (`docs/LIVE-VERIFICATION-CHECKLIST.md` §A).
- **MISSING (framework):** the BELLATRIX flavor itself has no layer to generate code against.
- No cell is BLOCKED-TOOLCHAIN: .NET, Maven/JDK 19 and Node were available for every compile check.
- In no cell have the generated tests been *run* against a live app yet (§C of the live checklist).
<!-- support-matrix:end -->

## Known limits

- **Copilot cloud agent:** reading `.claude/agents` is undocumented. To give the cloud agent native agents, scaffold
  Copilot without Claude, or verify it live.
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
  and `run_command`. Check it live ([checklist §A.8](docs/LIVE-VERIFICATION-CHECKLIST.md)).
- **Kiro CLI:** the agents use the v3 Markdown format (Kiro CLI 3.0); Kiro CLI 2.x reads only JSON agents.
- **OpenCode:** it ignores `@` imports, so like Codex it reads the fragment through a one-line pointer in `AGENTS.md`.
- **TypeScript is web only.** BELLATRIX-JavaScript has no desktop/mobile component or page-object layer, so
  `--language typescript --domains desktop|mobile` scaffolds only the language-agnostic CLI skills (execute-scenario,
  execute-spectra-test, locator specialist) and warns.
- **Framework bugs found in the source** (documented in the skills, with workarounds; to be fixed upstream): C# mobile
  `GetText()` is self-recursive on most components; Java desktop `byXPath` uses `By.tagName`; Java `AppConfiguration`
  lacks `equals()` (lifecycle restarts every test); BELLATRIX-JavaScript `WebComponent.ts` TS2313 depends on check order.
- Live checks per agent are tracked in [docs/LIVE-VERIFICATION-CHECKLIST.md](docs/LIVE-VERIFICATION-CHECKLIST.md).

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

- Guide, CLI reference and support matrix: https://bellatrix.solutions/agent/
- [docs/TESTING.md](docs/TESTING.md) — install and one copy-paste flow per domain
- [docs/architecture.md](docs/architecture.md) — sessions, context economy, the `init` contract, provider emitters
- [docs/LIVE-VERIFICATION-CHECKLIST.md](docs/LIVE-VERIFICATION-CHECKLIST.md) and
  [docs/LANGUAGE-VERIFICATION-CHECKLIST.md](docs/LANGUAGE-VERIFICATION-CHECKLIST.md) — manual live checks
- [docs/conformance-report.md](docs/conformance-report.md) — headless agent conformance results
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
