---
title: BELLATRIX Desktop — Current Project Structure
description: As-is snapshot of your desktop test project tree. Ground CREATE vs EDIT decisions on this file. Refresh whenever a new Pages/, Components/, Models/, or Tests/ folder is added.
tags: [BELLATRIX, desktop, project-structure, reference, as-is]
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.desktop.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.desktop.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no desktop project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
