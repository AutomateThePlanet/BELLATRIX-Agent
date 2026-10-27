## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `new LoginPage()` | `App.Create<LoginPage>()` |
| `App.Navigation.Navigate(url)` / `App.Browser…` | Mobile apps have no URL navigation — `App.Create<TPage>()`, `page.GoTo()` (Android), or tap through the UI |
| `App.Components.CreateByXpath<T>()` | `App.Components.CreateByXPath<T>()` (capital P) |
| `App.Components.CreateByAccessibilityId<T>()` | Android `CreateByDescription<T>()`; iOS `CreateById<T>()` |
| `App.Wait.Wait(element, …)` | Fluent waits: `element.ToBeVisible()`, `ToBeClickable()`, … |
| `App.DeviceService` / `App.KeyboardService` (iOS) | `App.Device` / `App.Keyboard` (the old names are obsolete) |
| `App.TouchActions.Swipe(...)` with no `Perform()` | `App.TouchActions.Swipe(...).Perform();` |

---

## App Types

| Test base | `App` type | Page base | `App` inside pages |
|-----------|-----------|-----------|--------------------|
| `AndroidTest` | `AndroidApp` | `AndroidPage` | `AndroidApp` |
| `IOSTest` | `IOSApp` | `IOSPage` | `IOSApp` |

`AndroidApp` / `IOSApp` live in `Bellatrix.Mobile`.

| Member | Android (`AndroidApp`) | iOS (`IOSApp`) |
|--------|------------------------|----------------|
| `Components` | `ComponentCreateService` | `ComponentCreateService` |
| `Create<TPage>()` | yes | yes |
| `AppService` | `AndroidAppService` | `IOSAppService` |
| `Device` | `AndroidDeviceService` | `IOSDeviceService` |
| `Keyboard` | `AndroidKeyboardService` | `KeyboardService<IOSDriver, AppiumElement>` |
| `Files` | `AndroidFileSystemService` | `FileSystemService<IOSDriver, AppiumElement>` |
| `TouchActions` | `TouchActionsService<AndroidDriver, AppiumElement>` | `TouchActionsService<IOSDriver, AppiumElement>` |
| `Assert` | `IAssert` (`AreEqual`, `AreNotEqual`, `IsTrue`, `IsFalse`, …) | same |

---

## ComponentCreateService (`App.Components`)

### Android (`using Bellatrix.Mobile.Android;`)

```csharp
App.Components.CreateByDescription<Button>("Log in")                         // content-desc (accessibility id)
App.Components.CreateById<TextField>("com.example.shop:id/username")         // full resource-id
App.Components.CreateByIdContaining<Button>("login")
App.Components.CreateByText<Label>("Order placed")
App.Components.CreateByTextContaining<Label>("placed")
App.Components.CreateByDescriptionContaining<Button>("Log")
App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log in\")")
App.Components.CreateByClass<SeekBar>("android.widget.SeekBar")
App.Components.CreateByXPath<Button>("//android.widget.Button[@text='Log in']")
```

### iOS (`using Bellatrix.Mobile.IOS;`)

```csharp
App.Components.CreateById<Button>("loginButton")                              // accessibility id / name
App.Components.CreateByName<Button>("Log in")
App.Components.CreateByIOSNsPredicate<Label>("label == \"Order placed\"")
App.Components.CreateByValueContaining<Label>("Total")
App.Components.CreateByClass<TextField>("XCUIElementTypeTextField")
App.Components.CreateByXPath<Button>("//XCUIElementTypeButton[@name='loginButton']")
```

### Collections

```csharp
App.Components.CreateAllById<Label>("com.example.shop:id/row_title")          // Android
App.Components.CreateAllByTextContaining<Label>("Pixel")                      // Android
App.Components.CreateAllByAccessibilityId<Button>("row")                      // iOS
App.Components.CreateAllByIOSNsPredicate<Button>("type == \"XCUIElementTypeCell\"") // iOS
```

---

## AppService (`App.AppService`)

### Both platforms

