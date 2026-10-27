---
description: Reference skill for BELLATRIX Java web app() services — create(), navigate(), browser(), script(), dialogs(), cookies(), waitFor(), page/section factories (goTo, createPage, createSection), and testFrameworkSettings configuration.
---
# BELLATRIX Web App Services Reference (Java)

`app()` (returns `solutions.bellatrix.web.services.App`) is the entry point to every web service. It is a **method**, not a property: tests get it from the `WebTest` base class; page objects (`WebPage`), sections (`WebSection`), maps (`PageMap`), and asserts (`PageAsserts`) all expose `app()` too. `WebPage` / `WebSection` / `PageMap` also have the shortcut `create()` (= `app().create()`), and `WebPage` / `WebSection` have `browser()` (= `app().browser()`).

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `app().create()` | `ComponentCreateService` — `byCss`, `byId`, `byXPath`, … and `allBy…` |
| Navigation | `app().navigate()` | `to(url)`, `toLocalPage(file)`, `waitForPartialUrl(…)`, `getQueryParameter(…)` |
| Browser | `app().browser()` | URL/title, back/forward/refresh, page-level waits, frames, tabs, storage, console logs, URL assertions |
| JavaScript | `app().script()` | Execute scripts |
| Dialogs | `app().dialogs()` | Native alert / confirm / prompt |
| Cookies | `app().cookies()` | Add / read / delete cookies |
| Waits | `app().waitFor()` | `wait(component, Wait.to().…())` — run a wait strategy now |
| Pages | `app().goTo(P.class)` / `app().createPage(P.class)` | Page object + navigate / page object only |
| Sections | `app().createSection(S.class)` | Section object |

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `app.navigate()` / `App.Navigation.Navigate(url)` | `app().navigate().to(url)` — `app()` is a method |
| `app().browser().navigate(url)` | `app().navigate().to(url)` |
| `new LoginPage()` | `app().createPage(LoginPage.class)` (or `app().goTo(LoginPage.class)` to also open it) |
| `app().create(LoginPage.class)` | `app().create()` is the component factory — pages come from `createPage` / `goTo` |
| `app().create().byXpath(…)` | `app().create().byXPath(…)` (capital **P**) |
| `app().create().byDataTestId(T.class, "x")` | `app().create().byCss(T.class, "[data-testid='x']")` |
| `app().create().byId("x", Button.class)` | `app().create().byId(Button.class, "x")` — class first |
| `app().interactions()…` | Does not exist in Java — Selenium `Actions` inside a custom component (below) |
| `app().javaScript()` on `App` | `app().script()` (`javaScript()` exists only on `WebPage` / `WebSection`) |
| `App.Assert…` | Does not exist in Java — JUnit `Assertions` / TestNG `Assert` / `SoftAssert` |
| `app().browser().takeScreenshot()` | Not a service method — screenshots on failure are automatic (`webSettings.screenshotsOnFailEnabled`) |

---

## ComponentCreateService (`app().create()`)

### Single Component Creation

```java
app().create().byCss(Button.class, "[data-testid='login']");             // PREFERRED (data-testid)
app().create().byId(TextInput.class, "email");                           // static id
app().create().byName(TextInput.class, "username");                      // name attribute
app().create().byXPath(Button.class, "//button[normalize-space()='Login']");
app().create().byLinkText(Anchor.class, "Sign in");
app().create().byLinkTextContaining(Anchor.class, "Sign");
app().create().byClass(Div.class, "alert-danger");
app().create().byClassContaining(Div.class, "woocommerce-message");
app().create().byTag(Heading.class, "h1");
app().create().byInnerTextContaining(Heading.class, "Billing details");
app().create().byAttributeContaining(Anchor.class, "data-product_id", "28");
app().create().byValueContaining(Button.class, "Apply coupon");
app().create().byIdContaining(TextInput.class, "email");
app().create().byIdEndingWith(TextInput.class, "_email");
app().create().byNameEnding(Select.class, "orderby");
```

### Collection Creation (returns `java.util.List<T>`, eager, no waiting)

```java
app().create().allByCss(Anchor.class, "nav a");
app().create().allByXPath(Div.class, "//div[@data-testid='product-card']");
app().create().allByClassContaining(NumberInput.class, "input-text qty text");
app().create().allByTag(Heading.class, "h2");
app().create().allByInnerTextContaining(Anchor.class, "Read more");
```

Single factories are lazy: the element is re-located on each use, which is what you want for dynamic pages.

---

## NavigationService (`app().navigate()`)

| Member | Purpose |
|--------|---------|
| `to(String url)` | Open a URL |
| `toLocalPage(String filePath)` | Open a local HTML file from the test classpath |
| `waitForPartialUrl(String partialUrl)` | Wait until the current URL contains the fragment |
| `getQueryParameter(String name)` | `List<String>` values of a query-string parameter of the current URL (`throws MalformedURLException`) |

