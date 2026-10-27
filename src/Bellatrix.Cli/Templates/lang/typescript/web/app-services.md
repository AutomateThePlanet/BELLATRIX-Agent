---
description: Reference skill for BELLATRIX TypeScript web App services — the component factory (create), navigation, browser, script, dialog, cookies, page object factories (goTo/createPage), and bellatrix.config.ts.
---
# BELLATRIX Web App Services Reference (TypeScript)

The `App` object (`@bellatrix/web/infrastructure`) is the entry point to every web service. Tests get it from the `WebTest` base class (`this.app`); page objects (`WebPage`) expose the same `this.app` getter. Page **maps** (`WebPageMap`) have no `app` — they only have `this.create(T)`.

**Everything is async.** Every service call, component action, getter, wait, and validation returns a `Promise` and MUST be `await`ed. A missing `await` still compiles (a floating promise) — the type check will not catch it, the test will just race.

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `this.app.components.…` / `App.Components.CreateByXpath<T>()` | `this.app.create(T).byXpath('…')` (in a test) / `this.create(T).byXpath('…')` (in a map) |
| `this.app.create(T).byXPath('…')` | `byXpath` — lowercase **p** (`byXPath` is TS2551) |
| `this.app.create(T).byDataTestId('x')` | `byCss("[data-testid='x']")` or `byAttribute('data-testid', 'x')` |
| `this.app.browser.navigateToUrl(url)` / `this.app.browser.navigate(url)` | `await this.app.navigation.navigate(url)` |
| `new LoginPage()` + `open()` by hand | `await this.app.goTo(LoginPage)` (opens it) / `this.app.createPage(LoginPage)` (already open) |
| `this.app.wait.…` | There is no `App.wait` — use `component.wait.toBeVisible()` etc. and `this.app.browser.waitForAjax()` |
| `this.app.interactions.…` / `this.app.javaScript.…` / `this.app.dialogs.…` | No interactions service; `this.app.script.execute(fn, …)`; `this.app.dialog` (singular) |
| `this.app.navigation.waitForPartialUrl('/x')` | Does not exist — use the project helper `waitForPartialUrl(this.app, '/x')` (see `bellatrix-web-project-structure`) |
| `this.app.script.execute('return document.title')` | Pass a **function**: `this.app.script.execute(() => document.title)` — a string is wrapped as `(<script>)(…)`, so a `return …` statement string is a runtime syntax error |
| `Thread.Sleep` / `setTimeout` | A component wait or `waitForAjax()`; only as a last resort `await this.app.sleep(ms)` |
| `component.scrollToVisible()` | `await component.scrollIntoView()` |
| `component.getWrappedElement()` / `component.getFindStrategy()` | `component.wrappedElement` / `component.findStrategy` (getters) |
| `executionSettings.browserAutomationTool` | `executionSettings.browserController` |

Some published BELLATRIX TypeScript docs still show the left-hand spellings (and `waitForPartialUrl`, per-call validation timeouts, `getAllOptions`, `getRange`/`setRange`); they do not exist in the framework source — the right-hand column is what compiles.

---

## Component Factory (`this.app.create(T)` / map `this.create(T)`)

`create(T)` returns a `ComponentService<T>`; every strategy has a single variant returning `T` and an `allBy*` variant returning `ComponentsList<T>`. Creating a component does NOT touch the browser — the element is located lazily on the first awaited call (waiting up to `findElementTimeout`).

### Single Component Creation

```ts
this.app.create(Button).byCss("[data-testid='login']")            // PREFERRED (data-testid)
this.app.create(Button).byAttribute('data-testid', 'login')        // same, exact attribute match
this.app.create(TextField).byId('email')                           // static id
this.app.create(TextField).byName('username')                      // name attribute ([name='username'])
this.app.create(Button).byXpath("//button[normalize-space()='Login']")
this.app.create(Anchor).byLinkText('Sign in')
this.app.create(Anchor).byLinkTextContaining('Sign')
this.app.create(Div).byClass('alert-danger')
this.app.create(Div).byClassContaining('woocommerce-message')
this.app.create(Heading).byTag('h1')
this.app.create(Heading).byInnerTextContaining('Billing details')
this.app.create(Anchor).byAttributeContaining('data-product_id', '28')
this.app.create(Button).byValueContaining('Apply coupon')
this.app.create(TextField).byIdContaining('email')
this.app.create(TextField).byIdEndingWith('_email')
this.app.create(Select).byNameEndingWith('orderby')
```

