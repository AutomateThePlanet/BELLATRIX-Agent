# Contributing to BELLATRIX Agent

Thanks for helping. This repository holds the `bellatrix` CLI (`src/Bellatrix.Cli`), its execution engine
(`src/Bellatrix.Execution`), the skill and agent templates `bellatrix init` scaffolds, and their tests. Please read the
[Code of Conduct](CODE_OF_CONDUCT.md) first.

## Build and test

You need the .NET 8 SDK (or newer). Windows is the primary platform: desktop automation needs WinAppDriver.

```bash
dotnet build BELLATRIX-Agent.sln
dotnet test tests/Bellatrix.Cli.Tests          # unit tests: no driver, browser, device or AI model needed
dotnet test tests/Bellatrix.Cli.E2E            # live E2E: every test is SKIPPED unless enabled (below)
```

CI (`.github/workflows/ci.yml`) runs the build and the unit tests on every pull request. A pull request must keep both
green.

## Golden files and fixtures

- **Scaffold golden files** (`tests/Bellatrix.Cli.Tests/Golden/`) pin the files `init` writes per agent and language.
  When you change a template or an emitter on purpose, regenerate them and review the diff:

  ```powershell
  $env:BELLATRIX_UPDATE_GOLDEN = "1"; dotnet test tests/Bellatrix.Cli.Tests; Remove-Item Env:\BELLATRIX_UPDATE_GOLDEN
  ```

- **Snapshot fixtures** (`tests/fixtures/snapshots/`) are produced by the real page-source parser. Regenerate them with
  `BELLATRIX_UPDATE_FIXTURES=1` the same way. `scripts/record-fixtures.ps1` re-records them from live drivers.

## Live end-to-end tests

The E2E suite runs the installed `bellatrix` (or `BELLATRIX_E2E_EXE`) as separate processes against real drivers.
It never starts or stops WinAppDriver, Appium or an emulator. Enable it per domain:

| Variable | Meaning |
|---|---|
| `BELLATRIX_E2E=1` | master switch |
| `BELLATRIX_E2E_DESKTOP=1` | desktop (WinAppDriver on `http://127.0.0.1:4723`, Windows Calculator) |
| `BELLATRIX_E2E_WEB=1` | web with a local browser driver (Chrome) |
| `BELLATRIX_E2E_WEB_REMOTE=1` | web through a Selenium server (`BELLATRIX_E2E_SELENIUM_URL`, default `http://localhost:4444`) |
| `BELLATRIX_E2E_MOBILE=1` | Android through Appium (`BELLATRIX_E2E_APK` = path to `ApiDemos-debug.apk`) |

`scripts/run-e2e.ps1` probes what is reachable, sets the matching variables and writes a report to `artifacts/e2e/`.

## Guard and drift tests

- **`PublicSafetyGuardTests`** fail when any tracked file contains a local machine path, a secret-shaped string, a link
  to a repository that is not public, or an email address outside `example.com`/`example.org`/`example.net` and
  `bellatrix.solutions`. The rules live in `tests/Bellatrix.Cli.Tests/guard-patterns.json`. Use public sample apps in
  examples (Windows Calculator, Notepad, https://demos.bellatrix.solutions/, ApiDemos) and placeholders such as
  `<path>` instead of real paths.
- **`HelpDriftTests`** keep the router, `bellatrix help --json`, the flags each handler reads, and every
  `bellatrix …` command shown in the templates and the README in sync. Add or change a verb or flag in its
  `CommandSpec` (`src/Bellatrix.Cli/Commands/*Commands.cs`), never only in a handler.
- **`SupportMatrixReadmeTests`** keep the README matrices equal to `docs/support-matrix.json`. Edit the JSON, then run
  `powershell -File scripts/gen-readme-matrix.ps1` (`-Check` verifies without writing).
- **`ConformanceConfigTests`** keep `scripts/conformance/agents.json` and `languages.json` equal to the CLI's own
  agent and language lists.

## Adding an AI agent

The agent list is data-driven: never hard-code a list of agents elsewhere.

1. Verify the agent's formats from its **official documentation**: instruction file(s) and import syntax, skills path,
   subagent path and frontmatter (including how to restrict tools to read-only + shell), delegation syntax, headless
   command, MCP config file, and repo markers. Put the URLs in the pull request.
2. Add an `AgentEmitter` subclass in `src/Bellatrix.Cli/Scaffolding/Emitters/ProviderEmitters.cs` (placement and
   frontmatter only — content stays provider-neutral in `Templates/`) and register it in
   `AgentSelection.All` (`src/Bellatrix.Cli/Scaffolding/AgentSelection.cs`).
3. Add the agent to `scripts/conformance/agents.json` (verified headless invocation, docs URLs, the paid-usage `cost` note) and to
   `docs/support-matrix.json` (`agents[]` plus one cell per language × domain, `PARTIAL` until verified live), then
   regenerate the README matrices.
4. Add a golden-file case (`ScaffoldTargetTests.Golden_PerAgentDesktopLayout`), auto-detect markers, and a section in
   `docs/LIVE-VERIFICATION-CHECKLIST.md` §A.
5. Respect the `init` contract (see [docs/architecture.md](docs/architecture.md)): never rewrite a human-owned file
   beyond one line or one marked block, one manifest per agent, no MCP server registration.

## Adding a language layer

1. Add `src/Bellatrix.Cli/Templates/lang/<language>/<domain>/…` files next to the language-agnostic
   `Templates/core/<domain>/…` parts. A core template includes the whole language file at a `{{> lang}}` line, or one
   `<!-- lang:<section> -->` section at each `{{> lang:<section>}}` line (see `TemplateCatalog`).
2. Add the language to `LanguageSelection.Known` (and to `Limits` for any domain the framework does not cover), to
   `scripts/conformance/languages.json`, and to `docs/support-matrix.json`.
3. Every framework API a template mentions must exist in that framework's current source. Generate a sample project
   from the templates and make sure it **compiles**; see
   [docs/LANGUAGE-VERIFICATION-CHECKLIST.md](docs/LANGUAGE-VERIFICATION-CHECKLIST.md).

## Pull requests

- One topic per pull request; describe what changed and how you verified it.
- Add or update tests with every behavior change, and update `CHANGELOG.md` under an `Unreleased` heading.
- Do not commit generated folders (`bin/`, `obj/`, `artifacts/`, `.bellatrix/`).

## License

By contributing, you agree that your contributions are licensed under the [Apache License 2.0](LICENSE).
