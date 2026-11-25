---
name: bellatrix-mobile-components
description: Reference skill for BELLATRIX mobile component types (Android and iOS), native class mapping, methods, properties, and creation patterns.
---

# BELLATRIX Mobile Components Reference

> **Project settings:** run `bellatrix config validate --json` and read `.bellatrix/config.json` before anything else.
> `{project.*}` below are the values of `domains.mobile.project` there (`{project.dir}` = the folder of
> `{project.path}`; `{project.rootPath}` = `{project.rootNamespace}` with `/` instead of `.`), and `{testCasesDir}` is
> `domains.mobile.testCasesDir`, else `testCasesDir`. If the file is missing or invalid, or has no mobile project,
> **stop and ask the user**, record the answer there and validate again. Never guess a project name or path.

A BELLATRIX component wraps one native Appium element and adds waits, validations, logging, and
retry. Pick the component from the element's **native class**, which the snapshot reports as `Class`
on Android and `Type` on iOS.

## Native Control Classes

| Role | Android `Class` (UiAutomator2) | iOS `Type` (XCUITest) |
|------|-------------------------------|------------------------|
| Button | `android.widget.Button` | `XCUIElementTypeButton` |
| Icon button | `android.widget.ImageButton` | `XCUIElementTypeButton` (no label text) |
| Text input | `android.widget.EditText` | `XCUIElementTypeTextField` |
| Password input | `android.widget.EditText` (password flag) | `XCUIElementTypeSecureTextField` |
| Static text | `android.widget.TextView` | `XCUIElementTypeStaticText` |
| Checkbox | `android.widget.CheckBox` | `XCUIElementTypeSwitch` (iOS has no native checkbox) |
| Radio button / group | `android.widget.RadioButton` / `android.widget.RadioGroup` | — (custom controls) |
| Switch / toggle | `android.widget.Switch`, `android.widget.ToggleButton` | `XCUIElementTypeSwitch` |
| Drop-down / picker | `android.widget.Spinner` | `XCUIElementTypePickerWheel` / pop-up button |
| Slider | `android.widget.SeekBar` | `XCUIElementTypeSlider` |
| Image | `android.widget.ImageView` | `XCUIElementTypeImage` |
| Progress | `android.widget.ProgressBar` | `XCUIElementTypeActivityIndicator` / `XCUIElementTypeProgressIndicator` |
| List / row | `androidx.recyclerview.widget.RecyclerView` / clickable row layout | `XCUIElementTypeTable`, `XCUIElementTypeCollectionView` / `XCUIElementTypeCell` |

`bellatrix mobile find --snapshot <path> --interactive-only` keeps only actionable classes (buttons,
fields, checkboxes, switches, spinners, radio buttons, seek bars, cells) — use it to shrink what enters
context.

## Interacting Live (CLI)

| Intent | CLI verb (exploration) | Notes |
|--------|------------------------|-------|
| Tap | `click` / `tap` | buttons, rows, cells, switches, tabs |
| Enter text | `type-text --text "…"` | clears the field first |
| Clear | `clear` | |
| Read text | `get-text` | visible text of the element |
| Read attribute | `get-attribute --attribute <name>` | Android `checked`/`enabled`/`content-desc`; iOS `value`/`label` |
| Visible / enabled | `is-visible` / `is-enabled` | |
| Reveal off-screen | `scroll-to-element` / scrolling UiSelector | the element must exist in the hierarchy for `scroll-to-element` |
| Gesture | `swipe --start-x … --start-y … --end-x … --end-y …` | raw coordinate swipe |

Always resolve a locator from a fresh snapshot, interact, then re-snapshot after the UI changes. The
component member that reproduces each verb in generated code is listed below.

---

{{> lang}}
