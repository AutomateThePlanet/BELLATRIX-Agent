# BELLATRIX Mobile System Tests Recipes (Java)

> **Note:** these recipes use components directly to show the API. Production code always goes through
> **Page Objects** (see `bellatrix-mobile-page-objects-and-patterns`):
>
> ```
> android/pages/{screen}/{Screen}.java  - Action methods (extends AndroidPage<Map, Asserts>)
> android/pages/{screen}/Map.java       - Component methods (extends PageMap)
> android/pages/{screen}/Asserts.java   - Assertion methods (extends PageAsserts<Map>)
> ios/pages/{screen}/…                  - the same with IOSPage and solutions.bellatrix.ios.*
> ```
>
> Android recipes import `solutions.bellatrix.android.*`; iOS recipes import `solutions.bellatrix.ios.*`. Every value
> below is a placeholder — use only locators validated live with the `bellatrix mobile` CLI. Snippets without a
> class are methods of a test class like the first recipe of their platform.

---

## Login Flow Recipe (Android)

```java
package {project.rootNamespace}.android.tests.login;

import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.android.components.Button;
import solutions.bellatrix.android.components.Label;
import solutions.bellatrix.android.components.PasswordField;
import solutions.bellatrix.android.components.TextField;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.junit.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH,
        appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY,
        lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginRecipeTests extends AndroidTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensHome() {
        // Arrange
        TextField userName = app().create().byId(TextField.class, "com.example.shop:id/username");
        PasswordField password = app().create().byId(PasswordField.class, "com.example.shop:id/password");
        Button loginButton = app().create().byDescription(Button.class, "Log in");

        // Act
        userName.setText("demo.user");
        password.setPassword("Secret123!");
        loginButton.click();

        // Assert
        Label welcome = app().create().byId(Label.class, "com.example.shop:id/welcome");
        welcome.toHaveContent().waitToBe();
        welcome.validateTextIs("Welcome, demo.user");
    }
}
```

---

## Login Flow Recipe (iOS)

```java
package {project.rootNamespace}.ios.tests.login;

import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.ios.components.Button;
import solutions.bellatrix.ios.components.Label;
import solutions.bellatrix.ios.components.PasswordField;
import solutions.bellatrix.ios.components.TextField;
import solutions.bellatrix.ios.infrastructure.ExecutionApp;
import solutions.bellatrix.ios.infrastructure.Lifecycle;
import solutions.bellatrix.ios.infrastructure.junit.IOSTest;

@ExecutionApp(appPath = Constants.IOS_APP_PATH,
        iOSVersion = Constants.IOS_VERSION,
        deviceName = Constants.IOS_DEVICE,
        lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginRecipeTests extends IOSTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensHome() {
        app().create().byAccessibilityIdContaining(TextField.class, "usernameField").setText("demo.user");
        app().create().byAccessibilityIdContaining(PasswordField.class, "passwordField").setPassword("Secret123!");
        app().create().byName(Button.class, "Log in").click();

        Label welcome = app().create().byIOSNsPredicate(Label.class, "label BEGINSWITH \"Welcome\"");
        welcome.toHaveContent().waitToBe();
        welcome.validateTextIs("Welcome, demo.user");
    }
}
```

---

## Form Submission Recipe (Android)

```java
@Test
public void register_WithValidData_ShowsSuccessMessage() {
    // Arrange
    TextField nameField = app().create().byId(TextField.class, "com.example.shop:id/name");
    TextField emailField = app().create().byId(TextField.class, "com.example.shop:id/email");
    SpinnerComboBox countrySpinner = app().create().byId(SpinnerComboBox.class, "com.example.shop:id/country");
    CheckBox termsCheckBox = app().create().byId(CheckBox.class, "com.example.shop:id/terms");
    Button submitButton = app().create().byDescription(Button.class, "Create account");

    // Act
    nameField.setText("John Doe");
    emailField.setText("john@example.com");
    app().keyboard().hideKeyboard();
    countrySpinner.selectByText("Bulgaria");   // custom override — the built-in ComboBox.selectByText crashes on Android
    termsCheckBox.check();
    submitButton.click();

    // Assert
    Label status = app().create().byId(Label.class, "com.example.shop:id/status");
    status.toHaveContent().waitToBe();
    status.validateTextIs("Registration successful");
}
```

`SpinnerComboBox` is the project's custom component (`{project.rootNamespace}.android.components`, code in
`bellatrix-mobile-generate-code`).

---

## Scroll a List and Open an Item Recipe

