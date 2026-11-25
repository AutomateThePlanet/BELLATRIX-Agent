---
name: bellatrix-web-project-structure
description: As-is snapshot of the BELLATRIX web test project tree plus the CREATE-vs-EDIT decision table. Ground every CREATE vs EDIT decision on it and refresh it whenever a Pages/, Sections/, Components/, Models/, or Tests/ entry is added.
---

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.web.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.web.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no web project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang}}
