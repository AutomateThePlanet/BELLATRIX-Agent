---
title: BELLATRIX Desktop Locators and Waits
description: Reference skill for BELLATRIX desktop find strategies, XPath patterns, WinAppDriver rules, and wait strategies.
tags: [BELLATRIX, desktop, locators, XPath, waits, reference]
---

# BELLATRIX Desktop Locators and Waits Reference

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.desktop.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.desktop.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no desktop project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

{{> lang:find}}
## WinAppDriver XPath Rules (CRITICAL)

**WinAppDriver requires PascalCase for both element tags and attributes. Lowercase WILL FAIL.**

### Correct vs. Incorrect

```text
CORRECT — PascalCase tags and attributes
//Button[@AutomationId='btnOk']
//Edit[@Name='Username']

WRONG — lowercase FAILS in WinAppDriver
//button[@automationid='btnOk']
//edit[@name='Username']
```

### PascalCase Element Tags

`Button`, `Edit`, `ComboBox`, `Text`, `Pane`, `Window`, `CheckBox`, `RadioButton`, `List`, `ListItem`, `Tab`, `TabItem`, `Menu`, `MenuItem`, `ToolBar`, `StatusBar`, `Tree`, `TreeItem`, `Image`, `Hyperlink`, `Document`, `Table`, `Custom`

### PascalCase Attributes

`AutomationId`, `Name`, `ClassName`, `ControlType`, `HelpText`, `Value.Value`, `IsEnabled`, `IsOffscreen`, `HasKeyboardFocus`

### Formatting Rules

1. **PascalCase tags**: `//Button` not `//button`
2. **PascalCase attributes**: `@AutomationId` not `@automationid`
3. **Single quotes for values**: `@Name='OK'` not `@Name="OK"`
4. **Simple predicates**: Use single attribute matches when possible
5. **No functions**: Avoid `contains()`, `normalize-space()` — WinAppDriver support is limited
6. **Direct paths**: Use `//Element[@Attr='Value']` pattern

---

## Common Windows Control Type XPaths

| Control | XPath Example |
|---------|---------------|
| Button | `//Button[@AutomationId='btnSubmit']` |
| TextBox (Edit) | `//Edit[@Name='Username']` |
| ComboBox (Dropdown) | `//ComboBox[@AutomationId='cmbCountry']` |
| Label (Text) | `//Text[@Name='Welcome']` |
| CheckBox | `//CheckBox[@Name='Remember me']` |
| RadioButton | `//RadioButton[@Name='Option 1']` |
| ListBox (List) | `//List[@AutomationId='lstItems']` |
| ListItem | `//ListItem[@Name='Item 1']` |
| TabControl (Tab) | `//Tab[@AutomationId='tabMain']` |
| TabItem | `//TabItem[@Name='Settings']` |
| MenuItem | `//MenuItem[@Name='File']` |
| Window | `//Window[@Name='Application Title']` |
| Pane | `//Pane[@AutomationId='mainPanel']` |
| Tree | `//Tree[@AutomationId='treeView']` |
| TreeItem | `//TreeItem[@Name='Folder']` |
| DataGrid (Table) | `//Table[@AutomationId='dataGrid']` |

---

## Anti-Patterns (NEVER Use)

| Anti-Pattern | Example | Why |
|-------------|---------|-----|
| Position-based selectors | `//Button[1]`, `//Edit[2]` | Position changes easily |
| Lowercase tags/attributes | `//button[@automationid='btn']` | WinAppDriver rejects lowercase |
| Complex XPath functions | `contains(@Name, 'Submit')` | Limited WinAppDriver support |
| Multiple conditions | `//Button[@AutomationId='btn' and @Name='OK']` | Unnecessary complexity |
| ClassName when better options exist | `//Edit[@ClassName='TextBox']` | Less stable than AutomationId/Name |

---

{{> lang:decision-and-waits}}
