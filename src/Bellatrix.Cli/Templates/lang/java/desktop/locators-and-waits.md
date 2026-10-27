<!-- lang:find -->
## Find Strategies

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| AutomationId | `byAutomationId(T.class, "id")` | `app().create().byAutomationId(Button.class, "btnOk")` |
| Name | `byName(T.class, "name")` | `app().create().byName(TextField.class, "Username")` |
| XPath | `by(T.class, new UiaXPathFindStrategy("xpath"))` | `app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@Name='OK']"))` |
| ClassName | `byClass(T.class, "class")` | `app().create().byClass(TextField.class, "TextBox")` |
| Tag | `byTag(T.class, "tag")` | `app().create().byTag(Label.class, "Text")` |
| AccessibilityId | `byAccessibilityId(T.class, "id")` | `app().create().byAccessibilityId(Button.class, "num8Button")` |

Each has an `allBy...` twin returning `java.util.List<T>`. There is **no `byId`** on desktop. `byAutomationId` builds
`//*[@AutomationId='id']`; `byAccessibilityId` uses Appium's accessibility-id strategy (same UIA AutomationId).

**Never `byXPath` / `allByXPath` in BELLATRIX-Java 1.0** — the built-in XPath strategy looks elements up with
`By.tagName(xpath)` on its final search, so `byXPath` fails after the wait and `allByXPath` returns an empty list.
Every XPath goes through the project's `UiaXPathFindStrategy` (code in `bellatrix-desktop-components`; create it once
in `{rootPackage}.common`) via `create().by(...)` / `create().allBy(...)`.

---

## Locator Strategy (Priority Order)

### CRITICAL: Prefer Direct Factory Methods Over XPath

When the locator is a single attribute match (e.g., AutomationId, Name, ClassName alone), ALWAYS use the direct
factory method — NOT an XPath. Direct factories are clearer, faster, and avoid XPath parsing pitfalls (and, in Java,
the XPath bug above).

| Condition | USE | DO NOT USE |
|-----------|-----|------------|
| Single AutomationId | `byAutomationId(T.class, "id")` | `by(T.class, new UiaXPathFindStrategy("//X[@AutomationId='id']"))` |
| Single Name | `byName(T.class, "name")` | `by(T.class, new UiaXPathFindStrategy("//X[@Name='name']"))` |
| Single ClassName | `byClass(T.class, "cls")` | `by(T.class, new UiaXPathFindStrategy("//X[@ClassName='cls']"))` |
| Single ControlType/Tag | `byTag(T.class, "Button")` | `by(T.class, new UiaXPathFindStrategy("//Button"))` |

Reserve XPath (`UiaXPathFindStrategy`) for genuinely structural locators: parent/child chains, sibling axes, multiple
predicates, dynamic fragments interpolated at runtime, or when no direct factory matches the attribute.

### Priority 1: AutomationId (MOST STABLE)

```java
// CORRECT — direct factory
app().create().byAutomationId(Button.class, "btnOk");
app().create().byAutomationId(TextField.class, "txtUsername");
app().create().byAutomationId(ComboBox.class, "cmbCountry");
```

```text
WRONG — unnecessary XPath wrapping (use byAutomationId instead)
app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@AutomationId='btnOk']"))
WRONG — broken in BELLATRIX-Java 1.0 (tag-name lookup)
app().create().byXPath(Button.class, "//Button[@AutomationId='btnOk']")
```

AutomationId is the gold standard. It is set by developers explicitly and rarely changes.

### Priority 2: Name Attribute (VERY STABLE)

```java
// CORRECT — direct factory
app().create().byName(Button.class, "OK");
app().create().byName(TextField.class, "Username");
app().create().byName(Label.class, "Welcome Message");
```

### Priority 3: ControlType + Name Combination (STABLE)

```java
app().create().by(CheckBox.class, new UiaXPathFindStrategy("//CheckBox[@Name='Remember me']"));
app().create().by(RadioButton.class, new UiaXPathFindStrategy("//RadioButton[@Name='Option 1']"));
```

### Priority 4: ClassName (LESS STABLE - Use carefully)

```java
// Only if AutomationId and Name are NOT available
app().create().by(TextField.class, new UiaXPathFindStrategy("//Edit[@ClassName='TextBox']"));
app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@ClassName='Button']"));
```

### Priority 5: HelpText (FOR TOOLTIPS)

```java
app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@HelpText='Click to submit form']"));
app().create().by(TextField.class, new UiaXPathFindStrategy("//Edit[@HelpText='Enter your email address']"));
```

**Critical Preference Rule:**
- **NEVER use ClassName** when AutomationId or Name attributes are available
- **ALWAYS prefer AutomationId** as the first choice for element identification

