---
description: Reference skill for BELLATRIX-Java mobile page object structure (AndroidPage/IOSPage), the 3-class package pattern (page, Map, Asserts), sections, models, and design patterns.
---

# BELLATRIX Mobile Page Objects and Patterns Reference (Java)

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. One Package per Screen, Three Classes

A Java page object is **three separate classes in their own package** (there are no partial classes in Java):

| File | Class | Extends (Android) | Extends (iOS) |
|------|-------|-------------------|---------------|
| `android/pages/login/LoginPage.java` | `LoginPage` | `AndroidPage<Map, Asserts>` | `IOSPage<Map, Asserts>` |
| `android/pages/login/Map.java` | `Map` | `PageMap` | `PageMap` |
| `android/pages/login/Asserts.java` | `Asserts` | `PageAsserts<Map>` | `PageAsserts<Map>` |

Android bases come from `solutions.bellatrix.android.pages`, iOS bases from `solutions.bellatrix.ios.pages`. The
package name is the lowercase screen name; the `package` line must match the folder; one public top-level class per
file, file name = class name.

### 2. Elements Are Methods, Not Fields

```java
// CORRECT - a method returning a fresh (lazy) component on each call
public Button loginButton() {
    return create().byDescription(Button.class, "Log in");
}
```

```text
WRONG - a field is created once, when the Map is constructed
public Button loginButton = create().byDescription(Button.class, "Log in");
```

Callers write `map().loginButton().click()`.

### 3. Use the Platform Page Base Class

```java
// Android
public class LoginPage extends AndroidPage<Map, Asserts> { }

// iOS
public class LoginPage extends IOSPage<Map, Asserts> { }
```

```text
WRONG - WebPage / DesktopPage are for other domains
public class LoginPage extends WebPage<Map, Asserts> { }
```

What the bases give you (verified in BELLATRIX-Java 1.0 source):

| Base | Members | Not available |
|------|---------|---------------|
| `AndroidPage<Map, Asserts>` | `map()`, `asserts()`, `app()` (Android `App`) | `create()`, `appService()`, `goTo()`, `open()`, package/activity overrides |
| `IOSPage<Map, Asserts>` | `map()`, `asserts()`, `create()`, `appService()` | `app()`, `goTo()` |
| `PageMap` (both) | `create()` → the platform's `ComponentCreateService` | — |
| `PageAsserts<Map>` (both) | `protected map()` | — |

There is no `GoTo()` / `PackageName` / `ActivityName` in Java. On Android, add your own `open()` that calls
`app().appService().startActivity(Constants.APP_PACKAGE, ".LoginActivity")`; on iOS reach the screen through the UI.
`Map` and `Asserts` are instantiated reflectively from the generic parameters: they need a public no-argument
constructor (the implicit one) and must be the exact types named in `AndroidPage<Map, Asserts>`.

### 4. One Platform per File

Import from `solutions.bellatrix.android.*` **or** `solutions.bellatrix.ios.*`, never both — the component classes
(`Button`, `TextField`, …) and page bases (`PageMap`, `PageAsserts`, `AppSection`) have the same simple names on both
platforms. Android and iOS versions of a screen live in separate packages (`…android.pages.login`,
`…ios.pages.login`).

### 5. Never Import `java.util.Map` in a Page Package

A single-type `import java.util.Map;` hides the page's own `Map` class (`type argument java.util.Map is not within
bounds`). Import `java.util.List` explicitly when you need it (rather than `java.util.*`).

### 6. Use validate* Methods in Asserts (NOT JUnit Assertions)

See `bellatrix-mobile-validations-and-assertions` for details (validators read once — wait first; there is no
`validateIsVisible()`).

---

## Project Structure (3-Class Pattern)

```
src/test/java/com/myproject/mobiletests/
  common/
    Constants.java
    ManualTestCase.java
  models/
    LoginCredentials.java
  android/
    pages/
      login/
        LoginPage.java     # Action methods (extends AndroidPage<Map, Asserts>)
        Map.java           # Component methods (element locators)
        Asserts.java       # Assertion methods using validate*
      home/
        HomePage.java
        Map.java
        Asserts.java
      shared/
        BottomNavigationSection.java
    components/
    tests/
      login/ShopLoginTests.java
  ios/
    pages/login/ …         # same layout, IOSPage + iOS components
    tests/login/ShopLoginTests.java
```

A single-platform project keeps only its own platform folder.

---

## Complete 3-Class Example: LoginPage (Android)

### Map.java

```java
package com.myproject.mobiletests.android.pages.login;

