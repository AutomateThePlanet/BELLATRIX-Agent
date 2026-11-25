# BELLATRIX Web System Tests Recipes (Java)

> **Note:** These recipes show direct component usage so each pattern is visible in one place. For production code, always put locators and workflows in **Page Objects** (see `bellatrix-web-page-objects-and-patterns`); tests then call page-object methods only.
>
> **Page Object Structure:**
> ```
> pages/{page}/{PageName}.java  - extends WebPage<Map, Asserts>: getUrl(), waitForPageLoad(), workflows
> pages/{page}/Map.java         - extends PageMap: component methods
> pages/{page}/Asserts.java     - extends PageAsserts<Map>: assertion methods using validate*
> ```
>
> All snippets assume `import solutions.bellatrix.web.components.*;` and `import org.junit.jupiter.api.Assertions;` (full import list at the end).

---

## Login Flow Recipe

```java
package {project.rootNamespace}.tests.login;

import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.web.components.*;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.junit.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginTests extends WebTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensDashboard() {
        // Arrange
        app().navigate().to("https://example.com/login");
        var emailField = app().create().byCss(TextInput.class, "[data-testid='email']");
        var passwordField = app().create().byId(PasswordInput.class, "password");
        var loginButton = app().create().byXPath(Button.class, "//button[normalize-space()='Log In']");

        // Act
        emailField.setText("user@example.com");
        passwordField.setPassword("Password123");
        loginButton.click();

        // Assert
        app().navigate().waitForPartialUrl("/dashboard");
        var welcome = app().create().byCss(Heading.class, "[data-testid='welcome']");
        welcome.validateTextContains("Welcome");
    }

    @Test
    @Tag("Regression")
    public void login_WithInvalidPassword_ShowsErrorMessage() {
        app().navigate().to("https://example.com/login");
        app().create().byCss(TextInput.class, "[data-testid='email']").setText("user@example.com");
        app().create().byId(PasswordInput.class, "password").setPassword("wrong");
        app().create().byXPath(Button.class, "//button[normalize-space()='Log In']").click();

        var error = app().create().byCss(Div.class, "[data-testid='login-error']");
        error.validateIsVisible();
        error.validateTextIs("Invalid email or password.");
    }
}
```

---

## Form Submission Recipe

```java
@Test
public void register_WithValidData_ShowsSuccessMessage() {
    app().navigate().to("https://example.com/register");

    app().create().byId(TextInput.class, "name").setText("John Doe");
    app().create().byId(EmailInput.class, "email").setEmail("john@example.com");
    app().create().byId(PhoneInput.class, "phone").setPhone("+359888123456");
    app().create().byId(DateInput.class, "birthdate").setDate(1990, 5, 17);
    app().create().byId(Select.class, "country").selectByText("United States");
    app().create().byCss(RadioButton.class, "input[name='plan'][value='pro']").click();
    app().create().byCss(CheckBox.class, "[data-testid='terms']").check();
    app().create().byId(TextArea.class, "notes").setText("Please call after 5 PM.");
    app().create().byXPath(Button.class, "//button[@type='submit']").click();

    var success = app().create().byCss(Div.class, "[data-testid='success']");
    success.validateTextIs("Registration successful");
}
```

`setText(…)` clears the field first — there is no `clear()` on any Java web component.

---

## Field Validation Recipe

```java
@Test
public void register_WithEmptyRequiredFields_ShowsValidationErrors() {
    app().navigate().to("https://example.com/register");

    var email = app().create().byId(EmailInput.class, "email");
    email.validateIsRequired();
    email.validatePlaceholderIs("you@example.com");

    app().create().byXPath(Button.class, "//button[@type='submit']").click();

    app().create().byXPath(Span.class, "//input[@id='email']/following-sibling::span[contains(@class,'error')]")
            .validateTextIs("Email is required.");
    app().create().byXPath(Button.class, "//button[@type='submit']").validateIsVisible();
}
```