---

<!-- lang:decision-and-waits -->
## Decision-Making Process

When generating a locator, follow this sequence:

1. **AutomationId available?** → `byAutomationId(T.class, "id")` (highest priority, direct factory)
2. **Name available?** → `byName(T.class, "name")` (direct factory)
3. **ControlType + Name needed for disambiguation?** → `by(T.class, new UiaXPathFindStrategy("//Button[@Name='OK']"))` (XPath is appropriate here — combining ControlType filter with attribute)
4. **ClassName only?** → `byClass(T.class, "cls")` (direct factory)
5. **HelpText / structural relation / dynamic fragment?** → `by(T.class, new UiaXPathFindStrategy(...))` (XPath is the right tool)
6. **Last resort — ControlType alone** → `byTag(T.class, "Button")`

**Rule:** If your chosen XPath collapses to a single `@Attr='value'` predicate with no structural traversal, you
should be using the direct factory instead.

---

## Wait Strategies

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `toExist()` / `toExist(timeout, sleep)` | Wait until element exists in UI tree |
| Not Exists | `toNotExist()` / `toNotExist(timeout, sleep)` | Wait until element no longer exists — see the trap below |
| Visible | `toBeVisible()` / `toBeVisible(timeout, sleep)` | Wait until element is displayed |
| Not Visible | `toNotBeVisible()` / `toNotBeVisible(timeout, sleep)` | Wait until element is hidden (still in the tree) |
| Clickable | `toBeClickable()` / `toBeClickable(timeout, sleep)` | Wait until element is enabled |
| Disabled | `toBeDisabled()` / `toBeDisabled(timeout, sleep)` | Wait until element is not enabled |
| Has Content | `toHaveContent()` / `toHaveContent(timeout, sleep)` | Wait until element text is non-empty |

Timeouts are **seconds** (`long`); defaults come from `desktopSettings.timeoutSettings` (`elementToExistTimeout`,
`elementToBeVisibleTimeout`, …, polling `sleepInterval`). Waits are queued on the component and applied on its next
lookup (the next action, or `waitToBe()`). With no queued wait, every lookup waits `toExist()`.

### The generic wait-chain trap (compile error)

`toBeVisible()` and friends are declared `<T extends DesktopComponent> T toBeVisible()` — the return type is inferred
from the **assignment target**. Chained directly, Java infers `DesktopComponent`, which has no `click()`/`setText()`:

```text
WRONG — does not compile (cannot find symbol: method click())
okButton.toBeVisible().click();
```

```java
// CORRECT — waitToBe() lives on DesktopComponent, so chains ending in it compile
okButton.toBeVisible().toBeClickable().waitToBe();

// CORRECT — queue the wait, then act (the action applies the queued waits)
okButton.toBeClickable();
okButton.click();

// CORRECT — let the assignment drive inference
Button readyButton = okButton.toBeVisible();
readyButton.click();

// CORRECT — explicit type witness
okButton.<Button>toBeVisible().click();
```

### Usage Examples

```java
// Fluent — apply wait condition at creation (lazy, evaluated on first use); assignment drives the type
Button button = app().create().byAutomationId(Button.class, "btnOk").toBeClickable();
Label label = app().create().byName(Label.class, "Status").toHaveContent();

// Chain multiple waits
TextField field = app().create().byAutomationId(TextField.class, "txtName").toExist().toBeVisible();

// Force immediate wait (blocks until the condition is met; throws on timeout)
app().create().byName(Button.class, "Submit").toBeVisible().waitToBe();
```

### Negative waits: use the wait service, not `toNotExist().waitToBe()`

`waitToBe()` always re-locates the element after the waits pass, so `toNotExist().waitToBe()` /
`toNotBeVisible().waitToBe()` **throw once the element is really gone**. Run negative waits through the wait service,
which only evaluates the condition:

```java
// in a test
Button spinner = app().create().byAutomationId(Button.class, "busyIndicator");
app().waitFor().wait(spinner, Wait.to().notExist());

// in a page object / custom component (no app()): the same service, constructed directly
new ComponentWaitService().wait(spinner, Wait.to().notExist(30, 1));
```

`Wait` is `solutions.bellatrix.desktop.waitstrategies.Wait` (`exist()`, `notExist()`, `beVisible()`,
`notBeVisible()`, `beClickable()`, `haveContent()`, each also `(timeout, sleep)`); `ComponentWaitService` is in
`solutions.bellatrix.desktop.services`. Prefer `notExist()` for elements that disappear — `notBeVisible()` keeps
polling until timeout when the element is removed from the tree instead of hidden.
