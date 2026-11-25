<!-- lang:page-matrix -->
| Page object folder exists for this page | **EDIT** — add new getters/methods to the existing Map, Page, and Asserts classes |
| Page object folder does NOT exist | **CREATE** — new folder with the 3 classes (`{PageName}Map`, `{PageName}`, `{PageName}Asserts`) |
<!-- lang:edit-naming -->
- **Keep the trio wired**: the page keeps `@Page({PageName}Map, {PageName}Asserts)` and `extends WebPage<{PageName}Map, {PageName}Asserts>` in sync with the Map/Asserts class names, and new pages are exported from `src/pages/index.ts`
<!-- lang:page-layout -->
Each page (or modal / wizard step) gets a dedicated lower-case folder with three classes wired together by the `@Page` decorator (TypeScript has no partial classes):

```
src/pages/
  {pagename}/
    {PageName}.ts          — @Page(Map, Asserts) class extending WebPage<Map, Asserts>: url, waitForPageLoad(), workflow methods
    {PageName}Map.ts       — class extending WebPageMap: component getters (validated locators only)
    {PageName}Asserts.ts   — class extending WebPageAsserts<Map>: assertion methods built from validate*/Assert
  index.ts                 — barrel: export * from './{pagename}/{PageName}';
```
<!-- lang:target -->
**Target project:** the BELLATRIX TypeScript web test project named in `.bellatrix/config.json` (`domains.web.project`; `{project.dir}/` = the folder holding its `package.json`, `tsconfig.json`, and `bellatrix.config.ts`).
- **Path mapping** — where this skill says `Pages/`, `Sections/`, `Components/`, `Models/`, `Tests/{FeatureArea}/{FeatureArea}Tests.*`, the TypeScript project uses `src/pages/{pagename}/`, `src/sections/{sectionname}/`, `src/components/`, `src/models/`, and `tests/{featurearea}/{FeatureArea}Tests.test.ts` (test files MUST end in `.test.ts`)
- **Imports**: framework subpaths only (`@bellatrix/web/components`, `@bellatrix/web/pages`, `@bellatrix/web/pages/decorators`, `@bellatrix/web/infrastructure`, `@bellatrix/web/test`, `@bellatrix/core/decorators`, `@bellatrix/core/assertions`); project files by relative, extension-less paths (`'../../src/pages'`)
- **Build command**: `{project.buildCommand}` (type check — there is no build output)
- **Language**: TypeScript (strict, TC39 decorators, ES modules); everything async — `await` every framework call

<!-- lang:page-objects -->
### Map Class (`{PageName}Map.ts`)

```ts
import { Anchor, Button, Div, Select, TextField } from '@bellatrix/web/components';
import { WebPageMap } from '@bellatrix/web/pages';

export class CheckoutPageMap extends WebPageMap {
    // PREFERRED — direct factories for single-attribute locators
    get emailField() {
        return this.create(TextField).byCss("[data-testid='email']");
    }

    get countrySelect() {
        return this.create(Select).byId('billing_country');
    }

    get usernameField() {
        return this.create(TextField).byName('username');
    }

    // XPath for text-based / structural locators (lowercase "p": byXpath)
    get placeOrderButton() {
        return this.create(Button).byXpath("//button[normalize-space()='Place order']");
    }

    get errorMessage() {
        return this.create(Div).byCss("[data-testid='checkout-error']");
    }

    // Dynamic locator — a method, not a getter
    removeLinkFor(productName: string) {
        return this.create(Anchor).byXpath(`//tr[td[normalize-space()='${productName}']]//a[contains(@class,'remove')]`);
    }

    // Collections
    get productCards() {
        return this.create(Div).allByCss("[data-testid='product-card']");
    }
}
```

**Locator factory rule:** `[data-testid]` / any CSS → `byCss`; static id → `byId`; `name` → `byName`; exact link text → `byLinkText`; text/structural → `byXpath`. See `bellatrix-web-locators-and-waits` for the full mapping from a validated CLI `{ strategy, selector }`.

**Component rule:** pick the type from the element recorded during execution (see the HTML → component table in `bellatrix-web-execute-scenario`). Only `Button`, `Anchor`, `RadioButton`, `Reset` have `click()` — create clickable non-buttons as `Button`.

**Getter rule:** fixed elements are getters (`get placeOrderButton()`), parameterized ones are methods. Getters are lazy — the element is located on each awaited use, so they survive re-renders. Never cache a component in a field.

### Page Class (`{PageName}.ts`)

```ts
import { WebPage } from '@bellatrix/web/pages';
import { Page } from '@bellatrix/web/pages/decorators';
import { BillingInfo } from '../../models/BillingInfo';
import { waitForPartialUrl } from '../../utilities/waits';
import { OrderReceivedPage } from '../orderreceivedpage/OrderReceivedPage';
import { CheckoutPageMap } from './CheckoutPageMap';
import { CheckoutPageAsserts } from './CheckoutPageAsserts';