`EmailInput` has no `validateText*` — validate its value with `validateEmailIs(…)` / `validateValueIs(…)`.

---

## Dropdown (Select) Recipe

```java
@Test
public void sortProducts_ByPriceLowToHigh_SelectsOption() {
    app().navigate().to("https://demos.bellatrix.solutions/");

    var sortDropDown = app().create().byNameEnding(Select.class, "orderby");
    sortDropDown.selectByText("Sort by price: low to high");

    // Select has no validateText*; getSelected().validate… re-locates the whole <select> — read once and assert
    Assertions.assertEquals("Sort by price: low to high", sortDropDown.getSelected().getText());
}
```

### Custom (non-`<select>`) dropdown

```java
app().create().byCss(Button.class, "[data-testid='currency-trigger']").click();
app().create().byXPath(Button.class, "//*[@role='option' and normalize-space()='EUR']").click();
app().create().byCss(Span.class, "[data-testid='currency-value']").validateTextIs("EUR");
```

---

## Table / Row Selection Recipe

```java
@Test
public void viewOrder_FromOrdersTable_OpensOrderDetails() {
    app().navigate().to("https://example.com/account/orders");

    // Locate the action inside the row identified by its visible text
    var viewLink = app().create().byXPath(Anchor.class,
            "//tr[td[normalize-space()='#4021']]//a[normalize-space()='View']");
    viewLink.click();

    app().create().byTag(Heading.class, "h1").validateTextContains("Order #4021");
}

@Test
public void cartRows_AfterAddingTwoProducts_ShowsTwoRows() {
    app().navigate().to("https://example.com/cart");

    // allBy* does not wait — wait for the first row before collecting
    app().create().byCss(Div.class, "tr.cart_item").toBeVisible().waitToBe();
    var rows = app().create().allByCss(Div.class, "tr.cart_item");
    Assertions.assertEquals(2, rows.size());
}
```

---

## Dialog Confirmation Recipe (native alert / confirm)

```java
@Test
public void deleteAddress_WhenConfirmed_RemovesAddress() {
    app().navigate().to("https://example.com/account/addresses");

    app().create().byXPath(Button.class, "//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").click();

    Assertions.assertEquals("Delete this address?", app().dialogs().getText());
    app().dialogs().handle();                                   // OK

    var homeAddress = app().create().byXPath(Span.class, "//span[normalize-space()='Home']");
    app().waitFor().wait(homeAddress, Wait.to().notExist());    // NOT toNotExist().waitToBe()
}

@Test
public void deleteAddress_WhenCancelled_KeepsAddress() {
    app().navigate().to("https://example.com/account/addresses");

    app().create().byXPath(Button.class, "//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").click();
    app().dialogs().handle(DialogButton.CANCEL);

    app().create().byXPath(Span.class, "//span[normalize-space()='Home']").validateIsVisible();
}
```

`DialogButton` is `solutions.bellatrix.web.services.DialogButton`; `Wait` is `solutions.bellatrix.web.waitstrategies.Wait`.

### HTML modal (not a native dialog)

```java
app().create().byCss(Button.class, "[data-testid='open-delete-modal']").click();
Div modal = app().create().byCss(Div.class, "[role='dialog']").toBeVisible();   // assignment drives the generic type
modal.createByXPath(Button.class, ".//button[normalize-space()='Confirm']").click();
app().waitFor().wait(app().create().byCss(Div.class, "[role='dialog']"), Wait.to().notBeVisible());
```

---

## Wait / AJAX Recipe

```java
@Test
public void applyCoupon_WithValidCode_ShowsSuccessAndNewTotal() {
    app().navigate().to("https://demos.bellatrix.solutions/cart/");

    app().create().byId(TextInput.class, "coupon_code").setText("happybirthday");
    app().create().byValueContaining(Button.class, "Apply coupon").click();

    var message = app().create().byClassContaining(Div.class, "woocommerce-message");
    message.toHaveContent().toBeVisible().waitToBe();
    message.validateTextIs("Coupon code applied successfully.");

    app().browser().waitForAjax();
    app().create().byXPath(Span.class, "//*[@class='order-total']//span").validateTextIs("54.00€");
}
```

