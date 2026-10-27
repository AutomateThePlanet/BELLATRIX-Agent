# BELLATRIX Desktop Components Reference

## Component Hierarchy

All desktop components inherit from the base `Component` class in `Bellatrix.Desktop` namespace.

## Desktop Components

| Component | WinAppDriver ControlType | Key Methods | Key Properties |
|-----------|-------------------------|-------------|----------------|
| `Button` | Button | `Click()`, `Hover()` | `InnerText`, `IsDisabled` |
| `TextField` | Edit | `SetText(string)`, `Hover()` | `InnerText`, `IsDisabled` |
| `TextArea` | Edit / Document | `SetText(string)`, `GetText()`, `Hover()` | `InnerText`, `IsDisabled` |
| `Password` | Edit (IsPassword=true) | `SetPassword(string)`, `GetPassword()`, `Hover()` | `IsDisabled` |
| `ComboBox` | ComboBox | `SelectByText(string)`, `Hover()` | `InnerText`, `IsDisabled` |
| `CheckBox` | CheckBox | `Check()`, `Uncheck()`, `Hover()` | `IsChecked`, `IsDisabled` |
| `RadioButton` | RadioButton | `Click()`, `Hover()` | `IsChecked`, `IsDisabled` |
| `Label` | Text | `Hover()` | `InnerText` |
| `ListBox` | List | `Hover()` | `IsDisabled` |
| `ListItem` | ListItem | `Select()`, `AddToSelection()`, `RemoveFromSelection()`, `Hover()` | `InnerText`, `IsDisabled` |
| `Date` | DatePicker | `SetDate(int year, int month, int day)`, `GetDate()`, `Hover()` | `IsDisabled` |
| `Time` | TimePicker | `SetTime(int hours, int minutes)`, `GetTime()`, `Hover()` | `IsDisabled` |
| `Calendar` | Calendar | `Hover()` | `IsDisabled` |
| `Tabs` | Tab | `Hover()` | — |
| `Menu` | Menu | `Hover()` | — |
| `Expander` | Expander | `Click()`, `Hover()` | `IsDisabled` |
| `Progress` | ProgressBar | `Hover()` | — |
| `Image` | Image | `Hover()` | — |
| `Window` | Window | — | `WindowHandle` |

---

## Creating Components

### Single Component (Factory Methods via `App.Components`)

```csharp
// By AutomationId (PREFERRED - most stable)
App.Components.CreateByAutomationId<Button>("btnOk")

// By Name
App.Components.CreateByName<TextField>("Username")

// By XPath (CRITICAL: PascalCase for WinAppDriver)
App.Components.CreateByXPath<Button>("//Button[@AutomationId='btnOk']")

// By ClassName
App.Components.CreateByClass<TextField>("TextBox")

// By Tag (ControlType)
App.Components.CreateByTag<Label>("Text")
```

### Component Collections (`CreateAllBy*`)

```csharp
// Returns ComponentsList<T> (IEnumerable<T>)
var buttons = App.Components.CreateAllByName<Button>("Submit");
var items = App.Components.CreateAllByAutomationId<ListItem>("item");
var labels = App.Components.CreateAllByTag<Label>("Text");

// Usage
buttons.ForEach(b => b.Click());
var count = items.Count();
var first = items[0];
```

### MANDATORY: Use BELLATRIX Factories — Never Drop to Raw Driver

When you need to locate elements inside a page object, action method, or **custom component**, ALWAYS go through `App.Components.Create*` / `App.Components.CreateAllBy*`. **Never** drop down to `driver.FindElement(s)(By.XPath(...))`, `WrappedDriver.FindElement(...)`, or raw Selenium/WinAppDriver `By` locators from the test/page code.

**Why:** BELLATRIX factory-returned components carry wait strategies, lifecycle integration, validation methods, logging, plugins, and the retry/healing pipeline. Raw `driver.FindElements` bypasses ALL of that — you lose waits, you lose validate methods, you lose logs, and you get brittle tests that don't survive minor UI timing changes.

#### Anti-pattern (DO NOT DO THIS)

```csharp
// WRONG — dropping to raw driver bypasses BELLATRIX entirely
var driver = StartingDatePicker.WrappedDriver;
var cells = driver.FindElements(By.XPath(
    $"//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='{day}']"));
if (cells.Count > 0)
{
    cells[0].Click();
}
```

#### Correct — use BELLATRIX factories

```csharp
// CORRECT — BELLATRIX factory returns a ComponentsList<Button> with full framework support
var cells = App.Components.CreateAllByXPath<Button>(
    $"//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='{day}']");

if (cells.Count() > 0)
{
    cells[0].Click();
}
```

#### Inside custom components (no `App` in scope)

Custom components that inherit from `Bellatrix.Desktop.Component` don't expose `App` or a `Components` member. Call the factory extensions on the component itself (`this.CreateAllByXPath<T>(…)`, `this.CreateByAutomationId<T>(…)` …) — they search inside this component's subtree:

```csharp
public class MonthViewCell : Component
{
    public void ClickDay(int day)
    {
        var cells = this.CreateAllByXPath<Button>(
            $"//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='{day}']");

        if (cells.Count() > 0)
        {
            cells[0].Click();
        }
    }
}
```

Alternatively, for child lookups scoped to the current component, use the instance factories on the component itself:

```csharp
var childItem = this.CreateByXPath<ListItem>(".//ListItem[@Name='Apply']");
```

#### The only times raw driver access is acceptable

- Genuinely low-level operations BELLATRIX does not expose (e.g., window-handle switching for native popup flyouts, key chord sequences, session diagnostics). Even then, acquire the driver once, do the minimum, and return to BELLATRIX components for element interaction
- Never use raw driver to **find or click** elements — use factories

### Nested Component Creation

```csharp
// Find child elements within a parent component
var comboBox = App.Components.CreateByAutomationId<ComboBox>("listBox");
var item = comboBox.CreateByAutomationId<ListItem>("lb2");
item.Hover();
```

---

## Base Component Properties (Available on All Components)

```csharp
// Visibility and presence
component.IsPresent      // bool - exists in UI tree
component.IsVisible      // bool - displayed on screen

// Location and size
component.Location       // Point (X, Y coordinates)
component.Size           // System.Drawing.Size (Width, Height)

// Identity
component.ComponentName  // string - element identifier
component.PageName       // string - page identifier

// Attributes
component.GetAttribute("Name")           // Get any UI Automation attribute
component.GetAttribute("AutomationId")
component.GetAttribute("ClassName")
component.GetAttribute("HelpText")
component.GetAttribute("IsEnabled")

// Actions
component.ScrollToVisible()    // Scroll element into view
component.WaitToBe()           // Force element location and wait
component.TakeScreenshot(path) // Capture element screenshot
```
