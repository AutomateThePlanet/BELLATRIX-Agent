# BELLATRIX Mobile System Tests Recipes

> **Note:** these recipes use components directly to show the API. Production code always goes through
> **Page Objects** (see `bellatrix-mobile-page-objects-and-patterns`):
>
> ```
> Pages/{ScreenName}/{ScreenName}.Map.cs        - base class + component properties
> Pages/{ScreenName}/{ScreenName}.Actions.cs    - workflow methods
> Pages/{ScreenName}/{ScreenName}.Assertions.cs - Assert* methods using Validate*
> ```
>
> Android recipes use `using Bellatrix.Mobile.Android;`; iOS recipes use `using Bellatrix.Mobile.IOS;`.
> Every value below is a placeholder — use only locators validated live with the `bellatrix mobile` CLI.

---

## Login Flow Recipe (Android)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.NUnit;
using NUnit.Framework;

namespace MyProject.MobileTests.Tests;

[TestFixture]
[Android(Constants.AppPath,
    Constants.AppId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Constants.StartActivity,
    Lifecycle.RestartEveryTime)]
public class LoginTests : AndroidTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        // Arrange
        var userName = App.Components.CreateById<TextField>("com.example.shop:id/username");
        var password = App.Components.CreateById<Password>("com.example.shop:id/password");
        var loginButton = App.Components.CreateByDescription<Button>("Log in");

        // Act
        userName.SetText("demo.user");
        password.SetPassword("Secret123!");
        loginButton.Click();

        // Assert
        var welcome = App.Components.CreateById<Label>("com.example.shop:id/welcome").ToBeVisible();
        welcome.ValidateTextIs("Welcome, demo.user");
    }
}
```

---

## Login Flow Recipe (iOS)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.IOS;
using Bellatrix.Mobile.NUnit;
using NUnit.Framework;

namespace MyProject.MobileTests.Tests;

[TestFixture]
[IOS(Constants.AppPath,
    Constants.BundleId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Lifecycle.RestartEveryTime)]
public class LoginTests : IOSTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        App.Components.CreateById<TextField>("usernameField").SetText("demo.user");
        App.Components.CreateById<Password>("passwordField").SetPassword("Secret123!");
        App.Components.CreateByName<Button>("Log in").Click();

        var welcome = App.Components.CreateByIOSNsPredicate<Label>("label BEGINSWITH \"Welcome\"");
        welcome.ValidateTextIs("Welcome, demo.user");
    }
}
```

---

## Form Submission Recipe (Android)

```csharp
[Test]
public void Register_WithValidData_ShowsSuccessMessage()
{
    // Arrange
    var nameField = App.Components.CreateById<TextField>("com.example.shop:id/name");
    var emailField = App.Components.CreateById<TextField>("com.example.shop:id/email");
    var countrySpinner = App.Components.CreateById<ComboBox>("com.example.shop:id/country");
    var termsCheckBox = App.Components.CreateById<CheckBox>("com.example.shop:id/terms");
    var submitButton = App.Components.CreateByDescription<Button>("Create account");

    // Act
    nameField.SetText("John Doe");
    emailField.SetText("john@example.com");
    countrySpinner.SelectByText("Bulgaria");
    termsCheckBox.Check();
    submitButton.Click();

    // Assert
    App.Components.CreateById<Label>("com.example.shop:id/status")
        .ToHasContent()
        .ValidateTextIs("Registration successful");
}
```

---

## Scroll a List and Open an Item Recipe

```csharp
// Android — CreateByText scrolls a scrollable container until the row is in view
[Test]
public void OpenProduct_FromCatalog_ShowsDetails()
{
    var row = App.Components.CreateByText<Button>("Pixel 9");
    row.Click();

    App.Components.CreateById<Label>("com.example.shop:id/product_title").ValidateTextIs("Pixel 9");
}

// iOS — scroll explicitly, then tap the cell
[Test]
public void OpenProduct_FromCatalog_ShowsDetails()
{
    var cell = App.Components.CreateByIOSNsPredicate<Button>("type == \"XCUIElementTypeCell\" AND label CONTAINS \"Pixel 9\"");
    cell.ScrollToVisible(ScrollDirection.Down);
    cell.Click();

    App.Components.CreateById<Label>("productTitle").ValidateTextIs("Pixel 9");
}
```

