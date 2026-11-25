# bellatrix — the model-free automation CLI for AI coding agents

`bellatrix` is a .NET global tool that drives **desktop** (WinAppDriver), **web** (Selenium) and **mobile**
(Appium, Android and iOS) automation from the shell, one action per command. It runs no AI model and holds no
credentials: your AI coding agent does the thinking, and the CLI does the driving.

`bellatrix init` writes skills, subagents and an instruction file into your repository, in your agent's native
layout. They teach the agent the BELLATRIX framework (C#, Java or TypeScript), page objects and test structure, and
how to resolve stable locators without reading whole UI trees into its context.

## Install

You need the .NET 8 SDK (also for Java and TypeScript projects).

```
dotnet tool install -g Bellatrix.Cli      # update: dotnet tool update -g Bellatrix.Cli
bellatrix --version
bellatrix help
```

To install from the `.nupkg` attached to each GitHub release instead (pinned or offline):
[INSTALL-PREVIEW.md](https://github.com/AutomateThePlanet/BELLATRIX-AI-Agents/blob/main/docs/INSTALL-PREVIEW.md).

## Scaffold your AI agent

Run this in the root of your test repository:

```
bellatrix init --agent claude --language csharp --domains desktop
```

Then open the repository in your agent and ask it, for example, *"Automate TC-1006"*.

`--agent` takes one or more of `claude`, `copilot`, `cursor`, `devin`, `codex`, `gemini`, `antigravity`, `kiro`
and `opencode`, comma-separated. `--language csharp|java|typescript` picks the BELLATRIX flavor (it is detected
from your build files when omitted). Add `--dry-run` to preview the files.

| Agent | `--agent` |
|---|---|
| Claude Code | `claude` |
| GitHub Copilot | `copilot` |
| Cursor | `cursor` |
| Devin Desktop (formerly Windsurf) | `devin` |
| Codex CLI | `codex` |
| Gemini CLI (enterprise) | `gemini` |
| Antigravity CLI | `antigravity` (or `agy`) |
| Kiro CLI | `kiro` |
| OpenCode | `opencode` |

`init` finds your BELLATRIX test project (or takes `--project <path>`) and records it in `.bellatrix/config.json`;
commit that file. The skills read it, so they never guess a project name. Check it with `bellatrix config validate`.

Re-running `bellatrix init` refreshes what is already scaffolded and never overwrites a file you edited (or
`.bellatrix/config.json`) unless you pass `--regenerate`.

## What is supported

| Language | Desktop | Web | Mobile (Android + iOS) |
|---|---|---|---|
| C# | ✓ | ✓ | ✓ |
| Java | ✓ | ✓ | ✓ |
| TypeScript | — | ✓ | — |

TypeScript: web only (the BELLATRIX TypeScript framework has no desktop or mobile component layer).

## Drive the CLI

The driver, browser or device is yours to run (for web, the CLI starts a local browser driver itself). Every
command prints one line of JSON.

```
bellatrix web start-session --browser chrome --session w1
bellatrix web navigate --session w1 --url https://demos.bellatrix.solutions/
bellatrix web snapshot --session w1          # saved to .bellatrix/snapshots/ -> { path, elementCount, summary }
bellatrix web find --session w1 --snapshot <path> --text "Falcon 9"
bellatrix web click --session w1 --locator "<selector>"
bellatrix web close-session --session w1
```

Sessions persist by name across invocations. `bellatrix <domain> help --json` lists every verb with its flags and
output.

## Learn more

- Guide, CLI reference and what is supported: https://bellatrix.solutions/agent/
- Source code, issues and contributing: https://github.com/AutomateThePlanet/BELLATRIX-AI-Agents
- BELLATRIX framework documentation: https://docs.bellatrix.solutions/
- Release notes: https://github.com/AutomateThePlanet/BELLATRIX-AI-Agents/blob/main/CHANGELOG.md
- Related: SPECTRA (AI test case generation, https://github.com/AutomateThePlanet/Spectra) and Testimize (test data
  optimization, https://github.com/AutomateThePlanet/Testimize)
