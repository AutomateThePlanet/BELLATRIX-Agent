<!-- lang:find -->
## Find Strategies (C# factory methods on `App.Components`)

All factories are extension methods on `ComponentCreateService` in the `Bellatrix.Web` namespace (`using Bellatrix.Web;`).

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| CSS selector | `CreateByCss<T>(css)` | `App.Components.CreateByCss<Button>("[data-testid='login']")` |
| Id (exact) | `CreateById<T>(id)` | `App.Components.CreateById<TextField>("email")` |
| XPath | `CreateByXpath<T>(xpath)` | `App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']")` |
| Class (single, exact) | `CreateByClass<T>(cls)` | `App.Components.CreateByClass<Div>("alert-danger")` |
| Class contains | `CreateByClassContaining<T>(fragment)` | `App.Components.CreateByClassContaining<Div>("woocommerce-message")` |
| Tag | `CreateByTag<T>(tag)` | `App.Components.CreateByTag<Heading>("h1")` |
| Link text (exact) | `CreateByLinkText<T>(text)` | `App.Components.CreateByLinkText<Anchor>("Sign in")` |
| Link text contains | `CreateByLinkTextContaining<T>(text)` | `App.Components.CreateByLinkTextContaining<Anchor>("Forgot")` |
| Inner text contains | `CreateByInnerTextContaining<T>(text)` | `App.Components.CreateByInnerTextContaining<Heading>("Billing details")` |
| Attribute contains | `CreateByAttributesContaining<T>(attr, value)` | `App.Components.CreateByAttributesContaining<Anchor>("data-product_id", "28")` |
| Id contains / ends with | `CreateByIdContaining<T>` / `CreateByIdEndingWith<T>` | `App.Components.CreateByIdEndingWith<TextField>("_email")` |
| Name ends with | `CreateByNameEndingWith<T>(name)` | `App.Components.CreateByNameEndingWith<Select>("orderby")` |
| Value contains | `CreateByValueContaining<T>(value)` | `App.Components.CreateByValueContaining<Button>("Apply coupon")` |

Collections: every strategy has a `CreateAllBy…` twin returning `ComponentsList<T>` — `CreateAllByCss`, `CreateAllById`, `CreateAllByXpath`, `CreateAllByClass`, `CreateAllByClassContaining`, `CreateAllByTag`, `CreateAllByLinkText`, `CreateAllByLinkTextContaining`, `CreateAllByInnerTextContaining`, `CreateAllByAttributesContaining`, `CreateAllByIdContaining`, `CreateAllByIdEndingWith`, `CreateAllByNameEndingWith`, `CreateAllByValueContaining`.

### Methods that do NOT exist in BELLATRIX Web (build errors)

| WRONG | CORRECT |
|-------|---------|
| `CreateByXPath<T>(…)` / `CreateAllByXPath<T>(…)` (capital P — desktop/mobile only) | `CreateByXpath<T>(…)` / `CreateAllByXpath<T>(…)` |
| `CreateByDataTestId<T>("x")` | `CreateByCss<T>("[data-testid='x']")` |
| `CreateByName<T>("x")` | `CreateByCss<T>("[name='x']")` (or `CreateByNameEndingWith<T>("x")`) |
| `CreateByAutomationId<T>(…)` | desktop only — use `CreateById<T>(…)` / `CreateByCss<T>(…)` |
| `CreateByText<T>(…)` | `CreateByXpath<T>("//tag[normalize-space()='Text']")` or `CreateByInnerTextContaining<T>(…)` |

### Mapping a Validated CLI Locator to a Factory

| Validated `{ strategy, selector }` | Factory |
|------------------------------------|---------|
| `css` `[data-testid='login']` | `CreateByCss<T>("[data-testid='login']")` |
| `css` `#email` | `CreateById<T>("email")` |
| `id` `email` | `CreateById<T>("email")` |
| `name` `username` / `css` `[name='username']` | `CreateByCss<T>("[name='username']")` |
| `css` (anything else) | `CreateByCss<T>(selector)` |
| `xpath` | `CreateByXpath<T>(selector)` |
| `class` `btn-primary` | `CreateByClass<T>("btn-primary")` |
| `tag` `h1` | `CreateByTag<T>("h1")` |
| `linktext` `Sign in` | `CreateByLinkText<T>("Sign in")` |
| `partiallinktext` `Sign` | `CreateByLinkTextContaining<T>("Sign")` |

---

## Locator Strategy (Priority Order) in C#

### CRITICAL: Prefer Direct Factories Over XPath

When the locator is a single attribute match, use the direct factory — NOT `CreateByXpath`:

| Condition | USE | DO NOT USE |
|-----------|-----|------------|
| Static id | `CreateById<T>("email")` | `CreateByXpath<T>("//*[@id='email']")` |
| data-testid | `CreateByCss<T>("[data-testid='login']")` | `CreateByXpath<T>("//*[@data-testid='login']")` |
| name attribute | `CreateByCss<T>("[name='q']")` | `CreateByXpath<T>("//input[@name='q']")` |
| Exact link text | `CreateByLinkText<Anchor>("Blog")` | `CreateByXpath<Anchor>("//a[text()='Blog']")` |

Reserve `CreateByXpath` for genuinely structural or text-based locators: label → input, row → cell, `normalize-space()` text matches, dynamic fragments interpolated at runtime.

### Priority 1: data-testid (MOST STABLE)

```csharp
App.Components.CreateByCss<Button>("[data-testid='login-submit']")
App.Components.CreateByCss<TextField>("[data-testid='email']")
```

### Priority 2: Static Id

```csharp
App.Components.CreateById<TextField>("billing_first_name")
App.Components.CreateById<Select>("billing_country")
```

