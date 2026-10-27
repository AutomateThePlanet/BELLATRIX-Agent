<!-- lang:catalog -->
## Component Hierarchy

All web components extend `WebComponent<HTMLType>` and are exported from `'@bellatrix/web/components'` — one import line covers every built-in (`import { Button, TextField, … } from '@bellatrix/web/components';`). Every method is `async`: `await` each call.

## Web Components

### Common Controls

| Component | HTML | Key Methods (all `async`) |
|-----------|------|---------------------------|
| `Button` | `button`, `input type=submit/button`, any clickable element | `click()`, `getInnerText()`, `getValue()`, `isDisabled()` |
| `Reset` | `input type=reset` | `click()`, `getInnerText()`, `getValue()`, `isDisabled()` |
| `Anchor` | `a` | `click()`, `getHref()`, `getInnerText()`, `getInnerHtml()`, `getTarget()`, `getRel()` |
| `TextField` | `input type=text` | `setText(text)`, `getValue()`, `getPlaceholder()`, `getMaxLength()`, `getMinLength()` (both read `max`/`min`, see limits below), `isDisabled()`, `isReadonly()`, `isRequired()` — **no `click()`, no `clear()`, no `typeText()`** (clear with `setText('')`) |
| `TextArea` | `textarea` | `setText(text)`, `getValue()` (always `''` with Playwright: read `evaluate(el => el.value)`), `getInnerText()`, `getPlaceholder()`, `getRows()`, `getCols()`, `isReadOnly()` (capital **O**), `isDisabled()`, `isRequired()` |
| `CheckBox` | `input type=checkbox` | `check()`, `uncheck()`, `isChecked()`, `isDisabled()`, `getValue()` |
| `RadioButton` | `input type=radio` | `click()`, `isChecked()`, `isDisabled()`, `getValue()` |
| `Select` | `select` | `selectByText(t)`, `selectByValue(v)`, `selectByIndex(i)`, `isDisabled()`, `isRequired()` — `getSelected()` is typed `Promise<void>` (unimplemented): read the selection with `evaluate` (see `bellatrix-web-validations-and-assertions`) |
| `Option` | `option` | `select()`, `isSelected()`, `isDisabled()`, `getInnerText()`, `getValue()` |
| `Label` | `label` | `getInnerText()`, `getInnerHtml()`, `getFor()` |
| `Heading` | `h1`–`h6` | `getInnerText()` |
| `Span` | `span` | `getInnerText()`, `getInnerHtml()` |
| `Div` | `div` and other text containers (`p`, `li`, `section`, `td`, `form`) | `getInnerText()`, `getInnerHtml()` |
| `Image` | `img` | `getSrc()`, `getAlt()`, `getSrcSet()`, `getSizes()`, `getWidth()`, `getHeight()` |
| `FileInput` | `input type=file` | `upload(filePath)`, `isRequired()`, `isMultiple()`, `getAccept()` |

### HTML5 Input Controls (`@bellatrix/web/components`)

| Component | HTML | Key Methods (all `async`) |
|-----------|------|---------------------------|
| `PasswordField` | `input type=password` | `setPassword(p)`, `getPassword()`, `getValue()`, `getPlaceholder()`, `isRequired()` |
| `EmailField` | `input type=email` | `setEmail(e)`, `getEmail()`, `getValue()`, `getPlaceholder()` |
| `PhoneField` | `input type=tel` | `setPhone(p)`, `getPhone()`, `getPattern()` |
| `SearchField` | `input type=search` | `setSearch(s)`, `getSearch()` |
| `UrlField` | `input type=url` | `setUrl(u)`, `getUrl()` |
| `NumberInput` | `input type=number` | `setNumber(n)`, `getNumber()`, `getMin()`, `getMax()`, `getStep()` |
| `RangeInput` | `input type=range` | `setValue(n)`, `getValue()` (number) |
| `DateInput` | `input type=date` | `setDate(date: Date)`, `getDate()` (`Date \| null`) |
| `TimeInput` | `input type=time` | `setTime(hours, minutes, seconds = 0)`, `getTime()` |
| `DateTimeInput` | `input type=datetime-local` | `setTime(dateTime: Date)`, `getTime()` |
| `MonthInput` / `WeekInput` | `input type=month` / `week` | `setMonth(year, month)` / `setWeek(year, week)` |
| `ColorInput` | `input type=color` | `setColor(c)`, `getColor()` |
| `Output` / `Progress` | `output` / `progress` | `Output`: `getInnerText()`, `getFor()`; `Progress`: `getValue()`, `getMax()` |
| `Frame` | `iframe` | `getName()`, `getHref()` only — there is NO frame switching (see below) |

### Base Members (every component, from `WebComponent`)

