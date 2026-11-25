# BELLATRIX Desktop Page Objects and Patterns Reference

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. File-Scoped Namespaces (C# 10+)

```csharp
// CORRECT - File-scoped namespace (no braces)
namespace {project.rootNamespace}.Pages;

public partial class LoginWindow : DesktopPage
{
    // ...
}

// WRONG - Old-style namespace
namespace {project.rootNamespace}.Pages
{
    public partial class LoginWindow : DesktopPage { }
}
```

### 2. Use `partial` Keyword

All page object files for the same window MUST use `partial class`.

### 3. Use `DesktopPage` Base Class

```csharp
// CORRECT
public partial class LoginWindow : DesktopPage { }

// WRONG - WebPage is for web, not desktop
public partial class LoginWindow : WebPage { }
```

### 4. Use Validate Methods in Assertions (NOT Assert.That)

See `bellatrix-desktop-validations-and-assertions` for details.

---

## Project Structure (3-File Pattern)

```
Pages/
  LoginWindow/
    LoginWindow.Actions.cs      # Action methods (login, fill form, etc.)
    LoginWindow.Map.cs          # Component properties (element locators)
    LoginWindow.Assertions.cs   # Assert* methods using Validate*
  SettingsWindow/
    SettingsWindow.Actions.cs
    SettingsWindow.Map.cs
    SettingsWindow.Assertions.cs
Models/
  LoginCredentials.cs
  UserProfile.cs
Tests/
  LoginTests.cs
  SettingsTests.cs
```

---

## Complete 3-File Example: LoginWindow

### LoginWindow.Map.cs

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.PageObjects; // DesktopPage

namespace {project.rootNamespace}.Pages;

public partial class LoginWindow : DesktopPage
{
    // ===== FORM FIELDS =====
    public TextField UsernameField =>
        App.Components.CreateByAutomationId<TextField>("txtUsername");

    public Password PasswordField =>
        App.Components.CreateByAutomationId<Password>("txtPassword");

    // ===== BUTTONS =====
    public Button LoginButton =>
        App.Components.CreateByAutomationId<Button>("btnLogin");

    public Button CancelButton =>
        App.Components.CreateByAutomationId<Button>("btnCancel");

    // ===== LABELS =====
    public Label ErrorMessage =>
        App.Components.CreateByAutomationId<Label>("lblError");

    public Label WelcomeLabel =>
        App.Components.CreateByAutomationId<Label>("lblWelcome");

    // ===== CHECKBOXES =====
    public CheckBox RememberMeCheckbox =>
        App.Components.CreateByName<CheckBox>("Remember me");
}
```

### LoginWindow.Actions.cs

```csharp
using Bellatrix.Desktop;

namespace {project.rootNamespace}.Pages;

public partial class LoginWindow
{
    public void LoginWith(string username, string password)
    {
        UsernameField.SetText(username);
        PasswordField.SetPassword(password);
        LoginButton.Click();
    }

    public void LoginWithRememberMe(string username, string password)
    {
        UsernameField.SetText(username);
        PasswordField.SetPassword(password);
        RememberMeCheckbox.Check();
        LoginButton.Click();
    }

    public MainWindow LoginAndNavigateToMain(string username, string password)
    {
        LoginWith(username, password);
        return App.Create<MainWindow>();
    }

    public void CancelLogin()
    {
        CancelButton.Click();
    }
}
```

### LoginWindow.Assertions.cs

```csharp
using Bellatrix.Desktop;

namespace {project.rootNamespace}.Pages;

public partial class LoginWindow
{
    public void AssertWindowLoaded()
    {
        LoginButton.ValidateIsVisible();
        UsernameField.ValidateIsVisible();
        PasswordField.ValidateIsVisible();
    }

    public void AssertErrorDisplayed(string expectedError)
    {
        ErrorMessage.ValidateIsVisible();
        ErrorMessage.ValidateInnerTextIs(expectedError);
    }

    public void AssertLoginSuccessful()
    {
        WelcomeLabel.ValidateIsVisible();
        WelcomeLabel.ValidateInnerTextIs("Welcome");
    }
}
```

---

## Design Patterns

### 1. Property-Based Element Definitions (Recommended)

Use expression-bodied properties — they are lazy-loaded (evaluated on each access). Prefer direct factory methods:

```csharp
public Button OkButton =>
    App.Components.CreateByAutomationId<Button>("btnOk");
```

### 2. Method-Based for Dynamic Elements

When the locator depends on runtime data, use direct factories when only one attribute varies:

```csharp
public ListItem GetListItem(string itemText) =>
    App.Components.CreateByName<ListItem>(itemText);

public Button GetTab(string tabName) =>
    App.Components.CreateByName<Button>(tabName);
```

Use `CreateByXPath` only when the dynamic locator genuinely requires a compound or structural expression.

### 3. Navigation Methods Return Page Objects

```csharp
public SettingsWindow OpenSettings()
{
    SettingsButton.Click();
    return App.Create<SettingsWindow>();
}

public MainWindow LoginAndNavigateToMain(string user, string pass)
{
    LoginWith(user, pass);
    return App.Create<MainWindow>();
}
```

### 4. Fluent Interface for Chaining

```csharp
public LoginWindow WithUsername(string username)
{
    UsernameField.SetText(username);
    return this;
}

public LoginWindow WithPassword(string password)
{
    PasswordField.SetPassword(password);
    return this;
}

// Usage: loginWindow.WithUsername("user").WithPassword("pass").LoginButton.Click();
```

### 5. Organizing Complex Windows with Regions

Use `// ===== REGION =====` comments to group locators within a single window's Map file:

```csharp
public partial class SettingsWindow : DesktopPage
{
    // ===== GENERAL SETTINGS =====
    public TextField AppNameField =>
        App.Components.CreateByAutomationId<TextField>("txtAppName");

    public CheckBox AutoStartCheckbox =>
        App.Components.CreateByAutomationId<CheckBox>("chkAutoStart");

    // ===== APPEARANCE =====
    public ComboBox ThemeDropdown =>
        App.Components.CreateByAutomationId<ComboBox>("cmbTheme");

    // ===== ACTIONS =====
    public Button SaveButton =>
        App.Components.CreateByAutomationId<Button>("btnSave");

    public Button CancelButton =>
        App.Components.CreateByAutomationId<Button>("btnCancel");
}
```

### 6. Reusable Sections Across Windows (Composition)

When two or more windows share the same locator block AND the same workflow around it, extract a section class under `Pages/Shared/` and embed it via a `public` property on each Map partial. Windows then delegate from their action methods instead of duplicating the logic.

A section is NOT a `DesktopPage` — it's a plain class that resolves `App` the same way `DesktopPage` does:

```csharp
// Pages/Shared/NumberPadSection.cs
using Bellatrix;
using Bellatrix.Desktop;

namespace {project.rootNamespace}.Pages.Shared;

public class NumberPadSection
{
    public App App => ServicesCollection.Current.Resolve<App>();

    public Button Num0Button => App.Components.CreateByAutomationId<Button>("num0Button");
    // ... Num1–Num9, DecimalButton ...

    public Button GetDigitButton(int digit) => /* switch on digit */;

    public void EnterNumber(string number)
    {
        foreach (char c in number.Trim())
        {
            GetDigitButton(c - '0').Click();
        }
    }
}
```

Embed it in each window's Map file and delegate from Actions:

```csharp
// Pages/CalculatorWindow/CalculatorWindow.Map.cs
public partial class CalculatorWindow : DesktopPage
{
    public NumberPadSection NumberPad { get; } = new NumberPadSection();
    // ...window-specific locators...
}

// Pages/CalculatorWindow/CalculatorWindow.Actions.cs
public partial class CalculatorWindow
{
    public void EnterNumber(string number) => NumberPad.EnterNumber(number);
}
```

**Rules of thumb:**
- Extract only after the duplication is real (≥2 windows). Don't pre-extract for hypothetical reuse.
- Keep the public action names on each window unchanged — the section is an implementation detail so tests don't need to change.
- A section owns its own locators AND its own workflow methods; a pure locator group stays on the window as a region.
- If the shared AutomationIds differ per window, do NOT force a section — extract only what's genuinely identical.

---

## Model Classes

Place data models in `Models/` folder:

```csharp
namespace {project.rootNamespace}.Models;

public class LoginCredentials
{
    public string Username { get; set; }
    public string Password { get; set; }
}
```

Usage in page object:

```csharp
public void LoginWith(LoginCredentials credentials)
{
    UsernameField.SetText(credentials.Username);
    PasswordField.SetPassword(credentials.Password);
    LoginButton.Click();
}
```

---

## Best Practices Summary

1. **One folder per window** with 3 partial class files (Map, Actions, Assertions)
2. **Map file**: Only component property declarations — no logic
3. **Actions file**: Business workflow methods — no assertions
4. **Assertions file**: Only `Assert*` methods using `Validate*` — no actions
5. **Use `App.Create<TPage>()`** to instantiate page objects in tests and navigation methods
6. **No direct `App.Components`** in test files — always go through page objects
7. **Descriptive method names** reflecting business operations, not UI mechanics
