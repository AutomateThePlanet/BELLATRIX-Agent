<!-- lang:find -->
## Find Strategies (`app().create().by…`)

The factories are methods of the platform's `ComponentCreateService`: `app().create()` in tests, `create()` in a
`Map` (`PageMap`). Import components from ONE platform package per file: `solutions.bellatrix.android.components.*`
**or** `solutions.bellatrix.ios.components.*`. The class token comes first, then the locator value. Mobile spells
XPath with a capital P: `byXPath`.

### Android (`solutions.bellatrix.android.services.ComponentCreateService`)

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| resource-id (exact) | `byId(T.class, "pkg:id/x")` | `app().create().byId(TextField.class, "com.example.shop:id/username")` |
| resource-id (contains) | `byIdContaining(T.class, "x")` | `app().create().byIdContaining(Button.class, "login")` |
| content-desc (= accessibility id) | `byDescription(T.class, "desc")` | `app().create().byDescription(Button.class, "Log in")` |
| content-desc (contains) | `byDescriptionContaining(T.class, "desc")` | `app().create().byDescriptionContaining(Button.class, "Log")` |
| text (exact) | `byText(T.class, "text")` | `app().create().byText(Label.class, "Order placed")` |
| text (contains) | `byTextContaining(T.class, "text")` | `app().create().byTextContaining(Label.class, "placed")` |
| class | `byClass(T.class, "class")` | `app().create().byClass(SeekBar.class, "android.widget.SeekBar")` |
| UiAutomator | `byAndroidUIAutomator(T.class, "expr")` | `app().create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log in\")")` |
| XPath | `byXPath(T.class, "xpath")` | `app().create().byXPath(Button.class, "//android.widget.Button[@text='Log in']")` |

`byId`, `byIdContaining`, `byDescription*`, `byText*`, and `byClass` are implemented as
`new UiScrollable(new UiSelector()).scrollIntoView(...)` selectors, so they also scroll a scrollable container to
reach the element.

**Android has NO `byAccessibilityId`.** The accessibility id on Android is `content-desc` → use
`byDescription(T.class, …)`. (`byName` / `byTag` exist but send Selenium `By.name` / `By.tagName`, which are not
UiAutomator2 locator strategies — do not use them.)

### iOS (`solutions.bellatrix.ios.services.ComponentCreateService`)

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| accessibility id (exact) | `byAccessibilityIdContaining(T.class, "name")` | `app().create().byAccessibilityIdContaining(Button.class, "loginButton")` |
| id | `byId(T.class, "name")` | `app().create().byId(TextField.class, "usernameField")` |
| name | `byName(T.class, "name")` | `app().create().byName(Button.class, "Log in")` |
| NSPredicate | `byIOSNsPredicate(T.class, "predicate")` | `app().create().byIOSNsPredicate(Label.class, "label == \"Order placed\"")` |
| class chain | `byIOSClassChain(T.class, "chain")` | ``app().create().byIOSClassChain(Button.class, "**/XCUIElementTypeButton[`name == \"loginButton\"`]")`` |
| type | `byClass(T.class, "XCUIElementType…")` | `app().create().byClass(TextField.class, "XCUIElementTypeTextField")` |
| value (contains) | `byValueContaining(T.class, "text")` | `app().create().byValueContaining(Label.class, "Total")` |
| XPath | `byXPath(T.class, "xpath")` | `app().create().byXPath(Button.class, "//XCUIElementTypeButton[@name='loginButton']")` |

**iOS has NO single `byAccessibilityId`, NO `byText`, NO `byDescription`, NO `byIdContaining`.** The single
accessibility-id factory is `byAccessibilityIdContaining` — despite its name it uses `AppiumBy.accessibilityId`,
an **exact** match. Unlike C#, BELLATRIX-Java HAS a class-chain factory (`byIOSClassChain`), so a validated
`iosclasschain` locator can be used as-is. `byValueContaining` builds the XPath `//*[contains(@value, '…')]`
(slow — prefer a `value CONTAINS "…"` NSPredicate).

### Collections (`allBy…` → `java.util.List<T>`)

| Android | iOS |
|---------|-----|
| `allById`, `allByIdContaining` | `allById`, `allByAccessibilityId` |
| `allByDescription`, `allByDescriptionContaining` | `allByName` |
| `allByText`, `allByTextContaining` | `allByIOSNsPredicate`, `allByIOSClassChain` |
| `allByClass`, `allByAndroidUIAutomator`, `allByXPath` | `allByClass`, `allByTag`, `allByXPath`, `allByValueContaining` |