### Collection Creation (returns `ComponentsList<T>`)

```ts
this.app.create(Anchor).allByCss('nav a')
this.app.create(Div).allByXpath("//div[@data-testid='product-card']")
this.app.create(NumberInput).allByClassContaining('input-text qty text')
this.app.create(Heading).allByTag('h2')
this.app.create(Anchor).allByInnerTextContaining('Read more')
```

`ComponentsList<T>` has `await list.count()`, `await list.get()` (→ `T[]`) and `await list.get(i)`. There is no indexer and no `for … of` over the list itself — iterate `await list.get()`. Prefer `(await list.get())[i]` over `await list.get(i)` (see `bellatrix-web-components`).

---

## NavigationService (`this.app.navigation`)

| Member | Purpose |
|--------|---------|
| `navigate(url: string \| URL)` | Open a URL (waits for `domcontentloaded` on Playwright) |
| `navigateToLocalPage(path)` | Open a local HTML file (`file://` + resolved path) |

There is **no** `waitForPartialUrl` and no `getQueryParameter` (commented out in the framework). Read the URL with `await this.app.browser.getUrl()` and parse it with `new URL(…)`.

```ts
await this.app.navigation.navigate('https://example.com/login');
const orderId = new URL(await this.app.browser.getUrl()).searchParams.get('orderId');
```

---

## BrowserService (`this.app.browser`)

| Member | Returns | Purpose |
|--------|---------|---------|
| `getUrl()` | `Promise<string>` | Current URL |
| `getTitle()` | `Promise<string>` | Page title |
| `getPageSource()` | `Promise<string>` | Full page source (debugging only — never in generated assertions) |
| `back()` / `forward()` / `refresh()` | `Promise<void>` | History navigation |
| `waitUntilPageLoadsCompletely()` | `Promise<void>` | Wait for `document.readyState === 'complete'` (`pageLoadTimeout`) |
| `waitForAjax()` | `Promise<void>` | Wait until no XMLHttpRequests are open (`waitForAjaxTimeout`; `fetch` is not tracked) |
| `clearLocalStorage()` | `Promise<void>` | Reset client-side state |
| `takeScreenshot()` | `Promise<Image>` | Screenshot of the visible viewport, not the full page (`Image` from `@bellatrix/core/image`) |

There is **no** frame switching, tab/window switching, `maximize`, session-storage clear, `waitForAngular`, or console-log API in the TS framework.

```ts
await this.app.browser.waitUntilPageLoadsCompletely();
Assert.areEqual('Cart – Bellatrix Demos', await this.app.browser.getTitle());
Assert.stringContains('/cart', await this.app.browser.getUrl());   // substring FIRST
```

---

## Other Services

### ScriptService (`this.app.script`)

```ts
const height = await this.app.script.execute(() => document.body.scrollHeight);              // number
await this.app.script.execute((element) => element.scrollIntoView(), this.map.placeOrderButton); // a component arg arrives as its DOM element
```

A component's own `evaluate` runs a function against its element: `await field.evaluate(el => el.form?.requestSubmit())`.

### DialogService (`this.app.dialog`) — native `alert` / `confirm` / `prompt`

```ts
await this.map.deleteButton.click();                  // the click returns while the dialog is open
const text = await this.app.dialog.getMessage();
await this.app.dialog.accept();                       // OK (accept('text') answers a prompt)
await this.app.dialog.dismiss();                      // Cancel
```

With the Playwright controller (default) an unhandled dialog is **auto-dismissed after 5 seconds** — handle it right after the triggering click. Calling `accept`/`dismiss`/`getMessage` when no dialog is open throws `Dialog not found.`

### CookiesService (`this.app.cookies`)

```ts
await this.app.cookies.addCookie('cookie_consent', 'accepted');      // (name, value, path = '/')
const session = await this.app.cookies.getCookie('session');          // Cookie | null
await this.app.cookies.deleteCookie('cart');
await this.app.cookies.clearCookies();
const all = await this.app.cookies.getAllCookies();
```

### No Interactions Service

There is no drag-and-drop, double-click, context-click, or keyboard-chord API. Available instead: `component.hover()`, `component.focus()`, `component.scrollIntoView()`, and `component.evaluate(fn)` / `this.app.script.execute(fn, …)` for DOM-level work (e.g. submitting a form instead of pressing Enter). If a scenario genuinely needs drag-and-drop or key chords, tell the user it is not supported by BELLATRIX TypeScript instead of inventing an API.