import solutions.bellatrix.android.components.Button;
import solutions.bellatrix.android.components.CheckBox;
import solutions.bellatrix.android.components.Label;
import solutions.bellatrix.android.components.PasswordField;
import solutions.bellatrix.android.components.TextField;
import solutions.bellatrix.android.pages.PageMap;

public class Map extends PageMap {
    // ===== FORM FIELDS =====
    public TextField userNameField() {
        return create().byId(TextField.class, "com.example.shop:id/username");
    }

    public PasswordField passwordField() {
        return create().byId(PasswordField.class, "com.example.shop:id/password");
    }

    // ===== BUTTONS =====
    public Button loginButton() {
        return create().byDescription(Button.class, "Log in");
    }

    // ===== OPTIONS =====
    public CheckBox rememberMeCheckBox() {
        return create().byText(CheckBox.class, "Remember me");
    }

    // ===== MESSAGES =====
    public Label errorMessage() {
        return create().byId(Label.class, "com.example.shop:id/error");
    }
}
```

### LoginPage.java (actions)

```java
package com.myproject.mobiletests.android.pages.login;

import com.myproject.mobiletests.android.pages.home.HomePage;
import com.myproject.mobiletests.common.Constants;
import com.myproject.mobiletests.models.LoginCredentials;
import solutions.bellatrix.android.pages.AndroidPage;

public class LoginPage extends AndroidPage<Map, Asserts> {
    public void open() {
        app().appService().startActivity(Constants.APP_PACKAGE, ".LoginActivity");
    }

    public void loginWith(String userName, String password) {
        map().userNameField().setText(userName);
        map().passwordField().setPassword(password);
        map().loginButton().click();
    }

    public void loginWith(LoginCredentials credentials) {
        loginWith(credentials.userName(), credentials.password());
    }

    public void loginWithRememberMe(String userName, String password) {
        map().userNameField().setText(userName);
        map().passwordField().setPassword(password);
        map().rememberMeCheckBox().check();
        map().loginButton().click();
    }

    public HomePage loginAndGoHome(String userName, String password) {
        loginWith(userName, password);
        return app().createPage(HomePage.class);
    }
}
```

### Asserts.java

```java
package com.myproject.mobiletests.android.pages.login;

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

    public void rememberMeChecked() {
        map().rememberMeCheckBox().validateIsChecked();
    }
}
```

Usage: `LoginPage login = app().createPage(LoginPage.class); login.loginWith("u", "p");
login.asserts().errorDisplayed("Invalid user name or password");` — `page.map()` and `page.asserts()` are public.

---

## The Same Screen on iOS

```java
// ios/pages/login/Map.java
package com.myproject.mobiletests.ios.pages.login;

import solutions.bellatrix.ios.components.Button;
import solutions.bellatrix.ios.components.Label;
import solutions.bellatrix.ios.components.PasswordField;
import solutions.bellatrix.ios.components.TextField;
import solutions.bellatrix.ios.pages.PageMap;

public class Map extends PageMap {
    public TextField userNameField() {
        return create().byAccessibilityIdContaining(TextField.class, "usernameField");
    }

    public PasswordField passwordField() {
        return create().byAccessibilityIdContaining(PasswordField.class, "passwordField");
    }

    public Button loginButton() {
        return create().byName(Button.class, "Log in");
    }

    // XCUIElementTypeSwitch → Button + the "value" attribute (iOS CheckBox / ToggleButton state does not work)
    public Button rememberMeSwitch() {
        return create().byIOSNsPredicate(Button.class, "type == \"XCUIElementTypeSwitch\" AND name == \"Remember me\"");
    }

    public Label errorMessage() {
        return create().byIOSNsPredicate(Label.class, "type == \"XCUIElementTypeStaticText\" AND name == \"loginError\"");
    }
}
```

```java
// ios/pages/login/LoginPage.java
package com.myproject.mobiletests.ios.pages.login;

import com.myproject.mobiletests.ios.pages.home.HomePage;
import solutions.bellatrix.ios.pages.IOSPage;
import solutions.bellatrix.ios.services.App;

public class LoginPage extends IOSPage<Map, Asserts> {
    public void loginWith(String userName, String password) {
        map().userNameField().setText(userName);
        map().passwordField().setPassword(password);
        map().loginButton().click();
    }

    public void setRememberMe(boolean on) {
        boolean isOn = "1".equals(map().rememberMeSwitch().getAttribute("value"));
        if (isOn != on) {
            map().rememberMeSwitch().click();
        }
    }

