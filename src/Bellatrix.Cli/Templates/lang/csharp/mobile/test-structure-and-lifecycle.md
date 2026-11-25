# BELLATRIX Mobile Test Structure and Lifecycle Reference

## Test Base Classes

| Platform | NUnit base | MSTest base | App attribute |
|----------|-----------|-------------|---------------|
| Android | `Bellatrix.Mobile.NUnit.AndroidTest` | `Bellatrix.Mobile.MSTest.AndroidTest` | `[Android(...)]` |
| iOS | `Bellatrix.Mobile.NUnit.IOSTest` | `Bellatrix.Mobile.MSTest.IOSTest` | `[IOS(...)]` |

The attribute classes (`AndroidAttribute`, `IOSAttribute`) and the `Lifecycle` enum live in
`Bellatrix.Mobile`.

### Android (NUnit)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.NUnit;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[Android(Constants.AppPath,
    Constants.AppId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Constants.StartActivity,
    Lifecycle.RestartEveryTime)]
public class LoginTests : AndroidTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        // Test code here
    }
}
```

### iOS (NUnit)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.IOS;
using Bellatrix.Mobile.NUnit;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[IOS(Constants.AppPath,
    Constants.BundleId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Lifecycle.RestartEveryTime)]
public class LoginTests : IOSTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        // Test code here
    }
}
```

### MSTest

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace {project.rootNamespace}.Tests;

[TestClass]
[Android(Constants.AppPath, Constants.AppId, Constants.PlatformVersion, Constants.DeviceName,
    Constants.StartActivity, Lifecycle.RestartEveryTime)]
