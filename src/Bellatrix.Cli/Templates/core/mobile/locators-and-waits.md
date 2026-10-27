---
name: bellatrix-mobile-locators-and-waits
description: Reference skill for BELLATRIX mobile find strategies, Android/iOS locator patterns, Appium rules, and wait strategies.
---

# BELLATRIX Mobile Locators and Waits Reference

{{> lang:find}}
## Appium Locator Strategies (CLI `--strategy`)

| `--strategy` | Android (UiAutomator2) matches | iOS (XCUITest) matches | Speed / stability |
|--------------|-------------------------------|------------------------|-------------------|
| `accessibilityid` | `content-desc` | `name` (accessibility identifier) | fast, most stable |
| `id` | `resource-id` (`com.app:id/x`) | `name` | fast, stable |
| `androiduiautomator` | UiSelector Java expression | — (Android only) | fast, flexible |
| `iospredicate` | — (iOS only) | NSPredicate over `type`, `name`, `label`, `value`, … | fast, flexible |
| `iosclasschain` | — (iOS only) | class-chain path with predicates | fast |
| `classname` | `class` (`android.widget.Button`) | `type` (`XCUIElementTypeButton`) | usually ambiguous |
| `xpath` | full XML tree | full XML tree | **slow, brittle — last resort** |

Without `--strategy`, the CLI treats a value starting with `/` or `(/` as `xpath` and everything else as
`accessibilityid`.

---

## Locator Hierarchy (stable → fragile)

**Android:** `accessibilityid` (content-desc) → `id` (full resource-id) → `androiduiautomator` →
`classname` → `xpath`

**iOS:** `accessibilityid` (name) → `iospredicate` → `iosclasschain` → `classname` → `xpath`

Accessibility ids are set by developers for screen readers and automation, survive layout changes, and
are the closest thing mobile has to a stable test id. Ask the app team to add them when they are
missing rather than falling back to XPath.

---

## Android Rules (UiAutomator2)

### resource-id must be fully qualified

```
com.example.shop:id/login      CORRECT
login                          WRONG for an exact match (only a "containing" match may use a fragment)
```

### UiSelector expressions (`androiduiautomator`)

| Need | Expression |
|------|------------|
| Exact text | `new UiSelector().text("Log in")` |
| Text contains | `new UiSelector().textContains("Log")` |
| content-desc | `new UiSelector().description("Log in")` |
| resource-id | `new UiSelector().resourceId("com.example.shop:id/login")` |
| Class + text | `new UiSelector().className("android.widget.Button").text("Log in")` |
| Clickable only | `new UiSelector().text("Log in").clickable(true)` |
| Scroll a list until found | `new UiScrollable(new UiSelector().scrollable(true)).scrollIntoView(new UiSelector().text("Checkout"))` |

Use double quotes inside the expression; escape them for the shell / host language as needed.

### Android XPath

Element names are the fully-qualified class; attributes are the raw XML names:

```
//android.widget.Button[@content-desc='Log in']
//android.widget.EditText[@resource-id='com.example.shop:id/username']
//android.widget.TextView[@text='Order placed']
//androidx.recyclerview.widget.RecyclerView//android.widget.TextView[@text='Pixel 9']
```

---

## iOS Rules (XCUITest)

### NSPredicate (`iospredicate`)

| Need | Predicate |
|------|-----------|
| Accessibility id | `name == "loginButton"` |
| Visible text | `label == "Log in"` |
| Text contains | `label CONTAINS "Log"` |
| Type + name | `type == "XCUIElementTypeButton" AND name == "Log in"` |
| Field value | `type == "XCUIElementTypeTextField" AND value == "demo.user"` |
| Secure field | `type == "XCUIElementTypeSecureTextField"` |
| Visible only | `label == "Save" AND visible == 1` |

### Class chain (`iosclasschain`)

