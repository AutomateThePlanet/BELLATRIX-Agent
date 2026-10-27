# BELLATRIX Mobile Validations and Assertions Reference (Java)

## CRITICAL: Use Native validate* Methods Over JUnit Assertions

For element-level assertions, **prefer** the BELLATRIX built-in `validate…` methods. They produce a failure message
naming the component and locator, integrate with BELLATRIX logging and screenshots-on-fail, and fail the test with
an `AssertionError`.

```java
// CORRECT - BELLATRIX validate methods (preferred)
welcomeLabel.toHaveContent().waitToBe();
welcomeLabel.validateTextIs("Welcome, demo.user");
```

```text
WRONG - raw JUnit assertions on element state (poor error messages)
Assertions.assertEquals("Welcome, demo.user", welcomeLabel.getText());
```

The `validate…` methods are interface default methods on the components (`solutions.bellatrix.android.components.contracts.*`
/ `solutions.bellatrix.ios.components.contracts.*`) — nothing extra to import. Names are identical on both platforms.

### How mobile validate* behaves (BELLATRIX-Java 1.0) — read before writing assertions

- **The value is read ONCE.** `validateTextIs(x)` captures `getText()` when it is called and then waits
  `timeoutSettings.validationsTimeout` on that captured value (`validations/ComponentValidator.java`, both
  platforms). It does **not** re-read the UI — a wrong value just burns the timeout and fails. So **wait first,
  validate second** (`toHaveContent().waitToBe()`, `toBeVisible().waitToBe()`, or the polling helper below).
- The actual value is `strip()`-ed before comparison; the expected value is not.
- `validateNumberIs(Number)` compares with `equals` against a `Double` — pass `3.0`, never `3`.
- Keep `validationsTimeout` short (e.g. 10 s) in `testFrameworkSettings.<env>.json` — retries never help here.

---

## Available Mobile Validate Methods

### Visibility — there is NO `validateIsVisible()` / `validateIsNotVisible()`

```java
// Visible: wait for it (throws on timeout, failing the test)
errorMessage.toBeVisible().waitToBe();

// Gone: wait through the wait service / an allBy… poll (see bellatrix-mobile-locators-and-waits)
app().waitFor().wait(spinner, Wait.to().notExist());   // Android
```

### Text — components implementing `ComponentText`

```java
label.validateTextIs("exact text");        // exact match (actual is stripped)
label.validateTextContains("partial");     // contains
label.validateTextNotContains("Error");    // does not contain
```

