# BELLATRIX Desktop Components Reference (Java)

## Component Hierarchy

All desktop components extend `solutions.bellatrix.desktop.components.DesktopComponent` (a concrete class — usable
directly for any element without a dedicated type). Every component lives in package
`solutions.bellatrix.desktop.components`. Members are **camelCase methods** (`getText()`, `isDisabled()`), never
properties.

## Desktop Components

| Component | WinAppDriver ControlType | Key Methods |
|-----------|-------------------------|-------------|
| `Button` | Button | `click()`, `getText()`, `isDisabled()` |
| `TextField` | Edit | `setText(String)`, `getText()`, `isDisabled()` |
| `TextArea` | Edit / Document | `setText(String)`, `getText()`, `isDisabled()` |
| `PasswordField` | Edit (IsPassword=true) | `setPassword(String)`, `getPassword()` |
| `ComboBox` | ComboBox | `selectByText(String)`, `getText()`, `isDisabled()` |
| `CheckBox` | CheckBox | `check()`, `uncheck()`, `isChecked()`, `isDisabled()` |
| `RadioButton` | RadioButton | `click()`, `isChecked()`, `isDisabled()` |
| `Label` | Text | `getText()` |
| `Anchor` | Hyperlink | `click()`, `getText()` |
| `Expander` | Expander | `click()`, `isDisabled()` |
| `ListBox` | List | `isDisabled()` |
| `DateInput` | DatePicker | `setDate(int year, int month, int day)`, `getDate()` |
| `TimeInput` | TimePicker | `setTime(int hours, int minutes)`, `getTime()` |
| `Tabs` | Tab | — |
| `Menu` | Menu | — |
| `Progress` | ProgressBar | — |
| `Image` | Image | — |
| `DesktopComponent` | any | base methods below |

Every component also has `hover()` and the base methods listed at the end of this file.

**No Java class exists for:** `ListItem`, `TabItem`, `MenuItem`, `TreeItem`, `Calendar`, `Window`, `Password`, `Date`,
`Time`. Map clickable items (ListItem, TabItem, MenuItem, TreeItem, DataItem) as `Button` (gives `click()` +
`getText()`), read-only text as `Label`, containers/anything else as `DesktopComponent`. The C# names `Password`,
`Date`, `Time` are `PasswordField`, `DateInput`, `TimeInput` in Java.

**Missing actions (compile errors if you call them):** no `clear()` on any component (`setText` already clears the
field; `setText("")` empties it), no `scrollToVisible()`, no `click()` on `Label`/`Menu`/`Tabs`/`ListBox`/`Image`/
`DesktopComponent`, no `isPresent()`.

**Behavior notes (BELLATRIX-Java 1.0 source):**
- `ComboBox.selectByText(text)` types the text into the combo (`sendKeys`) when the current text differs — it does
  not open the dropdown. For WinUI/UWP flyouts that ignore typing, write a custom component (see below).
- `CheckBox.check()` / `uncheck()` / `isChecked()` use the element's selected state.
- `isDisabled()` reads an attribute named `disabled`, not UIA `IsEnabled`; on `PasswordField`, `DateInput` and
  `TimeInput` it always returns `false`. Confirm enabled-state checks against the live app (`bellatrix desktop
  is-enabled`) and fall back to `getAttribute("IsEnabled")` when `isDisabled()` does not reflect it.
- `DateInput.setDate` sends text formatted `MM\yyyy-dd`; `TimeInput.setTime` sends `h:m:00`. If the picker rejects
  that, drive it like the user does (type into its `TextField` part or click the day `Button`s).

---

## Creating Components

The factory is `ComponentCreateService`, reached as `app().create()` in tests, `create()` in a `Map`/page, and
`getComponentCreateService()` inside a custom component. **Class token first, locator second.** There is no `byId` —
use `byAutomationId`.

### Single Component (Factory Methods)

