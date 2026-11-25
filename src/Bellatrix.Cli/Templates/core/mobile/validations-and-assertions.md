---
name: bellatrix-mobile-validations-and-assertions
description: Reference skill for BELLATRIX mobile Validate* methods (Android/iOS), which components support each, assertion patterns, and when to fall back to plain test-framework assertions.
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.mobile.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.mobile.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no mobile project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
