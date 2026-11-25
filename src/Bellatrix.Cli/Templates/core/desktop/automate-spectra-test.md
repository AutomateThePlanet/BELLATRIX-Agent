---
title: BELLATRIX Desktop - Automate SPECTRA Test Case
description: Skill for automating a SPECTRA manual test case as a BELLATRIX desktop system test — reads TC steps, executes scenario via the bellatrix desktop CLI, generates code with TC-ID traceability, and updates SPECTRA coverage.
tags: [BELLATRIX, desktop, SPECTRA, automation, traceability, coverage]
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.desktop.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.desktop.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no desktop project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