`allBy…` queries the device immediately (no BELLATRIX wait — only the driver's `implicitWaitTimeout`) and returns an
empty list when nothing matches.

---

## Locator Strategy (Priority Order)

### CRITICAL: Prefer Direct Factory Methods Over XPath

When the validated locator is a single attribute, ALWAYS use the direct factory — NOT `byXPath`. Direct factories
map to native Appium strategies; XPath forces a full page-source dump per lookup.

| Validated locator | USE | DO NOT USE |
|-------------------|-----|------------|
| Android content-desc | `byDescription(Button.class, "Log in")` | `byXPath(Button.class, "//*[@content-desc='Log in']")` |
| Android resource-id | `byId(Button.class, "com.app:id/login")` | `byXPath(Button.class, "//*[@resource-id='com.app:id/login']")` |
| Android text | `byText(Button.class, "Log in")` | `byXPath(Button.class, "//*[@text='Log in']")` |
| iOS name / accessibility id | `byAccessibilityIdContaining(Button.class, "loginButton")` | `byXPath(Button.class, "//*[@name='loginButton']")` |
| iOS label | `byIOSNsPredicate(Button.class, "label == \"Log in\"")` | `byXPath(Button.class, "//*[@label='Log in']")` |

Reserve `byXPath` for genuinely structural locators (parent/child chains, sibling axes) where no UiSelector /
NSPredicate / class chain can express the relation.

### Priority 1: Accessibility Id (MOST STABLE)

```java
// Android — content-desc
app().create().byDescription(Button.class, "Log in");

// iOS — accessibility identifier (name); exact match despite the method name
app().create().byAccessibilityIdContaining(Button.class, "loginButton");
```

### Priority 2: resource-id (Android) / NSPredicate (iOS)

```java
// Android — ALWAYS the fully-qualified resource-id
app().create().byId(TextField.class, "com.example.shop:id/username");

// Android — fragment match when the package prefix varies between flavors
app().create().byIdContaining(TextField.class, "username");

// iOS — type + name / label
app().create().byIOSNsPredicate(PasswordField.class, "type == \"XCUIElementTypeSecureTextField\" AND name == \"passwordField\"");
```

### Priority 3: UiAutomator (Android) / class chain or visible text

```java
app().create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log in\").clickable(true)");
app().create().byText(Label.class, "Order placed");                                           // Android
app().create().byIOSNsPredicate(Label.class, "label == \"Order placed\"");                     // iOS
app().create().byIOSClassChain(Button.class, "**/XCUIElementTypeCell[`label CONTAINS \"Pixel\"`]"); // iOS
```

### Priority 4: Class / XPath (LAST RESORT)

```java
app().create().byClass(SeekBar.class, "android.widget.SeekBar");                       // only control of its class
app().create().byXPath(Label.class, "//androidx.recyclerview.widget.RecyclerView//android.widget.TextView[@text='Pixel 9']");
```

---

<!-- lang:decision-and-waits -->
## Decision-Making Process

When turning a validated CLI locator into code:

1. **Android `accessibilityid`?** → `byDescription(T.class, value)`
2. **Android `id`?** → `byId(T.class, "pkg:id/x")` (or `byIdContaining(T.class, …)` for a stable fragment)
3. **iOS `accessibilityid`?** → `byAccessibilityIdContaining(T.class, value)` (exact match); iOS `id` → `byId(T.class, value)`
4. **`androiduiautomator` / `iospredicate` / `iosclasschain`?** → `byAndroidUIAutomator` / `byIOSNsPredicate` / `byIOSClassChain`
5. **Exact visible text only?** → `byText(T.class, …)` (Android) / `byIOSNsPredicate(T.class, "label == \"…\"")` (iOS)
6. **Structural relation or nothing else unique?** → `byXPath(T.class, …)`
7. **Child of a known container?** → create the container, then a **relative XPath** child (`createByXPath(T.class, ".//…")`)

**Rule:** if your XPath collapses to a single `[@attr='value']` predicate, use the direct factory instead.

### Nested (scoped) creation

Only `createByXPath` (relative `.//…`), `createByName`, `createByTag`, `createByImage` (and on iOS
`createByValueContaining`) search inside the parent — on Android use the relative XPath (name / tag name are not
UiAutomator2 strategies). Every other child factory (`createById`, `createByAccessibilityId`,
`createByIOSNsPredicate`, `createByIOSClassChain`, …) compiles but its lookup returns `null`.

```java
// Android
AndroidComponent list = app().create().byId(AndroidComponent.class, "com.example.shop:id/products");
Button row = list.createByXPath(Button.class, ".//android.widget.TextView[@text='Pixel 9']");

// iOS
IOSComponent table = app().create().byIOSNsPredicate(IOSComponent.class, "type == \"XCUIElementTypeTable\"");
Button cell = table.createByXPath(Button.class, ".//XCUIElementTypeCell[@name='row_pixel9']");
```

`AndroidComponent` is `solutions.bellatrix.android.components.AndroidComponent`; `IOSComponent` is
`solutions.bellatrix.ios.components.IOSComponent`.

---

## Wait Strategies

