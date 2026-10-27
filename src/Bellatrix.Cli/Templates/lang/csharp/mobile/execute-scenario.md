<!-- lang:component-mapping -->
## Native Class → BELLATRIX Component Mapping

Components live in `Bellatrix.Mobile.Android` (Android) and `Bellatrix.Mobile.IOS` (iOS). The type
names are the same on both platforms but they are **different classes** — never import both namespaces
in one file.

| Android `Class` | iOS `Type` | BELLATRIX Component | Notes |
|-----------------|-----------|---------------------|-------|
| `android.widget.Button` | `XCUIElementTypeButton` | `Button` | `Click()`, `GetText()` |
| `android.widget.ImageButton` | `XCUIElementTypeButton` (icon only) | `ImageButton` | `Click()` |
| `android.widget.EditText` | `XCUIElementTypeTextField` | `TextField` | `SetText()`, `GetText()` |
| `android.widget.EditText` (password) | `XCUIElementTypeSecureTextField` | `Password` | `SetPassword()` |
| `android.widget.EditText` (numeric) | `XCUIElementTypeTextField` (numeric) | `Number` | `SetNumber(int)` |
| `android.widget.TextView` | `XCUIElementTypeStaticText` | `Label` | read-only — `GetText()`; NO `Click()` |
| `android.widget.CheckBox` | `XCUIElementTypeSwitch` used as a check | `CheckBox` | `Check()`, `Uncheck()` |
| `android.widget.RadioButton` | — | `RadioButton` | `Click()`, `IsChecked` |
| `android.widget.RadioGroup` | — | `RadioGroup` | `ClickByText()`, `GetChecked()` |
| `android.widget.Switch` | `XCUIElementTypeSwitch` | `Switch` (Android) / `ToggleButton` (iOS) | `TurnOn()`, `TurnOff()` |
| `android.widget.ToggleButton` | — | `ToggleButton` | `TurnOn()`, `TurnOff()` |
| `android.widget.Spinner` | `XCUIElementTypePickerWheel` / picker | `ComboBox` | `SelectByText()` |
| `android.widget.SeekBar` | `XCUIElementTypeSlider` | `SeekBar` | `Set(double)` |
| `android.widget.ImageView` | `XCUIElementTypeImage` | `Image` | visibility only |
| `android.widget.ProgressBar` | — | `Progress` (Android only) | visibility only |
| clickable row (`android.widget.LinearLayout`, `android.view.ViewGroup`) | `XCUIElementTypeCell` | `Button` | rows must be tapped — use `Button`, not `Label` |
| container (`androidx.recyclerview.widget.RecyclerView`, `android.widget.ScrollView`) | `XCUIElementTypeTable` / `XCUIElementTypeCollectionView` | `AndroidComponent` / `IOSComponent` | parent for nested `Create*` |

`ComboBox.SelectByText` taps the control, then taps the option whose text (Android) / value (iOS)
contains the requested string — confirm during execution that the option list behaves that way.

## Validated Strategy → BELLATRIX Factory

Record which factory each validated locator will become, so code generation is mechanical:

| CLI `--strategy` | Android (`App.Components.…`) | iOS (`App.Components.…`) |
|------------------|------------------------------|--------------------------|
| `accessibilityid` | `CreateByDescription<T>(contentDesc)` | `CreateById<T>(name)` (or `CreateByName<T>`) |
| `id` | `CreateById<T>("com.app:id/x")` — full resource-id | `CreateById<T>(value)` |
| `androiduiautomator` | `CreateByAndroidUIAutomator<T>(selector)` | — |
| `iospredicate` | — | `CreateByIOSNsPredicate<T>(predicate)` |
| `iosclasschain` | — | no factory — re-express as `iospredicate` (or XPath) and re-validate with the CLI |
| `classname` | `CreateByClass<T>(class)` | `CreateByClass<T>(type)` |
| `xpath` | `CreateByXPath<T>(xpath)` | `CreateByXPath<T>(xpath)` |
| (exact `Text` from snapshot) | `CreateByText<T>(text)` | `CreateByIOSNsPredicate<T>("label == \"…\"")` |

`App.Components.CreateByAccessibilityId<T>` does **not** exist on either platform (on iOS it exists only
as a nested factory on a parent component; Android has no accessibility-id factory — use
`CreateByDescription`).

---

