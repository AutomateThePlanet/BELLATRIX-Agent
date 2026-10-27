---
description: Reference skill for BELLATRIX TypeScript web page object structure — the Map / Page / Asserts trio wired by @Page, reusable sections, models, and design patterns.
---
# BELLATRIX Web Page Objects and Patterns Reference (TypeScript)

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to type-check and run:**

### 1. Three Classes, Wired by `@Page`

TypeScript has no partial classes. A page is three classes in three files, joined at runtime by the `@Page(Map, Asserts)` decorator:

| Class | Extends | Holds |
|-------|---------|-------|
| `LoginPageMap` | `WebPageMap` (`@bellatrix/web/pages`) | component getters (locators only) |
| `LoginPage` | `WebPage<LoginPageMap, LoginPageAsserts>` + `@Page(LoginPageMap, LoginPageAsserts)` | `url`, `waitForPageLoad()`, workflow methods |
| `LoginPageAsserts` | `WebPageAsserts<LoginPageMap>` | assertion methods |

Without `@Page`, `page.map` / `page.asserts` throw `Type parameters may be lost during transpile time…` at runtime — the type checker does NOT catch a missing decorator. `@Page` comes from `'@bellatrix/web/pages/decorators'`. Pass `null` for an absent part (`@Page(LoginPageMap, null)` with `WebPage<LoginPageMap>`).

### 2. `protected override get url()` — a Getter, Not a Property

```ts
// CORRECT
@Page(LoginPageMap, LoginPageAsserts)
export class LoginPage extends WebPage<LoginPageMap, LoginPageAsserts> {
    protected override get url() {
        return 'https://example.com/login';
    }

    protected override async waitForPageLoad() {
        await this.map.loginButton.wait.toBeVisible();
    }
}

// WRONG
export class LoginPage extends WebPage<LoginPageMap, LoginPageAsserts> {     // missing @Page
    url = 'https://example.com/login';                                        // property, not a protected getter
    override async open() { await this.app.navigation.navigate(this.url); } // never override open()
}
```

`WebPage` gives you `this.map`, `this.asserts`, `this.app` (navigation, browser, script, dialog, cookies, `create`, `goTo`, `createPage`), and an inherited `async open()` that navigates to `url` and then awaits `waitForPageLoad()`. Without a `url` override, `open()` throws.

### 3. Everything Is Async

Every component call, wait, validation, and service call returns a `Promise`: page methods are `async` and `await` every call. A missing `await` compiles silently.

### 4. Validations in the Asserts Class

Use `validate('…').is(…)`, `validateIs('…')`, `validateIsNot('…')` and `Assert` — see `bellatrix-web-validations-and-assertions`.

### 5. Exact Import Subpaths

```ts
import { Anchor, Button, TextField } from '@bellatrix/web/components';
import { WebPage, WebPageAsserts, WebPageMap } from '@bellatrix/web/pages';
import { Page } from '@bellatrix/web/pages/decorators';
```

---

## Project Structure (3-File Pattern)

```
src/
  pages/
    loginpage/
      LoginPage.ts            # @Page(Map, Asserts) — url, waitForPageLoad(), workflow methods
      LoginPageMap.ts         # component getters (locators only)
      LoginPageAsserts.ts     # assertion methods
    cartpage/
      CartPage.ts
      CartPageMap.ts
      CartPageAsserts.ts
    index.ts                  # barrel: export * from './loginpage/LoginPage'; …
  sections/
    headersection/
      HeaderSection.ts        # @Page(Map, Asserts) — no url
      HeaderSectionMap.ts
      HeaderSectionAsserts.ts
  models/
    LoginCredentials.ts
    BillingInfo.ts
tests/
  login/LoginTests.test.ts
  cart/CartTests.test.ts
```

---

## Complete 3-File Example: LoginPage

### LoginPageMap.ts

