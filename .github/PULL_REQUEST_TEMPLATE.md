## What and why

<!-- What does this change, and which issue does it address? -->

## How it was verified

- [ ] `dotnet build BELLATRIX-Agent.sln` — 0 errors
- [ ] `dotnet test tests/Bellatrix.Cli.Tests` — all green
- [ ] Golden files regenerated (`BELLATRIX_UPDATE_GOLDEN=1`) only for intended changes, and the diff is explained below
- [ ] `scripts/gen-readme-matrix.ps1 -Check` passes (if `docs/support-matrix.json` changed)
- [ ] Live checks run, if any (which `docs/contributing/LIVE-VERIFICATION-CHECKLIST.md` items, with `bellatrix --version`)

## Checklist

- [ ] New or changed commands/flags are declared in their `CommandSpec` (help and docs stay in sync)
- [ ] No agent or language list is hard-coded outside `AgentSelection` / `LanguageSelection`
- [ ] Provider format changes cite the official documentation
- [ ] `CHANGELOG.md` updated under `Unreleased`
