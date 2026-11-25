# BELLATRIX Desktop System Tests Recipes

> **Note:** These recipes show direct component usage for reference. For production code, always use **Page Objects** (see `bellatrix-desktop-page-objects-and-patterns`).
>
> **Page Object Structure:**
> ```
> Pages/{WindowName}/{WindowName}.Actions.cs   - Action methods
> Pages/{WindowName}/{WindowName}.Map.cs       - Component properties
> Pages/{WindowName}/{WindowName}.Assertions.cs - Assert* methods
> ```

---

## Login Flow Recipe

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.NUnit;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[App(@"C:\Program Files\MyApp\MyApp.exe", Lifecycle.RestartEveryTime)]
public class LoginTests : DesktopTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensMainWindow()
    {
        // Arrange
        var usernameField = App.Components.CreateByXPath<TextField>("//Edit[@AutomationId='txtUsername']");
        var passwordField = App.Components.CreateByXPath<Password>("//Edit[@AutomationId='txtPassword']");
        var loginButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnLogin']");

        // Act
        usernameField.SetText("user@example.com");
        passwordField.SetPassword("Password123");
        loginButton.Click();

        // Assert
        var welcomeLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblWelcome']");
        welcomeLabel.ValidateIsVisible();
        welcomeLabel.ValidateInnerTextIs("Welcome");
    }
}
```

---

## Form Submission Recipe

```csharp
[Test]
public void RegisterUser_WithValidData_ShowsSuccessMessage()
{
    // Arrange
    var nameField = App.Components.CreateByXPath<TextField>("//Edit[@AutomationId='txtName']");
    var emailField = App.Components.CreateByXPath<TextField>("//Edit[@AutomationId='txtEmail']");
    var countryCombo = App.Components.CreateByXPath<ComboBox>("//ComboBox[@AutomationId='cmbCountry']");
    var termsCheckbox = App.Components.CreateByXPath<CheckBox>("//CheckBox[@AutomationId='chkTerms']");
    var submitButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnSubmit']");

    // Act
    nameField.SetText("John Doe");
    emailField.SetText("john@example.com");
    countryCombo.SelectByText("United States");
    termsCheckbox.Check();
    submitButton.Click();

    // Assert
    var successMessage = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblSuccess']");
    successMessage.ValidateInnerTextIs("Registration successful");
}
```

---

## List/Grid Selection Recipe

```csharp
[Test]
public void SelectItem_FromList_DisplaysDetails()
{
    // Arrange & Act
    var item = App.Components.CreateByXPath<ListItem>("//ListItem[@Name='Product A']");
    item.Select();

    // Assert
    var detailsLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblDetails']");
    detailsLabel.ValidateIsVisible();
    detailsLabel.ValidateInnerTextIs("Product A");
}
```

---

## Dialog Confirmation Recipe

```csharp
[Test]
public void DeleteItem_WithConfirmation_RemovesItem()
{
    // Act - trigger delete
    var deleteButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnDelete']");
    deleteButton.Click();

    // Act - confirm dialog
    var confirmButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnConfirm']");
    confirmButton.ValidateIsVisible();
    confirmButton.Click();

    // Assert
    var statusLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblStatus']");
    statusLabel.ValidateInnerTextIs("Item deleted");
}
```

---

## Menu Navigation Recipe

```csharp
[Test]
public void OpenSettings_ViaMenu_ShowsSettingsWindow()
{
    // Act - navigate menu: File > Settings
    var fileMenu = App.Components.CreateByXPath<Button>("//MenuItem[@Name='File']");
    fileMenu.Click();

    var settingsItem = App.Components.CreateByXPath<Button>("//MenuItem[@Name='Settings']");
    settingsItem.Click();

    // Assert
    var settingsTitle = App.Components.CreateByXPath<Label>("//Text[@Name='Settings']");
    settingsTitle.ValidateIsVisible();
}
```

---

## Settings Dialog Recipe (Tabs, Combos, Checkboxes)

```csharp
[Test]
public void ChangeTheme_ToDarkMode_UpdatesAppearance()
{
    // Arrange - navigate to appearance tab
    var appearanceTab = App.Components.CreateByXPath<Button>("//TabItem[@Name='Appearance']");
    appearanceTab.Click();

    // Act
    var themeCombo = App.Components.CreateByXPath<ComboBox>("//ComboBox[@AutomationId='cmbTheme']");
    themeCombo.SelectByText("Dark");

    var saveButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnSave']");
    saveButton.Click();

    // Assert
    var statusLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblStatus']");
    statusLabel.ValidateInnerTextIs("Settings saved");
}
```

---

## Data-Driven Test Recipe

```csharp
[Test]
[TestCase("admin", "admin123", "Welcome, Admin")]
[TestCase("user", "user123", "Welcome, User")]
[TestCase("guest", "guest123", "Welcome, Guest")]
public void Login_WithDifferentRoles_ShowsCorrectWelcome(string username, string password, string expectedWelcome)
{
    // Arrange
    var usernameField = App.Components.CreateByXPath<TextField>("//Edit[@AutomationId='txtUsername']");
    var passwordField = App.Components.CreateByXPath<Password>("//Edit[@AutomationId='txtPassword']");
    var loginButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnLogin']");

    // Act
    usernameField.SetText(username);
    passwordField.SetPassword(password);
    loginButton.Click();

    // Assert
    var welcomeLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblWelcome']");
    welcomeLabel.ValidateInnerTextIs(expectedWelcome);
}
```

---

## Page Objects End-to-End Recipe

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.NUnit;
using NUnit.Framework;
using {project.rootNamespace}.Pages;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[App(@"C:\Program Files\MyApp\MyApp.exe", Lifecycle.RestartEveryTime)]
public class EndToEndTests : DesktopTest
{
    [Test]
    [Category("Smoke")]
    public void CompleteWorkflow_LoginAndTransfer_ShowsConfirmation()
    {
        // Arrange
        var loginWindow = App.Create<LoginWindow>();
        loginWindow.AssertWindowLoaded();

        // Act - Login
        var mainWindow = loginWindow.LoginAndNavigateToMain("testuser", "password123");
        mainWindow.AssertWindowLoaded();

        // Act - Perform transfer
        mainWindow.TransferItem("Item2", "targetUser", "securePass");

        // Assert
        mainWindow.AssertTransferSuccessful();
    }
}
```

---

## Common XPath Patterns for Desktop

```csharp
// By AutomationId (PREFERRED)
"//Button[@AutomationId='btnOk']"
"//Edit[@AutomationId='txtUsername']"

// By Name
"//Button[@Name='OK']"
"//Edit[@Name='Username']"

// Dynamic with parameter
$"//ListItem[@Name='{itemName}']"
$"//TabItem[@Name='{tabName}']"
$"//TreeItem[@Name='{nodeName}']"

// Nested (child within parent)
"//ComboBox[@AutomationId='cmbList']//ListItem[@Name='Item1']"
```

---

## Required Using Directives

```csharp
using Bellatrix.Desktop;           // Components: Button, TextField, etc.
using Bellatrix.Desktop.PageObjects; // DesktopPage (Map files)
using Bellatrix.Desktop.Untils;    // Wait.To… strategies (namespace is spelled "Untils")
using Bellatrix.Desktop.NUnit;     // DesktopTest base (or Bellatrix.Desktop.MSTest)
using NUnit.Framework;              // [TestFixture], [Test], [Category]
using {project.rootNamespace}.Pages;              // Your page objects
using {project.rootNamespace}.Models;             // Your data models (if applicable)
```
