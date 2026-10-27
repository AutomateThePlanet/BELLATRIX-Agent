The entry point is the **method** `app()` on the test base class (`AndroidTest` / `IOSTest`), returning
`solutions.bellatrix.android.services.App` / `solutions.bellatrix.ios.services.App`. Inside page objects it differs
per platform: an Android page (`AndroidPage`) has `app()`; an iOS page (`IOSPage`) has NO `app()` — it exposes
`create()` and `appService()`, and builds the next page with `new App().createPage(...)`.

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `App.Components.CreateByXPath<T>(…)` (C#) | `app().create().byXPath(T.class, …)` (class token first) |
| `new LoginPage()` | `app().createPage(LoginPage.class)` |
| `app().goTo(...)` / `page.GoTo()` / navigating by URL | Android: `app().appService().startActivity(pkg, activity)`; iOS: tap through the UI |
| `app().create().byAccessibilityId(T.class, …)` | Android `byDescription(T.class, …)`; iOS `byAccessibilityIdContaining(T.class, …)` (exact match) |
| `app().appService().getCurrentActivity()` on iOS | Android only (iOS has no activities) |
| `button.toBeVisible().click()` | `button.toBeVisible().waitToBe(); button.click();` (wait-chain generics trap) |
| `app().touch().press(x, y).wait(500L)` | `.wait(Long.valueOf(500))` — `wait(500L)` binds to `Object.wait(long)` (void) |
| `appService()` inside an Android page / `app()` inside an iOS page | Android page → `app().appService()`; iOS page → `appService()` |

---

## App Types

| Test base | `app()` returns | Page base | Inside the page |
|-----------|-----------------|-----------|-----------------|
| `solutions.bellatrix.android.infrastructure.junit.AndroidTest` (or `.testng.AndroidTest`) | `solutions.bellatrix.android.services.App` | `AndroidPage<Map, Asserts>` | `app()` |
| `solutions.bellatrix.ios.infrastructure.junit.IOSTest` (or `.testng.IOSTest`) | `solutions.bellatrix.ios.services.App` | `IOSPage<Map, Asserts>` | `create()`, `appService()` (no `app()`) |

| Member | Android `App` | iOS `App` |
|--------|---------------|-----------|
| `create()` | `ComponentCreateService` | `ComponentCreateService` |
| `createPage(T.class)` / `createSection(T.class)` | yes | yes |
| `appService()` | `AppService` (activities, contexts, web views) | `AppService` (contexts) |
| `device()` | `DeviceService` | `DeviceService` |
| `keyboard()` | `KeyboardService` (+ Android keys) | `KeyboardService` (hide only) |
| `fileSystem()` | `FileSystemService` | `FileSystemService` |
| `touch()` | `TouchActionsService` | `TouchActionsService` |
| `waitFor()` | `ComponentWaitService` | `ComponentWaitService` |
| `web()` | `WebServiceFacade` (web services for hybrid / mobile-web) | `WebServiceFacade` |

All services live in `solutions.bellatrix.android.services` / `solutions.bellatrix.ios.services`. There is no
`App.Assert` — use JUnit `Assertions` (or TestNG `Assert`) where no `validate…` method fits.

---

## ComponentCreateService (`app().create()`)

### Android (`import solutions.bellatrix.android.components.*;`)

```java
app().create().byDescription(Button.class, "Log in");                         // content-desc (accessibility id)
app().create().byId(TextField.class, "com.example.shop:id/username");         // full resource-id
app().create().byIdContaining(Button.class, "login");
app().create().byText(Label.class, "Order placed");
app().create().byTextContaining(Label.class, "placed");
app().create().byDescriptionContaining(Button.class, "Log");
app().create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log in\")");
app().create().byClass(SeekBar.class, "android.widget.SeekBar");
app().create().byXPath(Button.class, "//android.widget.Button[@text='Log in']");
```

### iOS (`import solutions.bellatrix.ios.components.*;`)

```java
app().create().byAccessibilityIdContaining(Button.class, "loginButton");      // accessibility id (exact match)
app().create().byId(TextField.class, "usernameField");
app().create().byName(Button.class, "Log in");
app().create().byIOSNsPredicate(Label.class, "label == \"Order placed\"");
app().create().byIOSClassChain(Button.class, "**/XCUIElementTypeButton[`name == \"loginButton\"`]");
app().create().byValueContaining(Label.class, "Total");
app().create().byClass(TextField.class, "XCUIElementTypeTextField");
app().create().byXPath(Button.class, "//XCUIElementTypeButton[@name='loginButton']");
```

### Collections (`java.util.List<T>`)

```java
app().create().allById(Label.class, "com.example.shop:id/row_title");                 // Android
app().create().allByTextContaining(Label.class, "Pixel");                              // Android
app().create().allByAccessibilityId(Button.class, "row");                              // iOS
app().create().allByIOSNsPredicate(Button.class, "type == \"XCUIElementTypeCell\"");   // iOS
```

---

## AppService (`app().appService()`)

### Both platforms

| Method | Returns | Description |
|--------|---------|-------------|
| `backgroundApp(int seconds)` | `void` | Send the app to the background for N seconds, then return |
| `terminateApp(String appId)` | `void` | Terminate an app by package / bundle id |
| `activateApp(String appId)` | `void` | Bring an app (back) to the foreground |
| `installApp(String appPath)` | `void` | Install an app package |
| `removeApp(String appId)` | `void` | Uninstall by package / bundle id |
| `isAppInstalled(String bundleId)` | `boolean` | Installed check (returns `false` on any driver error) |
| `getContext()` / `setContext(String name)` | `String` / `void` | `NATIVE_APP` or a `WEBVIEW_…` context |
| `getWrappedAndroidDriver()` / `getWrappedIOSDriver()` | `AndroidDriver` / `IOSDriver` | Raw Appium driver — capabilities BELLATRIX lacks only, never element lookup |

### Android only

| Method | Returns | Description |
|--------|---------|-------------|
| `startActivity(String appPackage, String appActivity)` | `void` | Jump straight to an activity (`mobile: startActivity`) |
| `startActivity(String appPackage, String appActivity, boolean shouldWait, boolean stopApp)` | `void` | Same, hides the keyboard first; `stopApp=false` keeps the process |
| `getCurrentActivity()` | `String` | Name of the foreground activity |
| `getWebViews()` | `List<String>` | All context handles |
| `switchToWebView()` / `switchToWebView(String name)` / `switchToDefault()` | `void` | Hybrid app context switching |

There is no `closeApp()` / `launchApp()` in Java — use `terminateApp(id)` / `activateApp(id)`, and no
`getPageSource()` / view-snapshot helper (UI inspection is the CLI's job during scenario execution).

### Example Usage

```java
// Android — open a screen directly
app().appService().startActivity("com.example.shop", ".SettingsActivity");
Assertions.assertTrue(app().appService().getCurrentActivity().contains("Settings"));

