# BELLATRIX Desktop Validations and Assertions Reference

## CRITICAL: Use Native Validate Methods Over Assert.That

For element-level assertions, **always prefer** BELLATRIX built-in `Validate*` methods. They provide automatic retry, better error messages, and integrate with BELLATRIX reporting.

```csharp
// CORRECT - BELLATRIX Validate methods (preferred)
element.ValidateIsVisible();
element.ValidateInnerTextIs("Display is 42");

// WRONG - Raw NUnit assertions on elements (no retry, poor error messages)
Assert.That(element.IsVisible, Is.True);
Assert.That(element.InnerText, Is.EqualTo("Display is 42"));
```

---

## Available Desktop Validate Methods

### Visibility

```csharp
element.ValidateIsVisible();       // Element is displayed
element.ValidateIsNotVisible();    // Element is hidden or not displayed
```

### Text Content

```csharp
element.ValidateInnerTextIs("exact text");   // Exact match (with retry)
element.ValidateTextIs("exact text");        // For components that use GetText()
element.ValidateTextIsNull();                // Text is null
```

**IMPORTANT:** The Desktop module does NOT have `ValidateInnerTextContains`. Use `ValidateInnerTextIs` with the full expected string. If you need a partial/contains match, fall back to `Assert.That(element.InnerText, Does.Contain("partial"))`.

### State

```csharp
element.ValidateIsDisabled();       // Element is not enabled
element.ValidateIsNotDisabled();    // Element is enabled
element.ValidateIsChecked();        // CheckBox/RadioButton is checked
element.ValidateIsNotChecked();     // CheckBox/RadioButton is unchecked
```

### Date/Time

```csharp
element.ValidateDateIs("2024-01-15");   // Date picker value
element.ValidateTimeIs("14:30");         // Time picker value
```

**Availability** (each method is limited to the components that implement its interface — calling it on another type does not compile):

| Method | Components |
|--------|------------|
| `ValidateIsVisible` / `ValidateIsNotVisible` | all |
| `ValidateInnerTextIs` | `Button`, `ComboBox`, `Label`, `TextArea`, `TextField` (not `ListItem`, `Password`) |
| `ValidateTextIs` / `ValidateTextIsNull` | `TextArea` only |
| `ValidateIsDisabled` / `ValidateIsNotDisabled` | `Button`, `Calendar`, `CheckBox`, `ComboBox`, `Date`, `Expander`, `Password`, `RadioButton`, `TextArea`, `TextField`, `Time` (not `ListBox`, `ListItem`) |
| `ValidateIsChecked` / `ValidateIsNotChecked` | `CheckBox`, `RadioButton` |
| `ValidateDateIs` | `Date` |
| `ValidateTimeIs` | `Time` |

For a `ListItem`, read `InnerText` / `IsDisabled` and assert with NUnit instead.

---

## Usage in Page Object Assertions File

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

## When to Use NUnit/MSTest Assert

Use framework assertions **only** when no native Validate method covers your need:

```csharp
// Partial/contains text match (no ValidateInnerTextContains in Desktop)
Assert.That(element.InnerText, Does.Contain("partial text"));

// Window title (string, not a component)
Assert.That(App.AppService.Title, Does.Contain("Main Window"));

// Collection counts
Assert.That(items.Count(), Is.EqualTo(5));

// Attribute values not covered by Validate methods
Assert.That(element.GetAttribute("AutomationId"), Is.EqualTo("btnOk"));
Assert.That(element.GetAttribute("Name"), Is.EqualTo("Submit"));

// Check element is enabled (use ValidateIsNotDisabled when possible)
Assert.That(element.GetAttribute("IsEnabled"), Is.EqualTo("True"));

// Check toggle state
Assert.That(element.GetAttribute("Toggle.ToggleState"), Is.EqualTo("1"));
```

---

## Priority Order

1. **Always try native Validate methods first** — they have automatic retry and better reporting
2. **Fall back to NUnit Assert** only for partial text matching, attribute checks, or non-component values
3. **Never use raw Assert on properties that have a Validate method** (e.g., don't use `Assert.That(element.IsVisible, Is.True)` when `ValidateIsVisible()` exists)