```
**/XCUIElementTypeButton[`name == "loginButton"`]
**/XCUIElementTypeCell[`label CONTAINS "Pixel"`]/XCUIElementTypeStaticText[1]
```

### iOS XPath

```
//XCUIElementTypeButton[@name='loginButton']
//XCUIElementTypeStaticText[@label='Order placed']
//XCUIElementTypeTable//XCUIElementTypeCell[@name='row_pixel9']
```

---

## Formatting Rules (both platforms)

1. **Exact, case-sensitive values** — copy them from the snapshot (`ContentDesc`, `ResourceId`, `Name`,
   `Label`), never retype.
2. **Element names from `Class` (Android) / `Type` (iOS)** — never from the snapshot `Tag` key, which is
   lower-cased by the parser and will not match.
3. **Single attribute when it is unique** — add a second predicate only to disambiguate.
4. **Single quotes for XPath values**, double quotes inside UiSelector / NSPredicate strings.
5. **One locator, one element** — validate with `find` that the value matches exactly one node.

---

## Common Control Locators

| Control | Android | iOS |
|---------|---------|-----|
| Button | `accessibilityid` `Log in` / `id` `com.app:id/login` | `accessibilityid` `loginButton` |
| Text field | `id` `com.app:id/username` | `accessibilityid` `usernameField` |
| Password | `id` `com.app:id/password` | `iospredicate` `type == "XCUIElementTypeSecureTextField"` |
| Label / message | `id` `com.app:id/error` | `iospredicate` `label == "Invalid password"` |
| Checkbox / switch | `id` `com.app:id/remember` | `iospredicate` `type == "XCUIElementTypeSwitch" AND name == "Remember me"` |
| List row | `androiduiautomator` `new UiSelector().text("Pixel 9")` | `iospredicate` `type == "XCUIElementTypeCell" AND label CONTAINS "Pixel 9"` |
| Tab / bottom nav item | `accessibilityid` `Home` | `accessibilityid` `Home` |
| Alert button | `id` `android:id/button1` | `iospredicate` `type == "XCUIElementTypeButton" AND label == "OK"` |

---

## Anti-Patterns (NEVER Use)

| Anti-Pattern | Example | Why |
|-------------|---------|-----|
| Index / absolute XPath | `//android.widget.Button[2]`, `/hierarchy/…/android.widget.FrameLayout[1]/…` | Breaks on any layout change |
| XPath when a native strategy works | `//*[@content-desc='Log in']` | Slow (full tree dump on every lookup) and brittle |
| Partial resource-id for an exact lookup | `id` `login` | UiAutomator2 needs `com.app:id/login` |
| Generated / dynamic ids | `com.app:id/view_8f3a2c` | Changes between builds |
| Data-driven text as identity | `text == "3 items in cart"` | Changes with data and locale |
| Coordinates as a locator | tap at `(540, 1200)` | Device- and orientation-dependent |
| Android strategy on iOS (or vice versa) | `androiduiautomator` on an iOS session | Rejected by the driver |

---

## Reading UI State (Context Economy — MANDATORY)

Never read a full page source into context. Capture a snapshot (returns `{ path, elementCount, summary }`),
then either delegate to the **`bellatrix-mobile-locator-specialist`** subagent or query narrowly:

```
bellatrix mobile snapshot --session s --json
bellatrix mobile find --session s --snapshot <path> --resource-id "username" --json
bellatrix mobile find --session s --snapshot <path> --content-desc "Log in" --json
bellatrix mobile find --session s --snapshot <path> --text "Remember me" --json
bellatrix mobile find --session s --snapshot <path> --grep "XCUIElementTypeSecureTextField" --json
bellatrix mobile find --session s --snapshot <path> --interactive-only --json
```

Then prove the locator against the live app before it goes into code:

```
bellatrix mobile is-visible --session s --strategy id --locator "com.example.shop:id/username" --json
```

---

{{> lang:decision-and-waits}}