```java
app().navigate().to("https://example.com/login");
app().navigate().waitForPartialUrl("/dashboard");
```

---

## BrowserService (`app().browser()`)

| Member | Returns | Purpose |
|--------|---------|---------|
| `getUrl()` | `String` | Current URL |
| `getTitle()` | `String` | Page title |
| `getPageSource()` | `String` | Full page source (debugging only) |
| `back()` / `forward()` / `refresh()` | `void` | History navigation |
| `maximize()` / `setSize(int w, int h)` | `void` | Window size |
| `waitUntilPageLoadsCompletely()` | `void` | Wait for `document.readyState === 'complete'` |
| `waitForAjax()` | `void` | Wait until XHR/fetch is idle (logs and continues on timeout) |
| `waitForAjaxRequest(String partialUrl)` | `void` | Wait for a specific request |
| `waitForJavaScriptAnimations()` / `waitForAngular()` | `void` | Animations / Angular stability |
| `waitForPartialUrl(String partialUrl)` | `void` | Same as the navigation one |
| `switchToFrame(Frame frame)` / `switchToDefault()` | `void` | Enter / leave an iframe |
| `switchToLastTab()` / `switchToFirstBrowserTab()` / `switchToTab(String handle)` / `switchToNewTab()` | `void` | Tabs and windows |
| `waitNumberOfWindowsToBe(int n)` | `void` | Wait for a new tab/window to open |
| `clearLocalStorage()` / `clearSessionStorage()` | `void` | Reset client-side state |
| `getItemFromLocalStorage(key)` / `setItemInLocalStorage(key, value)` | | Local storage |
| `assertLandedOnPage(partialUrl)` / `assertNotLandedOnPage(partialUrl)` / `assertUrl(fullUrl)` / `assertUrlPath(path)` / `assertUrlPathAndQuery(pq)` | `void` | URL assertions — **one-shot, no retry** (`assertLandedOnPage` only waits for `readyState`); after a click that navigates, `app().navigate().waitForPartialUrl(…)` first |
| `assertNoConsoleErrorsLogged()` / `getBrowserLogs()` | | Browser console |

```java
app().browser().waitUntilPageLoadsCompletely();
Assertions.assertEquals("Cart – Bellatrix Demos", app().browser().getTitle());
Assertions.assertTrue(app().browser().getUrl().contains("/cart"));   // getUrl() is a String

Frame frame = app().create().byId(Frame.class, "payment-frame");
app().browser().switchToFrame(frame);
app().create().byName(TextInput.class, "cardnumber").setText("4242424242424242");
app().browser().switchToDefault();
```

(`Assertions` = `org.junit.jupiter.api.Assertions`.)

---

## Other Services

### JavaScriptService (`app().script()`)

```java
Object height = app().script().execute("return document.body.scrollHeight;");      // Object (Long here)
Long typedHeight = app().script().genericExecute("return document.body.scrollHeight;");
app().script().execute("arguments[0].scrollIntoView();", component);                // with a component
```

Traps: `execute(String script, Object... args)` casts the result to `String` and silently returns `""` for numbers/booleans — use `genericExecute(…)` for typed results. `execute(String, String)` is the **frame overload** (`execute(frameName, script)`), not "script + one argument".

### DialogService (`app().dialogs()`) — native `alert` / `confirm` / `prompt`

```java
String text = app().dialogs().getText();
app().dialogs().handle();                        // press OK
app().dialogs().handle(DialogButton.CANCEL);     // press Cancel (any button other than OK dismisses)
```

`DialogButton` is `solutions.bellatrix.web.services.DialogButton` (`OK`, `CANCEL`, `YES`, `NO`, …). Read the text with `getText()` before `handle(…)` rather than passing a lambda (the `handle(lambda, button)` overloads take a `Consumer<Alert>` or a `Function<Object, Alert>` and an expression lambda is ambiguous between them).

### CookiesService (`app().cookies()`)

```java
app().cookies().addCookie("cookie_consent", "accepted");
Cookie session = app().cookies().getCookie("session");    // org.openqa.selenium.Cookie (null if absent)
app().cookies().deleteCookie("cart");
app().cookies().deleteAllCookies();
```

### Keyboard chords, drag-and-drop, double-click — no Interactions service

BELLATRIX Java has **no** `InteractionsService`. `hover()` and `focus()` exist on every component; for anything else wrap Selenium `Actions` in a custom component method that acts on its own element (this is the pattern the official Java docs use for `ExtendedButton`):

