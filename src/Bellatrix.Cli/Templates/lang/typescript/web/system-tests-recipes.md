# BELLATRIX Web System Tests Recipes (TypeScript)

> **Note:** These recipes show direct component usage (`this.app.create(…)`) so each pattern is visible in one place. For production code, always put locators and workflows in **Page Objects** (see `bellatrix-web-page-objects-and-patterns`); tests then call page-object methods only.
>
> **Page Object Structure:**
> ```
> src/pages/{pagename}/{PageName}.ts         - @Page(Map, Asserts), url, waitForPageLoad(), workflows
> src/pages/{pagename}/{PageName}Map.ts      - component getters
> src/pages/{pagename}/{PageName}Asserts.ts  - assertion methods using validate*/Assert
> ```
>
> Every call is `async` — every line below that touches the browser is `await`ed.

---

## Login Flow Recipe

```ts
// tests/login/LoginTests.test.ts
import { Test, TestClass } from '@bellatrix/web/test';
import { Category } from '@bellatrix/core/decorators';
import { WebTest } from '@bellatrix/web/infrastructure';
import { Button, Div, Heading, PasswordField, TextField } from '@bellatrix/web/components';
import { Assert } from '@bellatrix/core/assertions';

import { Categories } from '../../src/Categories';
import { waitForPartialUrl } from '../../src/utilities/waits';

@TestClass
export class LoginTests extends WebTest {
    override async afterEach() {
        await this.app.cookies.clearCookies();   // one browser per class — log out by clearing cookies
    }

    @Test
    @Category(Categories.Smoke)
    async login_WithValidCredentials_OpensDashboard() {
        // Arrange
        await this.app.navigation.navigate('https://example.com/login');
        const emailField = this.app.create(TextField).byCss("[data-testid='email']");
        const passwordField = this.app.create(PasswordField).byId('password');
        const loginButton = this.app.create(Button).byXpath("//button[normalize-space()='Log In']");

        // Act
        await emailField.setText('user@example.com');
        await passwordField.setPassword('Password123');
        await loginButton.click();

        // Assert
        await waitForPartialUrl(this.app, '/dashboard');
        const welcome = this.app.create(Heading).byCss("[data-testid='welcome']");
        await welcome.validate('innerText').contains('Welcome');
        Assert.stringContains('Welcome', await welcome.getInnerText());
    }

    @Test
    @Category(Categories.Regression)
    async login_WithInvalidPassword_ShowsErrorMessage() {
        await this.app.navigation.navigate('https://example.com/login');
        await this.app.create(TextField).byCss("[data-testid='email']").setText('user@example.com');
        await this.app.create(PasswordField).byId('password').setPassword('wrong');
        await this.app.create(Button).byXpath("//button[normalize-space()='Log In']").click();

        const error = this.app.create(Div).byCss("[data-testid='login-error']");
        await error.validateIs('visible');
        await error.validate('innerText').is('Invalid email or password.');
    }
}
```

---

## Form Submission Recipe

```ts
@Test
async register_WithValidData_ShowsSuccessMessage() {
    await this.app.navigation.navigate('https://example.com/register');

    await this.app.create(TextField).byId('name').setText('John Doe');
    await this.app.create(EmailField).byId('email').setEmail('john@example.com');
    await this.app.create(PhoneField).byId('phone').setPhone('+359888123456');
    await this.app.create(DateInput).byId('birthdate').setDate(new Date(1990, 4, 17));   // month is 0-based
    await this.app.create(Select).byId('country').selectByText('United States');
    await this.app.create(RadioButton).byCss("input[name='plan'][value='pro']").click();
    await this.app.create(CheckBox).byCss("[data-testid='terms']").check();
    await this.app.create(TextArea).byId('notes').setText('Please call after 5 PM.');
    await this.app.create(Button).byXpath("//button[@type='submit']").click();

    const success = this.app.create(Div).byCss("[data-testid='success']");
    await success.validate('innerText').is('Registration successful');
}
```

---

## Field Validation Recipe

```ts
@Test
async register_WithEmptyRequiredFields_ShowsValidationErrors() {
    await this.app.navigation.navigate('https://example.com/register');

    const email = this.app.create(EmailField).byId('email');
    Assert.isTrue(await email.evaluate(el => el.required), 'Email should be required.');   // isRequired() misreads boolean attributes
    await email.validate('placeholder').is('you@example.com');

    await this.app.create(Button).byXpath("//button[@type='submit']").click();

    await this.app.create(Span).byXpath("//input[@id='email']/following-sibling::span[contains(@class,'error')]")
        .validate('innerText').is('Email is required.');
    await this.app.create(Button).byXpath("//button[@type='submit']").validateIs('visible');
}
```

---

## Dropdown (Select) Recipe

```ts
@Test
async sortProducts_ByPriceLowToHigh_SelectsOption() {
    await this.app.navigation.navigate('https://demos.bellatrix.solutions/');

    const sortDropDown = this.app.create(Select).byNameEndingWith('orderby');
    await sortDropDown.selectByText('Sort by price: low to high');

    // Select.getSelected() is unimplemented (typed void) — read the selected option's text from the DOM
    const selectedText = await sortDropDown.evaluate(el => el.options[el.selectedIndex]?.text ?? '');
    Assert.areEqual('Sort by price: low to high', selectedText.trim());
}
```