// Both — background and restore, then verify state survived
app().appService().backgroundApp(3);
cartPage.asserts().itemCountIs(2);

// Both — switch to a hybrid WebView and back
app().appService().setContext("WEBVIEW_com.example.shop");
app().appService().setContext("NATIVE_APP");
```

---

## Device Service (`app().device()`)

| Method | Platform | Description |
|--------|----------|-------------|
| `getScreenOrientation()` | both | Current `org.openqa.selenium.ScreenOrientation` |
| `rotate(ScreenOrientation)` | both | `ScreenOrientation.PORTRAIT` / `ScreenOrientation.LANDSCAPE` |
| `getDeviceTime()` | both | `LocalDateTime`, parsed with the pattern `yyyy-MM-dd HH:mm` (fails if the device reports another format) |
| `lock()` / `unlock()` / `isLocked()` | both | Lock state |
| `setSetting(String, Object)` / `getSetting()` | both | Appium driver settings |
| `openNotifications()` | Android | Pull down the notification shade |
| `turnOnLocationService()` | Android | Toggles location services (it does not force "on") |
| `getCurrentActivity()` | Android | Foreground activity |
| `shake()` | iOS | Shake gesture |

```java
import org.openqa.selenium.ScreenOrientation;

app().device().rotate(ScreenOrientation.LANDSCAPE);
Assertions.assertEquals(ScreenOrientation.LANDSCAPE, app().device().getScreenOrientation());
```

---

## Keyboard Service (`app().keyboard()`)

```java
app().keyboard().hideKeyboard();                                // both platforms (ignores "no keyboard" errors)