```java
package myproject.webtests.components;

import org.openqa.selenium.Keys;
import org.openqa.selenium.interactions.Actions;
import solutions.bellatrix.web.components.SearchField;
import solutions.bellatrix.web.components.WebComponent;

public class SearchBox extends SearchField {
    public void submitWithEnter() {
        new Actions(getWrappedDriver()).moveToElement(getWrappedElement()).sendKeys(Keys.ENTER).perform();
    }

    public void dragTo(WebComponent target) {
        new Actions(getWrappedDriver()).dragAndDrop(getWrappedElement(), target.getWrappedElement()).perform();
    }
}
```

Always finish an `Actions` chain with `.perform()`.

### Assertions

There is no `App.Assert` in Java. Use JUnit 5 `org.junit.jupiter.api.Assertions` (or TestNG `org.testng.Assert`) for hard asserts and TestNG `org.testng.asserts.SoftAssert` (`assertAll()` at the end) or JUnit `Assertions.assertAll(…)` for soft ones — both libraries arrive transitively with BELLATRIX. These work in page `Asserts` classes too.

---

## Page Object Factories

### `app().createPage(TPage.class)`

Returns the page object instance **without** navigating. Use when the page is already open (after a click or redirect):

```java
var cartPage = app().createPage(CartPage.class);
cartPage.asserts().totalPrice("114.00");
```

### `app().goTo(TPage.class)`

Returns the page object **and** calls its `open()` (navigates to `getUrl()`, then `waitForPageLoad()`):

```java
var homePage = app().goTo(HomePage.class);
homePage.addProductById(28);
```

`goTo` requires a `WebPage`; `createPage` / `createSection` accept any page-object model. Instances are cached **per thread** (singletons), so `createPage(CartPage.class)` twice returns the same object — keep page objects stateless. Never `new` a page object.

---

## Configuration: `testFrameworkSettings.<env>.json`

BELLATRIX reads `application.properties` → `environment=dev` from the classpath root (or `-Denvironment=dev`), then loads `testFrameworkSettings.dev.json` from the classpath root. Put both in `src/main/resources` (the BELLATRIX samples) or `src/test/resources`. The web part lives under `webSettings` (section name = settings class `WebSettings` with a lowercase first letter):

```json
{
  "webSettings": {
    "baseUrl": "https://example.com/",
    "executionType": "regular",
    "defaultBrowser": "chrome",
    "defaultLifeCycle": "restart every time",
    "defaultBrowserWidth": "0",
    "defaultBrowserHeight": "0",
    "artificialDelayBeforeAction": "0",
    "automaticallyScrollToVisible": "false",
    "waitUntilReadyOnElementFound": "false",
    "waitForAngular": "false",
    "shouldHighlightElements": "true",
    "shouldCaptureHttpTraffic": "false",
    "screenshotsOnFailEnabled": "true",
    "screenshotsSaveLocation": "${user.home}/BELLATRIX/Screenshots",
    "videosOnFailEnabled": "false",
    "videosSaveLocation": "${user.home}/BELLATRIX/Videos",
    "toastNotificationBddLogging": "false",
    "timeoutSettings": {
      "elementWaitTimeout": "30",
      "pageLoadTimeout": "30",
      "scriptTimeout": "1",
      "waitForAjaxTimeout": "30",
      "sleepInterval": "1",
      "waitUntilReadyTimeout": "30",
      "waitForJavaScriptAnimationsTimeout": "30",
      "waitForAngularTimeout": "30",
      "waitForPartialUrl": "30",
      "validationsTimeout": "30",
      "elementToBeVisibleTimeout": "30",
      "elementToExistTimeout": "30",
      "elementToNotExistTimeout": "30",
      "elementToBeClickableTimeout": "30",
      "elementNotToBeVisibleTimeout": "30",
      "elementToHaveContentTimeout": "15"
    },
    "gridSettings": [
      { "providerName": "grid", "url": "http://127.0.0.1:4444/wd/hub", "arguments": [ { } ] }
    ]
  }
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `executionType` | `"regular"` (local browser) or a grid/cloud provider name | `regular` launches a local browser; anything else uses the matching `gridSettings[]` entry (`providerName`, `url`, `arguments`) |
| `defaultBrowser` | `chrome`, `chrome_headless`, `firefox`, `firefox_headless`, `edge`, `edge_headless`, `safari`, … | Browser when no `@ExecutionBrowser` is present (unknown text → `chrome`) |
| `defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | Lifecycle when no `@ExecutionBrowser` is present (matched loosely; unknown text → restart every time) |
| `defaultBrowserWidth` / `defaultBrowserHeight` | pixels, `0` = default | Window size when the annotation sets none |
| `timeoutSettings.*` | **seconds** | Defaults for waits and `validate*` retries |

Custom sections (e.g. `"urlSettings"`) are read with `ConfigurationService.get(UrlSettings.class)` from a matching POJO whose class name, lower-camel-cased, is the section name.
