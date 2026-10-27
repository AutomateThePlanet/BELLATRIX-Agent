<!-- lang:component-mapping -->
## Native Class → BELLATRIX Component Mapping (Java)

Components live in `solutions.bellatrix.android.components` (Android) and `solutions.bellatrix.ios.components`
(iOS). The class names are the same on both platforms but they are **different classes** — a Java file imports
from ONE platform package only.

| Android `Class` | iOS `Type` | BELLATRIX Component | Notes |
|-----------------|-----------|---------------------|-------|
| `android.widget.Button` | `XCUIElementTypeButton` | `Button` | `click()`, `getText()` |
| `android.widget.ImageButton` | `XCUIElementTypeButton` (icon only) | `ImageButton` | `click()` |
| `android.widget.EditText` | `XCUIElementTypeTextField` | `TextField` | `setText(String)`, `getText()` (iOS reads `value`) |
| `android.widget.EditText` (password) | `XCUIElementTypeSecureTextField` | `PasswordField` | `setPassword(String)` |
| `android.widget.EditText` (numeric) | `XCUIElementTypeTextField` (numeric) | `NumberInput` | `setNumber(Number)`, `getNumber()` → `double` |
| `android.widget.TextView` | `XCUIElementTypeStaticText` | `Label` | read-only — `getText()`; NO `click()` |
| `android.widget.CheckBox` | — | `CheckBox` | `check()`, `uncheck()` (Android) |
| `android.widget.RadioButton` | — | `RadioButton` | `click()`, `isChecked()` |
| `android.widget.RadioGroup` | — | `RadioGroup` (Android: tap the radios as `Button`s instead) | its child lookups crash on Android |
| `android.widget.Switch` | `XCUIElementTypeSwitch` | `Switch` (Android) / `Button` + `getAttribute("value")` (iOS) | `turnOn()`, `turnOff()` on Android; iOS has no `Switch` and `ToggleButton` state does not work |
| `android.widget.ToggleButton` | — | `ToggleButton` | `turnOn()`, `turnOff()` |
| `android.widget.Spinner` | `XCUIElementTypePickerWheel` / pop-up button | `ComboBox` (Android: a custom override, see `bellatrix-mobile-generate-code`) | `selectByText(String)` |
| `android.widget.SeekBar` | `XCUIElementTypeSlider` | `SeekBar` | `set(Number)` — pass a `double` (`50.0`) |
| `android.widget.ImageView` | `XCUIElementTypeImage` | `Image` | visibility only |
| `android.widget.ProgressBar` | `XCUIElementTypeActivityIndicator` | `Progress` | wait for it to disappear |
| clickable row (`android.widget.LinearLayout`, `android.view.ViewGroup`) | `XCUIElementTypeCell` | `Button` | rows must be tapped — use `Button`, not `Label` |
| container (`androidx.recyclerview.widget.RecyclerView`, `android.widget.ScrollView`) | `XCUIElementTypeTable` / `XCUIElementTypeCollectionView` | `AndroidComponent` / `IOSComponent` | parent for nested `createByXPath(T.class, ".//…")` |

Record the platform caveats you hit while executing (spinner options, switch `value`, disabled state from
`get-attribute --attribute enabled`) — the component limitations are listed in `bellatrix-mobile-components`.

## Validated Strategy → BELLATRIX-Java Factory

Record which factory each validated locator will become, so code generation is mechanical (verified against
`solutions.bellatrix.android.services.ComponentCreateService` and `solutions.bellatrix.ios.services.ComponentCreateService`):

| CLI `--strategy` | Android (`app().create().…` / `create().…`) | iOS (`app().create().…` / `create().…`) |
|------------------|---------------------------------------------|-----------------------------------------|
| `accessibilityid` | `byDescription(T.class, contentDesc)` | `byAccessibilityIdContaining(T.class, name)` — exact match despite the name |
| `id` | `byId(T.class, "com.app:id/x")` — full resource-id (`byIdContaining` for a fragment) | `byId(T.class, value)` |
| `androiduiautomator` | `byAndroidUIAutomator(T.class, selector)` | — |
| `iospredicate` | — | `byIOSNsPredicate(T.class, predicate)` |
| `iosclasschain` | — | `byIOSClassChain(T.class, chain)` |
| `classname` | `byClass(T.class, class)` | `byClass(T.class, type)` |
| `xpath` | `byXPath(T.class, xpath)` | `byXPath(T.class, xpath)` |
| (exact `Text` from snapshot) | `byText(T.class, text)` | `byIOSNsPredicate(T.class, "label == \"…\"")` |

Android has no `byAccessibilityId` (content-desc → `byDescription`); iOS has no single `byAccessibilityId`, no
`byText`, no `byDescription`, no `byIdContaining`. Collections use the matching `allBy…` method.

---