---

## Hover Menu Recipe

```java
@Test
public void openSubmenu_ByHover_NavigatesToCategory() {
    app().navigate().to("https://example.com/");

    var productsMenu = app().create().byLinkText(Anchor.class, "Products");
    productsMenu.hover();

    Anchor rocketsLink = app().create().byLinkText(Anchor.class, "Rockets").toBeVisible();   // assignment drives the generic return type
    rocketsLink.click();

    app().navigate().waitForPartialUrl("/category/rockets");
}
```

`app().create().byLinkText(…).toBeVisible().click()` does NOT compile — see the generic return-type trap in `bellatrix-web-locators-and-waits`.

---

## iframe Recipe

```java
@Test
public void payByCard_InsidePaymentFrame_ShowsConfirmation() {
    app().navigate().to("https://example.com/checkout/payment");

    var paymentFrame = app().create().byCss(Frame.class, "iframe[title='Secure payment input frame']");
    app().browser().switchToFrame(paymentFrame);
    app().create().byName(TextInput.class, "cardnumber").setText("4242424242424242");
    app().browser().switchToDefault();

    app().create().byCss(Button.class, "[data-testid='pay']").click();
    app().create().byCss(Heading.class, "[data-testid='payment-status']").validateTextIs("Payment received");
}
```

---

## New Tab Recipe

```java
@Test
public void openTerms_InNewTab_ShowsTermsPage() {
    app().navigate().to("https://example.com/register");

    var termsLink = app().create().byLinkText(Anchor.class, "Terms and Conditions");
    termsLink.validateTargetIs("_blank");
    termsLink.click();

    app().browser().waitNumberOfWindowsToBe(2);
    app().browser().switchToLastTab();
    app().navigate().waitForPartialUrl("/terms");
    Assertions.assertTrue(app().browser().getUrl().contains("/terms"));   // getUrl() is a String

    app().browser().switchToFirstBrowserTab();
}
```

---

## File Upload Recipe

```java
@Test
public void uploadAvatar_WithPng_ShowsPreview() {
    app().navigate().to("https://example.com/account/profile");

    var filePath = Paths.get("src", "test", "resources", "testdata", "avatar.png").toAbsolutePath().toString();   // java.nio.file.Paths
    app().create().byCss(FileInput.class, "input[type='file']").upload(filePath);

    app().create().byCss(Image.class, "[data-testid='avatar-preview']").validateSrcIsSet();
}
```

---

## Keyboard Recipe (Enter key — no Interactions service)

BELLATRIX Java has no `App.Interactions`. Put the Selenium `Actions` call in a custom component (`SearchBox extends SearchField` with `submitWithEnter()` — full code in `bellatrix-web-app-services`) and use it like any component:

```java
@Test
public void search_WithEnterKey_ShowsResults() {
    app().navigate().to("https://example.com/");

    var searchBox = app().create().byCss(SearchBox.class, "[data-testid='search']");   // {project.rootNamespace}.components.SearchBox
    searchBox.setSearch("falcon");
    searchBox.submitWithEnter();

    app().navigate().waitForPartialUrl("?s=falcon");
}
```

---

## Data-Driven Test Recipe (non-SPECTRA tests only)

Needs the `junit-jupiter-params` dependency (not transitive from BELLATRIX):

```java
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

@ParameterizedTest
@CsvSource({
        "admin@example.com, admin123, 'Welcome, Admin'",
        "user@example.com, user123, 'Welcome, User'"
})
public void login_WithDifferentRoles_ShowsCorrectWelcome(String email, String password, String expected) {
    var loginPage = app().goTo(LoginPage.class);
    loginPage.loginWith(email, password);

    var dashboard = app().createPage(DashboardPage.class);
    dashboard.asserts().welcomeMessageIs(expected);
}
```