@Page(CheckoutPageMap, CheckoutPageAsserts)
export class CheckoutPage extends WebPage<CheckoutPageMap, CheckoutPageAsserts> {
    protected override get url() {
        return 'https://example.com/checkout/';
    }

    // open() (inherited — never override it) navigates to url, then awaits this
    protected override async waitForPageLoad() {
        await this.map.placeOrderButton.wait.toBeVisible();
    }

    async fillBillingInfo(billingInfo: BillingInfo) {
        await this.map.emailField.setText(billingInfo.email);
        await this.map.countrySelect.selectByText(billingInfo.country);
    }

    async placeOrder(): Promise<OrderReceivedPage> {
        await this.map.placeOrderButton.click();
        await waitForPartialUrl(this.app, '/order-received');
        return this.app.createPage(OrderReceivedPage);
    }
}
```

Every wait you needed during live execution (spinner, AJAX, URL change) belongs in the action method that triggers it. Import other pages by their file path (not the `src/pages` barrel) to avoid circular imports between pages.

### Asserts Class (`{PageName}Asserts.ts`)

```ts
import { App } from '@bellatrix/web/infrastructure';
import { WebPageAsserts } from '@bellatrix/web/pages';
import { Assert } from '@bellatrix/core/assertions';
import { resolve } from '@bellatrix/core/utilities';
import { CheckoutPageMap } from './CheckoutPageMap';

export class CheckoutPageAsserts extends WebPageAsserts<CheckoutPageMap> {
    // Asserts classes have this.map but no this.app — add this getter only when an assertion needs the browser
    private get app(): App {
        return resolve(App);
    }

    async pageLoaded() {
        await this.map.placeOrderButton.validateIs('visible');
        await this.map.emailField.validateIs('visible');
    }

    async errorMessageIs(expected: string) {
        await this.map.errorMessage.validateIs('visible');
        await this.map.errorMessage.validate('innerText').is(expected);
    }

    async onCheckoutPage() {
        Assert.stringContains('/checkout', await this.app.browser.getUrl());
    }
}
```

Use the exact texts observed during live execution. Assertion methods are called as `page.asserts.errorMessageIs('…')`, so name them after the expectation without an `assert` prefix. `Select` cannot report its selection through `validate` — read it with `evaluate` (see `bellatrix-web-validations-and-assertions`).

<!-- lang:custom-components -->
### Option A — New component extending `WebComponent`

```ts
// src/components/Toast.ts
import { Button, WebComponent } from '@bellatrix/web/components';
import { BellatrixWebComponent } from '@bellatrix/web/components/decorators';

@BellatrixWebComponent
export class Toast extends WebComponent<HTMLDivElement> {
    async getInnerText(): Promise<string> {
        return await this.wrappedElement.getInnerText();
    }

    async dismiss(): Promise<void> {
        await this.create(Button).byCss("button[aria-label='Close']").click();
    }
}
```

### Option B — Child component overriding a built-in

```ts
// src/components/ScrollingButton.ts
import { Button } from '@bellatrix/web/components';
import { BellatrixWebComponent } from '@bellatrix/web/components/decorators';

@BellatrixWebComponent
export class ScrollingButton extends Button {
    override async click(): Promise<void> {
        await this.scrollIntoView();
        await super.click();
    }
}
```

### Using custom components in Map classes

```ts
import { Toast } from '../../components/Toast';
import { ScrollingButton } from '../../components/ScrollingButton';