```ts
import { Anchor, Button, CheckBox, Div, Heading, PasswordField, TextField } from '@bellatrix/web/components';
import { WebPageMap } from '@bellatrix/web/pages';

export class LoginPageMap extends WebPageMap {
    // ===== FORM FIELDS =====
    get emailField() {
        return this.create(TextField).byCss("[data-testid='email']");
    }

    get passwordField() {
        return this.create(PasswordField).byId('password');
    }

    get rememberMeCheckBox() {
        return this.create(CheckBox).byName('remember');
    }

    // ===== BUTTONS / LINKS =====
    get loginButton() {
        return this.create(Button).byXpath("//button[normalize-space()='Log In']");
    }

    get forgotPasswordLink() {
        return this.create(Anchor).byLinkText('Forgot password?');
    }

    // ===== MESSAGES =====
    get errorMessage() {
        return this.create(Div).byCss("[data-testid='login-error']");
    }

    get pageHeading() {
        return this.create(Heading).byTag('h1');
    }
}
```

### LoginPage.ts

```ts
import { WebPage } from '@bellatrix/web/pages';
import { Page } from '@bellatrix/web/pages/decorators';
import { LoginCredentials } from '../../models/LoginCredentials';
import { waitForPartialUrl } from '../../utilities/waits';
import { DashboardPage } from '../dashboardpage/DashboardPage';
import { LoginPageMap } from './LoginPageMap';
import { LoginPageAsserts } from './LoginPageAsserts';

@Page(LoginPageMap, LoginPageAsserts)
export class LoginPage extends WebPage<LoginPageMap, LoginPageAsserts> {
    protected override get url() {
        return 'https://example.com/login';
    }

    protected override async waitForPageLoad() {
        await this.map.loginButton.wait.toBeVisible();
    }

    async loginWith(email: string, password: string) {
        await this.map.emailField.setText(email);
        await this.map.passwordField.setPassword(password);
        await this.map.loginButton.click();
    }

    async loginWithCredentials(credentials: LoginCredentials) {
        await this.loginWith(credentials.email, credentials.password);
    }

    async loginWithRememberMe(email: string, password: string) {
        await this.map.emailField.setText(email);
        await this.map.passwordField.setPassword(password);
        await this.map.rememberMeCheckBox.check();
        await this.map.loginButton.click();
    }

    async loginAndGoToDashboard(email: string, password: string): Promise<DashboardPage> {
        await this.loginWith(email, password);
        await waitForPartialUrl(this.app, '/dashboard');
        return this.app.createPage(DashboardPage);
    }
}
```

TypeScript has no method overloading by parameter list — give each variant its own name (`loginWith`, `loginWithCredentials`).

### LoginPageAsserts.ts

```ts
import { App } from '@bellatrix/web/infrastructure';
import { WebPageAsserts } from '@bellatrix/web/pages';
import { Assert } from '@bellatrix/core/assertions';
import { resolve } from '@bellatrix/core/utilities';
import { LoginPageMap } from './LoginPageMap';

export class LoginPageAsserts extends WebPageAsserts<LoginPageMap> {
    private get app(): App {
        return resolve(App);
    }

    async pageLoaded() {
        await this.map.pageHeading.validate('innerText').is('Log in');
        await this.map.emailField.validateIs('visible');
        await this.map.passwordField.validateIs('visible');
        await this.map.loginButton.validateIs('clickable');
    }

    async errorMessageContains(expectedText: string) {
        await this.map.errorMessage.validateIs('visible');
        await this.map.errorMessage.validate('innerText').contains(expectedText);
        Assert.stringContains(expectedText, await this.map.errorMessage.getInnerText());   // contains() alone can pass silently
    }

    async stillOnLoginPage() {
        Assert.stringContains('/login', await this.app.browser.getUrl());
    }
}
```

Asserts classes get `this.map` from `@Page`, but no `this.app`; add the `private get app()` getter shown above only when an assertion needs the browser (URL, title, dialog). There are no built-in URL assertions (`AssertLandedOnPage` has no TS equivalent) — use `Assert.stringContains(…, await this.app.browser.getUrl())`.

---

## Design Patterns

### 1. Getter-Based Component Definitions (Recommended)

Getters are lazy — each access creates a fresh component that is located on its first awaited call, so they survive re-renders:

