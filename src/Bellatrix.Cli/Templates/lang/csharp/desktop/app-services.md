# BELLATRIX Desktop App Services Reference

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `App.Navigation.Navigate(url)` | Desktop apps don't navigate by URL — use `App.Create<TPage>()` |
| `App.Browser.WaitUntilReady()` | `App.Wait` or wait strategies on components |
| `App.Components.CreateByXpath<T>()` | `App.Components.CreateByXPath<T>()` (capital P) |
| `new LoginWindow()` | `App.Create<LoginWindow>()` |

---

## ComponentCreateService (`App.Components`)

### Single Component Creation

```csharp
// By AutomationId (PREFERRED)
App.Components.CreateByAutomationId<Button>("btnOk")

// By Name
App.Components.CreateByName<TextField>("Username")

// By XPath (PascalCase required for WinAppDriver)
App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnOk']")

// By ClassName
App.Components.CreateByClass<TextField>("TextBox")

// By Tag (ControlType)
App.Components.CreateByTag<Label>("Text")
```

### Collection Creation

```csharp
App.Components.CreateAllByAutomationId<ListItem>("item")
App.Components.CreateAllByName<Button>("Submit")
App.Components.CreateAllByXPath<Label>("//Text[@ClassName='Label']")
App.Components.CreateAllByClass<CheckBox>("CheckBox")
App.Components.CreateAllByTag<Button>("Button")
```

---

## AppService (`App.AppService`)

| Property/Method | Returns | Description |
|----------------|---------|-------------|
| `Title` | `string` | Current window title |
| `Maximize()` | `void` | Maximize the application window |
| `Back()` | `void` | Navigate back |
| `Forward()` | `void` | Navigate forward |
| `GetCurrentViewSnapshot()` | `string` | Compact JSON array of key attributes (Tag, AutomationId, Name, ClassName, ControlType, Value, HelpText, state flags) for elements with a non-empty Name or AutomationId |

### Example Usage

```csharp
// Get window title
var title = App.AppService.Title;
Assert.That(title, Does.Contain("My Application"));

// Maximize window
App.AppService.Maximize();

// Get UI snapshot for analysis
var snapshot = App.AppService.GetCurrentViewSnapshot();
```

---

## Wait Service (`App.Wait`)

```csharp
using Bellatrix.Desktop.Untils; // `Wait` lives here (the namespace really is spelled "Untils")

// Wait for a component with a specific strategy
App.Wait.Wait(element, Wait.To.BeVisible());
```

Most commonly, use fluent wait methods on components instead:

```csharp
var button = App.Components.CreateByAutomationId<Button>("btn").ToBeClickable();
var label = App.Components.CreateByName<Label>("Status").ToHasContent();
```

---

## Page Object Factory

### `App.Create<TPage>()`

Creates a page object instance without any navigation. Use when the window is already open:

```csharp
var loginWindow = App.Create<LoginWindow>();
loginWindow.LoginWith("user", "pass");

var mainWindow = App.Create<MainWindow>();
mainWindow.AssertWindowLoaded();
```

---

## Configuration: `testFrameworkSettings.json`

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

| Setting | Values | Description |
|---------|--------|-------------|
| `executionSettings.defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | App lifecycle |
| `executionSettings.defaultAppPath` | Path or App User Model ID | Application to launch |
| `executionSettings.url` | URL (e.g. `http://127.0.0.1:4723`) | WinAppDriver endpoint |
| `executionSettings.shouldStartLocalService` | `"false"` | Never let tests start WinAppDriver; it is the user's to run |
