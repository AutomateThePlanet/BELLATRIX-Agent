---
title: BELLATRIX Desktop Test Structure and Lifecycle
description: Reference skill for BELLATRIX desktop test classes, App object, lifecycle configuration, and naming conventions.
tags: [BELLATRIX, desktop, test-structure, lifecycle, reference]
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.desktop.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.desktop.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no desktop project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
