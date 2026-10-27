<!-- lang:find -->
## Find Strategies (Java factory methods on `create()`)

All factories are methods of `solutions.bellatrix.web.services.ComponentCreateService` — `app().create()` in tests and `create()` in `PageMap` / `WebPage` / `WebSection`. Signature: `by…(Class<T> componentClass, String value)` — **class first**.

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| CSS selector | `byCss(T.class, css)` | `create().byCss(Button.class, "[data-testid='login']")` |
| Id (exact) | `byId(T.class, id)` | `create().byId(TextInput.class, "email")` |
| Name (exact) | `byName(T.class, name)` | `create().byName(TextInput.class, "username")` |
| XPath | `byXPath(T.class, xpath)` | `create().byXPath(Button.class, "//button[normalize-space()='Log In']")` |
| Class (single, exact) | `byClass(T.class, cls)` | `create().byClass(Div.class, "alert-danger")` |
| Class contains | `byClassContaining(T.class, fragment)` | `create().byClassContaining(Div.class, "woocommerce-message")` |
| Tag | `byTag(T.class, tag)` | `create().byTag(Heading.class, "h1")` |
| Link text (exact) | `byLinkText(T.class, text)` | `create().byLinkText(Anchor.class, "Sign in")` |
| Link text contains | `byLinkTextContaining(T.class, text)` | `create().byLinkTextContaining(Anchor.class, "Forgot")` |
| Inner text contains | `byInnerTextContaining(T.class, text)` | `create().byInnerTextContaining(Heading.class, "Billing details")` |
| Attribute contains | `byAttributeContaining(T.class, attr, value)` | `create().byAttributeContaining(Anchor.class, "data-product_id", "28")` |
| Id contains / ends with | `byIdContaining` / `byIdEndingWith` | `create().byIdEndingWith(TextInput.class, "_email")` |
| Name ends with | `byNameEnding(T.class, name)` | `create().byNameEnding(Select.class, "orderby")` |
| Value contains | `byValueContaining(T.class, value)` | `create().byValueContaining(Button.class, "Apply coupon")` |