Supported by: `Button`, `ImageButton`, `TextField`, `Label`, `CheckBox`, `RadioButton`, `ComboBox`, `ToggleButton`,
and Android `Switch`. **Not** by `PasswordField`, `NumberInput`, `SeekBar`, `Progress`, `Image`. Java mobile **does**
have `validateTextContains` (C#'s gap does not apply). There is no `validateInnerTextIs` and no
`validateTextIsNotSet` — the Java name is `validateTextIs`. iOS `TextField.getText()` reads the `value` attribute.

### Enabled / Disabled — components implementing `ComponentDisabled`

```java
saveButton.validateIsDisabled();
saveButton.validateNotDisabled();     // "is enabled" — there is no validateIsEnabled() / validateIsNotDisabled()
```

Supported by: `Button`, `ImageButton`, `TextField`, `PasswordField`, `NumberInput`, `CheckBox`, `RadioButton`,
`ComboBox`, `SeekBar`, `Progress`, `ToggleButton`, and Android `Switch`. **Caveat:** `isDisabled()` reads an
attribute named `disabled`, which UiAutomator2 / XCUITest do not expose (they expose `enabled`), and `CheckBox` /
`Switch` / `ToggleButton` hard-code `false`. So `validateIsDisabled()` can never pass and `validateNotDisabled()`
always passes. Assert the real state with `getAttribute("enabled")` (JUnit fallback below), after confirming the
value live with `bellatrix mobile get-attribute --attribute enabled`.

### Checked — `CheckBox`, `RadioButton`

```java
rememberMe.validateIsChecked();
rememberMe.validateIsUnchecked();     // there is no validateIsNotChecked()
```

Reads the `checked` attribute — reliable on Android. On iOS XCUITest has no `checked` attribute: assert
`getAttribute("value")` instead.

### On / Off — `ToggleButton`, Android `Switch`

```java
notificationsSwitch.validateIsOn();
notificationsSwitch.validateIsOff();
```

Android only in practice — iOS `ToggleButton.isOn()` also reads `checked`; on iOS assert `getAttribute("value")`
(`"1"` / `"0"`).

### Number — `NumberInput`

```java
quantity.validateNumberIs(3.0);
```

**Not available on built-ins:** `validateIsSelected()` / `validateNotSelected()` (declared on the
`ComponentSelected` / `ComponentVisible` contracts) and `validateDate…` / `validateTime…` exist as contracts, but no
built-in mobile component implements them — calling them on a built-in does not compile.

### Validate-method support at a glance

| Component | TextIs / Contains / NotContains | IsDisabled / NotDisabled | IsChecked / IsUnchecked | IsOn / IsOff | NumberIs |
|-----------|:-------------------------------:|:------------------------:|:-----------------------:|:------------:|:--------:|
| `Button`, `ImageButton` | ✓ | ✓ | | | |
| `TextField` | ✓ | ✓ | | | |
| `PasswordField` | | ✓ | | | |
| `NumberInput` | | ✓ | | | ✓ |
| `Label` | ✓ | | | | |
| `CheckBox` | ✓ | ✓ (always "not disabled") | ✓ | | |
| `RadioButton` | ✓ | ✓ | ✓ | | |
| `ComboBox` | ✓ | ✓ | | | |
| `ToggleButton` | ✓ | ✓ (always "not disabled") | | ✓ | |
| `Switch` (Android) | ✓ | ✓ (always "not disabled") | | ✓ | |
| `SeekBar`, `Progress` | | ✓ | | | |
| `Image`, `RadioGroup`, `Grid`, `Tabs` | | | | | |

### Values that change asynchronously — poll first, then validate

```java
// wait (re-reading the component) until the status settles, then validate for the report
new WebDriverWait(status.getWrappedDriver(), Duration.ofSeconds(10))
        .until(d -> status.getText().contains("Synced"));
status.validateTextContains("Synced");
```

(`org.openqa.selenium.support.ui.WebDriverWait`, `java.time.Duration`; it polls the BELLATRIX component — it does
not locate elements with the raw driver.)

---

## Grouping Assertions

There is no BELLATRIX `Assert.Multiple` in Java. Use JUnit's `assertAll` to report every failure:

```java
Assertions.assertAll(
        () -> welcomeLabel.validateTextIs("Welcome, demo.user"),
        () -> cartBadge.validateTextIs("2"),
        () -> Assertions.assertEquals("true", checkoutButton.getAttribute("enabled")));
```

---

## Usage in a Page Object `Asserts` Class

```java
package com.myproject.mobiletests.android.pages.login;

import org.junit.jupiter.api.Assertions;
import solutions.bellatrix.android.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void pageLoaded() {
        map().userNameField().toBeVisible().waitToBe();
        map().passwordField().toBeVisible().waitToBe();
        map().loginButton().toBeVisible().waitToBe();
    }

    public void errorDisplayed(String expectedError) {
        map().errorMessage().toHaveContent().waitToBe();
        map().errorMessage().validateTextIs(expectedError);
    }

    public void loginButtonDisabled() {
        Assertions.assertEquals("false", map().loginButton().getAttribute("enabled"));
    }

    public void rememberMeChecked() {
        map().rememberMeCheckBox().validateIsChecked();
    }
}
```

`map()` is `protected` inside `PageAsserts`; tests call `page.asserts().errorDisplayed("…")`.

---

## When to Use JUnit Assertions

Use `org.junit.jupiter.api.Assertions` (TestNG: `org.testng.Assert`) **only** when no native validate method covers
the need:

```java
// Enabled state (isDisabled() does not track it)
Assertions.assertEquals("true", loginButton.getAttribute("enabled"));

// Collection counts
Assertions.assertEquals(5, rows.size());

// Raw attribute values
Assertions.assertEquals("Log in", loginButton.getAttribute("content-desc"));          // Android
Assertions.assertFalse(passwordField.getAttribute("value").isEmpty());                // iOS

// iOS switch state
Assertions.assertEquals("1", rememberMeSwitch.getAttribute("value"));

// Device / app state (not components)
Assertions.assertEquals(ScreenOrientation.LANDSCAPE, app().device().getScreenOrientation());   // org.openqa.selenium
Assertions.assertTrue(app().appService().getCurrentActivity().contains("Settings"));           // Android
Assertions.assertTrue(app().appService().isAppInstalled("com.example.shop"));

// Values read from a component that has no validate counterpart
Assertions.assertEquals("1234", pinField.getPassword());
```

---

## Priority Order

1. **Always try native `validate…` first** — after waiting for the UI to settle (they read once)
2. **Fall back to JUnit `Assertions`** for the enabled state, counts, raw attributes, iOS switch / check state, and
   app / device state
3. **Never assert raw on a value that has a working validate method** (e.g. don't
   `assertEquals(x, c.getText())` when `validateTextIs(x)` exists); visibility is the exception — wait with
   `toBeVisible().waitToBe()`
4. **Never assert on the page source** — it is huge and brittle