| Member | Returns | Description |
|--------|---------|-------------|
| `BackgroundApp(int seconds)` | `void` | Send the app to the background for N seconds, then return |
| `CloseApp()` | `void` | Terminate the app under test (the configured app id) |
| `LaunchApp()` | `void` | Activate the app under test again |
| `InstallApp(string appPath)` | `void` | Install an app package |
| `RemoveApp(string appId)` | `void` | Uninstall by package / bundle id |
| `IsAppInstalled(string bundleId)` | `bool` | Installed check by package / bundle id |
| `Context` | `string` (get/set) | `NATIVE_APP` or a `WEBVIEW_…` context |
| `PageSource` | `string` | Raw XML page source (debugging only — never assert on it) |
| `GetCurrentViewSnapshot()` | `string` | JSON summary of the current view |

### Android only (`AndroidAppService`)

| Member | Returns | Description |
|--------|---------|-------------|
| `StartActivity(string appPackage, string appActivity, string appWaitPackage = "", string appWaitActivity = "", bool stopApp = true)` | `void` | Jump straight to an activity |
| `CurrentActivity` | `string` | Name of the foreground activity |

### Example Usage

```csharp
// Android — open a screen directly
App.AppService.StartActivity("com.example.shop", ".SettingsActivity");
Assert.That(App.AppService.CurrentActivity, Does.Contain("Settings"));

// Both — background and restore, then verify state survived
App.AppService.BackgroundApp(3);
cartPage.AssertItemCount(2);

// Both — switch to a hybrid WebView and back
App.AppService.Context = "WEBVIEW_com.example.shop";
App.AppService.Context = "NATIVE_APP";
```

---

## Device Service (`App.Device`)

| Member | Platform | Description |
|--------|----------|-------------|
| `Orientation` | both | Current `OpenQA.Selenium.ScreenOrientation` |
| `Rotate(ScreenOrientation)` | both | `ScreenOrientation.Portrait` / `ScreenOrientation.Landscape` |
| `DeviceTime` | both | Device clock (`DateTime`) |
| `Lock()` / `IsLocked` | Android | Lock the device / read the lock state (do not rely on `Unlock()` — it currently calls lock) |
| `OpenNotifications()` | Android | Pull down the notification shade |
| `TurnOnLocationService()` | Android | Toggle location services |
| `ConnectionType` | Android | Get/set network mode (`OpenQA.Selenium.Appium.Android.ConnectionType`) |
| `Lock(int seconds)` | iOS | Lock the device for N seconds |
| `ShakeDevice()` | iOS | Shake gesture |

```csharp
using OpenQA.Selenium;   // ScreenOrientation

App.Device.Rotate(ScreenOrientation.Landscape);
Assert.That(App.Device.Orientation, Is.EqualTo(ScreenOrientation.Landscape));
```

---

## Keyboard Service (`App.Keyboard`)

```csharp
App.Keyboard.HideKeyboard();                                   // both platforms

// Android only — key codes from OpenQA.Selenium.Appium.Android.Enums
App.Keyboard.PressKeyCode(AndroidKeyCode.Back);
App.Keyboard.LongPressKeyCode(AndroidKeyCode.Home);
```

On Android `SetText` / `SetPassword` / `SetNumber` already hide the keyboard after typing; on iOS call `App.Keyboard.HideKeyboard()`.

---

## Files Service (`App.Files`)

```csharp
byte[] data = App.Files.PullFile("/sdcard/Download/report.txt");    // both platforms
byte[] folder = App.Files.PullFolder("/sdcard/Download");            // both platforms
App.Files.PushFile("/sdcard/Download/input.txt", "hello");          // Android only
```

---

## Touch Actions (`App.TouchActions`)

Fluent builder — every chain MUST end with `.Perform()`.

| Method | Description |
|--------|-------------|
| `Tap(component, count = 1)` / `Tap(x, y, count = 1)` | Tap an element or a point |
| `Press(component, waitTimeSeconds = 0)` / `Press(x, y, …)` | Touch down, hold `waitTimeSeconds` (optional), then lift — a complete press |
| `LongPress(component, waitTimeSeconds)` / `LongPress(x, y, …)` | Long press |
| `MoveTo(component)` / `MoveTo(x, y)` | Move the pointer |
| `Release()` | No-op (kept for compatibility) — every Tap/Press/LongPress/Swipe already lifts the pointer |
| `Wait(long milliseconds)` | Pause inside the sequence |
| `Swipe(startX, startY, endX, endY, durationMs)` | Coordinate swipe |
| `Swipe(fromComponent, toComponent, durationMs)` | Swipe between two elements |
| `Perform()` | Execute the queued actions |