---

## Collection Recipe (count and iterate)

```csharp
[Test]
public void Cart_WithThreeItems_ListsThreeRows()
{
    var rows = App.Components.CreateAllById<Label>("com.example.shop:id/cart_item_title");

    Assert.That(rows.Count(), Is.EqualTo(3));
    foreach (var row in rows)
    {
        row.ValidateIsVisible();
    }
}
```

---

## Switch / Settings Recipe (Android)

```csharp
[Test]
public void EnableNotifications_InSettings_PersistsAfterRestart()
{
    App.AppService.StartActivity(Constants.AppId, ".SettingsActivity");

    var notifications = App.Components.CreateById<Switch>("com.example.shop:id/notifications");
    notifications.TurnOn();
    notifications.ValidateIsOn();

    App.AppService.CloseApp();
    App.AppService.LaunchApp();
    App.AppService.StartActivity(Constants.AppId, ".SettingsActivity");

    App.Components.CreateById<Switch>("com.example.shop:id/notifications").ValidateIsOn();
}
```

On iOS model `XCUIElementTypeSwitch` as `ToggleButton` (`TurnOn()`, `ValidateIsOn()`).

---

## Radio Group Recipe (Android)

```csharp
[Test]
public void ChooseShipping_Express_UpdatesTotal()
{
    var shipping = App.Components.CreateById<RadioGroup>("com.example.shop:id/shipping");
    shipping.ClickByText("Express");

    shipping.GetChecked().ValidateTextIs("Express");
    App.Components.CreateById<Label>("com.example.shop:id/total").ValidateTextIs("$24.99");
}
```

---

## Dialog Confirmation Recipe

```csharp
[Test]
public void DeleteItem_WithConfirmation_RemovesRow()
{
    App.Components.CreateByDescription<Button>("Delete Pixel 9").Click();

    // Android system dialog positive button
    var confirm = App.Components.CreateById<Button>("android:id/button1").ToBeClickable();
    confirm.ValidateTextIs("DELETE");
    confirm.Click();

    App.Components.CreateByText<Button>("Pixel 9").ToNotExists().WaitToBe();
}
```

iOS alert buttons: `App.Components.CreateByIOSNsPredicate<Button>("type == \"XCUIElementTypeButton\" AND label == \"Delete\"")`.

---

## Swipe Gesture Recipe

```csharp
[Test]
public void SwipeCarousel_Left_ShowsNextSlide()
{
    var carousel = App.Components.CreateById<AndroidComponent>("com.example.shop:id/carousel"); // using Bellatrix.Mobile.Controls.Android;
    var location = carousel.Location;
    var size = carousel.Size;

    App.TouchActions.Swipe(
        location.X + size.Width - 10,
        location.Y + size.Height / 2,
        location.X + 10,
        location.Y + size.Height / 2,
        300).Perform();

    App.Components.CreateById<Label>("com.example.shop:id/slide_title").ValidateTextIs("Slide 2");
}
```

---

## Background and Restore Recipe

```csharp
[Test]
public void Cart_AfterBackgrounding_KeepsItems()
{
    App.Components.CreateByDescription<Button>("Add Pixel 9 to cart").Click();

    App.AppService.BackgroundApp(5);

    App.Components.CreateById<Label>("com.example.shop:id/cart_badge").ValidateTextIs("1");
}
```

---

## Orientation Recipe

```csharp
using OpenQA.Selenium;   // ScreenOrientation

[Test]
public void Rotate_ToLandscape_KeepsFormValues()
{
    var name = App.Components.CreateById<TextField>("com.example.shop:id/name");
    name.SetText("John Doe");

    App.Device.Rotate(ScreenOrientation.Landscape);

    name.ValidateTextIs("John Doe");
    App.Device.Rotate(ScreenOrientation.Portrait);
}
```