### Custom (non-`<select>`) dropdown

```ts
await this.app.create(Button).byCss("[data-testid='currency-trigger']").click();
await this.app.create(Button).byXpath("//*[@role='option' and normalize-space()='EUR']").click();   // clickable li/div → Button
await this.app.create(Span).byCss("[data-testid='currency-value']").validate('innerText').is('EUR');
```

---

## Table / Row Selection Recipe

```ts
@Test
async viewOrder_FromOrdersTable_OpensOrderDetails() {
    await this.app.navigation.navigate('https://example.com/account/orders');

    // Locate the action inside the row identified by its visible text
    const viewLink = this.app.create(Anchor).byXpath(
        "//tr[td[normalize-space()='#4021']]//a[normalize-space()='View']");
    await viewLink.click();

    const heading = this.app.create(Heading).byTag('h1');
    await heading.validate('innerText').contains('Order #4021');
    Assert.stringContains('Order #4021', await heading.getInnerText());
}

@Test
async cartRows_AfterAddingTwoProducts_ShowsTwoRows() {
    await this.app.navigation.navigate('https://example.com/cart');

    const rows = this.app.create(Div).allByCss('tr.cart_item');
    Assert.areEqual(2, await rows.count());
}
```

There is no `Table` component: read cells with row-scoped components — `row.create(Span).byXpath(".//td[@class='product-price']//span")` for each `row` in `await rows.get()`.

---

## Dialog Confirmation Recipe (native alert / confirm)

```ts
@Test
async deleteAddress_WhenConfirmed_RemovesAddress() {
    await this.app.navigation.navigate('https://example.com/account/addresses');

    await this.app.create(Button).byXpath("//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").click();

    // Handle the dialog immediately — Playwright auto-dismisses it after 5 seconds
    Assert.areEqual('Delete this address?', await this.app.dialog.getMessage());
    await this.app.dialog.accept();                                   // OK

    await waitUntilGone(this.app, () => this.app.create(Span).allByXpath("//span[normalize-space()='Home']"));
}

@Test
async deleteAddress_WhenCancelled_KeepsAddress() {
    await this.app.navigation.navigate('https://example.com/account/addresses');

    await this.app.create(Button).byXpath("//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").click();
    await this.app.dialog.dismiss();                                  // Cancel

    await this.app.create(Span).byXpath("//span[normalize-space()='Home']").validateIs('visible');
}
```

### HTML modal (not a native dialog)

```ts
await this.app.create(Button).byCss("[data-testid='open-delete-modal']").click();
const modal = this.app.create(Div).byCss("[role='dialog']");
await modal.wait.toBeVisible();
await modal.create(Button).byXpath(".//button[normalize-space()='Confirm']").click();
await modal.wait.toNotBeVisible();   // same instance — it was found while visible, so the wait can observe it closing
```

---

## Wait / AJAX Recipe

```ts
@Test
async applyCoupon_WithValidCode_ShowsSuccessAndNewTotal() {
    await this.app.navigation.navigate('https://demos.bellatrix.solutions/cart/');

    await this.app.create(TextField).byId('coupon_code').setText('happybirthday');
    await this.app.create(Button).byValueContaining('Apply coupon').click();
    await this.app.browser.waitForAjax();

    const message = this.app.create(Div).byClassContaining('woocommerce-message');
    await message.wait.toBeVisible();
    await message.validate('innerText').is('Coupon code applied successfully.');

    await this.app.create(Span).byXpath("//*[@class='order-total']//span").validate('innerText').is('54.00€');
}
```

---

## Hover Menu Recipe

```ts
@Test
async openSubmenu_ByHover_NavigatesToCategory() {
    await this.app.navigation.navigate('https://example.com/');

    await this.app.create(Anchor).byLinkText('Products').hover();

    const rocketsLink = this.app.create(Anchor).byLinkText('Rockets');
    await rocketsLink.wait.toBeVisible();
    await rocketsLink.click();

    await waitForPartialUrl(this.app, '/category/rockets');
}
```

---

## iframe Recipe — NOT SUPPORTED

BELLATRIX TypeScript cannot reach elements inside an `<iframe>`: CSS/XPath do not cross frame boundaries and there is no frame-switching API (the `Frame` component only exposes `getName()`/`getHref()`). Do not invent one. When a scenario needs iframe content (embedded payment fields, rich-text editors):

1. Tell the user the step is not automatable with BELLATRIX TypeScript.
2. If the frame's document is same-origin and has its own URL, an acceptable fallback is to test that URL directly:

```ts
const frameSrc = await this.app.create(Frame).byCss("iframe[title='Terms']").getAttribute('src');
await this.app.navigation.navigate(new URL(frameSrc, await this.app.browser.getUrl()));
```

---

## New Tab Recipe (navigate instead of switching)