### Priority 3: name Attribute

```csharp
App.Components.CreateByCss<TextField>("[name='username']")
App.Components.CreateByNameEndingWith<Select>("orderby")   // [name$='orderby']
```

### Priority 4: ARIA

```csharp
App.Components.CreateByCss<Button>("[aria-label='Close dialog']")
App.Components.CreateByXpath<Button>("//*[@role='tab' and normalize-space()='Settings']")
```

### Priority 5: Label / Placeholder

```csharp
App.Components.CreateByXpath<TextField>("//label[normalize-space()='Email']/following-sibling::input")
App.Components.CreateByCss<Search>("input[placeholder='Search products']")
```

### Priority 6: Tag + Visible Text

```csharp
App.Components.CreateByXpath<Button>("//button[normalize-space()='Place order']")
App.Components.CreateByLinkText<Anchor>("Forgot password?")
```

### Priority 7–8: Structural / Positional (last resort)

```csharp
App.Components.CreateByCss<Button>("form#checkout button[type='submit']")
App.Components.CreateByXpath<Anchor>("(//div[@data-testid='product-card'])[1]//a")
```

---

<!-- lang:decision-and-waits -->
## Decision-Making Process (C#)

When turning a validated locator into a Map property:

1. **data-testid?** → `CreateByCss<T>("[data-testid='…']")`
2. **Static id?** → `CreateById<T>("…")`
3. **name attribute?** → `CreateByCss<T>("[name='…']")`
4. **aria-label / role?** → `CreateByCss<T>("[aria-label='…']")` or `CreateByXpath<T>("//*[@role='…' and normalize-space()='…']")`
5. **Only a label / visible text identifies it?** → `CreateByXpath<T>(…)` with `normalize-space()` (or `CreateByLinkText<Anchor>` for links)
6. **Dynamic value (row name, product id)?** → a Map *method* with an interpolated locator: `public Button EditButton(string row) => App.Components.CreateByXpath<Button>($"//tr[td[normalize-space()='{row}']]//button[normalize-space()='Edit']");`
7. **Last resort** → structural CSS, then positional XPath — add a comment explaining why.

**Rule:** if your XPath collapses to a single `@attr='value'` predicate with no traversal and no text match, use the direct factory instead.

---

## Wait Strategies (fluent, on components)

Wait extension methods live in `Bellatrix.Web` (`WaitStrategyComponentsExtensions`). They are **lazy**: they register a condition that is evaluated the next time the component's element is located (first action, property read, or an explicit `WaitToBe()`). Timeouts are in **seconds**; when omitted they come from `webSettings.timeoutSettings` in `testFrameworkSettings.<Configuration>.json`.

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `ToExists(timeout?, sleep?)` | Element is in the DOM (the default when no wait is set) |
| Not exists | `ToNotExists(timeout?, sleep?)` | Element removed from the DOM |
| Visible | `ToBeVisible(timeout?, sleep?)` | Element displayed |
| Not visible | `ToNotBeVisible(timeout?, sleep?)` | Element hidden but still in the DOM — if it is removed from the DOM use `ToNotExists()` |
| Clickable | `ToBeClickable(timeout?, sleep?)` | Element exists and is enabled (no visibility check — chain `ToBeVisible()` if needed) |
| Has content | `ToHasContent(timeout?, sleep?)` | Element has non-empty text |
| Has inner text | `ToHasInnerText("text", timeout?, sleep?)` | Element text equals the given text |
| Has not inner text | `ToHasNotInnerText("text", timeout?, sleep?)` | Element text no longer equals it |
| Has style | `ToHasStyle("display: block;", timeout?, sleep?)` | Element `style` attribute equals the value exactly |
| Disabled | `ToBeDisabled(timeout?, sleep?)` | Element is disabled |

### Usage Examples

```csharp
// Fluent at creation (lazy — evaluated on first use)
public Button PlaceOrderButton => App.Components.CreateByCss<Button>("[data-testid='place-order']").ToBeClickable();
public Select BillingState => App.Components.CreateById<Select>("billing_state").ToBeVisible().ToBeClickable();

// Custom timeout (seconds) and polling interval (seconds)
var promotions = App.Components.CreateByLinkText<Anchor>("Promotions").ToHasContent(40, 1);

// Chain waits, then force them immediately
var message = App.Components.CreateByClassContaining<Div>("woocommerce-message");
message.ToHasContent().ToBeVisible().WaitToBe();

// Wait for a spinner to disappear
App.Components.CreateByCss<Div>("[data-testid='spinner']").ToNotExists().WaitToBe();
```

### Page-Level Waits (`App.Browser`, `App.Navigation`)

```csharp
App.Browser.WaitUntilReady();             // document ready
App.Browser.WaitForAjax();                // no open XMLHttpRequests
App.Browser.WaitForAjaxRequest("/api/cart");  // a specific request finished
App.Browser.WaitForJavaScriptAnimations();
App.Browser.WaitForAngular();             // Angular apps
App.Navigation.WaitForPartialUrl("/order-received");
```

### Validate* Methods Already Wait

Every `Validate*` method retries until it passes or `validationsTimeout` expires — do NOT add a wait in front of a validation just to make it pass:

```csharp
// CORRECT — the validation itself waits
TotalSpan.ValidateInnerTextIs("114.00€");

// UNNECESSARY
TotalSpan.ToHasContent().WaitToBe();
TotalSpan.ValidateInnerTextIs("114.00€");
```

### Never Use

- `Thread.Sleep(…)` — use a fluent wait or a page-level wait.
- Raw `WebDriverWait` / `driver.FindElement(…)` in test-project code — use BELLATRIX factories and waits (see `bellatrix-web-components`).
