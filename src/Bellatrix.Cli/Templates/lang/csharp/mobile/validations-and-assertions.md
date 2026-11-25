# BELLATRIX Mobile Validations and Assertions Reference

## CRITICAL: Use Native Validate Methods Over Assert.That

For element-level assertions, **always prefer** the BELLATRIX `Validate*` extension methods. They retry
until the condition holds (up to `mobileSettings.timeoutSettings.validationsTimeout`), produce readable
failure messages, and feed BDD logging, screenshots-on-fail, and bug reporting.

```csharp
// CORRECT — BELLATRIX Validate methods (preferred)
welcomeLabel.ValidateIsVisible();
welcomeLabel.ValidateTextIs("Welcome, demo.user");

// WRONG — raw assertions on elements (no retry, poor messages)
Assert.That(welcomeLabel.IsVisible, Is.True);
Assert.That(welcomeLabel.GetText(), Is.EqualTo("Welcome, demo.user"));
```

`Validate*` methods come from the platform namespace — `using Bellatrix.Mobile.Android;` or
`using Bellatrix.Mobile.IOS;` (never both in one file). Names are identical on both platforms.

---

## Available Mobile Validate Methods

Every method takes optional `timeout` and `sleepInterval` (seconds):
`radioButton.ValidateIsChecked(timeout: 30, sleepInterval: 2);`

### Visibility — every component

```csharp
element.ValidateIsVisible();
element.ValidateIsNotVisible();
```

### Text — components with `GetText()`

```csharp
element.ValidateTextIs("exact text");     // exact, case-sensitive match (with retry)
element.ValidateTextIsNotSet();            // text is null or empty
```

Supported by: `Button`, `ImageButton`, `TextField`, `Label`, `CheckBox`, `RadioButton`, `ComboBox`,
`ToggleButton`, and Android `Switch`. **Not** by `Password`, `Number`, `SeekBar`, `Image`.

> **Runtime caveat (BELLATRIX bug):** `GetText()` is self-recursive (`public new virtual string GetText() { return GetText(); }`)
> on Android `Button`, `ImageButton`, `Label`, `CheckBox`, `RadioButton`, `Switch`, `ToggleButton` and iOS `Button`,
> `ImageButton`, `Label`, `CheckBox`, `RadioButton`, `ToggleButton`, `ComboBox` — it compiles but overflows the stack at
> runtime, and `ValidateTextIs` / `ValidateTextIsNotSet` go through it. `TextField` and Android `ComboBox` are safe. Until
> BELLATRIX fixes it (`return base.GetText();`), validate those types with `GetAttribute("text")` (Android) /
> `GetAttribute("label")` (iOS) plus an NUnit `Assert`, or a custom component that overrides `GetText()`.

**IMPORTANT:** mobile has NO `ValidateInnerTextIs` and NO `ValidateTextContains` / `ValidateInnerTextContains`.
Use `ValidateTextIs` with the full observed text; for a partial match fall back to
`Assert.That(element.GetText(), Does.Contain("partial"))`.

### Enabled / Disabled — components with `IsDisabled`

```csharp
element.ValidateIsDisabled();
element.ValidateIsNotDisabled();
```

Supported by: `Button`, `ImageButton`, `TextField`, `Password`, `Number`, `CheckBox`, `RadioButton`,
`ComboBox`, `SeekBar`, `ToggleButton`, and Android `Switch`. **Not** by `Label`, `Image`.

### Checked — `CheckBox`, `RadioButton`

```csharp
checkBox.ValidateIsChecked();
checkBox.ValidateIsNotChecked();
```

### On / Off — `ToggleButton`, Android `Switch`

```csharp
notificationsSwitch.ValidateIsOn();
notificationsSwitch.ValidateIsOff();
```

### Number — `Number`

```csharp
quantity.ValidateNumberIs(3);
```

`ValidateIsSelected`, `ValidateDateIs`, and `ValidateTimeIs` exist but no built-in mobile component
implements their contracts — do not use them on built-in components (they will not compile).

### Validate-method support at a glance

| Component | Visible | TextIs / TextIsNotSet | (Not)Disabled | (Not)Checked | On / Off | NumberIs |
|-----------|:-------:|:---------------------:|:-------------:|:------------:|:--------:|:--------:|
| `Button`, `ImageButton` | ✓ | ✓ | ✓ | | | |
| `TextField` | ✓ | ✓ | ✓ | | | |
| `Password` | ✓ | | ✓ | | | |
| `Number` | ✓ | | ✓ | | | ✓ |
| `Label` | ✓ | ✓ | | | | |
| `CheckBox` | ✓ | ✓ | ✓ | ✓ | | |
| `RadioButton` | ✓ | ✓ | ✓ | ✓ | | |
| `ComboBox` | ✓ | ✓ | ✓ | | | |
| `ToggleButton` | ✓ | ✓ | ✓ | | ✓ | |
| `Switch` (Android) | ✓ | ✓ | ✓ | | ✓ | |
| `SeekBar` | ✓ | | ✓ | | | |
| `Image`, `RadioGroup` | ✓ | | | | | |

---

## Grouping Assertions — `Assert.Multiple`

Run several validations and report every failure, not just the first:

```csharp
Bellatrix.Assertions.Assert.Multiple(
    () => welcomeLabel.ValidateIsVisible(),
    () => cartBadge.ValidateTextIs("2"),
    () => checkoutButton.ValidateIsNotDisabled());
```

---

## Usage in Page Object Assertions File

```csharp
using Bellatrix.Mobile.Android;

namespace {project.rootNamespace}.Pages;

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

    public void AssertLoginButtonDisabled()
    {
        LoginButton.ValidateIsDisabled();
    }

    public void AssertRememberMeChecked()
    {
        RememberMeCheckBox.ValidateIsChecked();
    }
}
```

---

## When to Use NUnit/MSTest Assert (or `App.Assert`)

Use framework assertions **only** when no native Validate method covers the need:

```csharp
// Partial / contains text (no ValidateTextContains on mobile)
Assert.That(statusLabel.GetText(), Does.Contain("placed"));

// Collection counts
Assert.That(rows.Count(), Is.EqualTo(5));

// Raw attribute values
Assert.That(loginButton.GetAttribute("content-desc"), Is.EqualTo("Log in"));      // Android
Assert.That(passwordField.GetAttribute("value"), Is.Not.Empty);                   // iOS

// Device / app state (not components)
Assert.That(App.Device.Orientation, Is.EqualTo(ScreenOrientation.Landscape));    // using OpenQA.Selenium;
Assert.That(App.AppService.CurrentActivity, Does.Contain("Settings"));            // Android
Assert.That(App.AppService.IsAppInstalled("com.example.shop"), Is.True);

// Values read from a component that has no Validate counterpart
Assert.That(quantityPassword.GetPassword(), Is.EqualTo("1234"));

// BELLATRIX's own assertion service is also available
App.Assert.AreEqual("Jupiter", planetComboBox.GetText());
App.Assert.IsTrue(termsCheckBox.IsChecked);
```

---

## Priority Order

1. **Always try native `Validate*` first** — retry plus reporting
2. **Fall back to NUnit `Assert.That` / `App.Assert`** only for partial text, counts, raw attributes, and
   app/device state
3. **Never assert raw on a property that has a Validate method** (e.g. no
   `Assert.That(button.IsDisabled, Is.True)` when `ValidateIsDisabled()` exists)
4. **Never assert on `App.AppService.PageSource`** — it is huge and brittle