```ts
get placeOrderButton() {
    return this.create(Button).byId('place_order');
}
```

### 2. Method-Based for Dynamic Elements

When the locator depends on runtime data, expose a method with an interpolated locator:

```ts
addToCartButton(productId: number) {
    return this.create(Anchor).byAttributeContaining('data-product_id', productId.toString());
}

removeButtonFor(productName: string) {
    return this.create(Anchor).byXpath(`//tr[td[normalize-space()='${productName}']]//a[@aria-label='Remove this item']`);
}
```

Prefer a direct factory when only one attribute varies; use `byXpath` only when the dynamic locator is genuinely structural or text-based.

### 3. Collections for Repeated Elements

```ts
// CartPageMap
get quantityBoxes() {
    return this.create(NumberInput).allByClassContaining('input-text qty text');
}

// CartPage
async updateProductQuantity(productNumber: number, newQuantity: number) {
    const boxes = await this.map.quantityBoxes.get();
    if (productNumber > boxes.length) {
        throw new Error('There are fewer items in the cart than the requested product number.');
    }

    await boxes[productNumber - 1].setNumber(newQuantity);
    await this.map.updateCartButton.click();
    await this.app.browser.waitForAjax();
}
```

### 4. Navigation Methods Return Page Objects

```ts
async proceedToCheckout(): Promise<CheckoutPage> {
    await this.map.proceedToCheckoutButton.click();
    await waitForPartialUrl(this.app, '/checkout');
    return this.app.createPage(CheckoutPage);
}
```

### 5. Fluent Interface for Chaining

Async methods return promises, so chain with sequential `await`s rather than dots:

```ts
async withEmail(email: string): Promise<this> {
    await this.map.emailField.setText(email);
    return this;
}

async withPassword(password: string): Promise<this> {
    await this.map.passwordField.setPassword(password);
    return this;
}

// Usage: await (await loginPage.withEmail('user@example.com')).withPassword('secret');
```

Prefer one intention-revealing workflow method (`loginWith(email, password)`) over long fluent chains in TypeScript.

### 6. Organizing Complex Maps with Regions

Group getters with `// ===== REGION =====` comments inside the Map class:

```ts
export class CheckoutPageMap extends WebPageMap {
    // ===== BILLING DETAILS =====
    get billingFirstName() { return this.create(TextField).byId('billing_first_name'); }
    get billingLastName() { return this.create(TextField).byId('billing_last_name'); }
    get billingCountry() { return this.create(Select).byId('billing_country'); }
    get billingPhone() { return this.create(PhoneField).byId('billing_phone'); }
    get billingEmail() { return this.create(EmailField).byId('billing_email'); }

    // ===== PAYMENT =====
    get checkPaymentsRadioButton() { return this.create(RadioButton).byAttributeContaining('for', 'payment_method_cheque'); }

    // ===== ACTIONS =====
    get placeOrderButton() { return this.create(Button).byId('place_order'); }
}
```

### 7. Reusable Sections Across Pages (Composition)

Headers, navigation bars, footers, cookie banners, and mini-carts appear on many pages. TypeScript has no `WebSection`; model each as a **url-less `WebPage`** with its own Map / class / Asserts trio under `src/sections/{sectionname}/`:

