# Release checklist

Releases are tag-driven: pushing `v<version>` runs `.github/workflows/release.yml`, which builds, tests and packs
`Bellatrix.Cli`, pushes it to NuGet.org with **Trusted Publishing**, and creates the GitHub release with the `.nupkg`
and its `.sha256` attached. No NuGet API key is stored anywhere: the `NuGet/login` action exchanges the run's GitHub
OIDC token for a NuGet key that is valid for one hour.

## One-time setup

1. **GitHub environment.** Repository → Settings → Environments → New environment → name `release`.
   Recommended: Deployment branches and tags → "Selected branches and tags" → add the tag rule `v*`; optionally add
   yourself as a required reviewer so every publish waits for your approval.
2. **nuget.org Trusted Publishing policy.** Sign in to nuget.org as the package owner (`angelovstanton`) → your user
   name → Trusted Publishing → Add policy:

   | Field | Value |
   |---|---|
   | Policy name | `BELLATRIX-Agent release` |
   | Package owner | `angelovstanton` |
   | Repository owner | `AutomateThePlanet` |
   | Repository | `BELLATRIX-Agent` |
   | Workflow file | `release.yml` (the file name only, not the path) |
   | Environment | `release` |

   The `user:` input of the `NuGet/login` step in `release.yml` must be the same nuget.org account (`angelovstanton`).
   If you change either, change both.
3. **Package id.** `Bellatrix.Cli` is created by the first push; the `Bellatrix.` id prefix is not reserved, and every
   existing `Bellatrix.*` package is owned by `angelovstanton`.

## Every release

Work through this list in order; the release is published by the tag push, so everything before step 7 happens on
`main` first.

1. **Version.** `<Version>` in `src/Bellatrix.Cli/Bellatrix.Cli.csproj` is the new version (for example `3.2.0`).
   The workflow fails if the tag and this version differ.
2. **Config schema tag.** `schemas/bellatrix-config.v1.schema.json` `$id` and the README example `$schema` use
   `https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/v<version>/schemas/bellatrix-config.v1.schema.json`.
   Every `.bellatrix/config.json` that `bellatrix init` writes points there, so that URL must work once the release
   is out. (`BellatrixConfigTests.GeneratedConfig_SchemaUrl_IsTheReleaseTagOfThePackageVersion` keeps the version, the
   `$id` and the README in step.)
3. **Changelog.** Give the `## [<version>]` section its date (`## [3.2.0] - YYYY-MM-DD`), remove its "Not released
   yet" line, keep an empty `## [Unreleased]` above it, and add the `[<version>]:` link at the bottom.
4. **Install docs.** Change the version in `docs/INSTALL-PREVIEW.md` (the workflow fails without
   `--version <version>` there) and `docs/TESTING.md`.
5. **Checks.**
   ```bash
   dotnet build BELLATRIX-Agent.sln
   dotnet test tests/Bellatrix.Cli.Tests
   powershell -File scripts/gen-readme-matrix.ps1 -Check
   dotnet pack src/Bellatrix.Cli/Bellatrix.Cli.csproj -c Release -o artifacts/nuget -p:Version=<version>
   ```
   Open the `.nupkg` (a zip) and check the `.nuspec`, `README.md` and `bellatrix_icon.png`.