Fluent methods on every component. They are lazy — queued on the component and applied on its next lookup (the
next action, a `get…()`, or `waitToBe()`). Timeouts are **seconds** (`long`). With no queued wait, every lookup waits
`toExist()`.

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `toExist()` / `toExist(timeout, sleep)` | Wait until the element is in the hierarchy (default for every lookup) |
| Not Exists | `toNotExist()` / `toNotExist(timeout, sleep)` | Element gone — use the wait service, see below |
| Visible | `toBeVisible()` / `toBeVisible(timeout, sleep)` | Wait until displayed |
| Not Visible | `toNotBeVisible()` / `toNotBeVisible(timeout, sleep)` | Hidden but still in the hierarchy — see below |
| Clickable | `toBeClickable()` / `toBeClickable(timeout, sleep)` | Wait until enabled |
| Disabled | `toBeDisabled()` / `toBeDisabled(timeout, sleep)` | Wait until not enabled |
| Has Content | `toHaveContent()` / `toHaveContent(timeout, sleep)` | Wait until the element text is non-empty |

Default timeouts come from `androidSettings.timeoutSettings` / `iOSSettings.timeoutSettings` in
`src/main/resources/testFrameworkSettings.<env>.json` (`elementToExistTimeout`, `elementToBeVisibleTimeout`,
`elementToBeClickableTimeout`, …, polling `sleepInterval`).

### The generic wait-chain trap (compile error)

`toBeVisible()` and friends are declared `<T extends AndroidComponent> T toBeVisible()` (`IOSComponent` on iOS) — the
return type is inferred from the **assignment target**. Chained directly, Java infers the base component, which has
no `click()` / `setText()`:

```text
WRONG — does not compile (cannot find symbol: method click())
loginButton.toBeVisible().click();
```

```java
// CORRECT — waitToBe() lives on the base component, so chains ending in it compile
loginButton.toBeClickable().toBeVisible().waitToBe();

// CORRECT — let the assignment drive inference
Button readyButton = loginButton.toBeClickable();
readyButton.click();

// CORRECT — explicit type witness
loginButton.<Button>toBeVisible().click();
```

A `Map` method returns a NEW component on every call, so `map().status().toHaveContent();` followed by
`map().status().validateTextIs(…)` waits on one instance and validates another — end the wait with `waitToBe()`.

### Usage Examples

```java
// Fluent — applied at creation, evaluated on first use; the assignment drives the type
Button loginButton = app().create().byDescription(Button.class, "Log in").toBeClickable();
loginButton.click();

// Wait for asynchronously loaded text (40 s timeout, poll every 1 s), then validate
Label status = app().create().byId(Label.class, "com.example.shop:id/status").toHaveContent(40, 1);
status.validateTextIs("Synced");

// Force the wait now (throws on timeout)
app().create().byDescription(Button.class, "Checkout").toBeVisible().waitToBe();

// Scrolling (Android): byId / byText / byDescription / byClass scroll the screen's scrollable container themselves
app().create().byText(Button.class, "Pixel 9").click();
```

There is no `scrollToVisible(direction)` in BELLATRIX-Java (`automaticallyScrollToVisible` in the config only moves
the pointer to an element that was already found). iOS has no auto-scrolling factory — swipe first (see
`bellatrix-mobile-app-services` → Touch Actions), then locate.

### Negative waits ("element gone")

`waitToBe()` always re-locates the element after the waits pass, so `toNotExist().waitToBe()` **throws once the
element is really gone**, and `toNotBeVisible()` keeps polling until timeout when the element is removed instead of
hidden. On iOS, `Wait.to().notExist()` / `notBeVisible()` also poll until timeout once the element is removed (the
lookup throws inside the wait).

```java
// Android — the wait service evaluates only the condition
Progress spinner = app().create().byId(Progress.class, "com.example.shop:id/loading");
app().waitFor().wait(spinner, Wait.to().notExist());
app().waitFor().wait(spinner, Wait.to().notExist(60, 1));

// both platforms (and the only reliable form on iOS) — poll an allBy… query until it is empty
new WebDriverWait(app().appService().getWrappedIOSDriver(), Duration.ofSeconds(30))
        .until(d -> app().create().allByAccessibilityId(Button.class, "spinner").isEmpty());

// in a page object / custom component (no app()): construct the service directly
new ComponentWaitService().wait(spinner, Wait.to().notExist(30, 1));
```

`Wait` is `solutions.bellatrix.android.waitstrategies.Wait` / `solutions.bellatrix.ios.waitstrategies.Wait`
(`exist()`, `notExist()`, `beVisible()`, `notBeVisible()`, `beClickable()`, `haveContent()`, each also
`(timeout, sleep)`); `ComponentWaitService` is in `solutions.bellatrix.android.services` /
`solutions.bellatrix.ios.services`; `WebDriverWait` is `org.openqa.selenium.support.ui.WebDriverWait`
(`java.time.Duration`). On Android use `getWrappedAndroidDriver()`. Never use `Thread.sleep`.
