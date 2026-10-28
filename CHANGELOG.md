# Changelog

All notable changes to `Bellatrix.Cli` (the `bellatrix` command) are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions before 3.1.2 were distributed as packages outside NuGet.org; 3.1.2 is the first NuGet.org release and the
first release from this repository.

## [Unreleased]

### Changed
- The README, the NuGet README and the website show what is supported: a language × domain table (✓ / —) and the
  nine agents with the files `bellatrix init --agent <name>` writes. Verification status (READY/PARTIAL, live-check
  notes) is no longer shown there.
- `docs/support-matrix.json` (schema 2): public fields `languages`, `domains`, `agents`, `support`, `footnotes`, plus an
  `internal` block with the per-cell status and evidence. `scripts/gen-readme-matrix.ps1` and
  `scripts/export-website-data.ps1` read only the public fields; the website export no longer copies `internal`.
- The live and language verification checklists and the conformance report moved to `docs/contributing/` and are
  linked from CONTRIBUTING.md only.

## [3.1.3] - 2026-10-28

### Fixed
- `--help`, `-h` and `help` on any command print that command's help and exit 0 with no side effects. Previously
  `bellatrix init --help` ran a real install, and a verb with `--help` ran the verb.

### Added
- Per-command help: `bellatrix <command> --help`, `bellatrix <domain> <verb> --help`, `bellatrix help <command>` and
  `bellatrix help <domain> <verb>`; add `--json` for the machine-readable form.

## [3.1.2] - 2026-10-27

### Added
- Public repository with CI, contribution guide, architecture notes and a public-safety guard test.
- Package metadata for NuGet.org: readme, icon, Apache-2.0 license, project and repository URLs, SourceLink.

### Changed
- Template and documentation examples use public sample apps only (Windows Calculator, Notepad, the BELLATRIX demo
  shop, ApiDemos).
- Scripts no longer assume machine-specific default paths (`-Website`, `-Apk` / `BELLATRIX_E2E_APK`).

### Removed
- A hidden, deprecated alias for a former single-purpose template command. Use
  `bellatrix init --template-source <dir> --prefix <prefix>`.

## [3.1.0] - 2026-10-27

### Added
- Three more AI agents: Antigravity CLI (`--agent antigravity`, alias `agy`), Kiro CLI (`--agent kiro`) and OpenCode
  (`--agent opencode`), for nine in total.
- A paid-usage notice per agent in the agent conformance harness.

### Changed
- **Behavior change:** `take-screenshot` saves a PNG under `.bellatrix/screenshots/` (or `--out <file>`) and returns
  `{path, width, height}`; the base64 image is returned only with `--inline`.
- Web and mobile snapshot summaries count interactive elements with their own domain's rules (web: `button`,
  `a[href]`, `input`, `select`, `textarea` and interactive ARIA roles).
- Gemini CLI is documented as enterprise-only (Gemini Code Assist).

### Fixed
- `init` no longer fails when an agent's MCP config file is not valid JSON; such files are left untouched.

## [3.0.1] - 2026-10-27

### Added
- `bellatrix help [--json]` and `bellatrix <domain> help [--json]`, generated from the same command registry the
  router uses.
- `BELLATRIX_TRACE=<file>`: one JSON line per invocation (`--text` values redacted, context-heavy calls flagged).
- Live CLI end-to-end suite (`scripts/run-e2e.ps1`), headless agent conformance harness
  (`scripts/agent-conformance.ps1`) and `docs/support-matrix.json` as the single source of the support matrix.
- `bellatrix init --template-source <dir> [--prefix <p>]` to render your own skill and agent templates with the same
  contract.

### Fixed
- The local chromedriver now matches the installed Chrome.
- A piped `web start-session` no longer hangs.
- `init`, `help` and `version` no longer create `.bellatrix/sessions.db`.

## [3.0.0] - 2026-10-26

### Added
- `--agent` × `--language` × `--domains` scaffolding: native layouts for Claude Code, GitHub Copilot, Cursor, Devin,
  Codex CLI and Gemini CLI; BELLATRIX skills for C# and Java (desktop, web, mobile) and TypeScript (web), each
  compile-verified against the framework source.
- Support matrix (language × domain × agent) in the README.

[3.1.3]: https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.3
[3.1.2]: https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.2