```ts
// src/sections/headersection/HeaderSectionMap.ts
import { Anchor, SearchField, Span } from '@bellatrix/web/components';
import { WebPageMap } from '@bellatrix/web/pages';

export class HeaderSectionMap extends WebPageMap {
    get searchField() { return this.create(SearchField).byCss("[data-testid='header-search']"); }
    get cartLink() { return this.create(Anchor).byCss("[data-testid='header-cart']"); }
    get cartCount() { return this.create(Span).byCss("[data-testid='cart-count']"); }
    menuItem(text: string) { return this.create(Anchor).byLinkText(text); }
}

// src/sections/headersection/HeaderSection.ts
import { WebPage } from '@bellatrix/web/pages';
import { Page } from '@bellatrix/web/pages/decorators';
import { HeaderSectionMap } from './HeaderSectionMap';
import { HeaderSectionAsserts } from './HeaderSectionAsserts';

@Page(HeaderSectionMap, HeaderSectionAsserts)
export class HeaderSection extends WebPage<HeaderSectionMap, HeaderSectionAsserts> {
    // no url — never open a section on its own

    async searchFor(term: string) {
        await this.map.searchField.setSearch(term);
        await this.map.searchField.evaluate(el => el.form?.requestSubmit());   // no keyboard API — submit the form
        await this.app.browser.waitUntilPageLoadsCompletely();
    }

    async openMenu(text: string) {
        await this.map.menuItem(text).click();
        await this.app.browser.waitUntilPageLoadsCompletely();
    }
}

// src/sections/headersection/HeaderSectionAsserts.ts
import { WebPageAsserts } from '@bellatrix/web/pages';
import { HeaderSectionMap } from './HeaderSectionMap';

export class HeaderSectionAsserts extends WebPageAsserts<HeaderSectionMap> {
    async cartCountIs(expected: number) {
        await this.map.cartCount.validate('innerText').is(expected.toString());
    }
}
```

Embed the section in each page class and delegate:

```ts
// src/pages/homepage/HomePage.ts
@Page(HomePageMap, HomePageAsserts)
export class HomePage extends WebPage<HomePageMap, HomePageAsserts> {
    protected override get url() {
        return 'https://example.com/';
    }

    get header(): HeaderSection {
        return this.app.createPage(HeaderSection);
    }

    async searchFor(term: string) {
        await this.header.searchFor(term);
    }
}
```

Tests may also get a section directly: `const header = this.app.createPage(HeaderSection);` — never `goTo(HeaderSection)`.

**Rules of thumb:**
- Extract a section only after the duplication is real (≥2 pages). Don't pre-extract for hypothetical reuse.
- Keep the public action names on each page unchanged — the section is an implementation detail.
- A section owns its own locators AND its own workflow methods; a pure locator group stays on the page as a region.
- If the shared block's locators differ per page, do NOT force a section.

---

## Model Types

Place data models in `src/models/` as `type` aliases (the example project's `PurchaseInfo` style):

```ts
// src/models/BillingInfo.ts
export type BillingInfo = {
    firstName: string;
    lastName: string;
    country: string;
    email: string;
    phone?: string;
    shouldCreateAccount?: boolean;
};
```

Usage in a page object:

```ts
async fillBillingInfo(billingInfo: BillingInfo) {
    await this.map.billingFirstName.setText(billingInfo.firstName);
    await this.map.billingLastName.setText(billingInfo.lastName);
    await this.map.billingCountry.selectByText(billingInfo.country);
    await this.map.billingEmail.setEmail(billingInfo.email);

    if (billingInfo.phone !== undefined) {
        await this.map.billingPhone.setPhone(billingInfo.phone);
    }

    if (billingInfo.shouldCreateAccount) {
        await this.map.createAccountCheckBox.check();
    }
}
```

Fixed option sets (sort orders, payment methods) also go in `src/models/` as frozen `as const` objects plus a matching type, mapped to visible texts inside the page object.

---

## Best Practices Summary

1. **One lower-case folder per page** with 3 classes (`XMap`, `X`, `XAsserts`) wired by `@Page`; one folder per section under `src/sections/`; every page exported from `src/pages/index.ts`
2. **Map class**: component getters and locator methods only — no logic
3. **Page class**: `@Page`, `extends WebPage<XMap, XAsserts>`, `protected override get url()`, optional `waitForPageLoad()`, business workflows — no assertions (a `wait.*` used as a synchronization point is acceptable)
4. **Asserts class**: assertion methods built from `validate*` / `Assert` — no actions
5. **`await this.app.goTo(TPage)` / `this.app.createPage(TPage)`** — never `new` + `open()`
6. **No `this.app.create(...)`** in test files — always go through page objects and sections
7. **Descriptive, business-level method names** (`applyCoupon`, `fillBillingInfo`), not UI mechanics (`clickButton3`)
8. **`await` everything** — and never store state on the (singleton) page, map, or asserts instances