public class LoginTests : AndroidTest
{
    [TestMethod]
    [TestCategory("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        // Test code here
    }
}
```

> If the project's root namespace starts with `Bellatrix.Mobile.` (as in the BELLATRIX samples), `NUnit`
> resolves to `Bellatrix.Mobile.NUnit` — write `: NUnit.AndroidTest`. In any other namespace use
> `using Bellatrix.Mobile.NUnit;` and `: AndroidTest`.

---

## The App Object

`App` is the entry point for all mobile interaction — `this.App` in test classes (typed `AndroidApp` /
`IOSApp`) and `App` in page objects (via `AndroidPage` / `IOSPage`).

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `App.Components` | Create and find components |
| Create | `App.Create<TPage>()` | Instantiate page objects |
| AppService | `App.AppService` | Background/close/launch/install app, contexts; `StartActivity` (Android) |
| Device | `App.Device` | Orientation, lock, notifications (Android), shake (iOS) |
| Keyboard | `App.Keyboard` | Hide keyboard; key codes (Android) |
| Files | `App.Files` | Pull files; push files (Android) |
| TouchActions | `App.TouchActions` | Tap, long press, swipe sequences |
| Assert | `App.Assert` | BELLATRIX assertions (`AreEqual`, `IsTrue`, …) |

---

## App Attributes and Lifecycle

```csharp
// Android: app path, app package, platform version, device name, launch activity, lifecycle
[Android(appPath, appId, platformVersion, deviceName, appActivity, Lifecycle)]

// iOS: app path, bundle id, platform version, device name, lifecycle
[IOS(appPath, bundleId, platformVersion, deviceName, Lifecycle)]
```

Attribute arguments must be compile-time constants — keep them in a `Constants` class:

```csharp
namespace {project.rootNamespace};

public static class Constants
{
    public const string AppPath = @"AssemblyFolder\Apps\MyApp.apk";   // "AssemblyFolder" = test output folder
    public const string AppId = "com.example.shop";
    public const string PlatformVersion = "14";
    public const string DeviceName = "Android Emulator";
    public const string StartActivity = ".MainActivity";
}
```

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RestartEveryTime` | Fresh app session before every test (cleanest, slowest) |
| `Lifecycle.ReuseIfStarted` | Reuse the running app (fastest, shared-state risk) |
| `Lifecycle.RestartOnFail` | Restart only after a failed test |

The attribute can be placed on the class (default for its tests) **and** on a single test method,
where it overrides the class-level setting for that test only:

```csharp
[TestFixture]
[Android(Constants.AppPath, Constants.AppId, Constants.PlatformVersion, Constants.DeviceName,
    Constants.StartActivity, Lifecycle.ReuseIfStarted)]
public class CartTests : AndroidTest
{
    [Test]
    public void AddItem_FromProductPage_IncrementsBadge() { /* reuses the running app */ }

    // RestartEveryTime: verifies the empty cart on first launch
    [Test]
    [Android(Constants.AppPath, Constants.AppId, Constants.PlatformVersion, Constants.DeviceName,
        Constants.StartActivity, Lifecycle.RestartEveryTime)]
    public void Cart_OnFirstLaunch_IsEmpty() { /* fresh app */ }
}
```

### Always Use the Attribute

Always put `[Android(...)]` / `[IOS(...)]` on the class. Without an attribute BELLATRIX always starts an
**Android** driver and overwrites the app/device capabilities from `mobileSettings.executionSettings.defaultAppPath`,
so a config-only setup silently breaks iOS and is fragile on Android. `mobileSettings.executionSettings` still
supplies the Appium `url` (see `bellatrix-mobile-app-services` → Configuration).

---

## Assembly Bootstrap (`TestsInitialize.cs`)

One per test project (NUnit `[SetUpFixture]` in the global namespace):

```csharp
using Bellatrix;
using Bellatrix.Mobile;
using NUnit.Framework;

[SetUpFixture]
public class TestsInitialize
{
    [OneTimeSetUp]
    public void AssemblyInitialize()
    {
        // No-op unless mobileSettings.executionSettings.shouldStartLocalService is "true".
        AndroidApp.StartAppiumLocalService();
    }

    [OneTimeTearDown]
    public void AssemblyCleanUp()
    {
        var app = ServicesCollection.Current.Resolve<AndroidApp>();
        app?.Dispose();
        app?.StopAppiumLocalService();
    }
}
```

For iOS replace `AndroidApp` with `IOSApp`. Keep `shouldStartLocalService` `"false"` — the user runs
Appium.

---

## Per-Test Hooks

Override the BELLATRIX hooks instead of adding `[SetUp]` / `[TearDown]` (the base class already owns
those):

| Hook | Runs |
|------|------|
| `TestsArrange()` | Once per class, before all tests (after the app starts) |
| `TestsAct()` | Once per class, after `TestsArrange()` |
| `TestInit()` | Before each test |
| `TestCleanup()` | After each test |
| `TestsCleanup()` | Once per class, after all tests (NUnit only — the MSTest base never calls it) |

```csharp
public override void TestInit()
{
    // Reset to a known screen when the class reuses the app (Android)
    App.Create<LoginPage>().GoTo();
}
```

---

## Test Naming Convention

Use `Action_Condition_ExpectedResult`:

```csharp
// Good
public void Login_WithValidCredentials_OpensHome()
public void AddToCart_FromProductPage_IncrementsBadge()
public void Rotate_ToLandscape_KeepsFormValues()

// Bad
public void TestLogin()
public void Test1()
public void VerifyCartWorks()
```

---

## Test Categories

```csharp
[Category("Smoke")]       // Critical path, every build
[Category("Regression")]  // Full suite
[Category("CI")]          // Fast tests for CI pipelines
```

---

## Complete Test Class Template (Android)

```csharp
using Bellatrix.Mobile;
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.NUnit;
using {project.rootNamespace}.Pages;
using NUnit.Framework;

namespace {project.rootNamespace}.Tests;

[TestFixture]
[Android(Constants.AppPath,
    Constants.AppId,
    Constants.PlatformVersion,
    Constants.DeviceName,
    Constants.StartActivity,
    Lifecycle.ReuseIfStarted)]
public class LoginTests : AndroidTest
{
    public override void TestInit()
    {
        App.Create<LoginPage>().GoTo();
    }

    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensHome()
    {
        var loginPage = App.Create<LoginPage>();
        loginPage.AssertPageLoaded();

        var homePage = loginPage.LoginAndGoHome("demo.user", "Secret123!");

        homePage.AssertWelcomeShown("Welcome, demo.user");
    }

    [Test]
    [Category("Regression")]
    public void Login_WithWrongPassword_ShowsError()
    {
        var loginPage = App.Create<LoginPage>();

        loginPage.LoginWith("demo.user", "wrong");

        loginPage.AssertErrorDisplayed("Invalid user name or password");
    }
}
```

---

## Required Using Directives

```csharp
// App types, [Android]/[IOS] attributes, Lifecycle, ScrollDirection
using Bellatrix.Mobile;

// Components, factories, waits, Validate* — choose ONE per file
using Bellatrix.Mobile.Android;
using Bellatrix.Mobile.IOS;

// Page object bases (page files only)
using Bellatrix.Mobile.PageObjects;

// Container components for nested lookups (only when used)
using Bellatrix.Mobile.Controls.Android;   // AndroidComponent
using Bellatrix.Mobile.Controls.IOS;       // IOSComponent

// Test base — choose one
using Bellatrix.Mobile.NUnit;              // AndroidTest / IOSTest for NUnit
using Bellatrix.Mobile.MSTest;             // AndroidTest / IOSTest for MSTest

// Test attributes — choose one
using NUnit.Framework;                                  // [TestFixture], [Test], [Category]
using Microsoft.VisualStudio.TestTools.UnitTesting;     // [TestClass], [TestMethod], [TestCategory]

// Your code
using {project.rootNamespace}.Pages;
```

Do not import `Bellatrix.Data.Attributes` in test files — it contains an `int`-based
`ManualTestCaseAttribute` that clashes with the project's SPECTRA `[ManualTestCase("TC-XXX")]`.