// Android only — io.appium.java_client.android.nativekey.AndroidKey
app().keyboard().pressKey(AndroidKey.BACK);
app().keyboard().longPressKey(AndroidKey.HOME);
```

`setText` / `setPassword` do NOT hide the keyboard — call `hideKeyboard()` when it covers the next control.

---

## File System Service (`app().fileSystem()`)

```java
byte[] data = app().fileSystem().pullFile("/sdcard/Download/report.txt");
byte[] folder = app().fileSystem().pullFolder("/sdcard/Download");
app().fileSystem().pushFile("/sdcard/Download/input.txt", "hello".getBytes());   // byte[] overload
app().fileSystem().pushFile("/sdcard/Download/input.txt", new File("input.txt")); // java.io.File overload
```

Same four methods on iOS (paths like `@com.example.shop:documents/report.txt`).

---

## Touch Actions (`app().touch()`)

| Method | Description |
|--------|-------------|
| `swipe(int startX, int startY, int endX, int endY, int durationMs)` | Coordinate swipe — **executes immediately**, no `perform()` |
| `swipe(component, component, int durationMs)` | Swipe between two components' top-left corners — executes immediately |
| `tap(component, int count)` | Tap a component's top-left corner `count` times — queued, needs `perform()` |
| `press(component)` / `press(Integer x, Integer y)` | Press — queued |
| `longPress(component)` / `longPress(Integer x, Integer y)` | Long press — queued |
| `moveTo(component)` / `moveTo(Integer x, Integer y)` | Move — queued |
| `release()` | Lift — queued |
| `wait(Long milliseconds)` | Pause — queued (pass a `Long`, see Common Mistakes) |
| `perform()` | Execute the queued actions |

These methods use Appium's legacy `TouchAction` / `MultiTouchAction` API (deprecated in java-client 9). Each queued
call is added as a separate action of one `MultiTouchAction`, so a `press(…).moveTo(…).release().perform()` chain is
not a single-finger drag; the Android service is reused for the whole app session and its queue is never cleared, so a
second `perform()` replays earlier actions. Prefer the self-executing `swipe(...)`. Whether a given Appium driver version still accepts legacy touch actions is not
something this skill can verify — if `swipe` is rejected, use W3C actions on the raw driver (gestures are a
capability BELLATRIX lacks, not element lookup):

```java
// Swipe a carousel left using the component's own geometry (java.awt.Point / java.awt.Dimension)
AndroidComponent carousel = app().create().byId(AndroidComponent.class, "com.example.shop:id/carousel");
Point p = carousel.getLocation();
Dimension s = carousel.getSize();
app().touch().swipe(p.x + s.width - 10, p.y + s.height / 2, p.x + 10, p.y + s.height / 2, 300);

// W3C fallback (org.openqa.selenium.interactions.PointerInput / Sequence, java.time.Duration, java.util.List)
var finger = new PointerInput(PointerInput.Kind.TOUCH, "finger");
var swipe = new Sequence(finger, 1)
        .addAction(finger.createPointerMove(Duration.ZERO, PointerInput.Origin.viewport(), 540, 1600))
        .addAction(finger.createPointerDown(PointerInput.MouseButton.LEFT.asArg()))
        .addAction(finger.createPointerMove(Duration.ofMillis(300), PointerInput.Origin.viewport(), 540, 400))
        .addAction(finger.createPointerUp(PointerInput.MouseButton.LEFT.asArg()));
app().appService().getWrappedAndroidDriver().perform(List.of(swipe));   // getWrappedIOSDriver() on iOS
```

On Android, prefer the self-scrolling factories (`byText`, `byId`, `byDescription`, …) over hand-written swipes.

---

## Wait Service (`app().waitFor()`)

```java
import solutions.bellatrix.android.waitstrategies.Wait;   // solutions.bellatrix.ios.waitstrategies.Wait on iOS

app().waitFor().wait(element, Wait.to().beVisible());
app().waitFor().wait(spinner, Wait.to().notExist());       // Android: right tool for "element gone"
```

`Wait.to()` offers `exist()`, `notExist()`, `beVisible()`, `notBeVisible()`, `beClickable()`, `haveContent()`, each
also with `(timeoutSeconds, sleepSeconds)`. Outside a test use `new ComponentWaitService().wait(...)`. Most commonly,
use the fluent component waits instead (assign to the concrete type — see `bellatrix-mobile-locators-and-waits`):

```java
Button save = app().create().byDescription(Button.class, "Save").toBeClickable();
save.click();
```

---

## Page Object Factory — `app().createPage(TPage.class)`

Returns the page object (one instance per page class and thread) without any navigation — the screen must
already be shown (Android: call `startActivity` first if needed).

```java
LoginPage loginPage = app().createPage(LoginPage.class);
loginPage.loginWith("demo.user", "Secret123!");