---

## Wait for Async Content Recipe

```csharp
[Test]
public void Sync_WhenTriggered_ShowsLastSyncedStatus()
{
    App.Components.CreateByDescription<Button>("Sync now").Click();

    App.Components.CreateById<Progress>("com.example.shop:id/sync_progress").ToNotExists(60, 1).WaitToBe();
    App.Components.CreateById<Label>("com.example.shop:id/sync_status").ToHasContent().ValidateTextIs("Up to date");
}
```

---

## Data-Driven Test Recipe

```csharp
[Test]
[TestCase("admin", "admin123", "Welcome, admin")]
[TestCase("guest", "guest123", "Welcome, guest")]
public void Login_WithDifferentRoles_ShowsCorrectWelcome(string userName, string password, string expectedWelcome)
{
    var homePage = App.Create<LoginPage>().LoginAndGoHome(userName, password);

    homePage.AssertWelcomeShown(expectedWelcome);
}
```

Data-driven `[TestCase]` is for ONE test case with several data rows. Never use it to merge several
SPECTRA test cases into one method.

---

## Page Objects End-to-End Recipe

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.NUnit;
using MyProject.MobileTests.Pages;
using NUnit.Framework;

namespace MyProject.MobileTests.Tests;

[TestFixture]
[Android(Constants.AppPath,
    Constants.AppId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Constants.StartActivity,
    Lifecycle.RestartEveryTime)]
public class CheckoutFlowTests : AndroidTest
{
    [Test]
    [Category("Smoke")]
    public void Checkout_WithSavedCard_ShowsConfirmation()
    {
        // Arrange
        var loginPage = App.Create<LoginPage>();
        loginPage.AssertPageLoaded();

        // Act
        var homePage = loginPage.LoginAndGoHome("demo.user", "Secret123!");
        homePage.OpenProduct("Pixel 9");
        var productPage = App.Create<ProductPage>();
        productPage.AddToCart();
        var checkoutPage = productPage.GoToCheckout();
        checkoutPage.PayWithSavedCard();

        // Assert
        checkoutPage.AssertOrderConfirmed();
    }
}
```

---

## Common Locator Patterns

```csharp
// ---- Android ----
App.Components.CreateByDescription<Button>("Log in")                                   // content-desc (PREFERRED)
App.Components.CreateById<TextField>("com.example.shop:id/username")                   // full resource-id
App.Components.CreateByText<Button>(productName)                                       // dynamic text
App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log in\").clickable(true)")
App.Components.CreateByXPath<Label>("//androidx.recyclerview.widget.RecyclerView//android.widget.TextView[@text='Pixel 9']")

// ---- iOS ----
App.Components.CreateById<Button>("loginButton")                                       // accessibility id (PREFERRED)
App.Components.CreateByName<Button>("Log in")
App.Components.CreateByIOSNsPredicate<Button>($"type == \"XCUIElementTypeCell\" AND label CONTAINS \"{productName}\"")
App.Components.CreateByXPath<Label>("//XCUIElementTypeTable//XCUIElementTypeStaticText[@label='Pixel 9']")
```

---

## Required Using Directives

```csharp
using Bellatrix.Mobile;                 // [Android]/[IOS], Lifecycle, ScrollDirection, AndroidApp/IOSApp
using Bellatrix.Mobile.Android;         // components, factories, waits, Validate* (or Bellatrix.Mobile.IOS)
using Bellatrix.Mobile.NUnit;           // AndroidTest / IOSTest (or Bellatrix.Mobile.MSTest)
using NUnit.Framework;                  // [TestFixture], [Test], [Category], [TestCase]
using OpenQA.Selenium;                  // ScreenOrientation (orientation tests only)
using MyProject.MobileTests.Pages;      // your page objects
using MyProject.MobileTests.Models;     // your data models (if applicable)
```