6. **Manual E2E checks** (a human, on the release candidate; details and expected results in
   [LIVE-VERIFICATION-CHECKLIST.md](LIVE-VERIFICATION-CHECKLIST.md)). **Record the screen during C.1 for the docs
   screenshots.** Note the version and date with every result.
   1. **Install the candidate and check the version.** From the repository root (PowerShell):
      ```powershell
      dotnet pack src/Bellatrix.Cli/Bellatrix.Cli.csproj -c Release -o artifacts/rc -p:Version=<version>
      dotnet tool update -g Bellatrix.Cli --add-source ./artifacts/rc --version <version>   # installs it if missing
      bellatrix --version          # → {"version":"<version>"}
      ```
   2. **D.1–D.8, config and interactive `init`**, in Windows Terminal, one empty scratch folder per step. Setup:
      ```powershell
      $d = Join-Path $env:TEMP "bx-d"; New-Item -ItemType Directory -Force "$d\src\Acme.UiTests", "$d\qa\Acme.Other" | Out-Null; Set-Location $d
      $proj = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Bellatrix.Desktop" Version="1.0.0" /></ItemGroup></Project>'
      Set-Content src\Acme.UiTests\Acme.UiTests.csproj $proj
      ```
      - D.1 Confirm: `bellatrix init --agent claude --language csharp --domains desktop`, press Enter.
      - Now add the second project for D.2–D.6: `Set-Content qa\Acme.Other\Acme.Other.csproj $proj`.
      - D.2 Reject + manual path: delete `.bellatrix`, run the D.1 command, type `nope\X.csproj`, then
        `qa\Acme.Other\Acme.Other.csproj`.
      - D.3 Several candidates (both projects): the D.1 command, type `2`.
      - D.4 Ctrl+C: the D.1 command, press Ctrl+C at the prompt; `dir -Force` shows no `.bellatrix`, `.claude`,
        `CLAUDE.md`.
      - D.5 Never prompts (both projects; each exits 2 at once and writes nothing):
        `$env:CI="true"; bellatrix init --agent claude --language csharp --domains desktop; Remove-Item Env:CI`,
        `bellatrix init --agent claude --language csharp --domains desktop --dry-run`,
        `bellatrix init --agent claude --language csharp --domains desktop --no-interaction`.
      - D.6 Re-run keeps your edits: after D.1, edit `buildCommand` in `.bellatrix\config.json`, then
        `bellatrix update-skills` and `bellatrix init --project desktop=qa\Acme.Other\Acme.Other.csproj` (unchanged),
        then `bellatrix init --regenerate --project desktop=qa\Acme.Other\Acme.Other.csproj` (replaced).
      - D.7 Validate: `bellatrix config validate --json`; delete `"name"` (exit 1, `config_invalid`); delete the file
        (`config_missing`); `bellatrix config show --json`.
      - D.8 The agent asks: in a repo with no BELLATRIX project, `bellatrix init --agent claude --domains desktop`, then
        ask Claude Code "Automate TC-1001"; it must ask you which project to use and record it.
   3. **C.1 golden path, Claude Code × C# × desktop, TC-1001 on Calculator. Record the screen.** WinAppDriver
      running on `http://127.0.0.1:4723`; a scratch copy of a BELLATRIX desktop test repo with
      `test-cases/unit-converter/TC-1001.md` (text in the live checklist, section C):
      ```powershell
      $env:BELLATRIX_TRACE = "$PWD\trace.jsonl"
      $env:McpSettings__Apps__0__Name="calculator"; $env:McpSettings__Apps__0__Path="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"
      bellatrix init --agent claude --language csharp --domains desktop --regenerate
      bellatrix config show --json
      claude
      ```
      Give the exact C.1 prompt from the live checklist, let the agent build, then run the generated test yourself
      (`dotnet test <desktop test project> --filter "FullyQualifiedName~UnitConverter"`) and check that
      `Select-String '"contextHeavy":true' trace.jsonl` prints nothing. Fill in the C.1 results table.
   4. **Recommended if the environment is ready: B.1 (desktop, Calculator) and B.2 (web, Chrome without a Selenium
      server).** Each line is its own process, run from the same folder:
      ```powershell
      $env:McpSettings__Apps__0__Name="calculator"; $env:McpSettings__Apps__0__Path="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"
      bellatrix desktop start-session --app calculator --session calc --json
      bellatrix desktop snapshot --session calc --json
      bellatrix desktop find --session calc --snapshot <path> --automation-id num7Button --json
      bellatrix desktop click --session calc --locator "//*[@AutomationId='num7Button']" --json
      bellatrix desktop get-text --session calc --locator "//*[@AutomationId='CalculatorResults']" --json
      bellatrix desktop close-session --session calc --json

      bellatrix web start-session --browser chrome --session w1 --json
      bellatrix web navigate --session w1 --url https://demos.bellatrix.solutions/ --json
      bellatrix web snapshot --session w1 --json
      bellatrix web find --session w1 --snapshot <path> --text "Falcon 9" --json
      bellatrix web click --session w1 --locator "[data-product_id*='28']" --json
      bellatrix web close-session --session w1 --json
      ```
7. **Tag.** On the release commit on `main`:
   ```bash
   git tag v<version>
   git push origin v<version>
   ```
8. **Watch the workflow** (Actions → Release; approve the `release` environment if you made it require a reviewer).
   Order: version check → the tag serves the schema → build → unit tests → README matrices → pack → SHA-256 and
   notes → NuGet login → `dotnet nuget push --skip-duplicate` → `gh release create`. Nothing is published before the
   checks pass. A failed run can be re-run: the push skips a version that is already on NuGet.org.
9. **After the release.**
   - `https://www.nuget.org/packages/Bellatrix.Cli/<version>` exists (validation and indexing take a few minutes,
     up to about an hour before `dotnet tool install` sees it).
   - `dotnet tool update -g Bellatrix.Cli` (or `install`), then `bellatrix --version`.
   - The GitHub release is marked Latest and has `Bellatrix.Cli.<version>.nupkg` and `.nupkg.sha256`;
     the `--add-source` install in `docs/INSTALL-PREVIEW.md` works with them.
   - `curl -fsS https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/v<version>/schemas/bellatrix-config.v1.schema.json`
     returns the schema, and `bellatrix init --dry-run` in a scratch repository shows that URL in
     `config.values.$schema`.
