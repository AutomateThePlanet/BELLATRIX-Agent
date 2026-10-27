---
description: Reference skill for BELLATRIX Java web validate* methods (which component supports which), assertion patterns, framework bugs to avoid, and when to fall back to JUnit/TestNG asserts.
---
# BELLATRIX Web Validations and Assertions Reference (Java)

## CRITICAL: Use Native validate* Methods Over Assertions.assertEquals

For element-level assertions, **always prefer** BELLATRIX `validate*` methods. They are `default` methods of the component contract interfaces (`solutions.bellatrix.web.components.contracts`), retry until the condition holds or `webSettings.timeoutSettings.validationsTimeout` (seconds) expires, produce readable failure messages (`AssertionError`), and integrate with BDD logging.

```java
// CORRECT - BELLATRIX validate methods (retry built in)
messageAlert.validateIsVisible();
messageAlert.validateTextIs("Coupon code applied successfully.");
totalSpan.validateTextContains("114.00");

// WRONG - raw assertions on elements (no retry, flaky on async pages)
Assertions.assertTrue(messageAlert.isVisible());
Assertions.assertTrue(totalSpan.getText().contains("114.00"));
```

Unlike C#, Java `validate*` methods take **no timeout argument** — they always use `validationsTimeout`. Text comparisons strip surrounding whitespace of the actual value (`"  114.00€ "` equals `"114.00€"`).

---

## A validate Method Compiles Only on Components That Support It

A component exposes a `validate*` family only if it implements the contract. Calling one on a component without it is a **compile error**:

| validate family (contract) | Available on |
|----------------------------|--------------|
| `validateIsVisible()`, `validateNotVisible()` (`ComponentVisible`); `validateStyleIs/Contains/NotContains/IsSet/NotSet`, `validateStyle(CssStyle, String)` (`ComponentStyle`) | every component |
| `validateTextIs/Contains/NotContains/IsSet` (`ComponentText`) | `Anchor`, `Button`, `Reset`, `Div`, `Heading`, `Label`, `Span`, `Option`, `TextInput`, `TextArea`, `Output`, `Progress`, `TableCell` — **NOT** `Select`, `PasswordInput`, `EmailInput`, `CheckBox`, `Image` |
| `validateHtmlIs/Contains/NotContains` (`ComponentHtml`) | `Anchor`, `Div`, `Label`, `Span`, `TextInput`, `Output`, `TableCell` |
| `validateValueIs/Contains/NotContains` (`ComponentValue`) | `TextInput`, `TextArea`, `Button`, `Reset`, `CheckBox`, `RadioButton`, `Option`, `PasswordInput`, `EmailInput`, `PhoneInput`, `SearchField`, `UrlField`, `NumberInput`, `Range`, `DateInput`, `TimeInput`, `ColorInput`, `Progress`, … |
| `validateIsDisabled()`, `validateNotDisabled()` (`ComponentDisabled`) | `Button`, `Reset`, `TextInput`, `TextArea`, `CheckBox`, `RadioButton`, `Select`, `Option`, `PasswordInput`, HTML5 inputs |
| `validateIsChecked()`, `validateIsChecked(boolean)`, `validateIsUnchecked()` (`ComponentChecked`) | `CheckBox`, `RadioButton` |
| `validateIsSelected()`, `validateNotSelected()` (`ComponentSelected`) | `Option` |
| `validateIsRequired()/NotRequired()`, `validateIsReadonly()/NotReadonly()` | form inputs, `Select`, `TextArea` (`FileInput`: required only) |
| `validatePlaceholderIs/Contains/NotContains/IsSet/NotSet` | `TextInput`, `TextArea`, `PasswordInput`, `EmailInput`, `PhoneInput`, `SearchField`, `UrlField`, `NumberInput` |
| `validateHrefIs/Contains/NotContains/IsSet/NotSet`, `validateTargetIs/IsSet/NotSet`, `validateRelIs/IsSet/NotSet` | `Anchor` |
| `validateSrcIs/Contains/IsSet/NotSet`, `validateAltIs/IsSet/NotSet`, `validateWidthIs(int)`/`IsSet`/`NotSet`, `validateHeightIs(int)`/`IsSet`/`NotSet` | `Image` |
| `validateEmailIs/Contains`, `validateSearchIs/Contains`, `validateUrlIs`, `validateUrlIsContains` (sic), `validatePasswordIs/Contains`, `validateNumberIs(Number)`, `validateRangeIs(Number)`, `validateDateIs(String)`, `validateTimeIs(String)`, `validateColorIs`, `validateMonthIs`, `validateWeekIs` | the matching HTML5 input component |
| `validateMaxLengthIs(int)`, `validateMinLengthIs(int)`, `validateMaxIs(…)`, `validateMinIs(…)`, `validateStepIs(Number)`, `validateRowsIs(int)`, `validateColsIs(int)` | inputs exposing those attributes |
| `validateForIs/IsSet/NotSet` | `Label`, `Output` |