```java
// Android — byText scrolls a scrollable container until the row is in view
@Test
public void openProduct_FromCatalog_ShowsDetails() {
    app().create().byText(Button.class, "Pixel 9").click();

    Label title = app().create().byId(Label.class, "com.example.shop:id/product_title");
    title.toHaveContent().waitToBe();
    title.validateTextIs("Pixel 9");
}

// iOS — no auto-scroll: swipe up until the cell exists, then tap it
@Test
public void openProduct_FromCatalog_ShowsDetails() {
    String cellPredicate = "type == \"XCUIElementTypeCell\" AND label CONTAINS \"Pixel 9\"";
    for (int i = 0; i < 5; i++) {
        if (!app().create().allByIOSNsPredicate(Button.class, cellPredicate).isEmpty()) {
            break;
        }

        app().touch().swipe(200, 700, 200, 250, 300);
    }

    app().create().byIOSNsPredicate(Button.class, cellPredicate).click();
    app().create().byAccessibilityIdContaining(Label.class, "productTitle").validateTextIs("Pixel 9");
}
```

---

## Collection Recipe (count and iterate)

```java
@Test
public void cart_WithThreeItems_ListsThreeRows() {
    List<Label> rows = app().create().allById(Label.class, "com.example.shop:id/cart_item_title");

    Assertions.assertEquals(3, rows.size());
    for (Label row : rows) {
        row.validateTextContains("Pixel");
    }
}
```

---

## Switch / Settings Recipe (Android)

```java
@Test
public void enableNotifications_InSettings_PersistsAfterRestart() {
    app().appService().startActivity(Constants.APP_PACKAGE, ".SettingsActivity");

    Switch notifications = app().create().byId(Switch.class, "com.example.shop:id/notifications");
    notifications.turnOn();
    notifications.validateIsOn();

    app().appService().terminateApp(Constants.APP_PACKAGE);
    app().appService().activateApp(Constants.APP_PACKAGE);
    app().appService().startActivity(Constants.APP_PACKAGE, ".SettingsActivity");

    app().create().byId(Switch.class, "com.example.shop:id/notifications").validateIsOn();
}
```

On iOS create the `XCUIElementTypeSwitch` as `Button` and check `getAttribute("value")` (`"1"` / `"0"`) — iOS
`ToggleButton.turnOn()` / `validateIsOn()` read an attribute XCUITest does not expose.

---

## Radio Choice Recipe (Android)

```java
@Test
public void chooseShipping_Express_UpdatesTotal() {
    // RadioGroup.clickByText / getAll crash on Android — tap the radio button itself
    RadioButton express = app().create().byText(RadioButton.class, "Express");
    express.click();
    express.validateIsChecked();

    Label total = app().create().byId(Label.class, "com.example.shop:id/total");
    total.toHaveContent().waitToBe();
    total.validateTextIs("$24.99");
}
```

---

## Dialog Confirmation Recipe

```java
@Test
public void deleteItem_WithConfirmation_RemovesRow() {
    app().create().byDescription(Button.class, "Delete Pixel 9").click();

    // Android system dialog positive button
    Button confirm = app().create().byId(Button.class, "android:id/button1").toBeClickable();
    confirm.validateTextIs("DELETE");
    confirm.click();

    Button row = app().create().byText(Button.class, "Pixel 9");
    app().waitFor().wait(row, Wait.to().notExist());
}
```

iOS alert buttons: `app().create().byIOSNsPredicate(Button.class, "type == \"XCUIElementTypeButton\" AND label == \"Delete\"")`;
wait for a row to disappear on iOS with the `allBy…`-is-empty poll from `bellatrix-mobile-locators-and-waits`.

---

## Swipe Gesture Recipe

```java
@Test
public void swipeCarousel_Left_ShowsNextSlide() {
    AndroidComponent carousel = app().create().byId(AndroidComponent.class, "com.example.shop:id/carousel");
    Point location = carousel.getLocation();   // java.awt.Point
    Dimension size = carousel.getSize();       // java.awt.Dimension

    app().touch().swipe(
            location.x + size.width - 10,
            location.y + size.height / 2,
            location.x + 10,
            location.y + size.height / 2,
            300);   // executes immediately — no perform()

    Label slideTitle = app().create().byId(Label.class, "com.example.shop:id/slide_title");
    slideTitle.toHaveContent().waitToBe();
    slideTitle.validateTextIs("Slide 2");
}
```

If the driver rejects the legacy touch API, use the W3C `PointerInput` fallback in `bellatrix-mobile-app-services`.

---

## Background and Restore Recipe

```java
@Test
public void cart_AfterBackgrounding_KeepsItems() {
    app().create().byDescription(Button.class, "Add Pixel 9 to cart").click();

    app().appService().backgroundApp(5);

    Label badge = app().create().byId(Label.class, "com.example.shop:id/cart_badge");
    badge.toHaveContent().waitToBe();
    badge.validateTextIs("1");
}
```

---

## Orientation Recipe

```java
@Test
public void rotate_ToLandscape_KeepsFormValues() {
    TextField name = app().create().byId(TextField.class, "com.example.shop:id/name");
    name.setText("John Doe");

    app().device().rotate(ScreenOrientation.LANDSCAPE);   // org.openqa.selenium.ScreenOrientation

    name.validateTextIs("John Doe");
    app().device().rotate(ScreenOrientation.PORTRAIT);
}
```

---

## Wait for Async Content Recipe

