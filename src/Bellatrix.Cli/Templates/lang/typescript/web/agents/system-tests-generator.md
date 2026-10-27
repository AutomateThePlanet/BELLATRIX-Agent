<!-- lang:page-objects-step -->
3. Generate/edit page objects (3 classes per page — Map, Page, Asserts — wired by `@Page`; url-less sections for blocks shared by ≥2 pages)
<!-- lang:target-project -->
## Target Project (TypeScript / Node)

All generated web tests, page objects, sections, and models go into the BELLATRIX TypeScript web test project recorded in `bellatrix-web-project-structure` (written here as `my-web-tests/` — use the real folder). Where the shared workflow says `Pages/`, `Sections/`, `Components/`, `Models/`, `Tests/`, use the paths below:

```
my-web-tests/
  package.json                 # "type": "module"
  tsconfig.json                # strict, experimentalDecorators: false
  bellatrix.config.ts          # browser, controller, timeouts (ms), baseUrl
  src/
    Categories.ts
    attributes/ManualTestCase.ts
    utilities/waits.ts         # waitForPartialUrl, waitUntilGone
    pages/{pagename}/
      {PageName}Map.ts
      {PageName}.ts
      {PageName}Asserts.ts
    pages/index.ts             # barrel
    sections/{sectionname}/
      {SectionName}Map.ts
      {SectionName}.ts
      {SectionName}Asserts.ts
    components/
      {CustomComponent}.ts
    models/
      {ModelName}.ts
  tests/{featurearea}/
    {FeatureArea}Tests.test.ts
```

- **Imports**: `@bellatrix/web/components`, `@bellatrix/web/pages`, `@bellatrix/web/pages/decorators`, `@bellatrix/web/infrastructure` (`WebTest`, `App`), `@bellatrix/web/test` (`Test`, `TestClass`), `@bellatrix/core/decorators` (`Category`, `TestCase`), `@bellatrix/core/assertions` (`Assert`, `is`); project files by relative, extension-less paths
- **Build command**: `npx tsc --noEmit -p tsconfig.json` (type check; there is no emit step)
- **Base classes**: pages `WebPage<Map, Asserts>` + `@Page(Map, Asserts)`, maps `WebPageMap`, asserts `WebPageAsserts<Map>`, sections a url-less `WebPage`, tests `WebTest` with `@TestClass`
- **SPECTRA traceability decorator**: `@ManualTestCase('TC-XXX')` from `src/attributes/ManualTestCase.ts`. BELLATRIX TypeScript has no built-in one, and `@TestCase` is data-driven — never use it for traceability. If the file does not exist yet, create it:

```ts
// src/attributes/ManualTestCase.ts
import { getMetadataFor } from '@bellatrix/core/utilities';
import type { BellatrixTest } from '@bellatrix/core/infrastructure';
import type { Method } from '@bellatrix/core/types';

const manualTestCaseKey = 'manualTestCase';

/**
 * Links an automated test to a SPECTRA manual test case for traceability.
 * Unlike @TestCase (data-driven parameterization) this is a metadata-only marker,
 * found by SPECTRA's `@ManualTestCase('{id}')` scan pattern and by
 * `bellatrix ./tests filter --manualTestCase TC-XXX`.
 */
export function ManualTestCase<
    This extends BellatrixTest = BellatrixTest,
    ClassMethod extends Method<This> = Method<This>
>(id: string) {
    return function (testMethod: ClassMethod, context: ClassMethodDecoratorContext): void {
        if (context.kind !== 'method') {
            throw new Error('@ManualTestCase can only decorate test methods.');
        }

        const testMetadata = getMetadataFor(testMethod);
        const existing = testMetadata.customData.get(manualTestCaseKey);
        const ids = Array.isArray(existing) ? existing as string[] : [];
        ids.push(id);
        testMetadata.customData.set(manualTestCaseKey, ids);
    };
}
```