What they generate (so you know what "contains" means): `byInnerTextContaining` → `//*[contains(text(), 'v')]` (the element's **own** text node, any tag — the first match in document order wins); `byLinkTextContaining` → `//a[contains(text(), 'v')]`; `byAttributeContaining` → CSS `[attr*='v']` (contains, not equals); `byClassContaining` → `[class*='v']`; `byValueContaining` → `[value*='v']`; `byNameEnding` → `[name$='v']`; `byIdContaining` / `byIdEndingWith` → `[id*='v']` / `[id$='v']`.

Collections: every strategy has an `allBy…` twin returning `java.util.List<T>` — `allByCss`, `allById`, `allByName`, `allByXPath`, `allByClass`, `allByClassContaining`, `allByTag`, `allByLinkText`, `allByLinkTextContaining`, `allByInnerTextContaining`, `allByAttributeContaining`, `allByIdContaining`, `allByIdEndingWith`, `allByNameEnding`, `allByValueContaining`. They are **eager and do not wait** — see `bellatrix-web-components`.

### Methods that do NOT exist in BELLATRIX Java web (compile errors)

| WRONG | CORRECT |
|-------|---------|
| `byXpath(…)` / `allByXpath(…)` (lowercase p — the C# web spelling) | `byXPath(…)` / `allByXPath(…)` |
| `byDataTestId(T.class, "x")` | `byCss(T.class, "[data-testid='x']")` |
| `byNameEndingWith(…)` | `byNameEnding(…)` |
| `byAttributesContaining(…)` (plural) | `byAttributeContaining(…)` |
| `byText(…)` | `byXPath(T.class, "//tag[normalize-space()='Text']")` or `byInnerTextContaining(…)` |
| `byAutomationId(…)` / `byAccessibilityId(…)` | desktop/mobile only — use `byId` / `byCss` |
| `app().create().byId("email", TextInput.class)` (value first) | `app().create().byId(TextInput.class, "email")` |
| `App.Components.CreateById<TextInput>(…)` (C# generics) | `app().create().byId(TextInput.class, …)` |

### Mapping a Validated CLI Locator to a Factory

| Validated `{ strategy, selector }` | Factory |
|------------------------------------|---------|
| `css` `[data-testid='login']` | `byCss(T.class, "[data-testid='login']")` |
| `css` `#email` | `byId(T.class, "email")` |
| `id` `email` | `byId(T.class, "email")` |
| `name` `username` / `css` `[name='username']` | `byName(T.class, "username")` |
| `css` (anything else) | `byCss(T.class, selector)` |
| `xpath` | `byXPath(T.class, selector)` |
| `class` `btn-primary` | `byClass(T.class, "btn-primary")` |
| `tag` `h1` | `byTag(T.class, "h1")` |
| `linktext` `Sign in` | `byLinkText(T.class, "Sign in")` |
| `partiallinktext` `Sign` | `byLinkTextContaining(T.class, "Sign")` |

---

## Locator Strategy (Priority Order) in Java

### CRITICAL: Prefer Direct Factories Over XPath

When the locator is a single attribute match, use the direct factory — NOT `byXPath`:

| Condition | USE | DO NOT USE |
|-----------|-----|------------|
| Static id | `byId(T.class, "email")` | `byXPath(T.class, "//*[@id='email']")` |
| data-testid | `byCss(T.class, "[data-testid='login']")` | `byXPath(T.class, "//*[@data-testid='login']")` |
| name attribute | `byName(T.class, "q")` | `byXPath(T.class, "//input[@name='q']")` |
| Exact link text | `byLinkText(Anchor.class, "Blog")` | `byXPath(Anchor.class, "//a[text()='Blog']")` |

Reserve `byXPath` for genuinely structural or text-based locators: label → input, row → cell, `normalize-space()` text matches, dynamic fragments interpolated at runtime.

### Priority 1: data-testid (MOST STABLE)

```java
create().byCss(Button.class, "[data-testid='login-submit']");
create().byCss(TextInput.class, "[data-testid='email']");
```

### Priority 2: Static Id

```java
create().byId(TextInput.class, "billing_first_name");
create().byId(Select.class, "billing_country");
```

### Priority 3: name Attribute

```java
create().byName(TextInput.class, "username");
create().byNameEnding(Select.class, "orderby");   // [name$='orderby']
```

### Priority 4: ARIA

```java
create().byCss(Button.class, "[aria-label='Close dialog']");
create().byXPath(Button.class, "//*[@role='tab' and normalize-space()='Settings']");
```

### Priority 5: Label / Placeholder

```java
create().byXPath(TextInput.class, "//label[normalize-space()='Email']/following-sibling::input");
create().byCss(SearchField.class, "input[placeholder='Search products']");
```

### Priority 6: Tag + Visible Text

```java
create().byXPath(Button.class, "//button[normalize-space()='Place order']");
create().byLinkText(Anchor.class, "Forgot password?");
```

### Priority 7–8: Structural / Positional (last resort)

```java
create().byCss(Button.class, "form#checkout button[type='submit']");
create().byXPath(Anchor.class, "(//div[@data-testid='product-card'])[1]//a");
```

---

<!-- lang:decision-and-waits -->
## Decision-Making Process (Java)

When turning a validated locator into a Map method:

1. **data-testid?** → `create().byCss(T.class, "[data-testid='…']")`
2. **Static id?** → `create().byId(T.class, "…")`
3. **name attribute?** → `create().byName(T.class, "…")`
4. **aria-label / role?** → `create().byCss(T.class, "[aria-label='…']")` or `create().byXPath(T.class, "//*[@role='…' and normalize-space()='…']")`
5. **Only a label / visible text identifies it?** → `create().byXPath(T.class, …)` with `normalize-space()` (or `create().byLinkText(Anchor.class, …)` for links)
6. **Dynamic value (row name, product id)?** → a Map method **with a parameter** and a formatted locator: `public Button editButton(String row) { return create().byXPath(Button.class, String.format("//tr[td[normalize-space()='%s']]//button[normalize-space()='Edit']", row)); }`
7. **Last resort** → structural CSS, then positional XPath — add a comment explaining why.

**Rule:** if your XPath collapses to a single `@attr='value'` predicate with no traversal and no text match, use the direct factory instead.

---

## Wait Strategies (fluent, on components)

Wait methods are defined on `WebComponent`. They **queue** a condition that is applied the next time the component's element is located (first action, getter, or an explicit `waitToBe()`). Timeouts are in **seconds**; when omitted they come from `webSettings.timeoutSettings` in `testFrameworkSettings.<env>.json`. Each (except `toShadowRootToBeAttached()`) also has a `(long timeoutInterval, long sleepInterval)` overload.

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `toExist()` | Element is in the DOM (the default when no wait is queued — `elementWaitTimeout`) |
| Not exists | `toNotExist()` | Element removed from the DOM — see the trap below |
| Visible | `toBeVisible()` | Element displayed |
| Not visible | `toNotBeVisible()` | Element hidden (spinners, closing modals) — see the trap below |
| Clickable | `toBeClickable()` | Found and enabled (it does not check visibility — chain `toBeVisible()` too when needed) |
| Has content | `toHaveContent()` | Element has non-empty text |
| Disabled | `toBeDisabled()` | Element is disabled |
| Shadow root | `toShadowRootToBeAttached()` | A shadow host's root is attached |

There is no `toHasInnerText`, `toHasNotInnerText`, or `toHasStyle` in Java — wait with a `validate*` method instead (they retry).

### CRITICAL: The Generic Return-Type Trap

`toBeVisible()`, `toBeClickable()`, … are declared `public <T extends WebComponent> T toBeVisible()` — the return type is inferred from the **assignment / return target**. Chained directly (receiver position) Java infers `WebComponent`, which has **no** `click()`, `setText()`, or `validateTextIs()`:

```java
// WRONG (shown as text — does not compile): btn.toBeVisible().click();  →  cannot find symbol: click()

btn.toBeVisible().toBeClickable().waitToBe();   // OK — waitToBe() is on WebComponent
btn.toBeClickable();                            // OK — queue the wait …
btn.click();                                    // … it is applied on this lookup
Button visibleButton = btn.toBeVisible();       // OK — the assignment drives inference
visibleButton.click();
btn.<Button>toBeVisible().click();              // OK — explicit type witness
```

In a Map method the `return` statement drives inference, so `return create().byId(Button.class, "x").toBeClickable();` compiles.

### Usage Examples

```java
// Queued at creation in a Map method (applied on first use)
public Button placeOrderButton() {
    return create().byCss(Button.class, "[data-testid='place-order']").toBeClickable();
}

public Select billingState() {
    return create().byId(Select.class, "billing_state").toBeVisible().toBeClickable();
}

// Custom timeout and polling interval (seconds)
Anchor promotions = app().create().byLinkText(Anchor.class, "Promotions").toHaveContent(40, 1);

// Chain waits, then force them immediately
Div message = app().create().byClassContaining(Div.class, "woocommerce-message");
message.toHaveContent().toBeVisible().waitToBe();
```

### Waiting for Something to Disappear — use `app().waitFor()`

**Framework bug:** `x.toNotExist().waitToBe()` (and `x.toNotBeVisible().waitToBe()` when the element is *removed* rather than hidden) waits correctly and then throws `NotFoundException` anyway, because `waitToBe()` re-locates the element after the wait. Use the wait service, which only runs the condition:

```java
Div spinner = app().create().byCss(Div.class, "[data-testid='spinner']");
app().waitFor().wait(spinner, Wait.to().notExist());        // removed from the DOM
app().waitFor().wait(spinner, Wait.to().notBeVisible());    // hidden or removed
app().waitFor().wait(spinner, Wait.to().notExist(60, 1));   // custom timeout / poll (seconds)
```

`Wait` is `solutions.bellatrix.web.waitstrategies.Wait`; `Wait.to()` also offers `exist()`, `beVisible()`, `beClickable()`, `haveContent()`.

### Page-Level Waits (`app().browser()`, `app().navigate()`)

```java
app().browser().waitUntilPageLoadsCompletely();   // document.readyState === 'complete'
app().browser().waitForAjax();                    // XHR/fetch idle (logs and continues on timeout)
app().browser().waitForAjaxRequest("/api/cart");  // a specific request finished
app().browser().waitForJavaScriptAnimations();
app().browser().waitForAngular();                 // Angular apps
app().navigate().waitForPartialUrl("/order-received");
```

### validate* Methods Already Wait

Every `validate*` method retries until it passes or `validationsTimeout` expires — do NOT add a wait in front of a validation just to make it pass:

```java
// CORRECT — the validation itself waits
totalSpan.validateTextIs("114.00€");

// UNNECESSARY
totalSpan.toHaveContent().waitToBe();
totalSpan.validateTextIs("114.00€");
```

### Never Use

- `Thread.sleep(…)` — use a fluent wait, `app().waitFor()`, or a page-level wait.
- Raw `WebDriverWait` / `driver.findElement(…)` in test-project code — use BELLATRIX factories and waits (see `bellatrix-web-components`).
