# BELLATRIX Desktop Validations and Assertions Reference (Java)

## CRITICAL: Use Native validate* Methods Over JUnit Assertions

For element-level assertions, **prefer** BELLATRIX built-in `validate*` methods. They produce a meaningful failure
message naming the component and locator, integrate with BELLATRIX logging and screenshots-on-fail, and fail the test
with an `AssertionError`.

```java
// CORRECT - BELLATRIX validate methods (preferred)
display.toBeVisible().waitToBe();
display.validateTextIs("Display is 42");
```

```text
WRONG - raw JUnit assertions on element state (poor error messages)
Assertions.assertTrue(display.isVisible());
Assertions.assertEquals("Display is 42", display.getText());
```

### How desktop validate* behaves (BELLATRIX-Java 1.0) — read before writing assertions

- **The value is read ONCE.** `validateTextIs(x)` captures `getText()` when it is called and then waits
  `timeoutSettings.validationsTimeout` on that captured value (`J/validations/ComponentValidator.java`). Unlike the C#
  or web validators it does **not** re-read the UI. A wrong value just burns the timeout and fails. So **wait first,
  validate second** (`toHaveContent().waitToBe()`, `toBeVisible().waitToBe()`, or the polling helper below).
- The actual value is `strip()`-ed before comparison; the expected value is not.
- Keep `validationsTimeout` short (e.g. 10 s) in `testFrameworkSettings.dev.json` — retries never help here.

---

## Available Desktop Validate Methods

### Visibility — there is NO `validateIsVisible()` / `validateNotVisible()`

```java
// Visible: wait for it (throws on timeout, failing the test)
errorMessage.toBeVisible().waitToBe();

// Gone: wait for it through the wait service (see bellatrix-desktop-locators-and-waits)
app().waitFor().wait(errorMessage, Wait.to().notExist());
```

**TRAP:** every desktop component has `validateIsSelected()` / `validateNotSelected()`, inherited from
`ComponentVisible` — they assert **visibility**, not selection. Never use them for selection state.

### Text Content (`Button`, `TextField`, `TextArea`, `Label`, `ComboBox`, `Anchor`)

```java
display.validateTextIs("exact text");           // exact match (actual is stripped)
display.validateTextContains("partial");        // contains
display.validateTextNotContains("Error");       // does not contain
```

Java desktop **does** have `validateTextContains` (C#'s `ValidateInnerTextContains` gap does not apply). There is no
`validateInnerTextIs` — the Java name is `validateTextIs`.

### State

```java
saveButton.validateIsDisabled();      // ComponentDisabled: Button, TextField, TextArea, ComboBox, CheckBox, RadioButton, ListBox, Expander
saveButton.validateNotDisabled();     // "is enabled" — there is no validateIsEnabled()
rememberMe.validateIsChecked();       // CheckBox / RadioButton
rememberMe.validateIsUnchecked();     // there is no validateIsNotChecked()
```

`validateIsDisabled`/`validateNotDisabled` evaluate `isDisabled()`, which reads a `disabled` attribute (always
`false` on `PasswordField`, `DateInput`, `TimeInput`). If it does not track the real state of your control, assert the
UIA property instead (see JUnit fallbacks below).

### Date/Time

```java
startDate.validateDateIs("1/15/2024");      // DateInput: also validateDateContains / validateDateNotContains
startTime.validateTimeIs("2:30 PM");        // TimeInput: also validateTimeContains / validateTimeNotContains
```

### Values that change asynchronously — poll first, then validate

```java
// wait (re-reading the component) until the display settles, then validate for the report
new WebDriverWait(display.getWrappedDriver(), Duration.ofSeconds(10))
        .until(d -> display.getText().contains("8"));
display.validateTextContains("8");
```

(`org.openqa.selenium.support.ui.WebDriverWait`, `java.time.Duration`; it polls the BELLATRIX component — it does
not locate elements with the raw driver.)

---

## Usage in a Page Object `Asserts` Class

```java
package demo.desktoptests.pages.loginwindow;

import solutions.bellatrix.desktop.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void windowLoaded() {
        map().loginButton().toBeVisible().waitToBe();
        map().usernameField().toBeVisible().waitToBe();
        map().passwordField().toBeVisible().waitToBe();
    }

    public void errorDisplayed(String expectedError) {
        map().errorMessage().toBeVisible().waitToBe();
        map().errorMessage().validateTextIs(expectedError);
    }

    public void loginSuccessful() {
        map().welcomeLabel().toHaveContent().waitToBe();
        map().welcomeLabel().validateTextIs("Welcome");
    }
}
```

`map()` is `protected` inside `PageAsserts`; tests call `page.asserts().errorDisplayed("…")`.

---

## When to Use JUnit Assertions

Use `org.junit.jupiter.api.Assertions` **only** when no native validate method covers your need:

```java
// Window title (string, not a component)
Assertions.assertTrue(app().appService().getTitle().contains("Main Window"));

// Collection counts
Assertions.assertEquals(5, items.size());

// Attribute values not covered by validate methods
Assertions.assertEquals("btnOk", okButton.getAttribute("AutomationId"));
Assertions.assertEquals("Submit", okButton.getAttribute("Name"));

// Enabled state straight from UIA (when isDisabled() does not reflect it)
Assertions.assertEquals("True", okButton.getAttribute("IsEnabled"));

// Toggle state
Assertions.assertEquals("1", rememberMe.getAttribute("Toggle.ToggleState"));

// Visibility as a boolean (prefer toBeVisible().waitToBe())
Assertions.assertTrue(errorMessage.isVisible());
```

Soft assertions (C# `Assert.Multiple`): `Assertions.assertAll(() -> …, () -> …)`. There is no BELLATRIX facility.

---

## Priority Order

1. **Always try native validate methods first** — after waiting for the UI to settle (they read once)
2. **Fall back to JUnit `Assertions`** only for attribute checks, window title, counts, or non-component values
3. **Never use raw `Assertions` on state that has a validate method** (e.g., don't `assertEquals(x, c.getText())` when
   `validateTextIs(x)` exists); visibility is the exception — wait with `toBeVisible().waitToBe()`
