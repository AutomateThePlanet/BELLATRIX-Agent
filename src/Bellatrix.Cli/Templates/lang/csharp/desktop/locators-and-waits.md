<!-- lang:find -->
## Find Strategies

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| AutomationId | `CreateByAutomationId<T>("id")` | `App.Components.CreateByAutomationId<Button>("btnOk")` |
| Name | `CreateByName<T>("name")` | `App.Components.CreateByName<TextField>("Username")` |
| XPath | `CreateByXPath<T>("xpath")` | `App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnOk']")` |
| ClassName | `CreateByClass<T>("class")` | `App.Components.CreateByClass<TextField>("TextBox")` |
| Tag | `CreateByTag<T>("tag")` | `App.Components.CreateByTag<Label>("Text")` |

---

## Locator Strategy (Priority Order)

### CRITICAL: Prefer Direct Factory Methods Over XPath

When the locator is a single attribute match (e.g., AutomationId, Name, ClassName alone), ALWAYS use the direct factory method — NOT `CreateByXPath`. Direct factories are clearer, faster, and avoid XPath parsing pitfalls.

| Condition | USE | DO NOT USE |
|-----------|-----|------------|
| Single AutomationId | `CreateByAutomationId<T>("id")` | `CreateByXPath<T>("//X[@AutomationId='id']")` |
| Single Name | `CreateByName<T>("name")` | `CreateByXPath<T>("//X[@Name='name']")` |
| Single ClassName | `CreateByClass<T>("cls")` | `CreateByXPath<T>("//X[@ClassName='cls']")` |
| Single ControlType/Tag | `CreateByTag<T>("Button")` | `CreateByXPath<T>("//Button")` |

Reserve `CreateByXPath` for genuinely structural locators: parent/child chains, sibling axes, multiple predicates, dynamic fragments interpolated at runtime, or when no direct factory matches the attribute.

### Priority 1: AutomationId (MOST STABLE)

```csharp
// CORRECT — direct factory
App.Components.CreateByAutomationId<Button>("btnOk")
App.Components.CreateByAutomationId<TextField>("txtUsername")
App.Components.CreateByAutomationId<ComboBox>("cmbCountry")

// WRONG — unnecessary XPath wrapping
App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnOk']")  // Use CreateByAutomationId instead
```

AutomationId is the gold standard. It is set by developers explicitly and rarely changes.

### Priority 2: Name Attribute (VERY STABLE)

```csharp
// CORRECT — direct factory
App.Components.CreateByName<Button>("OK")
App.Components.CreateByName<TextField>("Username")
App.Components.CreateByName<Label>("Welcome Message")
```

### Priority 3: ControlType + Name Combination (STABLE)

```csharp
App.Components.CreateByXPath<CheckBox>("//CheckBox[@Name='Remember me']")
App.Components.CreateByXPath<RadioButton>("//RadioButton[@Name='Option 1']")
```

### Priority 4: ClassName (LESS STABLE - Use carefully)

```csharp
// Only if AutomationId and Name are NOT available
App.Components.CreateByXPath<TextField>("//Edit[@ClassName='TextBox']")
App.Components.CreateByXPath<Button>("//Button[@ClassName='Button']")
```

### Priority 5: HelpText (FOR TOOLTIPS)

```csharp
App.Components.CreateByXPath<Button>("//Button[@HelpText='Click to submit form']")
App.Components.CreateByXPath<TextField>("//Edit[@HelpText='Enter your email address']")
```

**Critical Preference Rule:**
- **NEVER use ClassName** when AutomationId or Name attributes are available
- **ALWAYS prefer AutomationId** as the first choice for element identification

---

<!-- lang:decision-and-waits -->
## Decision-Making Process

When generating a locator, follow this sequence:

1. **AutomationId available?** → `CreateByAutomationId<T>("id")` (highest priority, direct factory)
2. **Name available?** → `CreateByName<T>("name")` (direct factory)
3. **ControlType + Name needed for disambiguation?** → `CreateByXPath<T>("//Button[@Name='OK']")` (XPath is appropriate here — combining ControlType filter with attribute)
4. **ClassName only?** → `CreateByClass<T>("cls")` (direct factory)
5. **HelpText / structural relation / dynamic fragment?** → `CreateByXPath<T>(...)` (XPath is the right tool)
6. **Last resort — ControlType alone** → `CreateByTag<T>("Button")`

**Rule:** If your chosen XPath collapses to a single `@Attr='value'` predicate with no structural traversal, you should be using the direct factory instead.

---

## Wait Strategies

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `ToExists(timeout?, sleep?)` | Wait until element exists in UI tree |
| Not Exists | `ToNotExists(timeout?, sleep?)` | Wait until element no longer exists |
| Visible | `ToBeVisible(timeout?, sleep?)` | Wait until element is displayed |
| Not Visible | `ToNotBeVisible(timeout?, sleep?)` | Wait until element is hidden |
| Clickable | `ToBeClickable(timeout?, sleep?)` | Wait until element is clickable |
| Has Content | `ToHasContent(timeout?, sleep?)` | Wait until element has text content |

### Usage Examples

```csharp
// Fluent - apply wait condition at creation (lazy, evaluated on first use)
var button = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnOk']").ToBeClickable();
var label = App.Components.CreateByName<Label>("Status").ToHasContent();

// Chain multiple waits
var field = App.Components.CreateByAutomationId<TextField>("txtName").ToExists().ToBeVisible();

// Force immediate wait
var element = App.Components.CreateByName<Button>("Submit").ToNotExists();
element.WaitToBe(); // Blocks until condition is met
```
