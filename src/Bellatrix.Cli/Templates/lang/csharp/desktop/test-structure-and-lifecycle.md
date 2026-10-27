# BELLATRIX Desktop Test Structure and Lifecycle Reference

## Test Base Classes

### NUnit

```csharp
using Bellatrix.Desktop;        // [App], Lifecycle
using Bellatrix.Desktop.NUnit;
using NUnit.Framework;

namespace MyProject.Tests;

[TestFixture]
[App(@"C:\Program Files\MyApp\MyApp.exe", Lifecycle.RestartEveryTime)]
public class LoginTests : DesktopTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensMainWindow()
    {
        // Test code here
    }
}
```

### MSTest

```csharp
using Bellatrix.Desktop;        // [App], Lifecycle
using Bellatrix.Desktop.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyProject.Tests;

[TestClass]
[App(@"C:\Program Files\MyApp\MyApp.exe", Lifecycle.RestartEveryTime)]
public class LoginTests : DesktopTest
{
    [TestMethod]
    [TestCategory("Smoke")]
    public void Login_WithValidCredentials_OpensMainWindow()
    {
        // Test code here
    }
}
```

---

## The App Object

The `App` object is your entry point for all desktop interactions. Available via `this.App` in test classes and `this.App` in page objects (via `DesktopPage` base class).

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `App.Components` | Create and find desktop components |
| AppService | `App.AppService` | Window operations (Title, Maximize, Back, Forward) |
| Wait | `App.Wait` | Wait for conditions |
| Create | `App.Create<TPage>()` | Instantiate page objects |

---

## App Attribute and Lifecycle

```csharp
[App(appPath, Lifecycle)]
```

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RestartEveryTime` | App restarts before every test (cleanest, slowest) |
| `Lifecycle.ReuseIfStarted` | Reuses running app (fastest, shared state risk) |
| `Lifecycle.RestartOnFail` | Restarts only after a failed test |

### Config-Based Alternative

Prefer `testFrameworkSettings.json` over attributes for CI/CD flexibility:

```json
{
  "desktopSettings": {
    "executionSettings": {
      "defaultLifeCycle": "restart every time",
      "shouldStartLocalService": "false",
      "defaultAppPath": "C:\\Program Files\\MyApp\\MyApp.exe",
      "url": "http://127.0.0.1:4723"
    }
  }
}
```

---

## Test Naming Convention

Use the pattern: `Action_Condition_ExpectedResult`

```csharp
// Good
public void Login_WithValidCredentials_OpensMainWindow()
public void DeleteItem_WithConfirmation_RemovesItemFromList()
public void SelectTheme_DarkMode_UpdatesAppearance()

// Bad
public void TestLogin()
public void Test1()
public void VerifyDeleteWorks()
```

---

## Test Categories

```csharp
[Category("Smoke")]       // Critical path tests, run on every build
[Category("Regression")]  // Full test suite
[Category("CI")]          // Fast tests suitable for CI pipelines
```

---

## Complete Test Class Template

```csharp
using Bellatrix.Desktop;
using Bellatrix.Desktop.NUnit;
using NUnit.Framework;

namespace MyProject.Tests;

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
        usernameField.SetText("testuser");
        passwordField.SetPassword("SecurePass123");
        loginButton.Click();

        // Assert
        var welcomeLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblWelcome']");
        welcomeLabel.ValidateIsVisible();
        welcomeLabel.ValidateInnerTextIs("Welcome");
    }

    [Test]
    [Category("Regression")]
    public void Login_WithInvalidPassword_ShowsErrorMessage()
    {
        // Arrange
        var usernameField = App.Components.CreateByXPath<TextField>("//Edit[@AutomationId='txtUsername']");
        var passwordField = App.Components.CreateByXPath<Password>("//Edit[@AutomationId='txtPassword']");
        var loginButton = App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnLogin']");

        // Act
        usernameField.SetText("testuser");
        passwordField.SetPassword("wrongpassword");
        loginButton.Click();

        // Assert
        var errorLabel = App.Components.CreateByXPath<Label>("//Text[@AutomationId='lblError']");
        errorLabel.ValidateIsVisible();
        errorLabel.ValidateInnerTextIs("Invalid credentials");
    }
}
```

---

## Required Using Directives

```csharp
// Core — components, [App], Lifecycle, Validate* extensions
using Bellatrix.Desktop;
using Bellatrix.Desktop.PageObjects; // DesktopPage (Map files)

// Test framework (choose one)
using Bellatrix.Desktop.NUnit;    // NUnit base class
using Bellatrix.Desktop.MSTest;   // MSTest base class

// Test attributes (choose one)
using NUnit.Framework;             // [TestFixture], [Test], [Category]
using Microsoft.VisualStudio.TestTools.UnitTesting; // [TestClass], [TestMethod]

// Page objects
using MyProject.Pages;
```
