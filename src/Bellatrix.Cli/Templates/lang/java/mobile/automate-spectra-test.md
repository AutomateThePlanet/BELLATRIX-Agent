<!-- lang:test-naming -->
Convert the SPECTRA title to `action_Condition_ExpectedResult` (lower-camel first segment):
- "Login with valid credentials opens the home screen" → `login_WithValidCredentials_OpensHome`
- "Checkout with expired card shows an error" → `checkout_WithExpiredCard_ShowsError`

<!-- lang:traceability -->
### SPECTRA TC-ID Annotation (MANDATORY)

Every generated test method **MUST** carry a `@ManualTestCase("TC-XXX")` annotation referencing the SPECTRA test
case ID. BELLATRIX-Java has no built-in string variant: it is a project-local, metadata-only annotation
(`{rootPackage}.common.ManualTestCase` — create it if missing, see `bellatrix-mobile-project-structure`):

```java
package com.myproject.mobiletests.common;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ManualTestCase {
    String value();
}
```

It is NOT JUnit's `@ParameterizedTest` / `@CsvSource`, which are for data-driven parameterization.

```java
@Test
@ManualTestCase("TC-101")
@Tag("Smoke")
public void login_WithValidCredentials_OpensHome() {
    LoginPage loginPage = app().createPage(LoginPage.class);
    loginPage.asserts().pageLoaded();

    HomePage homePage = loginPage.loginAndGoHome("demo.user", "Secret123!");

    homePage.asserts().welcomeShown("Welcome, demo.user");
}
```

### Priority → Tag Mapping

| SPECTRA Priority | JUnit 5 tag |
|-----------------|-------------|
| `high` | `@Tag("Smoke")` |
| `medium` | `@Tag("Regression")` |
| `low` | `@Tag("Regression")` |

(TestNG projects use `@Test(groups = "Smoke")`.)

### Comments Linking to SPECTRA

Add a Javadoc comment above the method:

```java
/**
 * Automates SPECTRA TC-101: Login with valid credentials opens the home screen
 * Source: docs/mobile-login.md
 * Criteria: AC-001, AC-002
 */
```

<!-- lang:one-per-method -->
```java
// CORRECT: each TC gets its own method
@Test
@ManualTestCase("TC-101")
@Tag("Smoke")
public void login_WithValidCredentials_OpensHome() {
    HomePage homePage = app().createPage(LoginPage.class).loginAndGoHome("demo.user", "Secret123!");
    homePage.asserts().welcomeShown("Welcome, demo.user");
}

@Test
@ManualTestCase("TC-102")
@Tag("Regression")
public void login_WithWrongPassword_ShowsError() {
    LoginPage loginPage = app().createPage(LoginPage.class);
    loginPage.loginWith("demo.user", "wrong");
    loginPage.asserts().errorDisplayed("Invalid user name or password");
}
```

```text
WRONG: two TCs merged into one parameterized method (and @ManualTestCase is not repeatable — it won't compile)
@ParameterizedTest
@ManualTestCase("TC-101")
@ManualTestCase("TC-102")
@CsvSource({"Secret123!", "wrong"})
public void login_WithPassword(String password) { /* ... */ }
```

<!-- lang:automated-by -->
Example value: `src/test/java/com/myproject/mobiletests/android/tests/login/ShopLoginTests.java`.

<!-- lang:build -->
```bash
mvn -q test-compile
```

The command the user can run afterwards (do not run it yourself):

```bash
mvn test -Dtest=ShopLoginTests#login_WithValidCredentials_OpensHome
```

<!-- lang:summary -->
```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Platform | Title | Test Method | File |
|-------|----------|-------|-------------|------|
| TC-101 | Android | Login with valid credentials opens the home screen | login_WithValidCredentials_OpensHome | android/tests/login/ShopLoginTests.java |

### Files Created/Modified
[CREATED] src/test/java/com/myproject/mobiletests/android/pages/login/Map.java
[CREATED] src/test/java/com/myproject/mobiletests/android/pages/login/LoginPage.java
[CREATED] src/test/java/com/myproject/mobiletests/android/pages/login/Asserts.java
[CREATED] src/test/java/com/myproject/mobiletests/android/tests/login/ShopLoginTests.java
[EDITED]  tests/login/TC-101.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
mvn -q test-compile succeeded
```

<!-- lang:scan-pattern -->
The `@ManualTestCase("TC-XXX")` annotation matches the configured pattern:

```json
{
  "coverage": {
    "scan_patterns": [
      "@ManualTestCase(\"{id}\")"
    ]
  }
}
```

As long as you use `@ManualTestCase("TC-101")`, SPECTRA detects the link during `spectra ai analyze --coverage` —
even without editing `automated_by`. The Phase 3 edit provides the explicit link; the scan pattern provides
automatic discovery.

> **Important:** do NOT use JUnit's `@ParameterizedTest` / `@CsvSource` (or TestNG data providers) for
> traceability — they are for data-driven parameterization. Use the project's `@ManualTestCase("TC-XXX")`
> (string-based, defined in `{rootPackage}.common.ManualTestCase`), exactly one per test method.
