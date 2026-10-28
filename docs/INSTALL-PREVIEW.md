# Install the preview release (3.1.4)

`Bellatrix.Cli` 3.1.4 is published as a pre-release on
[GitHub Releases](https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.4): you download the
`.nupkg` into a folder and install the `bellatrix` .NET tool from that folder. NuGet.org package coming soon.

## Prerequisites

- **.NET SDK 8 or later** (`dotnet --version`), also for Java and TypeScript projects: the CLI is a .NET tool.
- **Access to this GitHub repository.**
- **GitHub CLI (`gh`)** logged in (`gh auth login`), or a browser to download the package by hand.

## 1. Download

PowerShell or bash:

```bash
gh release download v3.1.4 -R AutomateThePlanet/BELLATRIX-Agent -p "*.nupkg" -D ./bellatrix-pkg
```

Browser alternative: open the
[v3.1.4 release page](https://github.com/AutomateThePlanet/BELLATRIX-Agent/releases/tag/v3.1.4), download
`Bellatrix.Cli.3.1.4.nupkg` and save it into a new folder named `bellatrix-pkg`.

## 2. Check the download (optional)

The release also has `Bellatrix.Cli.3.1.4.nupkg.sha256`. Download it and compare it with the hash of your file:

```bash
gh release download v3.1.4 -R AutomateThePlanet/BELLATRIX-Agent -p "*.sha256" -D ./bellatrix-pkg
```

PowerShell:

```powershell
(Get-FileHash .\bellatrix-pkg\Bellatrix.Cli.3.1.4.nupkg -Algorithm SHA256).Hash.ToLower()
Get-Content .\bellatrix-pkg\Bellatrix.Cli.3.1.4.nupkg.sha256
```

bash:

```bash
cd bellatrix-pkg && sha256sum -c Bellatrix.Cli.3.1.4.nupkg.sha256 && cd ..
```

The two hashes must be equal (`sha256sum -c` prints `OK`).

## 3. Install, update, uninstall

Run these from the folder that contains `bellatrix-pkg`:

```bash
# Install:
dotnet tool install -g Bellatrix.Cli --add-source ./bellatrix-pkg --version 3.1.4
# Update an existing install:
dotnet tool update -g Bellatrix.Cli --add-source ./bellatrix-pkg --version 3.1.4
# Uninstall:
dotnet tool uninstall -g Bellatrix.Cli
# Verify:
bellatrix --version          # → {"version":"3.1.4"}
bellatrix help
```

## Troubleshooting

| Problem | Fix |
|---|---|
| `version not found` / `Unable to find package Bellatrix.Cli` | Check that `--add-source` points at the folder that holds the `.nupkg` (not the file itself) and that `--version` matches the file name. |
| An older package is still used | Clear the NuGet caches: `dotnet nuget locals all --clear`, then install again. |
| `bellatrix` is not recognized | Open a new terminal. If it is still missing, add the .NET tools folder to `PATH`: `%USERPROFILE%\.dotnet\tools` on Windows, `~/.dotnet/tools` on Linux and macOS. |
| `Tool 'bellatrix.cli' is already installed` (an older install, also one from a local feed) | Use `dotnet tool update -g Bellatrix.Cli --add-source ./bellatrix-pkg --version 3.1.4`. |

## First steps

In the root of your test repository, scaffold the BELLATRIX skills and subagents for your AI agent:

```bash
bellatrix init --agent <your agent> --language <lang>
```

`--agent` is one or more of `claude`, `copilot`, `cursor`, `devin`, `codex`, `gemini`, `antigravity`, `kiro`,
`opencode`; `--language` is `csharp`, `java` or `typescript`. Add `--dry-run` to preview the files.

Then try one copy-paste flow per domain from
[docs/TESTING.md](https://github.com/AutomateThePlanet/BELLATRIX-Agent/blob/main/docs/TESTING.md):
[desktop](https://github.com/AutomateThePlanet/BELLATRIX-Agent/blob/main/docs/TESTING.md#2-desktop--windows-calculator),
[web](https://github.com/AutomateThePlanet/BELLATRIX-Agent/blob/main/docs/TESTING.md#3-web--chrome-against-the-bellatrix-demo-shop),
[mobile](https://github.com/AutomateThePlanet/BELLATRIX-Agent/blob/main/docs/TESTING.md#4-mobile--android-emulator).

## Report an issue

Open an issue in [this repository](https://github.com/AutomateThePlanet/BELLATRIX-Agent/issues/new/choose) and
include the output of `bellatrix --version`, the command you ran and the JSON it printed.
