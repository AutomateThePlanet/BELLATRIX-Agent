<!-- lang:find -->
## Find Strategies (`App.Components.CreateBy*`)

The factories are extension methods on `App.Components` and come from the platform namespace:
`using Bellatrix.Mobile.Android;` **or** `using Bellatrix.Mobile.IOS;` (never both in one file).
Mobile spells XPath with a capital P: `CreateByXPath`.

### Android (`Bellatrix.Mobile.Android`)

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| resource-id (exact) | `CreateById<T>("pkg:id/x")` | `App.Components.CreateById<TextField>("com.example.shop:id/username")` |
| resource-id (contains) | `CreateByIdContaining<T>("x")` | `App.Components.CreateByIdContaining<Button>("login")` |
| content-desc (= accessibility id) | `CreateByDescription<T>("desc")` | `App.Components.CreateByDescription<Button>("Log in")` |
| content-desc (contains) | `CreateByDescriptionContaining<T>("desc")` | `App.Components.CreateByDescriptionContaining<Button>("Log")` |
| text (exact) | `CreateByText<T>("text")` | `App.Components.CreateByText<Label>("Order placed")` |
| text (contains) | `CreateByTextContaining<T>("text")` | `App.Components.CreateByTextContaining<Label>("placed")` |
| class | `CreateByClass<T>("class")` | `App.Components.CreateByClass<SeekBar>("android.widget.SeekBar")` |
| UiAutomator | `CreateByAndroidUIAutomator<T>("expr")` | `App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log in\")")` |
| XPath | `CreateByXPath<T>("xpath")` | `App.Components.CreateByXPath<Button>("//android.widget.Button[@text='Log in']")` |

`CreateById`, `CreateByIdContaining`, `CreateByDescription*`, `CreateByText*`, and `CreateByClass` are
implemented as `new UiScrollable(new UiSelector()).scrollIntoView(...)` selectors, so they also scroll a
scrollable container to reach the element.

**Android has NO `App.Components.CreateByAccessibilityId`, `CreateByName`, or `CreateByTag`.** The
accessibility id on Android is `content-desc` → use `CreateByDescription<T>`.

### iOS (`Bellatrix.Mobile.IOS`)

| Strategy | Factory Method | Example |
|----------|---------------|---------|
| accessibility id / name | `CreateById<T>("name")` | `App.Components.CreateById<TextField>("usernameField")` |
| name | `CreateByName<T>("name")` | `App.Components.CreateByName<Button>("Log in")` |
| NSPredicate | `CreateByIOSNsPredicate<T>("predicate")` | `App.Components.CreateByIOSNsPredicate<Label>("label == \"Order placed\"")` |
| type | `CreateByClass<T>("XCUIElementType…")` | `App.Components.CreateByClass<TextField>("XCUIElementTypeTextField")` |
| value (contains) | `CreateByValueContaining<T>("text")` | `App.Components.CreateByValueContaining<Label>("Total")` |
| XPath | `CreateByXPath<T>("xpath")` | `App.Components.CreateByXPath<Button>("//XCUIElementTypeButton[@name='loginButton']")` |

**iOS has NO `App.Components.CreateByAccessibilityId` and NO class-chain factory.** Use `CreateById<T>`
for accessibility ids (XCUITest resolves `id`, `name`, and accessibility id to the same `name`
attribute). `CreateByAccessibilityId<T>` exists only as a **nested** factory on a parent component
(`table.CreateByAccessibilityId<Button>("row_1")`), and `CreateAllByAccessibilityId<T>` exists on
`App.Components`. Re-express a validated `iosclasschain` locator as an NSPredicate (or XPath) and
re-validate it with the CLI before using it.

### Collections (`CreateAllBy*`)

| Android | iOS |
|---------|-----|
| `CreateAllById`, `CreateAllByIdContaining` | `CreateAllById`, `CreateAllByAccessibilityId` |
| `CreateAllByDescription`, `CreateAllByDescriptionContaining` | `CreateAllByName` |
| `CreateAllByText`, `CreateAllByTextContaining` | `CreateAllByIOSNsPredicate` |
| `CreateAllByClass`, `CreateAllByAndroidUIAutomator`, `CreateAllByXPath` | `CreateAllByClass`, `CreateAllByTag`, `CreateAllByXPath`, `CreateAllByValueContaining` |

---

## Locator Strategy (Priority Order)

### CRITICAL: Prefer Direct Factory Methods Over XPath

When the validated locator is a single attribute, ALWAYS use the direct factory — NOT `CreateByXPath`.
Direct factories map to native Appium strategies; XPath forces a full page-source dump per lookup.

| Validated locator | USE | DO NOT USE |
|-------------------|-----|------------|
| Android content-desc | `CreateByDescription<T>("Log in")` | `CreateByXPath<T>("//*[@content-desc='Log in']")` |
| Android resource-id | `CreateById<T>("com.app:id/login")` | `CreateByXPath<T>("//*[@resource-id='com.app:id/login']")` |
| Android text | `CreateByText<T>("Log in")` | `CreateByXPath<T>("//*[@text='Log in']")` |
| iOS name / accessibility id | `CreateById<T>("loginButton")` | `CreateByXPath<T>("//*[@name='loginButton']")` |
| iOS label | `CreateByIOSNsPredicate<T>("label == \"Log in\"")` | `CreateByXPath<T>("//*[@label='Log in']")` |

