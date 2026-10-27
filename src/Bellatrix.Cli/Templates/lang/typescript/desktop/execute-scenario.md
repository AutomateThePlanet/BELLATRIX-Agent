<!-- lang:component-mapping -->
## Recording Results for Code Generation (TypeScript: CLI only)

BELLATRIX TypeScript has **no desktop component or page-object layer**. `@bellatrix/appium` ships only a raw
WinAppDriver HTTP client, so there is no BELLATRIX TypeScript component type to map a `ControlType` to and no
desktop code-generation skill for TypeScript. Use this skill to execute and validate the scenario through the
CLI, and record for each step:

| Record | From |
|--------|------|
| Validated XPath | the locator specialist's answer, confirmed with a successful CLI action |
| `ControlType` | the snapshot node (`Button`, `Edit`, `ComboBox`, …) |
| `Name` / `AutomationId` | the snapshot node, so the locator can be re-derived later |
| Action + observed result | the CLI JSON response |

These records are framework-neutral: they can be turned into BELLATRIX C# or Java desktop tests later
(`bellatrix init --language csharp|java --domains desktop`).

---

