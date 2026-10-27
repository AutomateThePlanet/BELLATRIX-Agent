---
description: Reference skill for BELLATRIX TypeScript web validations — validate('…').is/isNot/contains and validateIs/validateIsNot (which name works on which component), their framework pitfalls, and when to use Assert from @bellatrix/core/assertions.
---
# BELLATRIX Web Validations and Assertions Reference (TypeScript)

## CRITICAL: Use Retrying Validations Over One-Shot Asserts

For element-level checks, **prefer** the component validations. They retry until the condition holds or `webSettings.timeoutSettings.validationTimeout` expires (polling every `sleepInterval`), and they name the component in the failure.

```ts
// CORRECT - BELLATRIX validations (retry built in) — always awaited
await messageAlert.validateIs('visible');
await messageAlert.validate('innerText').is('Coupon code applied successfully.');
await totalSpan.validate('innerText').isNot('0.00€');

// WRONG - one-shot asserts on elements (no retry, flaky on async pages)
Assert.isTrue(await messageAlert.isVisible());
Assert.areEqual('Coupon code applied successfully.', await messageAlert.getInnerText());
```

There is **no per-call timeout** (no `validate…(…, 15)`) — change `validationTimeout` (milliseconds) in `bellatrix.config.ts` if a page is genuinely slow.

---

## How the Name Maps to a Getter

| Call | Resolves to | Then |
|------|-------------|------|
| `c.validate('innerText')` | `c.getInnerText()` | `.is(x)`, `.isNot(x)`, `.contains(x)` |
| `c.validate('value')` | `c.getValue()` | same |
| `c.validateIs('visible')` | `c.isVisible()` | passes when `true` |
| `c.validateIsNot('visible')` | `c.isVisible()` | passes when `false` |

The name is `get<Name>` / `is<Name>` with the prefix removed and the first letter lower-cased. **The name is NOT type-checked** — `validate('placeHolder')` or `validateIs('nonsense')` compile and fail at runtime. Use only names from the table below.

