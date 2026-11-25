---
name: bellatrix-web-validations-and-assertions
description: Reference skill for BELLATRIX web Validate* methods (which component supports which), assertion patterns, and when to fall back to the test framework's own asserts.
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.web.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.web.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no web project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
