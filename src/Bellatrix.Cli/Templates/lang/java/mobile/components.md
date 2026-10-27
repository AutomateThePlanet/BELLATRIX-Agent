## CLI Verb → Component Member

| CLI verb | Component member (generated code) |
|----------|-----------------------------------|
| `click` / `tap` | `click()` (`Button`, `ImageButton`, `RadioButton`); `check()` / `turnOn()` for Android checkboxes / switches |
| `type-text` | `setText(…)` (`TextField`), `setPassword(…)` (`PasswordField`), `setNumber(…)` (`NumberInput`) |
| `clear` | `setText("")` (`setText` clears the field before typing) |
| `get-text` | `getText()` |
| `get-attribute` | `getAttribute(name)` |
| `is-visible` | `toBeVisible().waitToBe()` — mobile components have NO `isVisible()` |
| `is-enabled` | `getAttribute("enabled")` — see the `isDisabled()` warning below |
| `scroll-to-element` | Android: the `byId` / `byText` / `byDescription` / `byClass` factories scroll by themselves; iOS: none built in |
| `swipe` | `app().touch().swipe(startX, startY, endX, endY, durationMs)` — executes immediately, no `perform()` |

## Component Hierarchy

| Platform | Base class | Components package | Factories / waits / validations |
|----------|-----------|--------------------|---------------------------------|
| Android | `solutions.bellatrix.android.components.AndroidComponent` | `solutions.bellatrix.android.components` | `app().create()` (`solutions.bellatrix.android.services.ComponentCreateService`), `toBe…()` on the component, `validate…()` from `solutions.bellatrix.android.components.contracts` |
| iOS | `solutions.bellatrix.ios.components.IOSComponent` | `solutions.bellatrix.ios.components` | `app().create()` (`solutions.bellatrix.ios.services.ComponentCreateService`), same `toBe…()` / `validate…()` names |

The class names are identical on both platforms (`Button`, `TextField`, …) but they are **different
classes** in different packages. A Java file imports from ONE platform package only — importing both
`solutions.bellatrix.android.components.*` and `solutions.bellatrix.ios.components.*` makes `Button`
ambiguous. Keep Android and iOS code in separate packages.

Elements are created through **methods**, never fields: every call returns a fresh component that is
located lazily on first use.

## Android Components (`solutions.bellatrix.android.components`)

| Component | Native class | Key methods | Validations |
|-----------|-------------|-------------|-------------|
| `Button` | `android.widget.Button`, clickable rows | `click()`, `getText()`, `isDisabled()` | text, disabled |
| `ImageButton` | `android.widget.ImageButton` | `click()`, `getText()`, `isDisabled()` | text, disabled |
| `TextField` | `android.widget.EditText` | `setText(String)`, `getText()`, `isDisabled()` | text, disabled |
| `PasswordField` | `android.widget.EditText` (password) | `setPassword(String)`, `getPassword()`, `isDisabled()` | disabled |
| `NumberInput` | `android.widget.EditText` (numeric) | `setNumber(Number)`, `getNumber()` → `double` | number, disabled |
| `Label` | `android.widget.TextView` | `getText()` | text — NO `click()` |
| `CheckBox` | `android.widget.CheckBox` | `check()`, `uncheck()`, `isChecked()`, `getText()` | checked, text |
| `RadioButton` | `android.widget.RadioButton` | `click()`, `isChecked()`, `getText()` | checked, text, disabled |
| `RadioGroup` | `android.widget.RadioGroup` | `getAll()`, `clickByText(String)`, `clickByIndex(int)`, `getChecked()` | — (see warning) |
| `Switch` | `android.widget.Switch` | `turnOn()`, `turnOff()`, `isOn()`, `getText()` | on/off, text |
| `ToggleButton` | `android.widget.ToggleButton` | `turnOn()`, `turnOff()`, `isOn()`, `getText()` | on/off, text |
| `ComboBox` | `android.widget.Spinner` | `selectByText(String)`, `getText()` | text, disabled |
| `SeekBar` | `android.widget.SeekBar` | `set(Number percentage)` | disabled |
| `Progress` | `android.widget.ProgressBar` | `isDisabled()` | disabled |
| `Image` | `android.widget.ImageView` | — | — |
| `Grid` / `Tabs` | grid / tab strip | `getAll(T.class, searchClass)` | — (see warning) |
| `AndroidComponent` | any container | `createByXPath`, `createByName`, `createByTag`, `createByImage` (+ `createAllBy…`) | — |

## iOS Components (`solutions.bellatrix.ios.components`)

