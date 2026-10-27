<!-- lang:component-mapping -->
## Recording Results for Code Generation (TypeScript: CLI only)

BELLATRIX TypeScript has **no mobile component or page-object layer**. `@bellatrix/appium` ships only a raw
Android HTTP client and has no iOS support, so there is no BELLATRIX TypeScript component type to map a native
class to and no mobile code-generation skill for TypeScript. Use this skill to execute and validate the scenario
through the CLI, and record for each step:

| Record | From |
|--------|------|
| Validated locator + `--strategy` | the locator specialist's answer, confirmed with a successful CLI action |
| Native class / type | the snapshot node (`android.widget.Button`, `XCUIElementTypeButton`, …) |
| `content-desc` / `resource-id` / `text` (Android), `name` / `label` (iOS) | the snapshot node, so the locator can be re-derived later |
| Action + observed result | the CLI JSON response |

These records are framework-neutral: they can be turned into BELLATRIX C# or Java mobile tests later
(`bellatrix init --language csharp|java --domains mobile`).

---

