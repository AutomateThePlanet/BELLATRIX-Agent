# BELLATRIX Desktop Page Objects and Patterns Reference (Java)

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. One Package per Window, Three Classes

A Java page object is **three separate classes in their own package** (there are no partial classes in Java):

| File | Class | Extends |
|------|-------|---------|
| `pages/loginwindow/LoginWindow.java` | `LoginWindow` | `DesktopPage<Map, Asserts>` |
| `pages/loginwindow/Map.java` | `Map` | `PageMap` |
| `pages/loginwindow/Asserts.java` | `Asserts` | `PageAsserts<Map>` |

All three from `solutions.bellatrix.desktop.pages`. The package name is the lowercase window name; the `package`
line must match the folder; one public top-level class per file, file name = class name.

### 2. Elements Are Methods, Not Fields

```java
// CORRECT - a method returning a fresh (lazy) component on each call
public Button loginButton() {
    return create().byAutomationId(Button.class, "btnLogin");
}
```

```text
WRONG - a field is created once, when the Map is constructed
public Button loginButton = create().byAutomationId(Button.class, "btnLogin");
```

Callers write `map().loginButton().click()`.

### 3. Use `DesktopPage`, Not `WebPage`

```java
// CORRECT
public class LoginWindow extends DesktopPage<Map, Asserts> { }
```

```text
WRONG - WebPage is for web (and needs getUrl()/open(), which desktop pages do not have)
public class LoginWindow extends WebPage<Map, Asserts> { }
```

`DesktopPage` has no `getUrl()`, `open()`, `waitForPageLoad()` or `app()` — it exposes `create()` and
`appService()`. `Map` and `Asserts` are instantiated reflectively from the generic parameters: they need a public
no-argument constructor (the implicit one) and must be the exact types named in `DesktopPage<Map, Asserts>`.

### 4. Never `import java.util.Map;` in a Page Package

A single-type `import java.util.Map;` shadows the page's own `Map` class, so `DesktopPage<Map, Asserts>` /
`PageAsserts<Map>` stop compiling (`type argument java.util.Map is not within bounds`). Import `java.util.List`
explicitly when you need it, and write a JDK map fully qualified (`java.util.Map<String, String>`).

### 5. Use validate* Methods in Asserts (NOT JUnit Assertions)

See `bellatrix-desktop-validations-and-assertions` for details (desktop validators read once — wait first).

---

## Project Structure (3-Class Pattern)

```
src/test/java/demo/desktoptests/
  pages/
    loginwindow/
      LoginWindow.java   # Action methods (login, fill form, etc.)
      Map.java           # Component methods (element locators)
      Asserts.java       # Assertion methods using validate*
    settingswindow/
      SettingsWindow.java
      Map.java
      Asserts.java
  models/
    LoginCredentials.java
    UserProfile.java
  tests/
    login/LoginTests.java
    settings/SettingsTests.java
```

---

## Complete 3-Class Example: LoginWindow

### Map.java

```java
package demo.desktoptests.pages.loginwindow;

import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.CheckBox;
import solutions.bellatrix.desktop.components.Label;
import solutions.bellatrix.desktop.components.PasswordField;
import solutions.bellatrix.desktop.components.TextField;
import solutions.bellatrix.desktop.pages.PageMap;

public class Map extends PageMap {
    // ===== FORM FIELDS =====
    public TextField usernameField() {
        return create().byAutomationId(TextField.class, "txtUsername");
    }

    public PasswordField passwordField() {
        return create().byAutomationId(PasswordField.class, "txtPassword");
    }

    // ===== BUTTONS =====
    public Button loginButton() {
        return create().byAutomationId(Button.class, "btnLogin");
    }

    public Button cancelButton() {
        return create().byAutomationId(Button.class, "btnCancel");
    }

    // ===== LABELS =====
    public Label errorMessage() {
        return create().byAutomationId(Label.class, "lblError");
    }

    public Label welcomeLabel() {
        return create().byAutomationId(Label.class, "lblWelcome");
    }

    // ===== CHECKBOXES =====
    public CheckBox rememberMeCheckbox() {
        return create().byName(CheckBox.class, "Remember me");
    }
}
```

### LoginWindow.java (actions)

```java
package demo.desktoptests.pages.loginwindow;

import demo.desktoptests.models.LoginCredentials;
import demo.desktoptests.pages.mainwindow.MainWindow;
import solutions.bellatrix.desktop.pages.DesktopPage;
import solutions.bellatrix.desktop.services.App;

public class LoginWindow extends DesktopPage<Map, Asserts> {
    public void loginWith(String username, String password) {
        map().usernameField().setText(username);
        map().passwordField().setPassword(password);
        map().loginButton().click();
    }

    public void loginWith(LoginCredentials credentials) {
        loginWith(credentials.username(), credentials.password());
    }

    public void loginWithRememberMe(String username, String password) {
        map().usernameField().setText(username);
        map().passwordField().setPassword(password);
        map().rememberMeCheckbox().check();
        map().loginButton().click();
    }

    public MainWindow loginAndNavigateToMain(String username, String password) {
        loginWith(username, password);
        return new App().createPage(MainWindow.class);
    }

    public void cancelLogin() {
        map().cancelButton().click();
    }
}
```

### Asserts.java

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

Usage: `LoginWindow login = app().createPage(LoginWindow.class); login.loginWith("u", "p");
login.asserts().errorDisplayed("Invalid credentials");` — `page.map()` and `page.asserts()` are public.

