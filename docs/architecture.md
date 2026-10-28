# Architecture

BELLATRIX Agent splits test automation between an **AI coding agent** that reasons and a **model-free CLI** that acts.
This page describes the roles, the session model, how the agent's context is kept small, the contract `bellatrix
init` follows, and how each AI agent gets its own native layout.

## Roles

| Part | Does | Never does |
|---|---|---|
| Your AI coding agent | reads the scaffolded skills, plans the scenario, calls `bellatrix` commands, writes page objects and tests | drive a browser, app or device directly |
| Locator specialist subagent (`bellatrix-<domain>-locator-specialist`) | reads one saved snapshot in its own isolated context and returns **one** locator | return the UI tree to the main agent |
| `bellatrix` CLI (`src/Bellatrix.Cli`) | parses the command, re-attaches the session, calls the engine, prints one line of JSON | run an AI model, hold credentials, start WinAppDriver/Appium/emulators |
| Execution engine (`src/Bellatrix.Execution`) | WinAppDriver / Selenium / Appium sessions, snapshots, narrow queries | know about agents or templates |
| Driver servers | WinAppDriver, Appium, a Selenium server or a local browser driver | — |

## Session model — no daemon

Every `bellatrix` invocation is a separate process. `start-session` creates the driver session and stores it in
`.bellatrix/sessions.db` (SQLite, in the current directory) under a **logical name per domain**
(`--session w1`). Each later command looks the name up and **re-attaches to the existing driver session by id**; no
new session is created and no background process is kept.

- Sessions are scoped per domain, so the same name can be used for desktop and web at once.
- Idle sessions are pruned after 10 minutes (desktop: the configured new-command timeout, default 10);
  `<domain> list-sessions` prunes and lists one domain, `bellatrix sessions [--prune]` all of them.
- For web, the CLI can start a local browser driver that matches the installed browser and stops it at
  `close-session`; with `--remote-url` it uses your Selenium server and never stops it. WinAppDriver, Appium and
  devices are always yours to run.

## Context economy

An AI agent's context is its scarcest resource; a full UI tree can be hundreds of kilobytes. So:

1. `bellatrix <domain> snapshot` writes the tree to `.bellatrix/snapshots/<session>-<ticks>.json` and returns only
   `{ path, elementCount, summary }` (the summary counts interactive elements per the domain's rules).
2. The agent hands `path` plus a description to the locator specialist subagent, which returns a single locator.
3. Where an agent has no subagents, it uses `bellatrix <domain> find --snapshot <path> --name|--text|--grep …`,
   which returns only the matching nodes.
4. `snapshot --inline`, `get-page-source` and `take-screenshot --inline` exist but are "context-heavy";
   `take-screenshot` saves a PNG and returns `{ path, width, height }` by default.
5. `BELLATRIX_TRACE=<file>` logs one JSON line per invocation and flags context-heavy calls, so a run can be audited.

Every scaffolded instruction fragment makes this mandatory: delegate, or query narrowly — never read a snapshot or
page source into the main context.

## The `init` contract (R1–R8)

`bellatrix init` writes into a repository that belongs to someone else, so it follows these rules:

| Rule | Meaning |
|---|---|
| R1 | Human-owned instruction files (`CLAUDE.md`, `AGENTS.md`, `GEMINI.md`) are never rewritten; BELLATRIX adds exactly one line or one `<!-- >>> bellatrix >>> -->` block. |
| R2 | All BELLATRIX instructions live in one fragment it owns: `.bellatrix/bellatrix.instructions.md` (or the agent's native always-on rule/steering file). |
| R3 | Registration is idempotent: one `@import` line (or one marker block), added once, replaced in place. |
| R4 | Skills are written as `<skills-dir>/bellatrix-<domain>-<name>/SKILL.md` (Agent Skills format: `name` = folder). |
| R5 | Subagents get prefixed, globally unique names (`bellatrix-<domain>-locator-specialist`, `…-system-tests-generator`). |
| R6 | A per-agent manifest (`.bellatrix/manifest.<agent>.json`) records a SHA-256 per owned file; on a re-run a file you edited is skipped and reported unless you pass `--regenerate`. Hashes ignore CRLF/LF differences. |
| R7 | No MCP server is registered. `init` only removes BELLATRIX's own stale MCP keys from the agents' MCP config files and keeps every other entry. |
| R8 | Re-runs are idempotent inside the BELLATRIX namespace only; files of other tools and of other agents are never touched. |

A custom template folder (`--template-source <dir> --prefix <p>`) follows the same rules in its own namespace
(`.bellatrix/<p>.instructions.md`, `<p>-…` names, `.bellatrix/<p>-manifest.<agent>.json`).

## Provider emitters

Content is provider-neutral (`src/Bellatrix.Cli/Templates/`):

- `bellatrix-instructions.md` — the fragment, with one block per domain;
- `core/<domain>/…` — the language-agnostic part of each skill and agent (CLI flow, locator ladder, SPECTRA bridge);
- `lang/<language>/<domain>/…` — the language part (framework API, code, build command), included at
  `{{> lang}}` / `{{> lang:<section>}}` markers.

Placement is per agent: one `AgentEmitter` subclass per agent in
`src/Bellatrix.Cli/Scaffolding/Emitters/ProviderEmitters.cs`, registered in `AgentSelection.All`. An emitter decides
only **where** each piece goes and **which frontmatter** the agent's loader expects:

- the fragment path and wrapper (e.g. Cursor `.mdc` with `alwaysApply`, Kiro steering with `inclusion: always`), or
  a host registration (Claude's `@import` line; the shared `AGENTS.md` pointer block);
- the skill directories the agent reads — the planner writes each skill once per set of agents that share a
  directory (`.claude/skills`, `.agents/skills`, …) and reports unavoidable duplicates as a warning;
- the subagent path and format (Markdown frontmatter, Codex TOML, Antigravity folder form) and the tool restriction
  that makes locator specialists read-only + shell;
- the MCP config files to clean and the repo markers used for auto-detection.

Everything else — the agent list in help, the conformance harness, the support matrix — is derived from
`AgentSelection.All`, `scripts/conformance/agents.json` and `docs/support-matrix.json`, and tests keep them equal.

## Verification levels

| Level | What | Where |
|---|---|---|
| Unit | routing, help drift, emitters + golden files, session store, snapshot queries, fixtures, public-safety guard | `tests/Bellatrix.Cli.Tests` (CI) |
| L1 | the installed CLI against real drivers, every step a separate process | `tests/Bellatrix.Cli.E2E`, `scripts/run-e2e.ps1` |
| L2 | each agent headless: discovers the skills and delegates locator work on recorded snapshots | `scripts/agent-conformance.ps1` → `docs/contributing/conformance-report.md` |
| L3 | a human runs a full golden path per language × domain × agent | `docs/contributing/LIVE-VERIFICATION-CHECKLIST.md` §C (see [CONTRIBUTING.md](../CONTRIBUTING.md)) |