```java
@Test
public void sync_WhenTriggered_ShowsLastSyncedStatus() {
    app().create().byDescription(Button.class, "Sync now").click();

    Progress progress = app().create().byId(Progress.class, "com.example.shop:id/sync_progress");
    app().waitFor().wait(progress, Wait.to().notExist(60, 1));

    Label status = app().create().byId(Label.class, "com.example.shop:id/sync_status");
    new WebDriverWait(status.getWrappedDriver(), Duration.ofSeconds(15))
            .until(d -> status.getText().equals("Up to date"));
    status.validateTextIs("Up to date");
}
```

---

## Data-Driven Test Recipe

```java
@ParameterizedTest
@CsvSource({
        "admin, admin123, 'Welcome, admin'",
        "guest, guest123, 'Welcome, guest'"
})
public void login_WithDifferentRoles_ShowsCorrectWelcome(String userName, String password, String expectedWelcome) {
    HomePage homePage = app().createPage(LoginPage.class).loginAndGoHome(userName, password);

    homePage.asserts().welcomeShown(expectedWelcome);
}
```

(`org.junit.jupiter.params.ParameterizedTest`, `org.junit.jupiter.params.provider.CsvSource` — needs the
`junit-jupiter-params` dependency.) Data-driven parameters are for ONE test case with several data rows. Never use
them to merge several SPECTRA test cases into one method.

---

## Page Objects End-to-End Recipe

```java
package {project.rootNamespace}.android.tests.checkout;

import {project.rootNamespace}.android.pages.checkout.CheckoutPage;
import {project.rootNamespace}.android.pages.home.HomePage;
import {project.rootNamespace}.android.pages.login.LoginPage;
import {project.rootNamespace}.android.pages.product.ProductPage;
import {project.rootNamespace}.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.android.infrastructure.ExecutionApp;
import solutions.bellatrix.android.infrastructure.Lifecycle;
import solutions.bellatrix.android.infrastructure.junit.AndroidTest;

@ExecutionApp(appPath = Constants.ANDROID_APP_PATH,
        appPackage = Constants.APP_PACKAGE,
        appActivity = Constants.MAIN_ACTIVITY,
        lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class CheckoutFlowTests extends AndroidTest {
    @Test
    @Tag("Smoke")
    public void checkout_WithSavedCard_ShowsConfirmation() {
        // Arrange
        LoginPage loginPage = app().createPage(LoginPage.class);
        loginPage.asserts().pageLoaded();

        // Act
        HomePage homePage = loginPage.loginAndGoHome("demo.user", "Secret123!");
        homePage.openProduct("Pixel 9");
        ProductPage productPage = app().createPage(ProductPage.class);
        productPage.addToCart();
        CheckoutPage checkoutPage = productPage.goToCheckout();
        checkoutPage.payWithSavedCard();

        // Assert
        checkoutPage.asserts().orderConfirmed();
    }
}
```

---

## Common Locator Patterns

```java
// ---- Android ----
app().create().byDescription(Button.class, "Log in");                                       // content-desc (PREFERRED)
app().create().byId(TextField.class, "com.example.shop:id/username");                       // full resource-id
app().create().byText(Button.class, productName);                                           // dynamic text (scrolls)
app().create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log in\").clickable(true)");
app().create().byXPath(Label.class, "//androidx.recyclerview.widget.RecyclerView//android.widget.TextView[@text='Pixel 9']");

// ---- iOS ----
app().create().byAccessibilityIdContaining(Button.class, "loginButton");                    // accessibility id (PREFERRED, exact)
app().create().byName(Button.class, "Log in");
app().create().byIOSNsPredicate(Button.class, "type == \"XCUIElementTypeCell\" AND label CONTAINS \"" + productName + "\"");
app().create().byIOSClassChain(Button.class, "**/XCUIElementTypeCell[`label CONTAINS \"Pixel\"`]");
app().create().byXPath(Label.class, "//XCUIElementTypeTable//XCUIElementTypeStaticText[@label='Pixel 9']");
```

---

## Required Imports

```text
solutions.bellatrix.android.components.*                 (or solutions.bellatrix.ios.components.* — never both)
solutions.bellatrix.android.infrastructure.ExecutionApp / Lifecycle / junit.AndroidTest
                                                         (iOS: solutions.bellatrix.ios.infrastructure.… / junit.IOSTest)
solutions.bellatrix.android.waitstrategies.Wait          (negative waits)
org.junit.jupiter.api.Test / Tag / Assertions
org.junit.jupiter.params.ParameterizedTest / provider.CsvSource   (data-driven only)
org.openqa.selenium.ScreenOrientation                    (orientation tests only)
org.openqa.selenium.support.ui.WebDriverWait, java.time.Duration  (polling helpers)
java.awt.Point, java.awt.Dimension                       (gesture geometry)
java.util.List                                           (collections)
{project.rootNamespace}.common.Constants / ManualTestCase
{project.rootNamespace}.android.pages.<screen>.<Screen>            (your page objects)
{project.rootNamespace}.android.components.*                       (your custom components)
```