| Component | Native type | Key methods | Validations |
|-----------|------------|-------------|-------------|
| `Button` | `XCUIElementTypeButton`, `XCUIElementTypeCell` | `click()`, `getText()`, `isDisabled()` | text, disabled |
| `ImageButton` | `XCUIElementTypeButton` (icon) | `click()`, `getText()` | text, disabled |
| `TextField` | `XCUIElementTypeTextField` | `setText(String)`, `getText()` | text, disabled |
| `PasswordField` | `XCUIElementTypeSecureTextField` | `setPassword(String)`, `getPassword()` | disabled |
| `NumberInput` | `XCUIElementTypeTextField` (numeric) | `setNumber(Number)`, `getNumber()` (reads `value`) | number, disabled |
| `Label` | `XCUIElementTypeStaticText` | `getText()` | text — NO `click()` |
| `CheckBox` | — | `check()`, `uncheck()`, `isChecked()` | see the iOS state warning |
| `RadioButton` | custom radio control | `click()`, `isChecked()`, `getText()` | text, disabled (checked: see the iOS state warning) |
| `RadioGroup` | container of `XCUIElementTypeRadioButton` | `getAll()`, `clickByText(String)`, `clickByIndex(int)`, `getChecked()` | — |
| `ToggleButton` | `XCUIElementTypeSwitch` | `turnOn()`, `turnOff()`, `isOn()` | see the iOS state warning |
| `ComboBox` | picker / pop-up button | `selectByText(String)` (taps, then the child whose `value` contains the text), `getText()` | text, disabled |
| `SeekBar` | `XCUIElementTypeSlider` | `set(Number percentage)` | disabled |
| `Progress` | `XCUIElementTypeActivityIndicator` / `XCUIElementTypeProgressIndicator` | `isDisabled()` | disabled |
| `Image` | `XCUIElementTypeImage` | — | — |
| `Grid` / `Tabs` | collection / tab bar | `getAll(T.class, searchClass)` | — |
| `IOSComponent` | any container | `createByXPath`, `createByName`, `createByTag`, `createByValueContaining`, `createByImage` (+ `createAllBy…`) | — |

iOS has **no `Switch`** class — `XCUIElementTypeSwitch` is a `ToggleButton` (or a `Button`, see below).

**Tapping rule:** `Label` has no `click()`. A tappable row, cell, or text link is created as `Button`
(or a custom component extending `Button`).

### Framework limitations you MUST respect (verified in BELLATRIX-Java source)

- **No visibility API.** Neither platform has `isVisible()` / `isPresent()` / `validateIsVisible()`.
  Visible: `x.toBeVisible().waitToBe()` (throws on timeout). Gone: see `bellatrix-mobile-locators-and-waits`.
- **`isDisabled()` is unreliable.** It reads an attribute named `disabled`; UiAutomator2 and XCUITest
  expose `enabled` instead, and `CheckBox` / `Switch` / `ToggleButton` hard-code `false`. Read
  `getAttribute("enabled")` (confirm the value live with `get-attribute --attribute enabled`).
- **iOS check/switch state.** iOS `CheckBox.isChecked()` and `ToggleButton.isOn()` read a `checked`
  attribute that XCUITest does not expose (switch state is `value` = `"1"` / `"0"`). So `check()` /
  `turnOn()` tap every time (an ON switch turns OFF), `uncheck()` / `turnOff()` never tap, and the
  checked/on validations cannot pass. On iOS create the switch as `Button`, read
  `getAttribute("value")`, and `click()` only when the state must change.
- **Android scoped built-ins crash.** Android `RadioGroup.getAll/clickByText/clickByIndex`,
  `Grid/Tabs.getAll`, `ComboBox.selectByText` (when the value differs), and the empty-text fallbacks of
  `ComboBox.getText` / `NumberInput.getNumber` look up children with strategies that return `null`
  inside a parent → `NullPointerException`. Tap the spinner / radio buttons as top-level `Button`s
  (`app().create().byText(Button.class, "Express")`) or use the custom `ComboBox` override in
  `bellatrix-mobile-generate-code`. `RadioGroup.getChecked()` uses the absolute XPath
  `//*[@checked='true']`, so it returns the first checked radio of the whole screen.
- **Number arguments.** `SeekBar.set(...)` casts its `Number` to `double` — pass `50.0`, an `int`
  throws `ClassCastException`. `validateNumberIs(...)` compares a `Double` with `equals` — pass `3.0`,
  never `3`. `setNumber(3)` types `"3"`; `setNumber(3.0)` types `"3.0"`.

---

## Creating Components

### Single component (`app().create()` in tests, `create()` in a `Map`)

The class token comes FIRST, then the locator value.

