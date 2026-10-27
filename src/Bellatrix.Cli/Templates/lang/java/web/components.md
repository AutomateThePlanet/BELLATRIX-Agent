<!-- lang:catalog -->
## Component Hierarchy (Java)

All web components extend `solutions.bellatrix.web.components.WebComponent` (a concrete class — usable directly for any element without a dedicated type) and live in package `solutions.bellatrix.web.components` — one `import solutions.bellatrix.web.components.*;` covers every basic component type. `Table` and `TableCell` live in `solutions.bellatrix.web.components.advanced.table` (import both explicitly), and `CssStyle` (for `validateStyle(CssStyle, String)`) in `solutions.bellatrix.web.components.enums`. `validate*` methods are `default` methods of the contract interfaces in `solutions.bellatrix.web.components.contracts` that each component implements, so they need no extra import.

Members are **camelCase methods**, never properties: `getText()`, `getValue()`, `isDisabled()`, `isChecked()`.

## Web Components

### Common Controls

| Component | HTML | Key Methods |
|-----------|------|-------------|
| `Button` | `button`, `input type=submit/button`, any clickable element | `click()`, `hover()`, `getText()`, `getValue()`, `isDisabled()` |
| `Reset` | `input type=reset` | `click()`, `getText()`, `getValue()`, `isDisabled()` |
| `Anchor` | `a` | `click()`, `hover()`, `getHref()`, `getText()`, `getHtml()`, `getTarget()`, `getRel()` |
| `TextInput` | `input type=text` (C# `TextField`) | `setText(String)`, `getText()` (falls back to `value`), `getValue()`, `getPlaceholder()`, `isDisabled()`, `isReadonly()`, `isRequired()`, `getMaxLength()`, `getMinLength()` |
| `TextArea` | `textarea` | `setText(String)`, `getText()`, `getValue()`, `getPlaceholder()`, `isDisabled()`, `isReadonly()`, `isRequired()`, `getRows()`, `getCols()` |
| `CheckBox` | `input type=checkbox` | `check()`, `check(boolean)`, `uncheck()`, `isChecked()`, `isDisabled()`, `getValue()` |
| `RadioButton` | `input type=radio` | `click()`, `isChecked()`, `isDisabled()`, `getValue()` |
| `Select` | `select` | `selectByText(String)`, `selectByIndex(int)`, `getSelected()` → `Option`, `getAllOptions()` → `List<Option>`, `isDisabled()`, `isRequired()`, `isReadonly()` |
| `Option` | `option` | `getText()`, `getValue()`, `isSelected()`, `isDisabled()` |
| `Label` | `label` | `getText()`, `getHtml()`, `getFor()` |
| `Heading` | `h1`–`h6` | `getText()` |
| `Span` | `span` | `getText()`, `getHtml()` |
| `Div` | `div` and other text containers (`p`, `li`, `section`, `td`, `form`) | `getText()`, `getHtml()` |
| `Image` | `img` | `click()`, `getSrc()`, `getAlt()`, `getHeight()`, `getWidth()`, `getSrcSet()`, `getSizes()` |
| `FileInput` | `input type=file` (C# `InputFile`) | `upload(String filePath)`, `isRequired()`, `isMultiple()`, `getAccept()` |

### HTML5 Input Controls

| Component | HTML | Key Methods |
|-----------|------|-------------|
| `PasswordInput` | `input type=password` (C# `Password`) | `setPassword(String)`, `getPassword()`, `getPlaceholder()`, `isDisabled()`, `isRequired()` |
| `EmailInput` | `input type=email` (C# `Email`) | `setEmail(String)`, `getEmail()`, `getPlaceholder()`, `isRequired()` |
| `PhoneInput` | `input type=tel` (C# `Phone`) | `setPhone(String)`, `getPhone()`, `getPlaceholder()` |
| `SearchField` | `input type=search` (C# `Search`) | `setSearch(String)`, `getSearch()`, `getPlaceholder()` |
| `UrlField` | `input type=url` (C# `Url`) | `setUrl(String)`, `getUrl()`, `getPlaceholder()` |
| `NumberInput` | `input type=number` (C# `Number`) | `setNumber(double)`, `getNumber()`, `getMax()`, `getMin()`, `getStep()` |
| `Range` | `input type=range` | `setRange(Number)`, `getRange()`, `getMax()`, `getMin()`, `getStep()` |
| `DateInput` | `input type=date` | `setDate(int year, int month, int day)`, `getDate()` |
| `TimeInput` | `input type=time` | `setTime(int hours, int minutes)`, `getTime()` |
| `DateTimeInput` / `MonthInput` / `WeekInput` | `input type=datetime-local/month/week` | `setTime(LocalDateTime)` / `setMonth(int year, int month)` / `setWeek(int year, int week)` |
| `ColorInput` | `input type=color` | `setColor(String)`, `getColor()` |
| `Output` / `Progress` | `output` / `progress` | `Output`: `getText()`, `getHtml()`, `getFor()`; `Progress`: `getValue()`, `getMax()`, `getText()` |

### Structural Components

| Component | HTML | Notes |
|-----------|------|-------|
| `Frame` | `iframe` | Switch into it with `app().browser().switchToFrame(frame)`, back with `app().browser().switchToDefault()` |
| `Table` (`solutions.bellatrix.web.components.advanced.table.Table`) | `table` | `getRows()`, `getRow(int)`, `getRowsCount()`, `getCell(String header, int row)`, `getHeaderNames()`, `getColumn(String header)`; `TableCell` (`solutions.bellatrix.web.components.advanced.table.TableCell`) has `getText()` and `validateTextIs(…)` |
| `WebComponent` | anything | The concrete base — use it for elements you only hover, wait for, or check visibility of |

### There Is No…

`TextField`, `Password`, `Email`, `Phone`, `Search`, `Url`, `Number`, `InputFile` (the C# names), nor `H1`, `ListItem`, `Paragraph`, `ComboBox`, `Link`, `Checkbox` (lowercase b), `Dropdown`, `Toast`, or `ScrollingButton` in BELLATRIX Java web. Use `TextInput`, `PasswordInput`, `EmailInput`, `PhoneInput`, `SearchField`, `UrlField`, `NumberInput`, `FileInput`, `Heading`, `Div`/`Span`, `Select`, `Anchor`, `CheckBox`, or a custom component.

### Only Some Components Can Be Clicked

Only `Button`, `Reset`, `Anchor`, `Image`, `RadioButton`, and `ActionImage` have `click()`. `Div`, `Span`, `Label`, `Heading`, `Option`, `TableCell`, and the text inputs have **no `click()`** — a compile error. To click a `div`/`span`/`li` that acts as a button (role `button`, `tab`, `menuitem`, custom checkbox), type it as `Button` — any element can be wrapped as a `Button` — or write a custom component (below). There is also **no `clear()`** on any web component: `setText(…)` already clears the field before typing.

---

<!-- lang:creating -->
## Creating Components (Java)

Factories live on `ComponentCreateService` (`solutions.bellatrix.web.services`), reached as `app().create()` in tests and as `create()` inside `PageMap`, `WebPage`, and `WebSection`. The **component class comes first**, the locator value second.

### Single Component

```java
// data-testid (PREFERRED) — there is no byDataTestId
app().create().byCss(Button.class, "[data-testid='login-submit']");

// Static id
app().create().byId(TextInput.class, "email");

// name attribute (Java HAS byName)
app().create().byName(TextInput.class, "username");

// Text / structural (capital "P" in byXPath)
app().create().byXPath(Button.class, "//button[normalize-space()='Log In']");

// Links
app().create().byLinkText(Anchor.class, "Sign in");

// Attribute / text / class / value fragments
app().create().byAttributeContaining(Anchor.class, "data-product_id", "28");
app().create().byInnerTextContaining(Heading.class, "Billing details");
app().create().byClassContaining(Div.class, "woocommerce-message");
app().create().byValueContaining(Button.class, "Update cart");
```

Creating a component does not touch the browser — the element is located (and re-located) lazily on each use. See `bellatrix-web-locators-and-waits` for the full factory table and which locator to prefer.

### Component Collections (`allBy*`)

```java
// Returns java.util.List<T>
List<NumberInput> quantityBoxes = app().create().allByClassContaining(NumberInput.class, "input-text qty text");
List<Anchor> navLinks = app().create().allByCss(Anchor.class, "nav a");

int count = quantityBoxes.size();
NumberInput first = quantityBoxes.get(0);
quantityBoxes.forEach(q -> q.setNumber(1));
for (Anchor link : navLinks) {
    link.validateIsVisible();
}

List<String> linkTexts = navLinks.stream().map(Anchor::getText).toList();
```

**`allBy*` is eager and does NOT wait:** it queries the DOM once, at call time, and returns an empty list if the elements have not rendered yet. Wait for the first item (or the container) before collecting: `app().create().byCss(Div.class, "[data-testid='product-card']").toBeVisible().waitToBe();`.

### Nested (Scoped) Component Creation

Every factory is also available on a component with a `create` prefix, searching only inside it:

```java
Div cartRow = app().create().byXPath(Div.class, "//tr[td[normalize-space()='Falcon 9']]");
Anchor removeLink = cartRow.createByCss(Anchor.class, "a.remove");
Span priceCell = cartRow.createByXPath(Span.class, ".//td[@class='product-price']//span");   // ".//" = relative
removeLink.click();
```

Use `.//` at the start of a scoped XPath — a plain `//` searches the whole document again. The same trap is built into `createByInnerTextContaining` and `createByLinkTextContaining`: they generate `//*[contains(text(), …)]` / `//a[contains(text(), …)]`, which are document-wide even when called on a parent — scope those with `createByXPath(T.class, ".//*[contains(normalize-space(),'…')]")` instead. A scoped `createBy…` locates the **parent** immediately (eager), the child lazily.

### MANDATORY: Use BELLATRIX Factories — Never Drop to the Raw Driver

Inside page objects, sections, tests, and custom components, ALWAYS locate elements through `create().by…` / `allBy…` or the scoped `createBy…` factories. **Never** call `getWrappedDriver().findElement(s)(By.…)` or build raw Selenium `By` locators in test-project code.

**Why:** factory-returned components carry wait strategies, `validate*` methods, BDD logging, highlighting, screenshots on failure, and the click retry pipeline (scroll + JS-click fallback). Raw `findElements` bypasses all of it.

#### Anti-pattern (DO NOT DO THIS)

```java
// WRONG — raw Selenium bypasses BELLATRIX
var rows = app().browser().getWrappedDriver().findElements(By.cssSelector("tr.cart_item"));
rows.get(0).findElement(By.cssSelector("a.remove")).click();
```

#### Correct — BELLATRIX factories

```java
List<Anchor> removeLinks = app().create().allByCss(Anchor.class, "tr.cart_item a.remove");
if (!removeLinks.isEmpty()) {
    removeLinks.get(0).click();
}
```

#### Inside custom components (no `app()` in scope)

A custom component extending `WebComponent` has the scoped `createBy…` factories and the protected `componentCreateService` field (also `getComponentCreateService()`) for page-wide lookups:

```java
package myproject.webtests.components;

import java.util.List;
import solutions.bellatrix.web.components.*;

public class ProductCard extends WebComponent {
    // scoped to this card
    public Anchor addToCartButton() {
        return createByCss(Anchor.class, "a.add_to_cart_button");
    }

    public Heading title() {
        return createByTag(Heading.class, "h2");
    }

    // page-wide lookup from inside a component
    public List<Anchor> allAddToCartButtons() {
        return componentCreateService.allByCss(Anchor.class, "a.add_to_cart_button");
    }
}
```

#### The only times raw driver access is acceptable

Genuinely low-level operations BELLATRIX Java does not expose — and even then never to find or click elements. Prefer the services first: `app().browser()` (tabs, frames, local/session storage, console logs), `app().script()` (JavaScript), `app().dialogs()` (native alerts), `app().cookies()`. **BELLATRIX Java has no Interactions service** (no drag-and-drop / key-chord API): put Selenium `Actions` inside a custom component method, acting on the component's own `getWrappedElement()` (see `bellatrix-web-app-services`).

---

## Base Component Members (available on all components)

```java
// Visibility — a one-shot read, no retry (use validate*/toBe* for waiting)
component.isVisible();          // boolean — false if not displayed; a MISSING element first blocks up to elementWaitTimeout, then returns false

// Identity and state
component.getTagName();         // e.g. "button"
component.getHtmlClass();       // class attribute
component.getAttribute("aria-expanded");
component.getCssValue("color");
component.getTitle();           // title attribute
component.getComponentName();   // logging name, e.g. "Button (css = …)"

// Geometry
component.getLocation();        // java.awt.Point
component.getSize();            // java.awt.Dimension

// Actions available on every component
component.scrollToVisible();
component.focus();
component.hover();
component.highlight();
component.setAttribute("value", "x");   // DOM attribute via JavaScript — test setup only
component.waitToBe();                   // force the queued waits now

// Re-type an element (same locator, other component class)
Button asButton = component.as(Button.class);
```

There is no `isPresent()`, `hide()`, or `takeScreenshot()` on a Java web component; screenshots on failure come from the `WebScreenshotPlugin` (`webSettings.screenshotsOnFailEnabled`).

---

## Custom Components

Add a custom component under the `components` package when a built-in is missing behavior or behaves wrongly for a specific widget. Never modify BELLATRIX source. A custom component needs a public no-arg constructor (the factories instantiate it reflectively) — the implicit one is enough.

### Option A — New component extending `WebComponent`

```java
package myproject.webtests.components;

import solutions.bellatrix.web.components.Button;
import solutions.bellatrix.web.components.WebComponent;
import solutions.bellatrix.web.components.contracts.ComponentText;

// Implementing ComponentText makes validateTextIs/Contains/NotContains/IsSet available on it
public class Toast extends WebComponent implements ComponentText {
    @Override
    public String getText() {
        return getWrappedElement().getText();
    }

    public boolean isError() {
        return getHtmlClass().contains("toast-error");
    }

    public void dismiss() {
        createByCss(Button.class, "button[aria-label='Close']").click();
    }
}
```

### Option B — Child component overriding a built-in

```java
package myproject.webtests.components;

import solutions.bellatrix.web.components.Button;

public class ScrollingButton extends Button {
    // Sticky headers sometimes cover the button — scroll it into view first
    @Override
    public void click() {
        scrollToVisible();
        super.click();
    }
}
```

### Using custom components in Map classes

```java
import myproject.webtests.components.ScrollingButton;
import myproject.webtests.components.Toast;

public Toast successToast() {
    return create().byCss(Toast.class, "[data-testid='toast']");
}

public ScrollingButton placeOrderButton() {
    return create().byId(ScrollingButton.class, "place_order");
}
```

Rules: one file per component, filename = class name; keep components control-level (business workflow stays in the page class); reuse them across pages.