**SPECTRA-traced tests never use `@ParameterizedTest`** — each SPECTRA TC becomes its own `@Test` method with `@ManualTestCase("TC-XXX")`. (TestNG projects use `@Test(dataProvider = "…")` + `@DataProvider` for the same non-SPECTRA case.)

---

## Page Objects End-to-End Recipe

```java
package {project.rootNamespace}.tests.checkout;

import {project.rootNamespace}.models.BillingInfo;
import {project.rootNamespace}.models.ProductSort;
import {project.rootNamespace}.pages.home.HomePage;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.web.infrastructure.Browser;
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.infrastructure.junit.WebTest;

@ExecutionBrowser(browser = Browser.CHROME, lifecycle = Lifecycle.REUSE_IF_STARTED)
public class PurchaseTests extends WebTest {
    @Test
    @Tag("Smoke")
    public void purchaseRocket_WithCouponAndBillingInfo_PlacesOrder() {
        var homePage = app().goTo(HomePage.class);
        homePage.sortBy(ProductSort.POPULARITY);
        homePage.addProductById(28);
        var cartPage = homePage.viewCart();

        cartPage.applyCoupon("happybirthday");
        cartPage.updateProductQuantity(1, 2);
        cartPage.asserts().totalPrice("114.00");

        var checkoutPage = cartPage.proceedToCheckout();
        var billingInfo = new BillingInfo();
        billingInfo.setFirstName("In");
        billingInfo.setLastName("Deepthought");
        billingInfo.setCountry("Bulgaria");
        billingInfo.setPhone("+00359894646464");
        billingInfo.setEmail("info@bellatrix.solutions");
        billingInfo.setShouldCreateAccount(true);
        checkoutPage.fillBillingInfo(billingInfo);

        var orderReceivedPage = checkoutPage.placeOrder();
        orderReceivedPage.asserts().orderReceived();
    }
}
```

---

## Common Web Locator Patterns

```java
// data-testid (PREFERRED) — there is no byDataTestId
app().create().byCss(Button.class, "[data-testid='submit']");

// Static id / name
app().create().byId(TextInput.class, "email");
app().create().byName(TextInput.class, "username");

// ARIA
app().create().byCss(Button.class, "[aria-label='Close']");

// Label → input
app().create().byXPath(TextInput.class, "//label[normalize-space()='Email']/following-sibling::input");

// Button / link by visible text
app().create().byXPath(Button.class, "//button[normalize-space()='Log In']");
app().create().byLinkText(Anchor.class, "Sign in");

// Element in a row identified by text
app().create().byXPath(Anchor.class, "//tr[td[normalize-space()='Product A']]//a[normalize-space()='Edit']");

// Dynamic (inside a Map method)
app().create().byAttributeContaining(Anchor.class, "data-product_id", String.valueOf(productId));
```

---

## Required Imports

```java
import solutions.bellatrix.web.components.*;                     // components (validate* come with them)
import solutions.bellatrix.web.infrastructure.junit.WebTest;     // WebTest base (TestNG: …infrastructure.testng.WebTest)
import solutions.bellatrix.web.infrastructure.Browser;           // @ExecutionBrowser + enums
import solutions.bellatrix.web.infrastructure.ExecutionBrowser;
import solutions.bellatrix.web.infrastructure.Lifecycle;
import solutions.bellatrix.web.services.DialogButton;            // native dialogs
import solutions.bellatrix.web.waitstrategies.Wait;              // app().waitFor().wait(c, Wait.to()…)
import org.junit.jupiter.api.Test;                               // @Test, @Tag, Assertions
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Assertions;
import {project.rootNamespace}.common.ManualTestCase;
import {project.rootNamespace}.pages.login.LoginPage;
import {project.rootNamespace}.models.BillingInfo;
```