| Validation | Works on |
|------------|----------|
| `validateIs('visible')` / `validateIsNot('visible')`, `validateIs('present')`, `validateIs('clickable')` / `validateIsNot('clickable')` (= enabled / disabled) | every component |
| `validate('innerText')` | `Anchor`, `Button`, `Div`, `Heading`, `Label`, `Span`, `Option`, `Reset`, `TextArea`, `Output`, `Progress` — NOT useful on `TextField` and other inputs (an input's inner text is empty: use `'value'`) |
| `validate('innerHtml')` | `Anchor`, `Div`, `Label`, `Span`, `TextField`, `Output` |
| `validate('value')` | `Button`, `CheckBox`, `RadioButton`, `Option`, `Reset`, `TextField`, `TextArea`, `PasswordField`, `EmailField`, `PhoneField`, `SearchField`, `UrlField`, `NumberInput`, `DateInput`, `TimeInput`, `ColorInput`, `MonthInput`, `WeekInput` — the `value` ATTRIBUTE with Playwright (pitfall 5) |
| `validate('placeholder')` | `TextField`, `TextArea`, `PasswordField`, `EmailField`, `PhoneField`, `SearchField`, `UrlField`, `NumberInput` |
| `validate('email' \| 'phone' \| 'search' \| 'url' \| 'password' \| 'color' \| 'time' \| 'month' \| 'week')` | the matching HTML5 input (`getEmail()`, `getPhone()`, …; `'time'` on `TimeInput` only, since `DateTimeInput.getTime()` returns a `Date`) |
| `validate('href' \| 'target' \| 'rel')` | `Anchor` (`href` is the raw attribute with Playwright — may be relative) |
| `validate('src' \| 'alt')` | `Image` |
| `validate('for')` | `Label`, `Output` |
| `validateIs('checked')` / `validateIsNot('checked')` | `CheckBox`, `RadioButton` |
| `validateIs('selected')` | `Option` |

### Known Framework Pitfalls (MUST read)

1. **`contains()` can pass silently.** When `validate(…).contains(x)` times out it falls back to "assert not equal", so a text that merely differs from `x` PASSES. Always back it with a hard check:

   ```ts
   await errorMessage.validate('innerText').contains('expired');                    // waits for the text
   Assert.stringContains('expired', await errorMessage.getInnerText());             // fails for real
   ```

2. **Numbers are typed as strings.** `validate('maxLength')`, `validate('width')`, `validate('number')` are typed `StringValidator`; `.is('20')` compiles and then fails at runtime (the getter returns a `number`). Use `Assert`. Also, `getMaxLength()`/`getMinLength()` (TextField, TextArea, PasswordField, EmailField, PhoneField, SearchField, UrlField) read the `max`/`min` attributes, NOT `maxlength`/`minlength`, so they return `NaN` for a length limit. Read the DOM property instead:

   ```ts
   Assert.areEqual(20, await usernameField.evaluate(el => el.maxLength));
   Assert.areEqual(2, await quantityBox.getNumber());               // server-rendered value (see pitfall 5)
   ```

3. **`disabled` / `readonly` / `required` read the attribute VALUE.** `isDisabled()`, `isReadonly()`/`isReadOnly()`, `isRequired()` (and `validateIs('disabled' | 'readonly' | 'required')`) are `true` only when the attribute literally equals `"true"`; with the Playwright controller `<button disabled>` reads `''` → `false`. Use:

   ```ts
   await placeOrderButton.validateIsNot('clickable');                                // disabled (retries)
   await placeOrderButton.validateIs('clickable');                                   // enabled (retries)
   Assert.isTrue(await emailField.evaluate(el => el.required), 'Email should be required.');
   Assert.isTrue(await notesArea.evaluate(el => el.readOnly), 'Notes should be read-only.');
   ```

4. **`validateIsNot('visible')` needs the element to have been visible.** Finding (Playwright) waits for visibility first; on an element that is already gone it times out and fails. To prove absence, use the project helper `waitUntilGone(this.app, () => list)` or `Assert.areEqual(0, await list.count())` (see `bellatrix-web-locators-and-waits`).

5. **`value` is the attribute, not what was typed (Playwright).** `getValue()` and every getter built on it (`getEmail()`, `getPassword()`, `getPhone()`, `getSearch()`, `getUrl()`, `getNumber()`, …) and therefore `validate('value' | 'email' | 'number' | …)` read the `value` ATTRIBUTE with the Playwright controller: the server-rendered initial value. Text typed by the user or by `setText()` is not in it, and a `<textarea>` has no `value` attribute at all. (With `browserController: 'selenium'` the live value is returned.) Check a value the test typed through the DOM property:

   ```ts
   await emailField.setEmail('john@example.com');
   Assert.areEqual('john@example.com', await emailField.evaluate(el => el.value));
   Assert.areEqual('comment body', await notesArea.evaluate(el => el.value));
   ```

### Validating a `<select>`

`Select.getSelected()` is unimplemented (typed `Promise<void>`) and `Select` has no `getValue`. Read the selection from the DOM:

```ts
const selectedText = await countrySelect.evaluate(el => el.options[el.selectedIndex]?.text ?? '');
Assert.areEqual('Bulgaria', selectedText.trim());
await countrySelect.validateIs('clickable');   // enabled
```

---

## Available Web Validations

### Visibility

```ts
await element.validateIs('visible');
await element.validateIsNot('visible');
await element.wait.toExist();          // presence (visible presence with Playwright)
```

There is **no** `validateExists` / `validateIsPresent`; `validateIs('present')` exists but (Playwright) still waits for visibility while finding.

### Text and Content

```ts
await element.validate('innerText').is('exact text');
await element.validate('innerText').isNot('old text');
await element.validate('innerText').contains('partial text');     // + Assert.stringContains (pitfall 1)
await element.validate('innerHtml').contains('<strong>');           // + Assert.stringContains
Assert.areEqual('comment body', await textArea.evaluate(el => el.value));   // textarea: no value attribute (pitfall 5)
```

There is no `notContains` — use `Assert.isFalse((await element.getInnerText()).includes('Error'))`.

### Values and Placeholders

```ts
await field.validate('value').is('Sofia');                       // value rendered by the server (pitfall 5)
await field.validate('placeholder').is('House number and street name');
await emailField.validate('email').is('john@example.com');       // pre-filled by the page, not typed by the test
Assert.areEqual(2, await quantity.getNumber());                  // quantity rendered by the server
Assert.areEqual('john@example.com', await emailField.evaluate(el => el.value));   // a value the test typed
```

### State

```ts
await button.validateIs('clickable');          // enabled
await button.validateIsNot('clickable');       // disabled
await checkBox.validateIs('checked');
await checkBox.validateIsNot('checked');
await option.validateIs('selected');
```

### Attributes, Links, Images

```ts
await link.validate('href').is('https://example.com/checkout/#');
await link.validate('target').is('_blank');
await image.validate('src').is('https://example.com/logo.png');
Assert.stringContains('is-active', await tab.getAttribute('class'));
Assert.areEqual('true', await menuToggle.getAttribute('aria-expanded'));
```

There is **no** generic `validateAttribute(name, value)`, no CSS-class or style validations. For an arbitrary attribute (`aria-expanded`, `data-state`, `class`) read it with `getAttribute(name)` (standard names, `data-*`, `aria-*`) and assert it with `Assert`.

---

## Usage in a Page Asserts Class

```ts
// src/pages/cartpage/CartPageAsserts.ts
import { App } from '@bellatrix/web/infrastructure';
import { WebPageAsserts } from '@bellatrix/web/pages';
import { Assert } from '@bellatrix/core/assertions';
import { resolve } from '@bellatrix/core/utilities';
import { CartPageMap } from './CartPageMap';

export class CartPageAsserts extends WebPageAsserts<CartPageMap> {
    private get app(): App {
        return resolve(App);
    }

    async couponApplied() {
        await this.map.messageAlert.validateIs('visible');
        await this.map.messageAlert.validate('innerText').is('Coupon code applied successfully.');
    }

    async totalPrice(price: string) {
        await this.map.totalSpan.validate('innerText').is(`${price}€`);
    }

    async cartIsEmpty() {
        await this.map.emptyCartMessage.validate('innerText').contains('Your cart is currently empty');
        Assert.stringContains('Your cart is currently empty', await this.map.emptyCartMessage.getInnerText());
    }

    async onCartPage() {
        Assert.stringContains('/cart', await this.app.browser.getUrl());   // no AssertLandedOnPage in TS
    }
}
```

---

## When to Use `Assert` (`@bellatrix/core/assertions`)

Use `Assert` when no retrying validation covers the need — it works the same in tests, page classes, and asserts classes:

```ts
// Browser-level values (strings, not components)
Assert.areEqual('Checkout – Bellatrix Demos', await this.app.browser.getTitle());
Assert.stringContains('/order-received', await this.app.browser.getUrl());     // (substring, value)

// Collection counts
Assert.areEqual(12, await this.map.productCards.count());

// Numbers and comparisons
Assert.areEqual(3, await quantityBox.getNumber());
Assert.that(await this.map.productCards.count(), is.greaterThan(0));            // `is` from '@bellatrix/core/assertions'

// Arbitrary attributes not covered by validate
Assert.areEqual('true', await menuToggle.getAttribute('aria-expanded'), 'The menu should be expanded.');

// Dialog text
Assert.areEqual('Are you sure?', await this.app.dialog.getMessage());

// Several checks, one report (each callback may be async)
await Assert.multiple(
    async () => Assert.areEqual('54.00€', await this.map.totalSpan.getInnerText()),
    async () => Assert.isTrue(await this.map.placeOrderButton.isClickable()),
);
```

Argument order: `Assert.areEqual(expected, actual, message?)`, `Assert.stringContains(substring, value, message?)`. `Assert.isTrue`/`isFalse` require a real boolean (`=== true`).

---

## Priority Order

1. **Retrying validations first** — `validate('…').is/isNot`, `validateIs`/`validateIsNot` (exact names from the table)
2. **`contains` only with a backing `Assert.stringContains`**
3. **`Assert`** for URL/title strings, counts, numbers, dialog text, arbitrary attributes, the boolean-attribute states of pitfall 3, and typed values read with `evaluate` (pitfall 5)
4. **Never** a one-shot `Assert` on something a retrying validation covers (`Assert.isTrue(await x.isVisible())` when `validateIs('visible')` exists)
