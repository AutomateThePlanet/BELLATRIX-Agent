# Security Policy

## Supported versions

Security fixes are released for the latest minor version of `Bellatrix.Cli` on NuGet.org. Please update with
`dotnet tool update -g Bellatrix.Cli` before reporting.

## Reporting a vulnerability

**Please do not report security vulnerabilities in public issues, discussions or pull requests.**

Report them privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**
(GitHub private vulnerability reporting). Include:

- the `bellatrix --version` output and your OS;
- the command(s) or files involved and the steps to reproduce;
- the impact you expect (for example: a scaffolded file written outside the repository, a credential exposed in
  output or in the `BELLATRIX_TRACE` log, a command injection through a flag value).

We aim to acknowledge a report within 5 business days and to agree on a disclosure timeline with you.

## Scope notes

- `bellatrix` runs no AI model and stores no credentials. It talks only to the driver server you point it at
  (WinAppDriver, Appium, Selenium or the local browser driver it starts) and writes under `.bellatrix/` in the current
  directory.
- `bellatrix init` writes only into the target repository: its own files, plus one line or one marked block in
  instruction files such as `CLAUDE.md` or `AGENTS.md`, and it removes only BELLATRIX's own MCP server entries.
- Vulnerabilities in the AI coding agents themselves, or in WinAppDriver, Appium or Selenium, should be reported to
  those projects.
