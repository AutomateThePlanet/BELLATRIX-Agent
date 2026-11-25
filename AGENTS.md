# AGENTS.md — developing BELLATRIX Agent with an AI coding agent

These instructions are for AI agents (and people) **changing this repository**. They are not the instructions
`bellatrix init` scaffolds into users' repositories — those live in `src/Bellatrix.Cli/Templates/`.

## What this repository is

- `src/Bellatrix.Cli` — the `bellatrix` .NET global tool (package `Bellatrix.Cli`): command registry and handlers
  (`Commands/`), scaffolding (`Scaffolding/`, one `AgentEmitter` per AI agent), embedded templates (`Templates/`).
- `src/Bellatrix.Execution` — the engine: WinAppDriver / Selenium / Appium sessions, SQLite session store, snapshots.
- `tests/Bellatrix.Cli.Tests` — unit tests (no driver or AI model); `tests/Bellatrix.Cli.E2E` — live tests, skipped
  unless enabled; `tests/fixtures` — recorded and synthetic snapshots, a custom-template fixture.
- `scripts/` — E2E runner, fixture recorder, agent conformance harness, README matrix generator.
- `docs/` — testing guide, architecture, `support-matrix.json`; `docs/contributing/` — verification checklists and
  the conformance report (linked from CONTRIBUTING.md only).

Read [docs/architecture.md](docs/architecture.md) before changing scaffolding or the session model.

## Rules

1. **Build and unit-test only.** Run `dotnet build BELLATRIX-Agent.sln` and `dotnet test tests/Bellatrix.Cli.Tests`.
   Do **not** run live E2E tests, start or stop WinAppDriver, Appium, emulators or browsers, or run
   `scripts/agent-conformance.ps1` without `-DryRun` (it invokes paid AI models). Live checks are for humans:
   `docs/contributing/LIVE-VERIFICATION-CHECKLIST.md`.
2. **Golden files are intentional.** If a template or emitter change alters scaffold output, regenerate with
   `BELLATRIX_UPDATE_GOLDEN=1`, then read the golden diff and explain it in the pull request. Never regenerate to make
   an unexplained failure go away.
3. **Keep the drift tests green.** Commands and flags are declared once in `CommandSpec` tables
   (`Commands/*Commands.cs`); help, routing and the README/template command examples are checked against them
   (`HelpDriftTests`).
4. **Never hard-code the agent list.** Agents come from `AgentSelection.All`; the harness table
   (`scripts/conformance/agents.json`) and `docs/support-matrix.json` must list the same ids (tests enforce it).
   Languages likewise come from `LanguageSelection.Known`.
5. **Verify provider formats from official documentation** before changing an emitter, and cite the URLs.
6. **Templates must match the framework.** Every BELLATRIX API a skill mentions must exist in the current source of
   that language's framework (BELLATRIX, BELLATRIX-Java, BELLATRIX-JavaScript). Do not edit those framework repos from
   here.
7. **Public-safety.** This repository is public. No local machine paths, credentials, private names or private URLs;
   use public samples (Windows Calculator, Notepad, https://demos.bellatrix.solutions/, ApiDemos) and placeholders.
   `PublicSafetyGuardTests` enforces it.
8. **The `init` contract (R1–R9) is not negotiable:** never rewrite a human-owned file beyond one line or one marked
   block, one manifest per agent, skip user-edited files, register no MCP server, and never overwrite
   `.bellatrix/config.json` without `--regenerate` (only merge in missing values).
   Templates contain no project names or paths: they use the `{project.*}` / `{testCasesDir}` placeholders and read
   `.bellatrix/config.json`.
9. Use file-scoped namespaces and match the surrounding code's style; `.editorconfig` applies.
10. **Public docs show what is supported, not verification status.** README, the NuGet README and the website never
    say READY/PARTIAL or "not yet verified live"; that tracking lives only in `docs/support-matrix.json` `internal` and
    `docs/contributing/` (linked from CONTRIBUTING.md). `SupportMatrixReadmeTests` enforces it.

## Useful commands

```bash
dotnet run --project src/Bellatrix.Cli -- help --json
dotnet run --project src/Bellatrix.Cli -- init --agent claude,cursor --domains web --dry-run --json --out <scratch-dir>
powershell -File scripts/gen-readme-matrix.ps1 -Check
powershell -File scripts/agent-conformance.ps1 -Agent all -Language all -DryRun
```