```ts
@Test
async openTerms_FromRegistration_ShowsTermsPage() {
    await this.app.navigation.navigate('https://example.com/register');

    const termsLink = this.app.create(Anchor).byLinkText('Terms and Conditions');
    await termsLink.validate('target').is('_blank');

    // No tab switching in BELLATRIX TypeScript — open the link's target in the current tab
    const href = await termsLink.getHref();   // Playwright returns the raw attribute (may be relative)
    await this.app.navigation.navigate(new URL(href, await this.app.browser.getUrl()));
    await this.app.browser.waitUntilPageLoadsCompletely();

    Assert.stringContains('/terms', await this.app.browser.getUrl());
}
```

---

## File Upload Recipe

```ts
import { resolve as resolvePath } from 'path';

@Test
async uploadAvatar_WithPng_ShowsPreview() {
    await this.app.navigation.navigate('https://example.com/account/profile');

    const filePath = resolvePath('test-data', 'avatar.png');   // relative to the folder the runner starts in
    await this.app.create(FileInput).byCss("input[type='file']").upload(filePath);

    const src = await this.app.create(Image).byCss("[data-testid='avatar-preview']").getSrc();
    Assert.isTrue(src.length > 0, 'The avatar preview should have a src.');
}
```

---

## Data-Driven Test Recipe (non-SPECTRA tests only)

`@TestCase(...args)` (from `@bellatrix/core/decorators`) registers one run per decorator; the arguments must match the method's parameters (type-checked). It replaces `@Test` on that method.

```ts
@TestCase('admin@example.com', 'admin123', 'Welcome, Admin')
@TestCase('user@example.com', 'user123', 'Welcome, User')
async login_WithDifferentRoles_ShowsCorrectWelcome(email: string, password: string, expected: string) {
    const loginPage = await this.app.goTo(LoginPage);
    await loginPage.loginWith(email, password);

    const dashboard = this.app.createPage(DashboardPage);
    await dashboard.asserts.welcomeMessageIs(expected);
}
```

**SPECTRA-traced tests never use `@TestCase`** — each SPECTRA TC becomes its own `@Test` method with `@ManualTestCase('TC-XXX')`.

---

## Page Objects End-to-End Recipe

```ts
// tests/purchase/PurchaseTests.test.ts
import { Test, TestClass } from '@bellatrix/web/test';
import { Category } from '@bellatrix/core/decorators';
import { WebTest } from '@bellatrix/web/infrastructure';

import { Categories } from '../../src/Categories';
import { ProductSort } from '../../src/models/ProductSort';
import { HomePage } from '../../src/pages';

@TestClass
export class PurchaseTests extends WebTest {
    override async afterEach() {
        await this.app.cookies.clearCookies();
    }

    @Test
    @Category(Categories.Smoke)
    async purchaseRocket_WithCouponAndBillingInfo_PlacesOrder() {
        const homePage = await this.app.goTo(HomePage);
        await homePage.sortProducts(ProductSort.Popularity);
        const cartPage = await homePage.addProductToCart(28);

        await cartPage.applyCoupon('happybirthday');
        await cartPage.updateProductQuantity(1, 2);
        await cartPage.asserts.totalPrice('114.00');

        const checkoutPage = await cartPage.proceedToCheckout();
        await checkoutPage.fillBillingInfo({
            firstName: 'In',
            lastName: 'Deepthought',
            email: 'info@bellatrix.solutions',
            country: 'Bulgaria',
            shouldCreateAccount: true,
        });
        const orderReceivedPage = await checkoutPage.placeOrder();
        await orderReceivedPage.asserts.orderReceived();
    }
}
```

---

## Common Web Locator Patterns

```ts
// data-testid (PREFERRED)
this.create(Button).byCss("[data-testid='submit']")

// Static id / name
this.create(TextField).byId('email')
this.create(TextField).byName('username')

// ARIA
this.create(Button).byCss("[aria-label='Close']")

// Label → input
this.create(TextField).byXpath("//label[normalize-space()='Email']/following-sibling::input")

// Button / link by visible text
this.create(Button).byXpath("//button[normalize-space()='Log In']")
this.create(Anchor).byLinkText('Sign in')

// Element in a row identified by text
this.create(Anchor).byXpath("//tr[td[normalize-space()='Product A']]//a[normalize-space()='Edit']")

// Dynamic (inside a map method)
this.create(Anchor).byAttributeContaining('data-product_id', productId.toString())
```

---

## Required Imports

```ts
import { Test, TestClass } from '@bellatrix/web/test';                 // @TestClass, @Test (web-typed)
import { Category, TestCase, Ignore } from '@bellatrix/core/decorators'; // @Category, data-driven @TestCase, @Ignore
import { WebTest } from '@bellatrix/web/infrastructure';               // test base class (this.app)
import { Button, TextField } from '@bellatrix/web/components';         // components (recipes / maps only)
import { Assert } from '@bellatrix/core/assertions';                   // Assert.areEqual / stringContains / isTrue
import { ManualTestCase } from '../../src/attributes/ManualTestCase';  // SPECTRA traceability
import { CartPage } from '../../src/pages';                            // page objects (barrel)
```