Usage (one per method — never stacked):

```ts
@Test
@ManualTestCase('TC-210')
@Category(Categories.Smoke)
async applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() {
    const cartPage = await this.app.goTo(CartPage);
    await cartPage.applyCoupon('happybirthday');
    await cartPage.asserts.couponApplied();
    await cartPage.asserts.totalPrice('54.00');
}
```

Also create `src/utilities/waits.ts` (definition in `bellatrix-web-project-structure`) before the first `waitForPartialUrl` / `waitUntilGone` use.

<!-- lang:lifecycle -->
## Browser Lifecycle Policy (TypeScript)

BELLATRIX TypeScript has **no browser/lifecycle attribute**. The lifecycle is fixed by the framework:

| Scope | Where | Behavior |
|-------|-------|----------|
| **Which browser** | `bellatrix.config.ts` → `webSettings.executionSettings` (`browser`, `browserController`, `headless`, `viewport`) | One configuration for the whole run — do not change it for a single test |
| **Browser instance** | `WebTest` (BrowserWorkflowPlugin) | Started once per **test class** before `beforeAll()`, closed after `afterAll()`; all tests of the class share it (cookies, storage, current page) |

### Rules

1. **Every test opens its own start page** — `await this.app.goTo(TPage)` (or `navigate`) at the start, never relying on where the previous test ended.
2. **Reset state in hooks**, not by restarting: `override async afterEach() { await this.app.cookies.clearCookies(); }`, plus `await this.app.browser.clearLocalStorage()` / emptying the cart through the page object when tests change them.
3. **Need a guaranteed fresh browser** (the test logs in and the session cannot be cleared by cookies; it verifies first-visit behavior such as a cookie banner, onboarding, default sort/currency; it closes windows or crashes the page)? Put it in **its own test class** — every class gets a new browser.
4. **Do NOT change `bellatrix.config.ts`** to make one test pass (browser, headless, timeouts) — report the need to the user instead.
5. **Document the isolation** with a comment above a class that exists only to get a fresh browser.

### Example

```ts
@TestClass
export class CartTests extends WebTest {
    override async afterEach() {
        await this.app.cookies.clearCookies();
    }

    @Test
    @ManualTestCase('TC-210')
    async applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }
}

// Own class = own fresh browser: verifies the cookie banner shown on the first visit
@TestClass
export class FirstVisitTests extends WebTest {
    @Test
    @ManualTestCase('TC-200')
    async firstVisit_ShowsCookieBanner() { /* ... */ }
}
```

<!-- lang:critical-rules -->
## Critical Rules (TypeScript)