```java
// By AutomationId (PREFERRED - most stable)
app().create().byAutomationId(Button.class, "btnOk");

// By Name
app().create().byName(TextField.class, "Username");

// By ClassName
app().create().byClass(TextField.class, "TextBox");

// By Tag (ControlType)
app().create().byTag(Label.class, "Text");

// By XPath (CRITICAL: PascalCase for WinAppDriver) — through the project's UiaXPathFindStrategy, see below
app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@Name='OK']"));
```

### MANDATORY: XPath goes through `UiaXPathFindStrategy` — never `byXPath` / `allByXPath`

In BELLATRIX-Java 1.0, `XPathFindStrategy.findAllElements(WindowsDriver)` searches with `By.tagName(xpath)` instead
of `By.xpath(xpath)` (`bellatrix.desktop/.../findstrategies/XPathFindStrategy.java`). Every page-level
`create().byXPath(...)` resolves its element through that method, so it passes the wait and then fails to find the
element; `create().allByXPath(...)` silently returns an **empty list**. The fix is a 20-line find strategy you add
**once** to the test project (`{project.rootNamespace}.common`) — the documented BELLATRIX extensibility point:

```java
package {project.rootNamespace}.common;

import io.appium.java_client.windows.WindowsDriver;
import org.openqa.selenium.By;
import org.openqa.selenium.WebElement;
import solutions.bellatrix.desktop.findstrategies.FindStrategy;

import java.util.List;

/** XPath for WinAppDriver: like BELLATRIX's XPathFindStrategy, but By.xpath on every path. */
public class UiaXPathFindStrategy extends FindStrategy {
    public UiaXPathFindStrategy(String xpath) {
        super(xpath);
    }

    @Override
    public WebElement findElement(WindowsDriver driver) {
        return driver.findElement(By.xpath(getValue()));
    }

    @Override
    public List<WebElement> findAllElements(WindowsDriver driver) {
        return driver.findElements(By.xpath(getValue()));
    }

    @Override
    public WebElement findElement(WebElement element) {
        return element.findElement(By.xpath(getValue()));
    }

    @Override
    public List<WebElement> findAllElements(WebElement element) {
        return element.findElements(By.xpath(getValue()));
    }

    @Override
    public String toString() {
        return String.format("xpath = %s", getValue());
    }
}
```

Use it with the generic factory entry points `by(...)` / `allBy(...)`:

```java
Button ok = app().create().by(Button.class, new UiaXPathFindStrategy("//Button[@Name='OK']"));
List<Button> days = app().create().allBy(Button.class, new UiaXPathFindStrategy("//DataItem[@Name='15']"));
```

### Component Collections (`allBy*`)

```java
// Returns java.util.List<T> (import java.util.List explicitly — never java.util.Map in a page package)
List<Button> buttons = app().create().allByName(Button.class, "Submit");
List<Button> items = app().create().allByAutomationId(Button.class, "item");
List<Label> labels = app().create().allByTag(Label.class, "Text");

// Usage
buttons.forEach(Button::click);
int count = items.size();
Button first = items.get(0);
```

Available: `allByAutomationId`, `allByName`, `allByClass`, `allByTag`, `allByAccessibilityId`, `allByIdContaining`,
`allByImage`, and `allBy(T.class, new UiaXPathFindStrategy(...))`.

### MANDATORY: Use BELLATRIX Factories — Never Drop to Raw Driver

When you need to locate elements inside a page object, action method, or **custom component**, ALWAYS go through the
BELLATRIX factories (`create().byAutomationId(...)`, `create().allBy(...)`, the scoped `createBy...` methods).
**Never** call `getWrappedDriver().findElement(s)(By.xpath(...))`, `getWrappedElement().findElement(...)`, or raw
Selenium/Appium `By` locators from test/page code.

**Why:** BELLATRIX factory-returned components carry wait strategies, lifecycle integration, validation methods,
logging, plugins, and screenshots on failure. Raw `driver.findElements` bypasses ALL of that — you lose waits, you
lose validate methods, you lose logs, and you get brittle tests that don't survive minor UI timing changes.

#### Anti-pattern (DO NOT DO THIS)