HomePage homePage = app().createPage(HomePage.class);
homePage.asserts().welcomeShown("Welcome, demo.user");
```

Inside an Android page: `app().createPage(HomePage.class)`; inside an iOS page:
`new App().createPage(HomePage.class)` (`solutions.bellatrix.ios.services.App`). Sections (`AppSection<Map, Asserts>`)
come from `app().createSection(X.class)`.

---

## Configuration: `src/main/resources/testFrameworkSettings.dev.json`

`application.properties` (same folder) selects the file: `environment=dev` → `testFrameworkSettings.dev.json`
(override with `-Denvironment=qa`). The section name is the settings class name with a lower-case first letter:
**`androidSettings`** (Android) and **`iOSSettings`** (iOS). Gson maps the keys case-sensitively.

```json
{
  "troubleshootingSettings": {
    "debugInformationEnabled": "true"
  },
  "androidSettings": {
    "serviceUrl": "http://127.0.0.1:4723",
    "executionType": "regular",
    "defaultLifeCycle": "restart every time",
    "defaultAppPath": "${user.home}/apps/shop.apk",
    "defaultAppPackage": "com.example.shop",
    "defaultAppActivity": ".MainActivity",
    "defaultDeviceName": "Android Emulator",
    "defaultAndroidVersion": "14",
    "defaultBrowser": "Chrome",
    "downloadDemoApps": "false",
    "artificialDelayBeforeAction": "0",
    "automaticallyScrollToVisible": "false",
    "screenshotsOnFailEnabled": "true",
    "screenshotsSaveLocation": "${user.home}/BELLATRIX/screenshots",
    "videosOnFailEnabled": "false",
    "videosSaveLocation": "${user.home}/BELLATRIX/videos",
    "allowImageFindStrategies": "false",
    "timeoutSettings": {
      "implicitWaitTimeout": "5",
      "elementWaitTimeout": "30",
      "sleepInterval": "1",
      "validationsTimeout": "10",
      "elementToBeVisibleTimeout": "30",
      "elementToExistTimeout": "30",
      "elementToNotExistTimeout": "30",
      "elementToBeClickableTimeout": "30",
      "elementNotToBeVisibleTimeout": "30",
      "elementToHaveContentTimeout": "15"
    }
  },
  "iOSSettings": {
    "serviceUrl": "http://127.0.0.1:4723",
    "executionType": "regular",
    "defaultLifeCycle": "restart every time",
    "defaultAppPath": "${user.home}/apps/Shop.app",
    "defaultDeviceName": "iPhone 15",
    "defaultIOSVersion": "17.5",
    "deviceIdentifier": "<simulator or device UDID>",
    "defaultBrowser": "Safari",
    "downloadDemoApps": "false",
    "artificialDelayBeforeAction": "0",
    "automaticallyScrollToVisible": "false",
    "screenshotsOnFailEnabled": "true",
    "screenshotsSaveLocation": "${user.home}/BELLATRIX/screenshots",
    "videosOnFailEnabled": "false",
    "videosSaveLocation": "${user.home}/BELLATRIX/videos",
    "allowImageFindStrategies": "false",
    "timeoutSettings": {
      "implicitWaitTimeout": "5",
      "elementWaitTimeout": "30",
      "sleepInterval": "1",
      "validationsTimeout": "10",
      "webviewConnectTimeout": "60",
      "elementToBeVisibleTimeout": "30",
      "elementToExistTimeout": "30",
      "elementToNotExistTimeout": "30",
      "elementToBeClickableTimeout": "30",
      "elementNotToBeVisibleTimeout": "30",
      "elementToHaveContentTimeout": "15"
    }
  }
}
```

A single-platform project keeps only its own section.

| Setting | Values | Description |
|---------|--------|-------------|
| `serviceUrl` | Appium URL | Where tests connect — set it explicitly (the framework samples use port 4722) |
| `executionType` | `"regular"` (or a `gridSettings[].providerName`) | Required — a missing value fails app start |
| `defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | Lifecycle when a class has no `@ExecutionApp`; unknown text → restart every time |
| `defaultAppPath` | `.apk` / `.app` / `.ipa` path | App to install when `@ExecutionApp` has no `appPath`; `${name}` resolves Java system properties only (`${user.home}`, not environment variables). On iOS an `@ExecutionApp(appPath = …)` value is used verbatim (no `${…}` expansion) |
| `defaultAppPackage`, `defaultAppActivity` | package, launch activity | Android: used when `@ExecutionApp` leaves them empty |
| `defaultDeviceName`, `defaultAndroidVersion` / `defaultIOSVersion` | device, platform version | Appium `deviceName` / `platformVersion` |
| `deviceIdentifier` | UDID | iOS: Appium `udid` |
| `automaticallyScrollToVisible`, `screenshotsOnFailEnabled`, `videosOnFailEnabled` | `"true"` / `"false"` | Required booleans — `automaticallyScrollToVisible` missing throws on every element lookup |
| `timeoutSettings.*` | seconds | Wait / validation timeouts; `sleepInterval` is the polling interval; iOS also has `webviewConnectTimeout` |

BELLATRIX-Java never starts Appium — the server, emulator / simulator, and device are the user's to run (there is no
`shouldStartLocalService`).