get successToast() {
    return this.create(Toast).byCss("[data-testid='toast']");
}

get placeOrderButton() {
    return this.create(ScrollingButton).byId('place_order');
}
```

`@BellatrixWebComponent` (from `'@bellatrix/web/components/decorators'`) is MANDATORY on every custom component — without it the element is never located. Only `async` methods are wrapped with find/retry. Inside a component, locate children with the scoped `this.create(T).by…` (`.//` for relative XPath) and act on itself through `this.wrappedElement` — never through the raw driver.

<!-- lang:sections -->
TypeScript has no `WebSection` base. A section is a `WebPage` subclass **without a `url`** (wired with `@Page` like a page, never opened with `goTo`), with the same Map / class / Asserts trio under `src/sections/{sectionname}/`:

```ts
// src/sections/headersection/HeaderSectionMap.ts
import { Anchor, Span } from '@bellatrix/web/components';
import { WebPageMap } from '@bellatrix/web/pages';

export class HeaderSectionMap extends WebPageMap {
    get cartLink() {
        return this.create(Anchor).byCss("[data-testid='header-cart']");
    }

    get cartCount() {
        return this.create(Span).byCss("[data-testid='cart-count']");
    }
}

// src/sections/headersection/HeaderSectionAsserts.ts
import { WebPageAsserts } from '@bellatrix/web/pages';
import { HeaderSectionMap } from './HeaderSectionMap';

export class HeaderSectionAsserts extends WebPageAsserts<HeaderSectionMap> {
    async cartCountIs(expected: number) {
        await this.map.cartCount.validate('innerText').is(String(expected));
    }
}

// src/sections/headersection/HeaderSection.ts
import { WebPage } from '@bellatrix/web/pages';
import { Page } from '@bellatrix/web/pages/decorators';
import { HeaderSectionMap } from './HeaderSectionMap';
import { HeaderSectionAsserts } from './HeaderSectionAsserts';

@Page(HeaderSectionMap, HeaderSectionAsserts)
export class HeaderSection extends WebPage<HeaderSectionMap, HeaderSectionAsserts> {
    // no url override — a section is never opened on its own

    async openCart() {
        await this.map.cartLink.click();
        await this.app.browser.waitUntilPageLoadsCompletely();
    }
}

// src/pages/homepage/HomePage.ts — embed by composition and delegate
get header(): HeaderSection {
    return this.app.createPage(HeaderSection);
}

async openCart() {
    await this.header.openCart();
}
```

Never call `goTo(HeaderSection)` or `header.open()` — the base `url` getter throws for a section.

<!-- lang:models -->
```ts
// src/models/BillingInfo.ts
export type BillingInfo = {
    firstName: string;
    lastName: string;
    email: string;
    country: string;
    phone?: string;
    shouldCreateAccount?: boolean;
};

// src/models/ProductSort.ts — fixed option set (frozen const object, like src/Categories.ts)
export const ProductSort = Object.freeze({
    Popularity: 'Popularity',
    PriceLowToHigh: 'PriceLowToHigh',
    PriceHighToLow: 'PriceHighToLow',
} as const);

export type ProductSort = typeof ProductSort[keyof typeof ProductSort];
```

Map option values to visible option texts inside the page object (a `Record<ProductSort, string>` or a `switch` in the action method), not in the model. Prefer `type` aliases and `as const` objects over classes and `enum`s for models.

<!-- lang:tests -->
### Test Class Template

```ts
// tests/checkout/CheckoutTests.test.ts
import { Test, TestClass } from '@bellatrix/web/test';
import { Category } from '@bellatrix/core/decorators';
import { WebTest } from '@bellatrix/web/infrastructure';

import { Categories } from '../../src/Categories';
import { ManualTestCase } from '../../src/attributes/ManualTestCase';
import { CheckoutPage } from '../../src/pages';

@TestClass
export class CheckoutTests extends WebTest {
    // One browser serves every test in this class — reset shared state after each test
    override async afterEach() {
        await this.app.cookies.clearCookies();
    }

    @Test
    @ManualTestCase('TC-210')
    @Category(Categories.Smoke)
    async placeOrder_WithValidBillingInfo_ShowsOrderReceived() {
        const checkoutPage = await this.app.goTo(CheckoutPage);
        await checkoutPage.asserts.pageLoaded();

        await checkoutPage.fillBillingInfo({
            firstName: 'In',
            lastName: 'Deepthought',
            email: 'info@example.com',
            country: 'Bulgaria',
        });
        const orderReceivedPage = await checkoutPage.placeOrder();

        await orderReceivedPage.asserts.orderReceived();
    }
}
```