Reserve `CreateByXPath` for genuinely structural locators (parent/child chains, sibling axes) where no
UiSelector / NSPredicate can express the relation.

### Priority 1: Accessibility Id (MOST STABLE)

```csharp
// Android — content-desc
App.Components.CreateByDescription<Button>("Log in")

// iOS — accessibility identifier (name)
App.Components.CreateById<Button>("loginButton")
```

### Priority 2: resource-id (Android) / NSPredicate (iOS)

```csharp
// Android — ALWAYS the fully-qualified resource-id
App.Components.CreateById<TextField>("com.example.shop:id/username")

// Android — fragment match when the package prefix varies between flavors
App.Components.CreateByIdContaining<TextField>("username")

// iOS — type + name / label
App.Components.CreateByIOSNsPredicate<Password>("type == \"XCUIElementTypeSecureTextField\" AND name == \"passwordField\"")
```

### Priority 3: UiAutomator (Android) / visible text

```csharp
App.Components.CreateByAndroidUIAutomator<Button>("new UiSelector().text(\"Log in\").clickable(true)")
App.Components.CreateByText<Label>("Order placed")                                  // Android
App.Components.CreateByIOSNsPredicate<Label>("label == \"Order placed\"")             // iOS
```

### Priority 4: Class / XPath (LAST RESORT)

```csharp
App.Components.CreateByClass<SeekBar>("android.widget.SeekBar")                       // only control of its class
App.Components.CreateByXPath<Label>("//androidx.recyclerview.widget.RecyclerView//android.widget.TextView[@text='Pixel 9']")
```

---

<!-- lang:decision-and-waits -->
## Decision-Making Process

When turning a validated CLI locator into code:

1. **Android `accessibilityid`?** → `CreateByDescription<T>(value)`
2. **Android `id`?** → `CreateById<T>("pkg:id/x")` (or `CreateByIdContaining<T>` for a stable fragment)
3. **iOS `accessibilityid` / `id`?** → `CreateById<T>(value)` (or `CreateByName<T>`)
4. **`androiduiautomator` / `iospredicate`?** → `CreateByAndroidUIAutomator<T>` / `CreateByIOSNsPredicate<T>`
5. **Exact visible text only?** → `CreateByText<T>` (Android) / `CreateByIOSNsPredicate<T>("label == \"…\"")` (iOS)
6. **`iosclasschain`?** → rewrite as NSPredicate (or XPath), re-validate live, then step 4
7. **Structural relation or nothing else unique?** → `CreateByXPath<T>(...)`
8. **Child of a known container?** → create the container, then use the nested factory on it

**Rule:** if your XPath collapses to a single `[@attr='value']` predicate, use the direct factory instead.

### Nested (scoped) creation

```csharp
// Android
var list = App.Components.CreateById<AndroidComponent>("com.example.shop:id/products");
var row = list.CreateByText<Button>("Pixel 9");

// iOS
var table = App.Components.CreateByIOSNsPredicate<IOSComponent>("type == \"XCUIElementTypeTable\"");
var cell = table.CreateByAccessibilityId<Button>("row_pixel9");
```

`AndroidComponent` is in `Bellatrix.Mobile.Controls.Android`; `IOSComponent` is in
`Bellatrix.Mobile.Controls.IOS`.

---

## Wait Strategies

Fluent extension methods from the platform namespace (`Bellatrix.Mobile.Android` / `Bellatrix.Mobile.IOS`).
They are lazy — the condition is applied the next time the element is used — and accept optional
`timeoutInterval` / `sleepInterval` in **seconds**.

| Wait | Method | Description |
|------|--------|-------------|
| Exists | `ToExists(timeout?, sleep?)` | Wait until the element is in the hierarchy (default for every element) |
| Not Exists | `ToNotExists(timeout?, sleep?)` | Wait until the element is gone (spinner, dismissed dialog) |
| Visible | `ToBeVisible(timeout?, sleep?)` | Wait until displayed |
| Not Visible | `ToNotBeVisible(timeout?, sleep?)` | Wait until hidden |
| Clickable | `ToBeClickable(timeout?, sleep?)` | Wait until enabled and displayed |
| Has Content | `ToHasContent(timeout?, sleep?)` | Wait until the element has text |

Default timeouts come from `mobileSettings.timeoutSettings` in `testFrameworkSettings.<Configuration>.json`
(`elementToExistTimeout`, `elementToBeVisibleTimeout`, `elementToBeClickableTimeout`, …, in seconds).

### Usage Examples

```csharp
// Fluent — applied at creation, evaluated on first use
var loginButton = App.Components.CreateByDescription<Button>("Log in").ToBeClickable().ToBeVisible();
loginButton.Click();

// Wait for asynchronously loaded text (40s timeout, poll every 1s)
var status = App.Components.CreateById<Label>("com.example.shop:id/status").ToHasContent(40, 1);
status.ValidateTextIs("Synced");

// Force the wait now (no action) — e.g. a loading spinner must disappear
App.Components.CreateById<Progress>("com.example.shop:id/loading").ToNotExists().WaitToBe();

// Scroll until visible (Up | Down | Left | Right — ScrollDirection is in Bellatrix.Mobile)
var checkout = App.Components.CreateByDescription<Button>("Checkout");
checkout.ScrollToVisible(ScrollDirection.Down);
checkout.Click();
```

`App.Wait` exposes no public wait method on mobile — use the fluent `To*` methods above. Never use
`Thread.Sleep`.
