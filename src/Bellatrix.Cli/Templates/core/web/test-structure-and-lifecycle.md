---
name: bellatrix-web-test-structure-and-lifecycle
description: Reference skill for BELLATRIX web test classes, the App object, the Browser attribute and lifecycle configuration, setup hooks, and naming conventions.
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.web.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.web.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no web project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