`hover()`, `focus()`, `scrollIntoView()`, `getAttribute(name)`, `isPresent()`, `isVisible()`, `isClickable()` (= enabled), `getOuterHtml()`, `evaluate(fn, …args)`, `getShadowRoot()`, `create(T)` (scoped factory), `as(T)` (re-type the same locator), `wait.*` (waits), `validate(…)` / `validateIs(…)` / `validateIsNot(…)` (validations). **The base has no `click()`.**

`getAttribute` takes the `HtmlAttribute` union: standard attribute names, `data-*` and `aria-*` compile; arbitrary strings do not.

### There Is No…

`Table`, `Grid`, `Navigation`, `Submit`, `Cite`, `H1`, `ListItem`, `Paragraph`, `ComboBox`, `Link`, `Checkbox` (lowercase b), `Dropdown`, `Password`, `Number`, `Email` (C# names) in BELLATRIX TypeScript. Use `Heading`, `Div`/`Span`, `Select`, `Anchor`, `CheckBox`, `PasswordField`, `NumberInput`, `EmailField`; model tables with `allBy*` lists and row-scoped XPath.

### Only Some Components Can Be Clicked

Only `Button`, `Anchor`, `RadioButton`, and `Reset` have `click()`. `Div`, `Span`, `Label`, `Heading`, `TextField`, `Image`, `CheckBox`, and `Option` do NOT (TS2339). To click a `div`/`span`/`li` that acts as a button (role `button`, `tab`, `menuitem`, custom checkbox, select2 option), create it as `Button` — `this.create(Button).byXpath("//li[normalize-space()='Germany']")` — or re-type an existing component with `.as(Button)`. For a checkbox use `check()`/`uncheck()`.

### Framework Limits to Know (Playwright controller)

- **Finding waits for VISIBLE.** Every awaited call first locates the element and (with `browserController: 'playwright'`) waits up to `findElementTimeout` for it to be **visible**; with `'selenium'` it waits for presence. Acting on a hidden element (a hidden native `<select>` behind a custom widget, an off-screen template) times out — drive the visible widget instead.
- **No iframe or tab support.** CSS/XPath do not cross `<iframe>` boundaries and there is no frame/tab switching API. Elements inside an iframe cannot be automated; for a link that opens a new tab, read its `href` and `navigate()` to it.
- **Boolean attribute getters are unreliable.** `isDisabled()`, `isReadonly()`/`isReadOnly()`, `isRequired()` return `true` only when the attribute's VALUE is the string `"true"`; with Playwright `<button disabled>` reads as `''` → `false`. Check enabled state with `isClickable()` / `wait.toBeDisabled()`, or read the DOM property: `await field.evaluate(el => el.required)`.
- **`getInnerText()` on inputs is empty, and `getValue()` reads the `value` ATTRIBUTE (Playwright).** `getValue()` and the getters built on it (`getEmail()`, `getNumber()`, …) return the server-rendered `value` attribute, not text typed during the test; a `<textarea>` has no `value` attribute. Read a live value with `await field.evaluate(el => el.value)`.
- **`getMaxLength()` / `getMinLength()` read the `max` / `min` attributes,** not `maxlength` / `minlength` (they return `NaN` for a length limit). Use `await field.evaluate(el => el.maxLength)`.

---

<!-- lang:creating -->
## Creating Components (TypeScript)

### Single Component (factory on `this.create(T)` in maps, `this.app.create(T)` in tests)

```ts
// data-testid (PREFERRED)
this.create(Button).byCss("[data-testid='login-submit']")

// Static id
this.create(TextField).byId('email')

// name attribute
this.create(TextField).byName('username')

// Text / structural (lowercase "p" in Xpath!)
this.create(Button).byXpath("//button[normalize-space()='Log In']")

// Links
this.create(Anchor).byLinkText('Sign in')

// Attribute / text / class fragments
this.create(Anchor).byAttributeContaining('data-product_id', '28')
this.create(Heading).byInnerTextContaining('Billing details')
this.create(Div).byClassContaining('woocommerce-message')
this.create(Button).byValueContaining('Update cart')
```

See `bellatrix-web-locators-and-waits` for the full factory table and which locator to prefer.

### Component Collections (`allBy*`)

```ts
// Returns ComponentsList<T> — NOT an array
const quantityBoxes = this.create(NumberInput).allByClassContaining('input-text qty text');
const navLinks = this.create(Anchor).allByCss('nav a');

const count = await quantityBoxes.count();
const boxes = await quantityBoxes.get();          // NumberInput[]
const first = boxes[0];
for (const box of boxes) {
    await box.setNumber(1);
}

const linkTexts: string[] = [];
for (const link of await navLinks.get()) {
    linkTexts.push(await link.getInnerText());
}
```

Index into the array from `await list.get()` — avoid `await list.get(i)` on a fresh list: it rebuilds the locator with a CSS `:nth-child(i+1)` / XPath `(…)[i+1]` suffix and drops the parent scope of scoped lists.

### Nested (Scoped) Component Creation

Every factory is also available on a component, searching only inside it:

```ts
const cartRow = this.create(Div).byXpath("//tr[td[normalize-space()='Falcon 9']]");
const removeLink = cartRow.create(Anchor).byCss('a.remove');
const priceCell = cartRow.create(Span).byXpath(".//td[@class='product-price']//span");   // ".//" = relative
await removeLink.click();
```

Use `.//` at the start of a scoped XPath — a plain `//` searches the whole document again. `.as(T)` keeps the locator but NOT the parent scope — re-create scoped children with `parent.create(T)` instead.

### Shadow DOM

```ts
const shadowRoot = await this.create(Div).byCss('my-widget').getShadowRoot();
await shadowRoot.create(Button).byCss('button.confirm').click();
```

### MANDATORY: Use BELLATRIX Factories — Never Drop to the Raw Driver

Inside page objects, sections, tests, and custom components, ALWAYS locate elements through `create(T).by*` / `allBy*` (map, app, or scoped component). **Never** resolve `BrowserController` to call `findElement(s)`, never import `@playwright/test` or `selenium-webdriver` in test-project code, and never query the DOM with `document.querySelector` inside `script.execute` to perform the test action.

**Why:** factory-returned components carry lazy finding with the configured wait, stale-element retry, BDD logging hooks, `wait.*`, and `validate*`. Raw driver calls bypass all of it.

#### Anti-pattern (DO NOT DO THIS)

```ts
// WRONG — raw driver / DOM bypasses BELLATRIX
await this.app.script.execute(() => (document.querySelector('tr.cart_item a.remove') as HTMLElement).click());
```

#### Correct — BELLATRIX factories

```ts
const removeLinks = await this.app.create(Anchor).allByCss('tr.cart_item a.remove').get();
if (removeLinks.length > 0) {
    await removeLinks[0].click();
}
```

#### Inside custom components (scoped `this.create`)

A custom component extending `WebComponent` (or a built-in) uses `this.create(T)` for children and `this.wrappedElement` for its own element — and MUST carry `@BellatrixWebComponent`:

```ts
import { Anchor, Heading, WebComponent } from '@bellatrix/web/components';
import { BellatrixWebComponent } from '@bellatrix/web/components/decorators';

@BellatrixWebComponent
export class ProductCard extends WebComponent<HTMLDivElement> {
    // scoped to this card (getters are lazy)
    get addToCartButton() {
        return this.create(Anchor).byCss('a.add_to_cart_button');
    }

    get title() {
        return this.create(Heading).byTag('h2');
    }

    async getTitleText(): Promise<string> {
        return await this.title.getInnerText();
    }
}
```

#### The only times lower-level access is acceptable

Genuinely low-level operations BELLATRIX does not expose — and even then never to find or click elements: `component.evaluate(fn)` / `this.app.script.execute(fn, component)` for DOM properties, scrolling, or submitting a form; `this.app.cookies`; `this.app.dialog`; `this.app.browser.clearLocalStorage()`.

---

## Custom Components

Add a custom component under `src/components/` when a built-in is missing behavior or behaves wrongly for a specific widget. Never modify BELLATRIX source.

### Option A — New component extending `WebComponent`

```ts
// src/components/Toast.ts
import { Button, WebComponent } from '@bellatrix/web/components';
import { BellatrixWebComponent } from '@bellatrix/web/components/decorators';

@BellatrixWebComponent
export class Toast extends WebComponent<HTMLDivElement> {
    // get… methods are what validate('innerText') resolves to
    async getInnerText(): Promise<string> {
        return await this.wrappedElement.getInnerText();
    }

    async isError(): Promise<boolean> {
        return (await this.getAttribute('class')).includes('toast-error');
    }

    async dismiss(): Promise<void> {
        await this.create(Button).byCss("button[aria-label='Close']").click();
    }
}
```

### Option B — Child component overriding a built-in

```ts
// src/components/ScrollingButton.ts
import { Button } from '@bellatrix/web/components';
import { BellatrixWebComponent } from '@bellatrix/web/components/decorators';

@BellatrixWebComponent
export class ScrollingButton extends Button {
    // Sticky headers sometimes cover the button — scroll it into view first
    override async click(): Promise<void> {
        await this.scrollIntoView();
        await super.click();
    }
}
```

### Using custom components in Map classes

```ts
import { Toast } from '../../components/Toast';
import { ScrollingButton } from '../../components/ScrollingButton';

get successToast() {
    return this.create(Toast).byCss("[data-testid='toast']");
}

get placeOrderButton() {
    return this.create(ScrollingButton).byId('place_order');
}
```

Rules: one file per component, filename = class name; ALWAYS decorate with `@BellatrixWebComponent` (without it the element is never located — `wrappedElement` stays undefined); new methods must be `async` (only async methods get the find/retry wrapper); keep components control-level (business workflow stays in the page class); reuse them across pages.
