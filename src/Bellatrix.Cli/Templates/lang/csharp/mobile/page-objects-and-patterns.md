# BELLATRIX Mobile Page Objects and Patterns Reference

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. File-Scoped Namespaces (C# 10+)

```csharp
// CORRECT — file-scoped namespace (no braces)
namespace MyProject.MobileTests.Pages;

public partial class LoginPage : AndroidPage
{
    // ...
}

// WRONG — block-scoped namespace
namespace MyProject.MobileTests.Pages
{
    public partial class LoginPage : AndroidPage { }
}
```

### 2. Use `partial` on Every File

All files of one screen declare `public partial class {ScreenName}`. Declare the base class **once**
(in the Map file).

### 3. Use the Platform Page Base Class (`Bellatrix.Mobile.PageObjects`)

```csharp
// Android — MUST override PackageName and ActivityName (both abstract)
public partial class LoginPage : AndroidPage
{
    protected override string PackageName => Constants.AppId;
    protected override string ActivityName => ".LoginActivity";
}

// iOS — no members to override
public partial class LoginPage : IOSPage
{
}

// WRONG — WebPage / DesktopPage are for other domains
public partial class LoginPage : WebPage { }
```

`AndroidPage` gives you `App` (`AndroidApp`) and `GoTo()`, which starts `PackageName`/`ActivityName`
directly via `App.AppService.StartActivity(...)`. `IOSPage` gives you `App` (`IOSApp`); iOS has no
activity jump — reach the screen through the UI.

### 4. One Platform per File

Import `Bellatrix.Mobile.Android` **or** `Bellatrix.Mobile.IOS`, never both — component names
(`Button`, `TextField`, …) and the `Validate*` extension methods collide.

### 5. Use Validate Methods in Assertions (NOT Assert.That)

See `bellatrix-mobile-validations-and-assertions`.

---

## Project Structure (3-File Pattern)

The BELLATRIX samples use `{Name}.Map.cs`, `{Name}.Actions.cs`, `{Name}.Assertions.cs`:

```
Pages/
  LoginPage/
    LoginPage.Map.cs          # base class, PackageName/ActivityName (Android), component properties
    LoginPage.Actions.cs      # business workflow methods
    LoginPage.Assertions.cs   # Assert* methods using Validate*
  HomePage/
    HomePage.Map.cs
    HomePage.Actions.cs
    HomePage.Assertions.cs
  Shared/
    BottomNavigationSection.cs
Models/
  LoginCredentials.cs
Tests/
  Login/LoginTests.cs
```

If the project targets both platforms, keep the Android and iOS page objects in separate namespaces /
folders (or separate test projects, as the BELLATRIX samples do) — they cannot share one class.

---

## Complete 3-File Example: LoginPage (Android)

### LoginPage.Map.cs

```csharp
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.PageObjects;

namespace MyProject.MobileTests.Pages;

public partial class LoginPage : AndroidPage
{
    // AndroidPage requires both; GoTo() starts this activity directly.
    protected override string PackageName => Constants.AppId;
    protected override string ActivityName => ".LoginActivity";

    // ===== FORM FIELDS =====
    public TextField UserNameField =>
        App.Components.CreateById<TextField>("com.example.myapp:id/username");

    public Password PasswordField =>
        App.Components.CreateById<Password>("com.example.myapp:id/password");

    // ===== BUTTONS =====
    public Button LoginButton =>
        App.Components.CreateByDescription<Button>("Log in");

    // ===== OPTIONS =====
    public CheckBox RememberMeCheckBox =>
        App.Components.CreateByText<CheckBox>("Remember me");

    // ===== MESSAGES =====
    public Label ErrorMessage =>
        App.Components.CreateById<Label>("com.example.myapp:id/error");
}
```

### LoginPage.Actions.cs

```csharp
using Bellatrix.Mobile.Android;
using MyProject.MobileTests.Models;

namespace MyProject.MobileTests.Pages;

public partial class LoginPage
{
    public void LoginWith(string userName, string password)
    {
        UserNameField.SetText(userName);
        PasswordField.SetPassword(password);
        LoginButton.Click();
    }

    public void LoginWith(LoginCredentials credentials)
    {
        LoginWith(credentials.UserName, credentials.Password);
    }

    public void LoginWithRememberMe(string userName, string password)
    {
        UserNameField.SetText(userName);
        PasswordField.SetPassword(password);
        RememberMeCheckBox.Check();
        LoginButton.ToBeClickable().Click();
    }

    public HomePage LoginAndGoHome(string userName, string password)
    {
        LoginWith(userName, password);
        return App.Create<HomePage>();
    }
}
```

### LoginPage.Assertions.cs

```csharp
using Bellatrix.Mobile.Android;

namespace MyProject.MobileTests.Pages;

public partial class LoginPage
{
    public void AssertPageLoaded()
    {
        UserNameField.ValidateIsVisible();
        PasswordField.ValidateIsVisible();
        LoginButton.ValidateIsVisible();
    }

    public void AssertErrorDisplayed(string expectedError)
    {
        ErrorMessage.ValidateIsVisible();
        ErrorMessage.ValidateTextIs(expectedError);
    }

    public void AssertRememberMeChecked()
    {
        RememberMeCheckBox.ValidateIsChecked();
    }
}
```

---

## The Same Screen on iOS

```csharp
// LoginPage.Map.cs (iOS project / namespace)
using Bellatrix.Mobile.IOS;
using Bellatrix.Mobile.PageObjects;

namespace MyProject.MobileTests.Pages;

public partial class LoginPage : IOSPage
{
    public TextField UserNameField =>
        App.Components.CreateById<TextField>("usernameField");

    public Password PasswordField =>
        App.Components.CreateById<Password>("passwordField");

    public Button LoginButton =>
        App.Components.CreateByName<Button>("Log in");

    public CheckBox RememberMeSwitch =>
        App.Components.CreateByIOSNsPredicate<CheckBox>("type == \"XCUIElementTypeSwitch\" AND name == \"Remember me\"");

    public Label ErrorMessage =>
        App.Components.CreateByIOSNsPredicate<Label>("type == \"XCUIElementTypeStaticText\" AND name == \"loginError\"");
}
```

The Actions and Assertions files are identical except for `using Bellatrix.Mobile.IOS;`. Keep public
member names the same on both platforms so test bodies read the same.

---

## Design Patterns

### 1. Property-Based Element Definitions (Recommended)

Expression-bodied properties are re-resolved on every access, so they never hold stale elements:

```csharp
public Button SaveButton =>
    App.Components.CreateByDescription<Button>("Save");
```

### 2. Method-Based for Dynamic Elements

When one attribute varies at runtime, keep the direct factory:

```csharp
public Button ProductRow(string productName) =>
    App.Components.CreateByText<Button>(productName);                         // Android

public Button ProductCell(string productName) =>
    App.Components.CreateByIOSNsPredicate<Button>($"type == \"XCUIElementTypeCell\" AND label CONTAINS \"{productName}\""); // iOS
```

### 3. Navigation Methods Return Page Objects

```csharp
public HomePage LoginAndGoHome(string userName, string password)
{
    LoginWith(userName, password);
    return App.Create<HomePage>();
}
```

### 4. Fluent Interface for Chaining

```csharp
public LoginPage WithUserName(string userName)
{
    UserNameField.SetText(userName);
    return this;
}

public LoginPage WithPassword(string password)
{
    PasswordField.SetPassword(password);
    return this;
}

// Usage: loginPage.WithUserName("demo.user").WithPassword("Secret123!").LoginButton.Click();
```

### 5. Scroll Inside the Page, Not the Test

```csharp
public void OpenProduct(string productName)
{
    var row = ProductRow(productName);
    row.ScrollToVisible(ScrollDirection.Down);   // using Bellatrix.Mobile;
    row.Click();
}
```

### 6. Organizing Complex Screens with Regions

Group locators in the Map file with `// ===== REGION =====` comments (form fields, buttons, lists,
messages, navigation).

### 7. Reusable Sections Across Screens (Composition)

When two or more screens share the same block AND workflow (bottom navigation, toolbar, number pad),
extract a section class under `Pages/Shared/`. A section is NOT a page — it resolves `App` itself:

```csharp
// Pages/Shared/BottomNavigationSection.cs
using Bellatrix;
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;

namespace MyProject.MobileTests.Pages.Shared;

public class BottomNavigationSection
{
    public AndroidApp App => ServicesCollection.Current.Resolve<AndroidApp>();   // IOSApp on iOS

    public Button HomeTab => App.Components.CreateByDescription<Button>("Home");
    public Button SettingsTab => App.Components.CreateByDescription<Button>("Settings");

    public void OpenHome() => HomeTab.Click();

    public void OpenSettings() => SettingsTab.Click();
}
```

Embed it in each screen's Map file and delegate from Actions:

```csharp
// Pages/HomePage/HomePage.Map.cs
public partial class HomePage : AndroidPage
{
    public BottomNavigationSection Navigation { get; } = new BottomNavigationSection();
    // ...screen-specific locators...
}

// Pages/HomePage/HomePage.Actions.cs
public partial class HomePage
{
    public void GoToSettings() => Navigation.OpenSettings();
}
```

**Rules of thumb:**
- Extract only after the duplication is real (≥2 screens).
- Keep public action names on each screen unchanged — the section is an implementation detail.
- A section owns its locators AND its workflow methods; a pure locator group stays on the screen.

### 8. Constructor Dependencies

`App.Create<TPage>()` resolves pages from the BELLATRIX container, so a page may request services in
its constructor (as the BELLATRIX Android sample does):

```csharp
public partial class CheckoutPage
{
    private readonly AndroidKeyboardService _keyboard;   // using Bellatrix.Mobile.Services.Android;

    public CheckoutPage(AndroidKeyboardService keyboard) => _keyboard = keyboard;
}
```

Usually `App.Keyboard` is simpler — use constructor injection only when you need it.

---

## Model Classes

Plain data in `Models/` — no BELLATRIX types:

```csharp
namespace MyProject.MobileTests.Models;

public class LoginCredentials
{
    public string UserName { get; set; }
    public string Password { get; set; }
}
```

---

## Best Practices Summary

1. **One folder per screen** with 3 partial files (Map, Actions, Assertions)
2. **Map file**: base class, `PackageName`/`ActivityName` (Android), component properties — no logic
3. **Actions file**: business workflows — no assertions
4. **Assertions file**: only `Assert*` methods using `Validate*` — no actions
5. **`App.Create<TPage>()`** everywhere — never `new TPage()`
6. **No `App.Components` in test files** — tests call page object methods only
7. **Business-level method names** (`LoginWith`, `AddToCart`), not UI mechanics (`TapButton3`)
8. **One platform per file** — never import both `Bellatrix.Mobile.Android` and `Bellatrix.Mobile.IOS`