**Does not exist on any built-in web component** (compile errors): `validateInnerTextIs` (C# name — use `validateTextIs`), `validateIsNotVisible` (use `validateNotVisible`), `validateIsNotDisabled` (use `validateNotDisabled`), `validateIsNotChecked` (use `validateIsUnchecked`), `validateIsEnabled` (use `validateNotDisabled`), `validateExists`, `validateAttributeIs`, `validatePhoneIs`, and — although the contracts exist — `validateClassIs/Contains` and `validateTitleIs/Contains` (no built-in component implements `ComponentHtmlClass` / `ComponentTitle`).

**Framework quirk — `PhoneInput`:** its contract declares `validatePasswordIs/Contains/NotContains` (copy-paste bug) instead of `validatePhoneIs`. Validate a phone field with `validateValueIs("+359…")`.

### Validating a `<select>` — do NOT use `getSelected().validate…`

`Select` has no text validation, and the `Option` returned by `getSelected()` / `getAllOptions()` is **broken for `validate*`**: it carries the `<select>`'s own locator, so each validation retry re-locates the whole `<select>` and compares the text of *all* options. Read the selected option's text once and assert it:

```java
Assertions.assertEquals("Bulgaria", countrySelect.getSelected().getText());   // getText() is correct
countrySelect.validateNotDisabled();
```

---

## Available Web validate Methods

### Visibility

```java
element.validateIsVisible();
element.validateNotVisible();      // also passes when the element is not in the DOM (after elementWaitTimeout)
```

There is **no** `validateExists` / `validateNotExists`. To wait for an element to leave the DOM use `app().waitFor().wait(element, Wait.to().notExist());` (NOT `toNotExist().waitToBe()` — see `bellatrix-web-locators-and-waits`); for presence without visibility, `element.toExist().waitToBe();`.

### Text and Content

```java
element.validateTextIs("exact text");
element.validateTextNotContains("old text");
element.validateTextContains("partial text");
element.validateTextIsSet();                     // any non-empty text
div.validateHtmlContains("<strong>");
textArea.validateTextIs("comment body");
```

### Values and Placeholders

```java
field.validateValueIs("john@example.com");
field.validateValueContains("john");
field.validatePlaceholderIs("House number and street name");
quantity.validateNumberIs(2.0);                 // getNumber() is a double — pass a double
```

### State

```java
button.validateIsDisabled();
button.validateNotDisabled();
checkBox.validateIsChecked();
checkBox.validateIsUnchecked();
option.validateIsSelected();
field.validateIsRequired();
field.validateIsReadonly();
```

### Attributes, Links, Style

```java
link.validateHrefIs("https://example.com/checkout/#");
link.validateHrefIsSet();
link.validateTargetIs("_blank");
image.validateSrcIs("https://example.com/logo.png");
element.validateStyleContains("display: none");
```

There is **no** generic `validateAttributeIs(name, value)` and no working class/title validation — assert those explicitly (see below).

---

## Usage in a Page `Asserts` Class

```java
package myproject.webtests.pages.cart;

import solutions.bellatrix.web.pages.PageAsserts;

public class Asserts extends PageAsserts<Map> {
    public void couponApplied() {
        map().messageAlert().validateIsVisible();
        map().messageAlert().validateTextIs("Coupon code applied successfully.");
    }

    public void totalPrice(String price) {
        map().totalSpan().validateTextIs(price + "€");
    }

    public void cartIsEmpty() {
        map().emptyCartMessage().validateTextContains("Your cart is currently empty");
        map().proceedToCheckoutButton().validateNotVisible();
    }

    public void onCartPage() {
        app().browser().assertLandedOnPage("/cart");   // BrowserService URL assertion
    }
}
```

`map()` is available inside `Asserts` (protected) and returns the page's `Map`. `app().browser()` also provides `assertNotLandedOnPage(partialUrl)`, `assertUrl(fullUrl)`, `assertUrlPath(path)`, `assertUrlPathAndQuery(pathAndQuery)`, and `assertNoConsoleErrorsLogged()`.

---

## When to Use JUnit / TestNG Asserts

Use framework assertions **only** when no `validate*` method covers the need. JUnit 5 is the default (`org.junit.jupiter.api.Assertions` — note the argument order `assertEquals(expected, actual)`); TestNG projects use `org.testng.Assert` (`assertEquals(actual, expected)` — reversed). Both are on the classpath via BELLATRIX, and both work in page `Asserts` classes as well as in tests.

```java
// Browser-level values (strings, not components)
Assertions.assertEquals("Checkout – Bellatrix Demos", app().browser().getTitle());
Assertions.assertTrue(app().browser().getUrl().contains("/order-received"));

// Collection counts
Assertions.assertEquals(12, app().create().allByCss(Div.class, "[data-testid='product-card']").size());

// Arbitrary attributes / classes not covered by validate*
Assertions.assertEquals("true", menuToggle.getAttribute("aria-expanded"));
Assertions.assertTrue(menuToggle.getHtmlClass().contains("is-active"));

// Dialog text
Assertions.assertEquals("Are you sure?", app().dialogs().getText());
```

Soft (collect-all) assertions — there is no `App.Assert.Multiple` in Java:

```java
var softly = new SoftAssert();   // org.testng.asserts.SoftAssert
softly.assertEquals(totalSpan.getText(), "120.00€");
softly.assertTrue(updateCart.isDisabled());
softly.assertAll();
```

or JUnit `Assertions.assertAll(() -> …, () -> …)`.

---

## Priority Order

1. **Native `validate*` first** — retry, reporting, `*Contains` variants
2. **`app().browser().assertLandedOnPage(…)` / `assertUrlPath(…)`** for navigation outcomes — they assert once (no retry), so `app().navigate().waitForPartialUrl(…)` first when a click triggers the navigation
3. **`Assertions.*` / `SoftAssert`** only for browser-level strings, counts, dialog text, the selected `<select>` option, classes, and attributes with no `validate*`
4. **Never** use a raw assert on a value that has a `validate*` method (`assertTrue(x.isVisible())` when `validateIsVisible()` exists)