```java
// WRONG — dropping to raw driver bypasses BELLATRIX entirely
var driver = startingDatePicker.getWrappedDriver();
var cells = driver.findElements(By.xpath(
        "//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='" + day + "']"));
if (!cells.isEmpty()) {
    cells.get(0).click();
}
```

#### Correct — use BELLATRIX factories

```java
// CORRECT — BELLATRIX factory returns a List<Button> with full framework support
List<Button> cells = app().create().allBy(Button.class, new UiaXPathFindStrategy(
        "//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='" + day + "']"));

if (!cells.isEmpty()) {
    cells.get(0).click();
}
```

#### Inside custom components (no `app()` in scope)

Custom components extend `DesktopComponent` (or a concrete component) and have no `app()`. Use the inherited
`getComponentCreateService()` — the same factory as `app().create()`:

```java
package {project.rootNamespace}.components;

import {project.rootNamespace}.common.UiaXPathFindStrategy;
import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.DesktopComponent;

import java.util.List;

public class MonthViewCell extends DesktopComponent {
    public void clickDay(int day) {
        List<Button> cells = getComponentCreateService().allBy(Button.class, new UiaXPathFindStrategy(
                "//Pane[@AutomationId='MonthViewScrollViewer']//DataItem[@Name='" + day + "']"));

        if (!cells.isEmpty()) {
            cells.get(0).click();
        }
    }
}
```

For a **single** child scoped to the current component, use the instance factories on the component itself
(`createByAutomationId`, `createByName`, `createByXPath`, `createByClass`, `createByTag`, `createByIdContaining`,
`createByImage`). Scoped `createByXPath` is fine (it queries `By.xpath` against the parent); start the XPath with `.//`:

```java
Button applyItem = createByXPath(Button.class, ".//ListItem[@Name='Apply']");
```

**Scoped collections are broken in 1.0:** every element of `parent.createAllBy...(...)` resolves to the **first**
match (the child lookup ignores the element index). For a list inside a container, use a page-level
`allBy(T.class, new UiaXPathFindStrategy("//Parent[...]//Child[...]"))` instead. Avoid scoped
`createByAccessibilityId` too (it queries a non-existent `@AccessibilityId` attribute) — use `createByAutomationId`.

#### The only times raw driver access is acceptable

- Genuinely low-level operations BELLATRIX does not expose (e.g., window-handle switching for native popup flyouts,
  key chord sequences via `org.openqa.selenium.interactions.Actions`, session diagnostics). Even then, acquire the
  driver once (`app().appService().getWrappedDriver()` or `getWrappedDriver()` inside a component), do the minimum,
  and return to BELLATRIX components for element interaction
- Never use raw driver to **find or click** elements — use factories

### Nested Component Creation

```java
// Find a child element within a parent component
ComboBox comboBox = app().create().byAutomationId(ComboBox.class, "listBox");
Button item = comboBox.createByAutomationId(Button.class, "lb2");
item.hover();
```

Creating a child eagerly locates the parent (the parent must exist at that moment).

---

## Base Component Methods (Available on All Components)

```java
// Visibility (no isPresent(); an absent element waits for elementToExistTimeout, then returns false)
boolean visible = component.isVisible();

// Location and size
java.awt.Point location = component.getLocation();
java.awt.Dimension size = component.getSize();

// Identity — "Button (automationId btnOk)"
String name = component.getComponentName();

// Attributes (UI Automation property names, PascalCase)
component.getAttribute("Name");
component.getAttribute("AutomationId");
component.getAttribute("ClassName");
component.getAttribute("HelpText");
component.getAttribute("IsEnabled");      // "True" / "False"

// Actions / waiting
component.hover();
component.waitToBe();                     // force element location now (applies queued toBe...() waits)

// Escape hatches (low-level operations only — see the raw-driver rule above)
org.openqa.selenium.WebElement element = component.getWrappedElement();
io.appium.java_client.windows.WindowsDriver driver = component.getWrappedDriver();
```

Presence check without the exist-timeout: `app().create().allByAutomationId(Label.class, "lblError").isEmpty()`.