### Assertions (`Assert` from `@bellatrix/core/assertions`)

`Assert` is framework-neutral and usable anywhere (tests, pages, asserts classes): `areEqual`, `areNotEqual`, `isTrue`, `isFalse`, `stringContains(substring, value)`, `isNull`, `isNotNull`, `that(value, is.greaterThan(n))`, `await Assert.multiple(...)`.

```ts
Assert.stringContains('/order-received', await this.app.browser.getUrl(), 'Order confirmation page was not opened.');
```

---

## Page Object Factories

### `this.app.createPage(TPage)`

Returns the page object **without** navigating (synchronous — no `await`). Use when the page is already open (after a click or redirect):

```ts
const cartPage = this.app.createPage(CartPage);
await cartPage.asserts.totalPrice('114.00');
```

### `await this.app.goTo(TPage)`

Returns the page object **after** calling its `open()` (navigates to the page's `url`, then runs its `waitForPageLoad()`):

```ts
const homePage = await this.app.goTo(HomePage);
await homePage.addProductById(28);
```

Both require a `WebPage` subclass decorated with `@Page(Map, Asserts)`. Pages, maps, and asserts are **singletons** — `goTo`/`createPage` return the same instance every time, so never keep test state in page fields.

---

## Configuration: `bellatrix.config.ts`

The runner finds `bellatrix.config.ts` by walking up from the current directory. The `webSettings` type is added by `@bellatrix/web/test` (a module augmentation), so the config starts with a type-only import of that module. The import is erased when the runner transpiles the config, and it also stops the order-dependent `TS2313 Type parameter 'T' has a circular constraint` errors that `tsc` can otherwise report inside `node_modules/@bellatrix/web/src/components/core/WebComponent.ts` (for example when a custom component file is type-checked before any test file).

```ts
import type {} from '@bellatrix/web/test';   // webSettings types; keep this the FIRST line
import { BellatrixConfigurationOverride } from '@bellatrix/core/types';

const config: BellatrixConfigurationOverride = {
    frameworkSettings: {
        testSettings: {
            testTimeout: 300000,
            testFramework: 'playwright',        // 'playwright' | 'vitest'
            testReporter: 'junit',              // 'junit' | 'json' | 'console-only' ('tap' is vitest-only: the runner throws for it under playwright)
            testReportDirectory: './reports',
            testReportName: 'result',
            parallelExecution: false,           // not functional yet
        },
        parallelExecutionSettings: {},
    },
    webSettings: {
        timeoutSettings: {
            findElementTimeout: 30000,          // all timeouts in MILLISECONDS
            validationTimeout: 30000,
            pageLoadTimeout: 30000,
            waitForAjaxTimeout: 30000,
            scriptTimeout: 10000,
            sleepInterval: 1000,
        },
        actionSettings: {
            delayBeforeAction: 0,
        },
        executionSettings: {
            browserController: 'playwright',    // 'playwright' | 'selenium'
            browser: 'chrome',                  // 'chrome' | 'edge' | 'firefox' | 'safari'
            viewport: { width: 1920, height: 1080 },
            startMaximized: false,
            headless: true,
            executionType: 'local',             // 'local' | 'remote'
            baseUrl: 'https://example.com/',    // opened when the browser starts
        },
    },
};

export default config;
```

| Setting | Values | Description |
|---------|--------|-------------|
| `executionSettings.browserController` | `'playwright'` (default in the example), `'selenium'` | Automation backend |
| `executionSettings.browser` | `'chrome'`, `'edge'`, `'firefox'`, `'safari'` | Browser for ALL test classes (there is no per-class/per-test browser attribute) |
| `executionSettings.executionType` | `'local'`, `'remote'` | `remote` needs `webSettings.remoteExecutionSettings` |
| `remoteExecutionSettings` | `{ provider: 'Selenium Grid' \| 'Selenoid', remoteUrl, capabilities }` or `{ provider: 'LambdaTest' \| 'BrowserStack', username, accessKey, capabilities }` | Grid / cloud |
| `timeoutSettings.*` | milliseconds | Find, validation, page-load, AJAX waits; `sleepInterval` is the polling interval |

Read settings in code with `BellatrixSettings.get()` from `@bellatrix/core/settings` — e.g. `new URL('/cart/', BellatrixSettings.get().webSettings.executionSettings.baseUrl)`. Navigation does NOT prepend `baseUrl` automatically.
