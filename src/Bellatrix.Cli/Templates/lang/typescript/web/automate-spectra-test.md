<!-- lang:traceability -->
### TypeScript: `@ManualTestCase('TC-XXX')` + `@Category`

In TypeScript the TC-ID marker is the project's custom `@ManualTestCase('TC-XXX')` method decorator (defined once in `src/attributes/ManualTestCase.ts` — see `bellatrix-web-project-structure`; create it first if it is missing). It stores the ID in the test's metadata (so `bellatrix ./tests filter --manualTestCase TC-XXX` works) and is NOT BELLATRIX's `@TestCase`, which is data-driven parameterization — `@TestCase('TC-210')` on a zero-argument test is even a compile error (TS2554).

TS method names are camelCase: lower-case only the first letter of the `Action_Condition_ExpectedResult` name (`ApplyCoupon_WithValidCode_…` → `applyCoupon_WithValidCode_…`).

```ts
/**
 * Automates SPECTRA TC-210: Apply a valid coupon in the cart
 * Source: docs/cart.md
 * Criteria: AC-004
 */
@Test
@ManualTestCase('TC-210')
@Category(Categories.Smoke)
async applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() {
    const cartPage = await this.app.goTo(CartPage);

    await cartPage.applyCoupon('happybirthday');

    await cartPage.asserts.couponApplied();
    await cartPage.asserts.totalPrice('54.00');
}
```

One TC per method — two similar TCs are still two methods:

```ts
@Test
@ManualTestCase('TC-210')
@Category(Categories.Smoke)
async applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }

@Test
@ManualTestCase('TC-211')
@Category(Categories.Regression)
async applyCoupon_WithExpiredCode_ShowsCouponError() { /* ... */ }
```

**BANNED:** `@ManualTestCase('TC-210') @ManualTestCase('TC-211')` on one method; `@TestCase('TC-210')`; one `@TestCase`-parameterized method covering several TCs.

`Categories` is the frozen constant object in `src/Categories.ts` (`Smoke`, `Regression`, `CI`); `@Category('Smoke')` with a string literal works too. Imports: `Test`, `TestClass` from `'@bellatrix/web/test'`; `Category` from `'@bellatrix/core/decorators'`; `ManualTestCase` from the project's `src/attributes/ManualTestCase`.

<!-- lang:automated-by -->
**Before:**
```yaml
---
id: TC-210
priority: high
tags: [checkout, coupon]
component: cart
source_refs: [docs/cart.md]
criteria: [AC-004]
---
```

**After:**
```yaml
---
id: TC-210
priority: high
tags: [checkout, coupon]
component: cart
source_refs: [docs/cart.md]
criteria: [AC-004]
automated_by:
  - {project.dir}/tests/cart/CartTests.test.ts
---
```

<!-- lang:build -->
TypeScript has no build output — the runner executes `.ts` directly — so the build check is a full type check of the test project (`{project.buildCommand}` from `.bellatrix/config.json`, run from the repository root; it type-checks through `-p <folder>/tsconfig.json`):

```bash
{project.buildCommand}
```

Fix every error until it exits 0 (typical: missing `await` is NOT reported — re-read every new line for it; `byXPath` → `byXpath`; `click()` on a `Div`/`Span`/`Label`/`TextField` → create it as `Button`; missing `override` on `url`/`waitForPageLoad`/hooks; missing `@Page(Map, Asserts)`; wrong import subpath). Then give the user — do not run it yourself:

```bash
npx bellatrix ./tests filter --manualTestCase TC-210
```

<!-- lang:summary -->
```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Title | Test Method | File |
|-------|-------|-------------|------|
| TC-210 | Apply a valid coupon in the cart | applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal | tests/cart/CartTests.test.ts |

### Files Created/Modified
[CREATED] src/pages/cartpage/CartPageMap.ts
[CREATED] src/pages/cartpage/CartPage.ts
[CREATED] src/pages/cartpage/CartPageAsserts.ts
[EDITED]  src/pages/index.ts — exported CartPage
[CREATED] tests/cart/CartTests.test.ts
[EDITED]  {testCasesDir}/cart/TC-210.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
{project.buildCommand} — 0 errors

### Run It
npx bellatrix ./tests filter --manualTestCase TC-210
```

<!-- lang:scan-pattern -->
```json
{
  "coverage": {
    "scan_patterns": [
      "@ManualTestCase('{id}')",
      "@ManualTestCase(\"{id}\")"
    ]
  }
}
```

> **Important:** Do NOT use BELLATRIX's `@TestCase('TC-XXX')` for traceability — that decorator parameterizes data-driven tests. Use `@ManualTestCase('TC-XXX')`, the metadata-only decorator defined in `src/attributes/ManualTestCase.ts`. If the project does not have it yet, create it first (definition in `bellatrix-web-project-structure`). Keep both quote styles in `scan_patterns` so either spelling is discovered.