- `await this.app.goTo(TPage)` opens the page; `this.app.createPage(TPage)` (no `await`) wraps a page already displayed.
- `@ManualTestCase('TC-XXX')` only when the test automates a SPECTRA case (see `bellatrix-web-automate-spectra-test`).
- Lifecycle: the browser starts once per test **class** (before `beforeAll`) and closes after `afterAll`; there is no per-test restart and no browser attribute. Reset state in `afterEach`/`beforeEach`; put a test that needs a brand-new browser (first-visit cookie banner, login state that cannot be cleared) in its own test class — and comment why.

<!-- lang:code-rules -->
## Code Rules (MUST FOLLOW)

### Syntax — Avoid Type-Check and Runtime Errors
1. **`await` every framework call** — actions, getters, waits, validations, `goTo`, services. A missing `await` compiles and fails at runtime; re-read every generated line for it
2. **Three classes per page** wired by `@Page(XMap, XAsserts)`: `X extends WebPage<XMap, XAsserts>`, `XMap extends WebPageMap`, `XAsserts extends WebPageAsserts<XMap>`. Without `@Page`, `map`/`asserts` throw at runtime
3. **`protected override get url()`** (a protected getter, not a property) and `protected override async waitForPageLoad()`; never override `open()`
4. **`override`** on every overridden member (`url`, `waitForPageLoad`, `configure`, `beforeAll`, `beforeEach`, `afterEach`, `afterAll`, component methods) — `noImplicitOverride` is on
5. **`byXpath`** — lowercase **p** (`byXPath` is TS2551); `allByXpath` likewise
6. **No `byDataTestId`** — use `byCss("[data-testid='…']")` or `byAttribute('data-testid', '…')`
7. **Validations by exact name**: `validate('innerText'|'value'|'href'|'placeholder'|…)` maps to `get<Name>()`, `validateIs('visible'|'checked'|'selected'|'clickable')` maps to `is<Name>()`. The name is NOT type-checked — a typo compiles and fails at runtime. Numbers → `Assert.areEqual(n, await c.getX())` (but `maxLength`/`minLength` and live typed values only through `evaluate(el => el.maxLength / el.value)`). `contains()` must be backed by `Assert.stringContains` (see `bellatrix-web-validations-and-assertions`)
8. **Import subpaths** exactly as in the examples (`'@bellatrix/web/components'`, `'@bellatrix/web/pages'`, `'@bellatrix/web/pages/decorators'`, `'@bellatrix/web/test'`, …) — there is no root `'@bellatrix/web'` import

