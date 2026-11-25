<!-- lang:traceability -->
### C#: `[ManualTestCase("TC-XXX")]` + `[Category]`

In C# the TC-ID marker is the custom `[ManualTestCase("TC-XXX")]` attribute (defined once in the project as `ManualTestCaseAttribute.cs` — see `bellatrix-web-project-structure`). It is NOT NUnit's `[TestCase]`, which is for data-driven parameterization.

```csharp
/// <summary>
/// Automates SPECTRA TC-210: Apply a valid coupon in the cart
/// Source: docs/cart.md
/// Criteria: AC-004
/// </summary>
[Test]
[ManualTestCase("TC-210")]
[Category("Smoke")]
public void ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal()
{
    var cartPage = App.GoTo<CartPage>();

    cartPage.ApplyCoupon("happybirthday");

    cartPage.AssertCouponApplied();
    cartPage.AssertTotalPrice("54.00");
}
```

One TC per method — two similar TCs are still two methods:

```csharp
[Test]
[ManualTestCase("TC-210")]
[Category("Smoke")]
public void ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal() { /* ... */ }

[Test]
[ManualTestCase("TC-211")]
[Category("Regression")]
public void ApplyCoupon_WithExpiredCode_ShowsCouponError() { /* ... */ }
```

**BANNED:** `[ManualTestCase("TC-210")] [ManualTestCase("TC-211")]` on one method; `[TestCase("TC-210")]`; `[TestCaseSource]` merging several TCs.

MSTest projects use `[TestMethod]` + `[TestCategory("Smoke")]` with the same `[ManualTestCase("TC-XXX")]`.

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
  - {project.dir}/Tests/Cart/CartTests.cs
---
```

<!-- lang:build -->
```bash
{project.buildCommand}
```

Fix any build errors until the build succeeds (missing `using Bellatrix.Web;`, `CreateByXPath` → `CreateByXpath`, missing `override`, `Validate*` on an unsupported component). Then give the user — do not run it yourself:

```bash
dotnet test {project.path} --filter "FullyQualifiedName~ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal" --no-build --logger "console;verbosity=detailed"
```

<!-- lang:summary -->
```
## Automation Summary

### SPECTRA Test Cases Automated
| TC-ID | Title | Test Method | File |
|-------|-------|-------------|------|
| TC-210 | Apply a valid coupon in the cart | ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal | Tests/Cart/CartTests.cs |

### Files Created/Modified
[CREATED] Pages/CartPage/CartPage.Map.cs
[CREATED] Pages/CartPage/CartPage.Actions.cs
[CREATED] Pages/CartPage/CartPage.Assertions.cs
[CREATED] Tests/Cart/CartTests.cs
[EDITED]  tests/cart/TC-210.md — added automated_by

### Coverage Impact
- Automation coverage: 12% → 14% (+1 test automated)

### Build Status
dotnet build succeeded

### Run It
dotnet test {project.path} --filter "FullyQualifiedName~ApplyCoupon_WithValidCode_ShowsSuccessAndDiscountedTotal"
```

<!-- lang:scan-pattern -->
```json
{
  "coverage": {
    "scan_patterns": [
      "[ManualTestCase(\"{id}\")]"
    ]
  }
}
```

> **Important:** Do NOT use NUnit's `[TestCase("TC-XXX")]` for traceability — that attribute parameterizes data-driven tests. Use `[ManualTestCase("TC-XXX")]`, the metadata-only attribute defined in `{project.dir}/ManualTestCaseAttribute.cs`. If the project does not have it yet, create it first (definition in `bellatrix-web-project-structure`).