```csharp
// Swipe a carousel left using the element's own geometry
var carousel = App.Components.CreateById<AndroidComponent>("com.example.shop:id/carousel");
var p = carousel.Location;
var s = carousel.Size;
App.TouchActions.Swipe(p.X + s.Width - 10, p.Y + s.Height / 2, p.X + 10, p.Y + s.Height / 2, 300).Perform();

// Long-press a row to open its context menu
App.TouchActions.LongPress(App.Components.CreateByText<Button>("Pixel 9"), 2).Perform();
```

Prefer `component.ScrollToVisible(ScrollDirection.Down)` over hand-written swipes for scrolling.

---

## Waits

`App.Wait` has no public wait method on mobile. Use the fluent component waits
(`ToExists`, `ToNotExists`, `ToBeVisible`, `ToNotBeVisible`, `ToBeClickable`, `ToHasContent`) — see
`bellatrix-mobile-locators-and-waits`.

```csharp
var save = App.Components.CreateByDescription<Button>("Save").ToBeClickable();
save.Click();
```

---

## Page Object Factory — `App.Create<TPage>()`

Resolves a page object (and its constructor dependencies) from the BELLATRIX container. No navigation
happens — the screen must already be shown, or call `GoTo()` on an `AndroidPage`.

```csharp
var loginPage = App.Create<LoginPage>();
loginPage.GoTo();                      // Android only — StartActivity(PackageName, ActivityName)
loginPage.LoginWith("demo.user", "Secret123!");

var homePage = App.Create<HomePage>();
homePage.AssertWelcomeShown("Welcome, demo.user");
```

---

## Configuration: `testFrameworkSettings.<Configuration>.json`

BELLATRIX reads `testFrameworkSettings.$(Configuration).json` (e.g. `testFrameworkSettings.Debug.json`,
copied to the output folder). All timeouts are in **seconds**.

```json
{
  "mobileSettings": {
    "timeoutSettings": {
      "implicitWaitTimeout": "0",
      "elementWaitTimeout": "30",
      "sleepInterval": "1",
      "validationsTimeout": "30",
      "elementToBeVisibleTimeout": "30",
      "elementToExistTimeout": "30",
      "elementToNotExistTimeout": "30",
      "elementToBeClickableTimeout": "30",
      "elementNotToBeVisibleTimeout": "30",
      "elementToHaveContentTimeout": "15"
    },
    "executionSettings": {
      "defaultLifeCycle": "restart every time",
      "shouldStartLocalService": "false",
      "isCloudRun": "false",
      "url": "http://127.0.0.1:4723",
      "arguments": [
        {
          "platformName": "Android",
          "platformVersion": "14",
          "deviceName": "Android Emulator",
          "app": "AssemblyFolder\\Apps\\MyApp.apk",
          "appPackage": "com.example.shop",
          "appActivity": ".MainActivity",
          "appWaitActivity": "*"
        }
      ]
    }
  }
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `executionSettings.defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | Default app lifecycle (a class/method `[Android]` / `[IOS]` attribute overrides it) |
| `executionSettings.shouldStartLocalService` | `"false"` (recommended) | `"true"` makes `TestsInitialize` spawn its own Appium; keep `"false"` when the user runs Appium |
| `executionSettings.url` | Appium URL | Where tests connect (default `http://127.0.0.1:4723`) |
| `executionSettings.arguments` | capability objects | Appium capabilities (`platformName`, `deviceName`, `app`, …) used only when the class/method has no `[Android]`/`[IOS]` attribute (ignored otherwise) |
| `timeoutSettings.*` | seconds | Defaults for the fluent waits and `Validate*` methods |

For iOS use `"platformName": "iOS"` and the `.app` / `.ipa` path (or bundle id) as `"app"`.