### Architecture
9. **Page instantiation**: `await this.app.goTo(TPage)` / `this.app.createPage(TPage)` — pages, maps, and asserts are singletons; never keep test state in their fields
10. **Lifecycle**: one browser per test class (configured in `bellatrix.config.ts`); clean up in `afterEach`; isolate clean-browser tests in their own class — see the generator agent for the full policy
11. **No direct components in tests**: tests use only page-object and section methods
12. **Methods for dynamic elements**: parameters for product names, row ids, menu texts
13. **Navigation**: `await this.app.navigation.navigate(url)` (NOT `this.app.browser.navigateToUrl`); URL waits through the project helper `waitForPartialUrl(this.app, '…')`
14. **Never the raw driver** (`resolve(BrowserController)`, `@playwright/test`, `selenium-webdriver`, `document.querySelector` actions) and never `setTimeout` waits in test-project code — BELLATRIX factories and waits only

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `while`, and `do` body MUST use `{ }` braces — even for single statements. Opening brace on the same line (the framework's style).

```ts
// WRONG
for (const card of await this.map.productCards.get()) {
    if (!(await card.isVisible())) continue;
    Assert.stringContains('€', await card.getInnerText());
}

// CORRECT
for (const card of await this.map.productCards.get()) {
    if (!(await card.isVisible())) {
        continue;
    }

    Assert.stringContains('€', await card.getInnerText());
}
```

Applies everywhere — maps, pages, asserts, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `this.app.create(Button).byXPath('…')` | `this.app.create(Button).byXpath('…')` |
| `this.create(Div).byCss('…').click()` | `this.create(Button).byCss('…').click()` (only Button/Anchor/RadioButton/Reset click) |
| `this.map.emailField.setText('x')` without `await` | `await this.map.emailField.setText('x')` |
| `get url() { … }` / `url = '…'` | `protected override get url() { return '…'; }` |
| `new CheckoutPage().open()` | `await this.app.goTo(CheckoutPage)` |
| `await this.app.createPage(CartPage)` | `this.app.createPage(CartPage)` (synchronous) |
| `@TestCase('TC-210')` for traceability | `@ManualTestCase('TC-210')` |
| `textField.clear()` / `textField.typeText('x')` | `await textField.setText('')` / `await textField.setText('x')` |
| `await c.validate('maxLength').is('20')` / `await c.getMaxLength()` (reads the `max` attribute) | `Assert.areEqual(20, await c.evaluate(el => el.maxLength))` |
| `await field.validate('value').is(typed)` after `setText` (Playwright reads the `value` ATTRIBUTE) | `Assert.areEqual(typed, await field.evaluate(el => el.value))` |
| `await select.getSelected()` (returns `void`) | `await select.evaluate(el => el.options[el.selectedIndex]?.text ?? '')` |
| `await c.wait.toBeVisible(10000)` | `await c.wait.toBeVisible()` (no per-call timeout) |
| `//Button[@Name='OK']` (desktop syntax) | `//button[normalize-space()='OK']` |
| `await this.app.sleep(2000)` as a wait | `await c.wait.toBeVisible()` / `await this.app.browser.waitForAjax()` |

<!-- lang:build -->
```bash
{project.buildCommand}
```

Run it from the repository root; it always goes through `-p <folder>/tsconfig.json` (`tsc file.ts` ignores the tsconfig and reports spurious framework errors). Exit code 0 = the build succeeds.

Common errors:
- `TS2551 Property 'byXPath' does not exist` → `byXpath`
- `TS2339 Property 'click' does not exist on type 'Div'` → create the element as `Button` (or `Anchor`)
- `TS2339` on `this.app` inside an Asserts class → add the `private get app(): App { return resolve(App); }` getter
- `TS4114 This member must have an 'override' modifier` → add `override`
- `TS2554 Expected 0 arguments` on `@TestCase('TC-…')` → use `@ManualTestCase`
- `TS2307 Cannot find module` → wrong import subpath or a missing `src/pages/index.ts` export
- `TS2313 Type parameter 'T' has a circular constraint` inside `node_modules/@bellatrix/web/src/components/core/WebComponent.ts` → not your code: `bellatrix.config.ts` must start with `import type {} from '@bellatrix/web/test';` and be the first `include` entry (see `bellatrix-web-project-structure`)
- **Not reported by tsc:** a missing `await`, a misspelled `validate('…')` name — review for both

The command the user runs afterwards (include it in the hand-off; do NOT run it yourself):

```bash
npx bellatrix ./tests filter --testName {testMethodName}
```

<!-- lang:output -->
## Output Format

### Files Created/Modified

```
[CREATED] src/pages/{pagename}/{PageName}Map.ts
[CREATED] src/pages/{pagename}/{PageName}.ts
[CREATED] src/pages/{pagename}/{PageName}Asserts.ts
[EDITED]  src/pages/index.ts — exported {PageName}
[EDITED]  src/pages/{existingpage}/{ExistingPage}Map.ts — added 3 new getters
[EDITED]  src/pages/{existingpage}/{ExistingPage}.ts — added 2 new methods
[CREATED] src/sections/{sectionname}/{SectionName}Map.ts (+ {SectionName}.ts, {SectionName}Asserts.ts)
[CREATED] src/models/{ModelName}.ts
[CREATED] tests/{featurearea}/{FeatureArea}Tests.test.ts
[EDITED]  bellatrix-web-project-structure skill — added {PageName} to the Pages table
```

### Build Verification
```
{project.buildCommand} — 0 errors
```
