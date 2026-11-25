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
   | Policy name | `BELLATRIX-AI-Agents release` |
   | Package owner | `angelovstanton` |
   | Repository owner | `AutomateThePlanet` |
   | Repository | `BELLATRIX-AI-Agents` |
   | Workflow file | `release.yml` (the file name only, not the path) |
   | Environment | `release` |

   The `user:` input of the `NuGet/login` step in `release.yml` must be the same nuget.org account (`angelovstanton`).
   If you change either, change both.
3. **Package id.** `Bellatrix.Cli` is created by the first push; the `Bellatrix.` id prefix is not reserved, and every
   existing `Bellatrix.*` package is owned by `angelovstanton`.

## Every release

Work through this list in order; the release is published by the tag push, so everything before step 6 happens on
`main` first.

1. **Version.** `<Version>` in `src/Bellatrix.Cli/Bellatrix.Cli.csproj` is the new version (for example `3.2.0`).
   The workflow fails if the tag and this version differ.
2. **Config schema tag.** `schemas/bellatrix-config.v1.schema.json` `$id` and the README example `$schema` use
   `https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-AI-Agents/v<version>/schemas/bellatrix-config.v1.schema.json`.
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
   Open the `.nupkg` (a zip) and check the `.nuspec`, `README.md` and `bellatrix_icon.png`. Run the live checks in
   [LIVE-VERIFICATION-CHECKLIST.md](LIVE-VERIFICATION-CHECKLIST.md) (at least B.1, C.1 and D).
6. **Tag.** On the release commit on `main`:
   ```bash
   git tag v<version>
   git push origin v<version>
   ```
7. **Watch the workflow** (Actions → Release; approve the `release` environment if you made it require a reviewer).
   Order: version check → the tag serves the schema → build → unit tests → README matrices → pack → SHA-256 and
   notes → NuGet login → `dotnet nuget push --skip-duplicate` → `gh release create`. Nothing is published before the
   checks pass. A failed run can be re-run: the push skips a version that is already on NuGet.org.
8. **After the release.**
   - `https://www.nuget.org/packages/Bellatrix.Cli/<version>` exists (validation and indexing take a few minutes,
     up to about an hour before `dotnet tool install` sees it).
   - `dotnet tool update -g Bellatrix.Cli` (or `install`), then `bellatrix --version`.
   - The GitHub release is marked Latest and has `Bellatrix.Cli.<version>.nupkg` and `.nupkg.sha256`;
     the `--add-source` install in `docs/INSTALL-PREVIEW.md` works with them.
   - `curl -fsS https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-AI-Agents/v<version>/schemas/bellatrix-config.v1.schema.json`
     returns the schema, and `bellatrix init --dry-run` in a scratch repository shows that URL in
     `config.values.$schema`.
