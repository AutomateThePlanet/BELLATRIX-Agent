---
description: Reference skill for BELLATRIX TypeScript web test classes (@TestClass/@Test on WebTest), the App object, the one-browser-per-class lifecycle configured in bellatrix.config.ts, setup hooks, the runner, and naming conventions.
---
# BELLATRIX Web Test Structure and Lifecycle Reference (TypeScript)

## Test Base Class

```ts
// tests/login/LoginTests.test.ts
import { Test, TestClass } from '@bellatrix/web/test';
import { Category } from '@bellatrix/core/decorators';
import { WebTest } from '@bellatrix/web/infrastructure';

import { Categories } from '../../src/Categories';

@TestClass
@Category(Categories.CI)
export class LoginTests extends WebTest {
    @Test
    @Category(Categories.Smoke)
    async login_WithValidCredentials_OpensDashboard() {
        // test code here — every framework call awaited
    }
}
```

| C# / NUnit concept | TypeScript |
|--------------------|------------|
| `[TestFixture]` / `[TestClass]` | `@TestClass` (alias `@Suite`) from `'@bellatrix/web/test'` — keep it the TOP class decorator |
| `[Test]` / `[TestMethod]` | `@Test` from `'@bellatrix/web/test'` |
| `: WebTest` | `extends WebTest` from `'@bellatrix/web/infrastructure'` |
| `[Category("Smoke")]` | `@Category('Smoke')` / `@Category(Categories.Smoke)` from `'@bellatrix/core/decorators'` — on the class (all its tests) or a method |
| `[Ignore]` | `@Ignore` (`'@bellatrix/core/decorators'`); `@Only` runs just that test while debugging — never commit it |
| `[TestCase(a, b)]` (data-driven) | `@TestCase(a, b)` from `'@bellatrix/core/decorators'` — arguments must match the method parameters |
| `[ManualTestCase("TC-1")]` | the project's `@ManualTestCase('TC-1')` (see `bellatrix-web-project-structure`) — NEVER `@TestCase` |

Rules: the file name MUST end in `.test.ts` (the runner only collects `**/*.test.ts`); test methods are `async`, take no parameters (except `@TestCase` methods), and are camelCase; the class is `export`ed.

---

## The App Object

`this.app` (type `App` from `'@bellatrix/web/infrastructure'`) is the entry point for all web interactions. It is a getter on `WebTest` in tests and on `WebPage` in page objects and sections.

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `this.app.create(T).by…(…)` | Create components (`byCss`, `byId`, `byXpath`, …; `allBy…` for lists) |
| Navigation | `this.app.navigation` | `navigate(url)`, `navigateToLocalPage(path)` |
| Browser | `this.app.browser` | `getUrl()`, `getTitle()`, `back/forward/refresh()`, `waitForAjax()`, `waitUntilPageLoadsCompletely()`, `clearLocalStorage()`, `takeScreenshot()` |
| Script | `this.app.script` | `execute(fn, …args)` |
| Dialog | `this.app.dialog` | Native alert / confirm / prompt: `getMessage()`, `accept()`, `dismiss()` |
| Cookies | `this.app.cookies` | `addCookie`, `getCookie`, `deleteCookie`, `clearCookies`, `getAllCookies` |
| GoTo | `await this.app.goTo(TPage)` | Page object + `open()` |
| CreatePage | `this.app.createPage(TPage)` | Page object, no navigation (synchronous) |
| Sleep | `await this.app.sleep(ms)` | Last-resort pause — prefer a real wait |

There is no `App.Wait`, `App.Components`, `App.Interactions`, `App.JavaScript`, `App.Dialogs`, or `App.Assert` in TypeScript. Assertions come from `Assert` in `'@bellatrix/core/assertions'`. See `bellatrix-web-app-services`.

---

## Browser Lifecycle (no Browser attribute)

TypeScript BELLATRIX has **no `[Browser]` attribute and no lifecycle choice**:

| Aspect | Behavior |
|--------|----------|
| Which browser | `webSettings.executionSettings.browser` in `bellatrix.config.ts` (`'chrome'`, `'edge'`, `'firefox'`, `'safari'`) — one value for the whole run |
| Controller | `executionSettings.browserController`: `'playwright'` or `'selenium'` |
| Headless / window | `executionSettings.headless`, `viewport: { width, height }`, `startMaximized` |
| When it starts | once per **test class**, before `beforeAll()` (the `BrowserWorkflowPlugin` that `WebTest.configure()` registers) — it opens `baseUrl` first |
| When it closes | after the class's `afterAll()` |
| Between tests of one class | the SAME browser and context — cookies, local storage, and the current page carry over |

Consequences for generated tests:
- **Every test opens its own start page** (`await this.app.goTo(TPage)`) so it does not depend on where the previous test left the browser.
- **Reset shared state in hooks**: `await this.app.cookies.clearCookies()`, `await this.app.browser.clearLocalStorage()`, emptying the cart through the page object.
- **Need a guaranteed fresh browser** (first-visit cookie banner, onboarding, auth that cannot be cleared)? Put those tests in **their own test class** — each class gets a new browser — and comment why.

### Config