---

## Design Patterns

### 1. Method-Based Element Definitions (Recommended)

Each element is a method returning a new component — lazy, located on each use. Prefer direct factory methods:

```java
public Button okButton() {
    return create().byAutomationId(Button.class, "btnOk");
}
```

### 2. Parameterized Methods for Dynamic Elements

When the locator depends on runtime data, use direct factories when only one attribute varies:

```java
public Button listItem(String itemText) {
    return create().byName(Button.class, itemText);
}

public Button tab(String tabName) {
    return create().byName(Button.class, tabName);
}
```

Use `create().by(T.class, new UiaXPathFindStrategy(...))` only when the dynamic locator genuinely requires a compound
or structural expression (never `byXPath` — broken in BELLATRIX-Java 1.0).

### 3. Navigation Methods Return Page Objects

```java
public SettingsWindow openSettings() {
    map().settingsButton().click();
    return new App().createPage(SettingsWindow.class);
}
```

(`new App()` is `solutions.bellatrix.desktop.services.App`; `createPage` returns the per-thread instance — the same
object the test gets from `app().createPage(...)`.)

### 4. Fluent Interface for Chaining

```java
public LoginWindow withUsername(String username) {
    map().usernameField().setText(username);
    return this;
}

public LoginWindow withPassword(String password) {
    map().passwordField().setPassword(password);
    return this;
}

// Usage: loginWindow.withUsername("user").withPassword("pass").map().loginButton().click();
```

### 5. Organizing Complex Windows with Regions

Use `// ===== REGION =====` comments to group locators within a single window's `Map`:

```java
public class Map extends PageMap {
    // ===== GENERAL SETTINGS =====
    public TextField appNameField() {
        return create().byAutomationId(TextField.class, "txtAppName");
    }

    public CheckBox autoStartCheckbox() {
        return create().byAutomationId(CheckBox.class, "chkAutoStart");
    }

    // ===== APPEARANCE =====
    public ComboBox themeDropdown() {
        return create().byAutomationId(ComboBox.class, "cmbTheme");
    }

    // ===== ACTIONS =====
    public Button saveButton() {
        return create().byAutomationId(Button.class, "btnSave");
    }

    public Button cancelButton() {
        return create().byAutomationId(Button.class, "btnCancel");
    }
}
```

### 6. Reusable Sections Across Windows (Composition)

When two or more windows share the same locator block AND the same workflow around it, extract a section class under
`pages/shared/` and expose it from each window's `Map`. Windows then delegate from their action methods instead of
duplicating the logic.

A section is NOT a `DesktopPage` — it's a plain class that uses the same factory a page does
(`ComponentCreateService`):

```java
package demo.desktoptests.pages.shared;

import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.services.ComponentCreateService;

public class NumberPadSection {
    private ComponentCreateService create() {
        return new ComponentCreateService();
    }

    public Button digitButton(int digit) {
        return create().byAutomationId(Button.class, "num" + digit + "Button");
    }

    public Button decimalButton() {
        return create().byAutomationId(Button.class, "decimalSeparatorButton");
    }

    public void enterNumber(String number) {
        for (char c : number.strip().toCharArray()) {
            if (c == '.') {
                decimalButton().click();
            } else {
                digitButton(c - '0').click();
            }
        }
    }
}
```

Expose it from each window's `Map` and delegate from the page's actions:

```java
// pages/calculatorwindow/Map.java
public class Map extends PageMap {
    public NumberPadSection numberPad() {
        return new NumberPadSection();
    }
    // ...window-specific locators...
}

// pages/calculatorwindow/CalculatorWindow.java
public class CalculatorWindow extends DesktopPage<Map, Asserts> {
    public void enterNumber(String number) {
        map().numberPad().enterNumber(number);
    }
}
```

(BELLATRIX also has `solutions.bellatrix.desktop.pages.AppSection<Map, Asserts>` + `app().createSection(X.class)` — a
section with its own package whose `Map extends PageMap` and `Asserts extends PageAsserts<Map>` (same package
`solutions.bellatrix.desktop.pages`); use it when the shared block also needs shared assertions.)

**Rules of thumb:**
- Extract only after the duplication is real (≥2 windows). Don't pre-extract for hypothetical reuse.
- Keep the public action names on each window unchanged — the section is an implementation detail so tests don't need to change.
- A section owns its own locators AND its own workflow methods; a pure locator group stays on the window as a region.
- If the shared AutomationIds differ per window, do NOT force a section — extract only what's genuinely identical.

---

## Model Classes

Place data models in the `models` package — prefer Java records for immutable form data:

```java
package demo.desktoptests.models;

public record LoginCredentials(String username, String password) {
}
```

Usage in a page object (records expose `username()`, `password()`):

```java
public void loginWith(LoginCredentials credentials) {
    loginWith(credentials.username(), credentials.password());
}
```

---

## Best Practices Summary

1. **One package per window** with 3 classes (`<Window>`, `Map`, `Asserts`)
2. **Map**: only component-returning methods — no logic
3. **Page class**: business workflow methods — no assertions
4. **Asserts**: only assertion methods using `validate*` (after waiting) — no actions
5. **Use `app().createPage(TPage.class)`** in tests and `new App().createPage(TPage.class)` in navigation methods
6. **No direct `app().create()`** in test methods — always go through page objects
7. **Descriptive method names** reflecting business operations, not UI mechanics
