<!-- lang:traceability -->
### Java: `@ManualTestCase("TC-XXX")` + `@Tag`

In Java the TC-ID marker is the project-defined `@ManualTestCase("TC-XXX")` annotation (`common/ManualTestCase.java` — definition in `bellatrix-web-project-structure`; BELLATRIX-Java has no built-in string version). It is NOT JUnit's `@ParameterizedTest`, which is for data-driven parameterization. Java method names start lower-case: `applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal`.

```java
/**
 * Automates SPECTRA TC-210: Apply a valid coupon in the cart
 * Source: docs/cart.md
 * Criteria: AC-004
 */
@Test
@ManualTestCase("TC-210")
@Tag("Smoke")
public void applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() {
    var homePage = app().goTo(HomePage.class);
    homePage.addProductById(28);
    var cartPage = homePage.viewCart();

    cartPage.applyCoupon("happybirthday");

    cartPage.asserts().couponApplied();
    cartPage.asserts().totalPrice("54.00");
}
```

One TC per method — two similar TCs are still two methods:

```java
@Test
@ManualTestCase("TC-210")
@Tag("Smoke")
public void applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }

@Test
@ManualTestCase("TC-211")
@Tag("Regression")
public void applyCoupon_WithExpiredCode_ShowsCouponError() { /* ... */ }
```

**BANNED:** `@ManualTestCase("TC-210") @ManualTestCase("TC-211")` on one method (does not compile — the annotation is deliberately not `@Repeatable`; never make it repeatable); `@ParameterizedTest` / `@CsvSource` / `@MethodSource` merging several TCs.

Imports: `myproject.webtests.common.ManualTestCase`, `org.junit.jupiter.api.Test`, `org.junit.jupiter.api.Tag`. TestNG projects use `@org.testng.annotations.Test(groups = "Smoke")` with the same `@ManualTestCase("TC-XXX")`.

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
  - my-project-web-tests/src/test/java/myproject/webtests/tests/cart/CartTests.java
---
```

<!-- lang:build -->
From the folder that holds `pom.xml` (compile only — never `mvn test` / `mvn verify`):

```bash
mvn -q test-compile
```

Fix any build errors until the build succeeds (missing imports or `ManualTestCase`, `byXpath` → `byXPath`, the `toBeVisible().click()` generic trap, `validate*` on an unsupported component, `package` line not matching the folder). Then give the user — do not run it yourself:

```bash
mvn test -Dtest=CartTests#applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal
```

<!-- lang:summary -->
```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Title | Test Method | File |
|-------|-------|-------------|------|
| TC-210 | Apply a valid coupon in the cart | applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal | tests/cart/CartTests.java |

### Files Created/Modified
[CREATED] src/test/java/myproject/webtests/pages/cart/CartPage.java
[CREATED] src/test/java/myproject/webtests/pages/cart/Map.java
[CREATED] src/test/java/myproject/webtests/pages/cart/Asserts.java
[CREATED] src/test/java/myproject/webtests/tests/cart/CartTests.java
[EDITED]  tests/cart/TC-210.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
mvn -q test-compile succeeded

### Run It
mvn test -Dtest=CartTests#applyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal
```

<!-- lang:scan-pattern -->
```json
{
  "coverage": {
    "scan_patterns": [
      "@ManualTestCase(\"{id}\")"
    ]
  }
}
```

> **Important:** Do NOT use JUnit's `@ParameterizedTest` (or TestNG's `@DataProvider`) for traceability — those parameterize data-driven tests. Use `@ManualTestCase("TC-XXX")`, the metadata-only annotation defined in `src/test/java/myproject/webtests/common/ManualTestCase.java`. If the project does not have it yet, create it first (definition in `bellatrix-web-project-structure`). The C# pattern `[ManualTestCase("{id}")]` does not match Java code.