```ts
// bellatrix.config.ts (excerpt — full file in bellatrix-web-app-services)
webSettings: {
    executionSettings: {
        browserController: 'playwright',
        browser: 'chrome',
        headless: true,
        viewport: { width: 1920, height: 1080 },
        executionType: 'local',
        baseUrl: 'https://example.com/',
    },
},
```

---

## Setup and Teardown Hooks

Override the `WebTest` hooks — all `async`, all with `override`:

```ts
@TestClass
export class CartTests extends WebTest {
    // Once per run (first test class only) — ALWAYS call super: it registers the browser plugin
    override async configure() {
        await super.configure();
        addPlugin(ScreenshotOnFailPlugin);            // optional, from '@bellatrix/extras-web/plugins'
    }

    // Once per class, after the browser started
    override async beforeAll() {
        await this.app.cookies.addCookie('cookie_consent', 'accepted');
    }

    // Before each test
    override async beforeEach() {
        await this.app.browser.clearLocalStorage();
    }

    // After each test
    override async afterEach() {
        await this.app.cookies.clearCookies();
    }

    // Once per class, before the browser closes
    override async afterAll() {
        // class-level cleanup
    }
}
```

`addPlugin` comes from `'@bellatrix/core/infrastructure'`. `ScreenshotOnFailPlugin` only writes files when `screenshotOnFailPluginSettings: { isPluginEnabled: true, outputPath: './reports/screenshots', shouldCreateFolderPerSuite: false }` is in `bellatrix.config.ts` as a **top-level key** of the config object (next to `webSettings`, not inside it — inside it fails with TS2353); the key only type-checks when some file in the program imports `@bellatrix/extras-web/plugins`. There is no assembly bootstrap file (no `TestsInitialize` equivalent) — the plugin disposes the browser.

---

## Running Tests (the USER runs them — never the agent)

```bash
npx bellatrix ./tests                                          # everything under ./tests (**/*.test.ts)
npx bellatrix ./tests filter --category Smoke                  # by @Category
npx bellatrix ./tests filter --manualTestCase TC-210           # by @ManualTestCase
npx bellatrix ./tests filter --testName login_WithValidCredentials_OpensDashboard
npx bellatrix ./tests filter --suiteName LoginTests
```

Filter values are anchored regular expressions. The agent's own verification is the type check only: `npx tsc --noEmit -p tsconfig.json`.

---

## Test Naming Convention

Use `action_Condition_ExpectedResult` — the shared `Action_Condition_ExpectedResult` pattern with a lower-case first letter:

```ts
// Good
async login_WithValidCredentials_OpensDashboard() {}
async applyCoupon_WithExpiredCode_ShowsErrorMessage() {}
async addToCart_WhenOutOfStock_DisablesButton() {}

// Bad
async testLogin() {}
async test1() {}
async verifyCartWorks() {}
```

---

## Test Categories

```ts
@Category(Categories.Smoke)       // critical path, every build
@Category(Categories.Regression)  // full suite
@Category(Categories.CI)          // fast, stable tests for CI pipelines
```

`Categories` is the frozen object in `src/Categories.ts`; add a name there before using it.

---

## Complete Test Class Template

```ts
// tests/checkout/CheckoutTests.test.ts
import { Test, TestClass } from '@bellatrix/web/test';
import { Category } from '@bellatrix/core/decorators';
import { WebTest } from '@bellatrix/web/infrastructure';

import { Categories } from '../../src/Categories';
import { CheckoutPage, HomePage } from '../../src/pages';

@TestClass
export class CheckoutTests extends WebTest {
    override async afterEach() {
        await this.app.cookies.clearCookies();
    }

    @Test
    @Category(Categories.Smoke)
    async applyCoupon_WithValidCode_ReducesTotal() {
        const homePage = await this.app.goTo(HomePage);
        const cartPage = await homePage.addProductToCart(28);

        await cartPage.applyCoupon('happybirthday');

        await cartPage.asserts.couponApplied();
        await cartPage.asserts.totalPrice('54.00');
    }

    @Test
    @Category(Categories.Regression)
    async placeOrder_WithValidBillingInfo_ShowsOrderReceived() {
        const checkoutPage = await this.app.goTo(CheckoutPage);
        await checkoutPage.fillBillingInfo({
            firstName: 'In',
            lastName: 'Deepthought',
            country: 'Bulgaria',
            email: 'info@example.com',
        });
        const orderReceivedPage = await checkoutPage.placeOrder();

        await orderReceivedPage.asserts.orderReceived();
    }
}
```

Tests contain only page-object calls — no `this.app.create(…)` in test methods.

---

## Required Imports

```ts
// Test decorators (web-typed) and base class
import { Test, TestClass } from '@bellatrix/web/test';
import { WebTest } from '@bellatrix/web/infrastructure';

// Categories, data-driven cases, skipping
import { Category, TestCase, Ignore } from '@bellatrix/core/decorators';

// Assertions (only when a test asserts a browser-level value directly)
import { Assert } from '@bellatrix/core/assertions';

// Plugins (optional)
import { addPlugin } from '@bellatrix/core/infrastructure';
import { ScreenshotOnFailPlugin } from '@bellatrix/extras-web/plugins';

// Project
import { Categories } from '../../src/Categories';
import { ManualTestCase } from '../../src/attributes/ManualTestCase';
import { CheckoutPage } from '../../src/pages';
```
