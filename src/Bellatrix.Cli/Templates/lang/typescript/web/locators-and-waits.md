<!-- lang:find -->
## Find Strategies (TypeScript `create(T).by*` factory)

`create(T)` exists on a page map (`this.create`), on the app in tests (`this.app.create`), and on any component (scoped). It returns a `ComponentService<T>` from `@bellatrix/web/services`; nothing is located until the component is first awaited.

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| CSS selector | `byCss(css)` | `this.create(Button).byCss("[data-testid='login']")` |
| Id (exact) | `byId(id)` | `this.create(TextField).byId('email')` |
| XPath | `byXpath(xpath)` | `this.create(Button).byXpath("//button[normalize-space()='Log In']")` |
| Name (exact) | `byName(name)` | `this.create(TextField).byName('username')` |
| Attribute (exact) | `byAttribute(attr, value)` | `this.create(Button).byAttribute('data-testid', 'login')` |
| Attribute contains | `byAttributeContaining(attr, value)` (singular) | `this.create(Anchor).byAttributeContaining('data-product_id', '28')` |
| Class (single, exact) | `byClass(cls)` | `this.create(Div).byClass('alert-danger')` |
| Class contains | `byClassContaining(fragment)` | `this.create(Div).byClassContaining('woocommerce-message')` |
| Tag | `byTag(tag)` | `this.create(Heading).byTag('h1')` |
| Link text (exact) | `byLinkText(text)` | `this.create(Anchor).byLinkText('Sign in')` |
| Link text contains | `byLinkTextContaining(text)` | `this.create(Anchor).byLinkTextContaining('Forgot')` |
| Inner text contains | `byInnerTextContaining(text)` | `this.create(Heading).byInnerTextContaining('Billing details')` (matches the element's OWN text nodes — `//*[contains(text(), …)]` — not text inside child elements) |
| Id contains / ends with | `byIdContaining` / `byIdEndingWith` | `this.create(TextField).byIdEndingWith('_email')` |
| Name ends with | `byNameEndingWith(name)` | `this.create(Select).byNameEndingWith('orderby')` |
| Value contains | `byValueContaining(value)` | `this.create(Button).byValueContaining('Apply coupon')` |
| Custom strategy | `by(new XpathFindStrategy('…'))` | strategies from `'@bellatrix/web/findstrategies'` |

Collections: every strategy has an `allBy…` twin returning `ComponentsList<T>` — `allByCss`, `allById`, `allByXpath`, `allByName`, `allByAttribute`, `allByAttributeContaining`, `allByClass`, `allByClassContaining`, `allByTag`, `allByLinkText`, `allByLinkTextContaining`, `allByInnerTextContaining`, `allByIdContaining`, `allByIdEndingWith`, `allByNameEndingWith`, `allByValueContaining`, `allBy(strategy)`.

### Methods that do NOT exist in BELLATRIX TypeScript (type errors)

| WRONG | CORRECT |
|-------|---------|
| `byXPath(…)` / `allByXPath(…)` (capital P) | `byXpath(…)` / `allByXpath(…)` |
| `byDataTestId('x')` | `byCss("[data-testid='x']")` or `byAttribute('data-testid', 'x')` |
| `byAttributesContaining(…)` (plural, C# spelling) | `byAttributeContaining(attr, value)` |
| `byAutomationId(…)` / `byAccessibilityId(…)` | desktop/mobile only — use `byId(…)` / `byCss(…)` |
| `byText(…)` | `byXpath("//tag[normalize-space()='Text']")` or `byInnerTextContaining(…)` |
| `App.Components.CreateBy…<T>(…)` | `this.create(T).by…(…)` |

### Mapping a Validated CLI Locator to a Factory

| Validated `{ strategy, selector }` | Factory |
|------------------------------------|---------|
| `css` `[data-testid='login']` | `byCss("[data-testid='login']")` |
| `css` `#email` | `byId('email')` |
| `id` `email` | `byId('email')` |
| `name` `username` / `css` `[name='username']` | `byName('username')` |
| `css` (anything else) | `byCss(selector)` |
| `xpath` | `byXpath(selector)` |
| `class` `btn-primary` | `byClass('btn-primary')` |
| `tag` `h1` | `byTag('h1')` |
| `linktext` `Sign in` | `byLinkText('Sign in')` |
| `partiallinktext` `Sign` | `byLinkTextContaining('Sign')` |

What the factories generate (so you can predict matches): `byId('x')` → CSS `#x` (an id containing `.`, `:` or starting with a digit needs `byCss("[id='x']")`); `byClass('a')` → `.a` (ONE class — never pass `'a b'`; use `byClassContaining('a b')` for a multi-class attribute fragment); `byName` → `[name='x']`; `byLinkText` → `//a[normalize-space()='x']` (visible link text, not the `href`); `byAttributeContaining` → `[attr*='v']`.

Quote style: wrap selectors that contain single quotes in double quotes (`"[data-testid='login']"`) and use template literals only for interpolated values (`` `//tr[td[normalize-space()='${name}']]` ``).

---

## Locator Strategy (Priority Order) in TypeScript

### CRITICAL: Prefer Direct Factories Over XPath

When the locator is a single attribute match, use the direct factory — NOT `byXpath`:

| Condition | USE | DO NOT USE |
|-----------|-----|------------|
| Static id | `byId('email')` | `byXpath("//*[@id='email']")` |
| data-testid | `byCss("[data-testid='login']")` | `byXpath("//*[@data-testid='login']")` |
| name attribute | `byName('q')` | `byXpath("//input[@name='q']")` |
| Exact link text | `byLinkText('Blog')` | `byXpath("//a[text()='Blog']")` |

Reserve `byXpath` for genuinely structural or text-based locators: label → input, row → cell, `normalize-space()` text matches, dynamic fragments interpolated at runtime.

### Priority 1: data-testid (MOST STABLE)

```ts
this.create(Button).byCss("[data-testid='login-submit']")
this.create(TextField).byCss("[data-testid='email']")
```

### Priority 2: Static Id

```ts
this.create(TextField).byId('billing_first_name')
this.create(Select).byId('billing_country')
```

### Priority 3: name Attribute

```ts
this.create(TextField).byName('username')
this.create(Select).byNameEndingWith('orderby')   // [name$='orderby']
```

### Priority 4: ARIA

```ts
this.create(Button).byCss("[aria-label='Close dialog']")
this.create(Button).byXpath("//*[@role='tab' and normalize-space()='Settings']")
```

### Priority 5: Label / Placeholder

```ts
this.create(TextField).byXpath("//label[normalize-space()='Email']/following-sibling::input")
this.create(SearchField).byCss("input[placeholder='Search products']")
```

### Priority 6: Tag + Visible Text

```ts
this.create(Button).byXpath("//button[normalize-space()='Place order']")
this.create(Anchor).byLinkText('Forgot password?')
```

### Priority 7–8: Structural / Positional (last resort)

```ts
this.create(Button).byCss("form#checkout button[type='submit']")
this.create(Anchor).byXpath("(//div[@data-testid='product-card'])[1]//a")
```

---

<!-- lang:decision-and-waits -->
## Decision-Making Process (TypeScript)

When turning a validated locator into a Map getter:

1. **data-testid?** → `byCss("[data-testid='…']")`
2. **Static id?** → `byId('…')`
3. **name attribute?** → `byName('…')`
4. **aria-label / role?** → `byCss("[aria-label='…']")` or `byXpath("//*[@role='…' and normalize-space()='…']")`
5. **Only a label / visible text identifies it?** → `byXpath(…)` with `normalize-space()` (or `byLinkText` for links)
6. **Dynamic value (row name, product id)?** → a Map *method* with an interpolated locator: ``editButtonFor(row: string) { return this.create(Button).byXpath(`//tr[td[normalize-space()='${row}']]//button[normalize-space()='Edit']`); }``
7. **Last resort** → structural CSS, then positional XPath — add a comment explaining why.

**Rule:** if your XPath collapses to a single `@attr='value'` predicate with no traversal and no text match, use the direct factory instead.

---

## Wait Strategies (`component.wait.*`)

Waits live on every component's `wait` property (`ComponentWaitService`). They are **eager** — each one is an awaited call that polls until the condition holds or `validationTimeout` expires (polling every `sleepInterval`). They take **no arguments**: there is no per-call timeout; timeouts (milliseconds) come from `webSettings.timeoutSettings` in `bellatrix.config.ts`.

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `await c.wait.toExist()` | Element is in the DOM (with Playwright the implicit find already waits for it to be visible) |
| Not exists | `await c.wait.toNotExist()` | Element removed from the DOM (see the caveat below) |
| Visible | `await c.wait.toBeVisible()` | Element displayed |
| Not visible | `await c.wait.toNotBeVisible()` | Element hidden (spinners, closing modals — see the caveat) |
| Clickable | `await c.wait.toBeClickable()` | Element enabled |
| Disabled | `await c.wait.toBeDisabled()` | Element NOT enabled |

Implicit wait: every awaited action/getter first locates the element, waiting up to `findElementTimeout` — with the Playwright controller for the element to be **visible**, with Selenium for it to be present. So `await button.click()` on a not-yet-rendered button already waits; add an explicit `wait.*` only for a condition other than "it appeared".

### Usage Examples

```ts
// In a page's waitForPageLoad() — runs after open()
protected override async waitForPageLoad() {
    await this.map.placeOrderButton.wait.toBeVisible();
}

// Before acting on a button that is rendered disabled first
await this.map.placeOrderButton.wait.toBeClickable();
await this.map.placeOrderButton.click();

// A message that appears after an AJAX call
await this.app.browser.waitForAjax();
await this.map.messageAlert.wait.toBeVisible();
```

### CAVEAT: Waiting for Something to Disappear

`toNotBeVisible()` / `toNotExist()` first locate the element (Playwright: waits until it is VISIBLE, up to `findElementTimeout`). They pass only when the element is visible when the wait starts — or appears within `findElementTimeout` — and then goes away. If the spinner may already be gone (fast response), they fail. For "gone" checks use the project helper `waitUntilGone` (in `src/utilities/waits.ts`, see `bellatrix-web-project-structure`), which polls the match count of an `allBy*` list — finding a list never waits:

```ts
import { waitUntilGone } from '../../utilities/waits';

await this.map.applyCouponButton.click();
await waitUntilGone(this.app, () => this.map.loadingOverlays);   // get loadingOverlays() { return this.create(Div).allByCss('.blockUI'); }
```

### Page-Level Waits (`this.app.browser`, project helpers)

```ts
await this.app.browser.waitUntilPageLoadsCompletely();   // document.readyState === 'complete' (pageLoadTimeout)
await this.app.browser.waitForAjax();                    // no open XMLHttpRequests (waitForAjaxTimeout); fetch() is not tracked
await waitForPartialUrl(this.app, '/order-received');    // project helper — the framework has no URL wait
```

There is no `waitForAngular`, `waitForJavaScriptAnimations`, or `waitForAjaxRequest(url)`.

### Validations Already Wait

`validate(…).is/isNot/contains` and `validateIs`/`validateIsNot` retry until they pass or `validationTimeout` expires — do NOT add a wait in front of a validation just to make it pass:

```ts
// CORRECT — the validation itself waits
await this.map.totalSpan.validate('innerText').is('114.00€');

// UNNECESSARY
await this.map.totalSpan.wait.toBeVisible();
await this.map.totalSpan.validate('innerText').is('114.00€');
```

### Never Use

- `setTimeout` promises or `this.app.sleep(ms)` as a synchronization strategy — use a component wait, `waitForAjax()`, or the project helpers. `this.app.sleep` is only for a documented, unavoidable animation delay.
- Playwright `page.waitFor…` / Selenium `driver.wait` in test-project code — use BELLATRIX waits (see `bellatrix-web-components`).
