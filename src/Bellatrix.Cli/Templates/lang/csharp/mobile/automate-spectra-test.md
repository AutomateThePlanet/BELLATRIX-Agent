<!-- lang:traceability -->
### SPECTRA TC-ID Attribute (MANDATORY)

Every generated test method **MUST** carry a `[ManualTestCase("TC-XXX")]` attribute referencing the
SPECTRA test case ID. It is a project-local, metadata-only attribute (`ManualTestCaseAttribute.cs` at the
project root — create it if missing, see `bellatrix-mobile-project-structure`). It is NOT NUnit's
`[TestCase]`, which is for data-driven parameterization.

```csharp
[Test]
[ManualTestCase("TC-101")]
[Category("Smoke")]
public void Login_WithValidCredentials_OpensHome()
{
    var loginPage = App.Create<LoginPage>();
    loginPage.AssertPageLoaded();

    var homePage = loginPage.LoginAndGoHome("demo.user", "Secret123!");

    homePage.AssertWelcomeShown("Welcome, demo.user");
}
```

### Priority → Category Mapping

| SPECTRA Priority | NUnit Category |
|-----------------|----------------|
| `high` | `[Category("Smoke")]` |
| `medium` | `[Category("Regression")]` |
| `low` | `[Category("Regression")]` |

(MSTest projects use `[TestCategory("…")]`.)

### Comments Linking to SPECTRA

Add a summary comment above the method:

```csharp
/// <summary>
/// Automates SPECTRA TC-101: Login with valid credentials opens the home screen
/// Source: docs/mobile-login.md
/// Criteria: AC-001, AC-002
/// </summary>
```

<!-- lang:one-per-method -->
```csharp
// CORRECT: each TC gets its own method
[Test]
[ManualTestCase("TC-101")]
[Category("Smoke")]
public void Login_WithValidCredentials_OpensHome()
{
    var homePage = App.Create<LoginPage>().LoginAndGoHome("demo.user", "Secret123!");
    homePage.AssertWelcomeShown("Welcome, demo.user");
}

[Test]
[ManualTestCase("TC-102")]
[Category("Regression")]
public void Login_WithWrongPassword_ShowsError()
{
    var loginPage = App.Create<LoginPage>();
    loginPage.LoginWith("demo.user", "wrong");
    loginPage.AssertErrorDisplayed("Invalid user name or password");
}

// WRONG: two TCs merged into one parameterized method
[Test]
[ManualTestCase("TC-101")]
[ManualTestCase("TC-102")]
[TestCase("Secret123!")]
[TestCase("wrong")]
public void Login_WithPassword(string password) { /* ... */ }
```

<!-- lang:automated-by -->
Example value: `MyProject.MobileTests/Tests/Login/ShopLoginTests.cs`.

<!-- lang:build -->
```bash
dotnet build MyProject.MobileTests/MyProject.MobileTests.csproj
```

The command the user can run afterwards (do not run it yourself):

```bash
dotnet test MyProject.MobileTests/MyProject.MobileTests.csproj --filter "FullyQualifiedName~Login_WithValidCredentials_OpensHome" --no-build --logger "console;verbosity=detailed"
```

<!-- lang:summary -->
```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Platform | Title | Test Method | File |
|-------|----------|-------|-------------|------|
| TC-101 | Android | Login with valid credentials opens the home screen | Login_WithValidCredentials_OpensHome | Tests/Login/ShopLoginTests.cs |

### Files Created/Modified
[CREATED] Pages/LoginPage/LoginPage.Map.cs
[CREATED] Pages/LoginPage/LoginPage.Actions.cs
[CREATED] Pages/LoginPage/LoginPage.Assertions.cs
[CREATED] Tests/Login/ShopLoginTests.cs
[EDITED]  tests/login/TC-101.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
dotnet build succeeded
```

<!-- lang:scan-pattern -->
The `[ManualTestCase("TC-XXX")]` attribute matches the configured pattern:

```json
{
  "coverage": {
    "scan_patterns": [
      "[ManualTestCase(\"{id}\")]"
    ]
  }
}
```

As long as you use `[ManualTestCase("TC-101")]`, SPECTRA detects the link during
`spectra ai analyze --coverage` — even without editing `automated_by`. The Phase 3 edit provides the
explicit link; the scan pattern provides automatic discovery.

> **Important:** do NOT use NUnit's `[TestCase("TC-XXX")]` for traceability — that attribute is for
> data-driven parameterization. Use the project's `[ManualTestCase("TC-XXX")]` (string-based, defined
> in `ManualTestCaseAttribute.cs`), and do not import `Bellatrix.Data.Attributes`, whose `int`-based
> `ManualTestCaseAttribute` would clash.
<!-- lang:test-naming -->
Convert the SPECTRA title to `Action_Condition_ExpectedResult`:
- "Login with valid credentials opens the home screen" → `Login_WithValidCredentials_OpensHome`
- "Checkout with expired card shows an error" → `Checkout_WithExpiredCard_ShowsError`

