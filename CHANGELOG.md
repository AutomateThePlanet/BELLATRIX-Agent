# Changelog

All notable changes to `Bellatrix.Cli` (the `bellatrix` command) are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions before 3.1.2 were distributed as packages outside NuGet.org; 3.1.2 is the first version from this
repository. No version is on NuGet.org yet: releases are published on
[GitHub Releases](https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases) (the first one is 3.1.4) and installed
from the downloaded `.nupkg` with `--add-source` ([docs/INSTALL-PREVIEW.md](docs/INSTALL-PREVIEW.md)).

## [Unreleased]

### Added
- `.bellatrix/config.json`: the project settings for your repository (the test project per domain: path, name, root
  namespace or base package, build command; and the test-case folder), with a JSON schema
  (`schemas/bellatrix-config.v1.schema.json`, `"version": 1`). Commit it.
- `bellatrix init` finds the BELLATRIX test project per domain (C# `*.csproj`, Java `pom.xml` / `build.gradle`,
  TypeScript `package.json` that reference BELLATRIX; the framework's own projects are skipped), proposes it and, in
  a terminal, asks you to confirm. New flags `--project <path>` / `--project desktop=<path>,web=<path>` and
  `--test-cases-dir <dir>` (default: SPECTRA's `tests.dir`, else `test-cases`). Without a terminal, several
  candidates are a usage error (exit 2) and nothing is written.
- `bellatrix config show` and `bellatrix config validate`.
- The init JSON has a `config` field (path, status, values, commit, warnings).

### Fixed
- The README, the NuGet README and this changelog no longer say the package is on NuGet.org. It is not published
  there yet; install it from GitHub Releases (`dotnet tool install -g Bellatrix.Cli --add-source ./bellatrix-pkg`).
- The skills no longer hard-code a sample project (`Demo.DesktopTests`, `MyProject.WebTests`, `demo-desktop-tests`,
  `my-web-tests`, …) or its build command. They use `{project.*}` placeholders from `.bellatrix/config.json` and ask
  you when it has no project.
- One test-case convention in every skill: `{testCasesDir}/{suite}/TC-{id}.md`. Previously some skills read
  `tests/{suite}/…` and others `test-cases/{suite}/…`. The folder is recorded in `.bellatrix/config.json`
  (default: SPECTRA's `tests.dir`, else `test-cases`).

### Changed
- `--no-interaction` now has an effect: `init` is the one command that may prompt, and only in a terminal.
- `init` contract rule R9: re-running `init` / `update-skills` never overwrites `.bellatrix/config.json` without
  `--regenerate`; missing values are merged in, an invalid file is left untouched.

## [3.1.4] - 2026-10-28

Released on GitHub Releases (NuGet.org package coming soon). Install it with
[docs/INSTALL-PREVIEW.md](https://github.com/AutomateThePlanet/BELLATRIX-Agent/blob/main/docs/INSTALL-PREVIEW.md).

### Added
- `docs/INSTALL-PREVIEW.md`: download, integrity check, install, update, uninstall and troubleshooting for the preview
  package.
- Release workflow (`.github/workflows/release.yml`): pushing a `v*` tag builds, runs the unit tests, packs, and
  creates a GitHub release (marked Latest) with the `.nupkg`, its `.sha256` file and the release notes from this changelog.

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

[3.1.4]: https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.4
[3.1.3]: https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.3
[3.1.2]: https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.2