    public HomePage loginAndGoHome(String userName, String password) {
        loginWith(userName, password);
        return new App().createPage(HomePage.class);   // IOSPage has no app()
    }
}
```

The iOS `Asserts` class is written like the Android one, importing `solutions.bellatrix.ios.pages.PageAsserts`. Keep
the public method names the same on both platforms so test bodies read the same.

---

## Design Patterns

### 1. Method-Based Element Definitions (Recommended)

Each element is a method returning a new component — lazy, located on each use, never stale:

```java
public Button saveButton() {
    return create().byDescription(Button.class, "Save");
}
```

### 2. Parameterized Methods for Dynamic Elements

When one attribute varies at runtime, keep the direct factory:

```java
// Android
public Button productRow(String productName) {
    return create().byText(Button.class, productName);
}

// iOS
public Button productCell(String productName) {
    return create().byIOSNsPredicate(Button.class, "type == \"XCUIElementTypeCell\" AND label CONTAINS \"" + productName + "\"");
}
```

### 3. Navigation Methods Return Page Objects

```java
// Android page
public HomePage loginAndGoHome(String userName, String password) {
    loginWith(userName, password);
    return app().createPage(HomePage.class);
}

// iOS page
public HomePage loginAndGoHome(String userName, String password) {
    loginWith(userName, password);
    return new App().createPage(HomePage.class);
}
```

`createPage` returns the per-thread instance — the same object the test gets from `app().createPage(...)`.

### 4. Fluent Interface for Chaining

```java
public LoginPage withUserName(String userName) {
    map().userNameField().setText(userName);
    return this;
}

public LoginPage withPassword(String password) {
    map().passwordField().setPassword(password);
    return this;
}

// Usage: loginPage.withUserName("demo.user").withPassword("Secret123!").map().loginButton().click();
```

### 5. Scroll Inside the Page, Not the Test

```java
// Android — byText scrolls the scrollable container until the row is in view
public void openProduct(String productName) {
    map().productRow(productName).click();
}
```

iOS has no self-scrolling factory: swipe in the page method first (`new App().touch().swipe(...)`, see
`bellatrix-mobile-app-services`), then tap.

### 6. Organizing Complex Screens with Regions

Group locators in `Map` with `// ===== REGION =====` comments (form fields, buttons, lists, messages, navigation).

### 7. Reusable Sections Across Screens (Composition)

When two or more screens share the same block AND workflow (bottom navigation, toolbar, number pad), extract a
section class under `pages/shared/`. A section is NOT a page — it's a plain class that uses the same factory a
`Map` does:

```java
package com.myproject.mobiletests.android.pages.shared;

import solutions.bellatrix.android.components.Button;
import solutions.bellatrix.android.services.ComponentCreateService;

public class BottomNavigationSection {
    private ComponentCreateService create() {
        return new ComponentCreateService();
    }

    public Button homeTab() {
        return create().byDescription(Button.class, "Home");
    }

    public Button settingsTab() {
        return create().byDescription(Button.class, "Settings");
    }

    public void openHome() {
        homeTab().click();
    }

    public void openSettings() {
        settingsTab().click();
    }
}
```

Expose it from each screen's `Map` and delegate from the page's actions:

```java
// android/pages/home/Map.java
public class Map extends PageMap {
    public BottomNavigationSection navigation() {
        return new BottomNavigationSection();
    }
    // ...screen-specific locators...
}

// android/pages/home/HomePage.java
public class HomePage extends AndroidPage<Map, Asserts> {
    public void goToSettings() {
        map().navigation().openSettings();
    }
}
```

On iOS the section imports `solutions.bellatrix.ios.services.ComponentCreateService`. (BELLATRIX also has
`AppSection<Map, Asserts>` + `app().createSection(X.class)` — a section with its own Map/Asserts package; use it when
the shared block also needs shared assertions.)

**Rules of thumb:**
- Extract only after the duplication is real (≥2 screens).
- Keep public action names on each screen unchanged — the section is an implementation detail.
- A section owns its locators AND its workflow methods; a pure locator group stays on the screen as a region.

---

## Model Classes

Plain data in the shared `models` package — prefer Java records, no BELLATRIX types:

```java
package com.myproject.mobiletests.models;

public record LoginCredentials(String userName, String password) {
}
```

Records expose `userName()` / `password()`.

---

## Best Practices Summary

1. **One package per screen** with 3 classes (`<Screen>`, `Map`, `Asserts`), one platform per package
2. **Map**: only component-returning methods — no logic
3. **Page class**: business workflow methods — no assertions
4. **Asserts**: only assertion methods using `validate*` (after waiting) — no actions
5. **`app().createPage(TPage.class)`** in tests and Android pages; `new App().createPage(TPage.class)` in iOS pages — never `new TPage()`
6. **No `app().create()` in test methods** — tests call page object methods only
7. **Business-level method names** (`loginWith`, `addToCart`), not UI mechanics (`tapButton3`)