```java
// ---- Android (import solutions.bellatrix.android.components.*;) ----
app().create().byDescription(Button.class, "Log in")                          // content-desc — PREFERRED
app().create().byId(TextField.class, "com.example.shop:id/username")          // FULL resource-id
app().create().byIdContaining(Button.class, "login")                          // resource-id fragment
app().create().byText(Label.class, "Order placed")                            // exact text
app().create().byTextContaining(Label.class, "placed")
app().create().byDescriptionContaining(Button.class, "Log")
app().create().byAndroidUIAutomator(Button.class, "new UiSelector().text(\"Log in\")")
app().create().byClass(SeekBar.class, "android.widget.SeekBar")
app().create().byXPath(Button.class, "//android.widget.Button[@text='Log in']")

// ---- iOS (import solutions.bellatrix.ios.components.*;) ----
app().create().byAccessibilityIdContaining(Button.class, "loginButton")      // accessibility id — PREFERRED (exact match despite the name)
app().create().byId(TextField.class, "usernameField")
app().create().byName(Button.class, "Log in")
app().create().byIOSNsPredicate(PasswordField.class, "type == \"XCUIElementTypeSecureTextField\"")
app().create().byIOSClassChain(Button.class, "**/XCUIElementTypeButton[`name == \"loginButton\"`]")
app().create().byValueContaining(Label.class, "Total")
app().create().byClass(TextField.class, "XCUIElementTypeTextField")
app().create().byXPath(Button.class, "//XCUIElementTypeButton[@name='loginButton']")
```

Android has **no** `byAccessibilityId` (content-desc → `byDescription`). iOS has **no** `byText`, no
`byIdContaining`, no `byDescription`, and no single `byAccessibilityId` — the single accessibility-id
factory is `byAccessibilityIdContaining` (it uses `AppiumBy.accessibilityId`, an exact match).

### Collections (`allBy…` → `java.util.List<T>`)

```java
List<Label> rows = app().create().allById(Label.class, "com.example.shop:id/row_title");          // Android
List<Button> cells = app().create().allByIOSNsPredicate(Button.class, "type == \"XCUIElementTypeCell\""); // iOS

int count = rows.size();
Label first = rows.get(0);
for (Label row : rows) {
    row.toBeVisible().waitToBe();
}
```

### Nested (child) components

Child factories exist only for XPath, name, tag, and image (iOS also value-containing). On iOS,
`createById` / `createByAccessibilityId` / `createByIOSNsPredicate` / `createByIOSClassChain` compile, but
their child lookup returns `null` at runtime (the first action throws) — never use them. Use a **relative** XPath (`.//…`):

```java
// Android
AndroidComponent products = app().create().byId(AndroidComponent.class, "com.example.shop:id/products");
Button pixel = products.createByXPath(Button.class, ".//android.widget.TextView[@text='Pixel 9']");
List<Label> titles = products.createAllByXPath(Label.class, ".//android.widget.TextView");

// iOS
IOSComponent table = app().create().byIOSNsPredicate(IOSComponent.class, "type == \"XCUIElementTypeTable\"");
Button cell = table.createByXPath(Button.class, ".//XCUIElementTypeCell[@name='row_pixel9']");
```

Wait strategies on a child are evaluated against the whole screen (the parent is ignored), so make the
child locator unique on its own.

### MANDATORY: Use BELLATRIX Factories — Never Drop to the Raw Driver

Inside page objects, actions, sections, custom components, and tests, ALWAYS locate elements through
`create().by…` / `allBy…` or a parent's `createBy…`. **Never** call
`getWrappedDriver().findElement(...)`, `AppiumBy.*`, or Selenium `By` locators in project code.

**Why:** factory-created components carry wait strategies, validations, BDD logging, and screenshots on
failure. Raw lookups bypass all of it.

```java
// WRONG — raw Appium bypasses BELLATRIX
var cells = app().appService().getWrappedIOSDriver().findElements(AppiumBy.accessibilityId("row"));

// CORRECT — BELLATRIX collection (iOS)
List<Button> rows = app().create().allByAccessibilityId(Button.class, "row");
```

Raw driver access is acceptable only for capabilities BELLATRIX does not expose — never to find or tap
elements.

### Custom Components

Extend a built-in component when you need control-level behavior it lacks (see
`bellatrix-mobile-generate-code` → Custom Components):

```java
package com.myproject.mobiletests.android.components;

import solutions.bellatrix.android.components.Button;

public class TabButton extends Button {
    public boolean isSelectedTab() {
        return "true".equals(getAttribute("selected"));
    }

    public void open() {
        if (!isSelectedTab()) {
            click();
        }
    }
}
```

---

## Base Component Members (all components)

```java
component.getAttribute("content-desc")   // Android raw attribute ("checked", "enabled", "selected", "resource-id", …)
component.getAttribute("value")          // iOS raw attribute ("label", "name", "enabled", …)
component.getLocation()                  // java.awt.Point — use .x / .y (ints)
component.getSize()                      // java.awt.Dimension — use .width / .height (ints)
component.getComponentName()             // "Button (resource-id = …)" for logs
component.hover()
component.waitToBe()                     // locate now, applying the queued toBe…() waits
component.getWrappedElement()            // underlying WebElement (diagnostics only)
```

`getLocation()` returns `java.awt.Point`, whose `getX()` returns `double`; pass `p.x` / `p.y` to
methods that take `int`.
