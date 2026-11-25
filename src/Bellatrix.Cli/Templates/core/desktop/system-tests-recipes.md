---
title: BELLATRIX Desktop System Tests Recipes
description: Reference skill with complete test examples and common patterns for BELLATRIX desktop system tests.
tags: [BELLATRIX, desktop, recipes, examples, reference]
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.desktop.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.desktop.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no desktop project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