- **`await` everything** — every component action/getter, wait, validation, service call, and `this.app.goTo(...)`. A missing `await` compiles and breaks at runtime; re-read each generated line for it. `this.app.createPage(...)` is the one synchronous factory
- **Page object = three classes** wired by `@Page(XMap, XAsserts)`: `X extends WebPage<XMap, XAsserts>` with `protected override get url()` (+ optional `protected override async waitForPageLoad()`), `XMap extends WebPageMap` with component **getters**, `XAsserts extends WebPageAsserts<XMap>`. Never override `open()`; never forget `@Page`. Sections are url-less `WebPage`s exposed by a page getter (`this.app.createPage(Section)`)
- **`byXpath` — lowercase p.** `byXPath` is a type error. **No `byDataTestId`** — `byCss("[data-testid='…']")` or `byAttribute('data-testid', '…')`
- **Prefer direct factories over XPath:** `[data-testid]` → `byCss`; static id → `byId`; `name` → `byName`; exact link text → `byLinkText`. Reserve `byXpath` for text-based (`normalize-space()`) or structural locators and dynamic fragments
- **Component types from the live element:** `Button`, `Anchor`, `TextField`, `PasswordField`, `EmailField`, `PhoneField`, `NumberInput`, `CheckBox`, `RadioButton`, `Select`, `Option`, `TextArea`, `Label`, `Heading`, `Span`, `Div`, `Image`, `FileInput`. No `Table`, `H1`, `ListItem`, `ComboBox`. Only `Button`/`Anchor`/`RadioButton`/`Reset` have `click()` — create clickable `div`/`span`/`li`/`img` as `Button`
- **Validations:** `validate('innerText'|'value'|'href'|'placeholder'|…).is()/.isNot()`, `validateIs('visible'|'checked'|'selected'|'clickable')`, `validateIsNot(…)` — exact names only (not type-checked). Back every `.contains()` with `Assert.stringContains(sub, await c.getInnerText())` (a timed-out `contains` can pass silently). Numbers via `Assert.areEqual(n, await c.getX())`; `maxLength`/`minLength` and values typed during the test via `evaluate(el => el.maxLength / el.value)` (`getMaxLength()` reads the `max` attribute, and with Playwright `getValue()` reads the `value` attribute, not the live value). Disabled/required/readonly via `validateIsNot('clickable')` or `evaluate(el => el.required)` — `isDisabled()`/`isRequired()`/`isReadonly()` misread boolean attributes. `Select` selection via `evaluate(el => el.options[el.selectedIndex]?.text)`
- **Waits:** `c.wait.toBeVisible()/toExist()/toBeClickable()/toBeDisabled()/toNotBeVisible()/toNotExist()` (no arguments, global timeouts in ms), `this.app.browser.waitForAjax()` / `waitUntilPageLoadsCompletely()`, project helpers `waitForPartialUrl(this.app, '…')` and `waitUntilGone(this.app, () => list)` for things that may already be gone. Never `setTimeout`; `this.app.sleep` only for a documented animation delay
- **Never drop to the raw driver** — no `resolve(BrowserController)`, no `@playwright/test` / `selenium-webdriver` imports, no `document.querySelector(...).click()` in scripts. Use `create(T).by*` (map, app, scoped component), `component.evaluate(fn)` / `this.app.script.execute(fn, …)` with **function** arguments (never `'return …'` strings), `this.app.dialog`, `this.app.cookies`
- **Unsupported in BELLATRIX TypeScript — say so, don't invent:** iframe content, tab/window switching, drag-and-drop, keyboard chords, per-call timeouts, `waitForPartialUrl` on the navigation service
- **Custom components when built-ins aren't enough:** under `src/components/`, `@BellatrixWebComponent` + `extends WebComponent<HTMLxElement>` (or a built-in, overriding `async` methods); use via `this.create(MyComponent).by…`. Never modify BELLATRIX source
- **Navigation:** `await this.app.navigation.navigate(url)`; pages via `await this.app.goTo(TPage)` (opens) / `this.app.createPage(TPage)` (already open). Pages/maps/asserts are singletons — no state in fields
- **Tests use page objects only** — no `this.app.create(...)` in test methods
- **`override`** on every overridden member (`noImplicitOverride`); TC39 decorators only (`experimentalDecorators: false`); test files end in `.test.ts`
- **Always brace control flow:** every `if`/`else`/`for`/`while`/`do` body uses `{ }`, even single statements — no `if (x) continue;`
- **SPECTRA traceability:** `@ManualTestCase('TC-XXX')` (NOT `@TestCase`) on every automated SPECTRA case, plus `@Category(Categories.Smoke)` for high priority / `@Category(Categories.Regression)` otherwise; update the SPECTRA file's `automated_by` with `my-web-tests/tests/{featurearea}/{FeatureArea}Tests.test.ts`; `spectra.config.json` `scan_patterns` include `@ManualTestCase('{id}')` and `@ManualTestCase("{id}")`
- **Build-only:** `npx tsc --noEmit -p tsconfig.json` until it exits 0. Never run the tests yourself; give the user `npx bellatrix ./tests filter --manualTestCase TC-XXX` (or `--testName {testMethodName}`)

