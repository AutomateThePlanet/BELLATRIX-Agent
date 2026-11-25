# Release checklist

Releases are tag-driven: pushing `v<version>` runs `.github/workflows/release.yml`, which builds, tests, packs and
creates the GitHub release. Nothing goes to NuGet.org. Work through this list in order; the release is published by
the tag push, so everything before step 6 happens on `main` first.

1. **Version.** `<Version>` in `src/Bellatrix.Cli/Bellatrix.Cli.csproj` is the new version (for example `3.2.0`).
2. **Config schema tag.** `schemas/bellatrix-config.v1.schema.json` `$id` and the README example `$schema` use
   `https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/v<version>/schemas/bellatrix-config.v1.schema.json`.
   Every `.bellatrix/config.json` that `bellatrix init` writes points there, so that URL must work once the release
   is out. (`BellatrixConfigTests.GeneratedConfig_SchemaUrl_IsTheReleaseTagOfThePackageVersion` keeps the version, the
   `$id` and the README in step.)
3. **Changelog.** Give the `## [<version>]` section its date (`## [3.2.0] - YYYY-MM-DD`), remove its "Not released
   yet" line, and keep an empty `## [Unreleased]` above it.
4. **Install docs.** Change `3.1.4`/`v3.1.4` to the new version in `README.md`, `docs/INSTALL-PREVIEW.md` (the workflow
   fails without `--version <version>` there) and `docs/TESTING.md`. The Package README already names the version it
   ships with.
5. **Checks.** `dotnet build BELLATRIX-Agent.sln`, `dotnet test tests/Bellatrix.Cli.Tests`,
   `powershell -File scripts/gen-readme-matrix.ps1 -Check`, and the live checks in
   [LIVE-VERIFICATION-CHECKLIST.md](LIVE-VERIFICATION-CHECKLIST.md) (at least B.1, C.1 and D).
6. **Tag.** `git tag v<version>` on the release commit and `git push origin v<version>`.
7. **The tag exists and serves the schema — before the release is published.**
   - `git ls-remote --tags origin v<version>` prints the tag.
   - `curl -fsS https://raw.githubusercontent.com/AutomateThePlanet/BELLATRIX-Agent/v<version>/schemas/bellatrix-config.v1.schema.json`
     returns the schema, and its `$id` is that same URL.
   - The release workflow checks this too (step "Config schema is served at this tag") and stops before
     `gh release create` if it fails.
8. **After the release.** Download the `.nupkg` from the release, install it as `docs/INSTALL-PREVIEW.md` says, run
   `bellatrix --version`, and run `bellatrix init --dry-run` in a scratch repository: `config.values.$schema` names the
   new tag.
