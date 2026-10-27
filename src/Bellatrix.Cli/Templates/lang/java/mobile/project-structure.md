---
description: As-is snapshot of the BELLATRIX-Java mobile test project (Maven, Android/iOS). Ground CREATE vs EDIT decisions on this file and refresh it whenever a new pages/, components/, models/, or tests/ package is added.
---

# my-mobile-tests — Current Project Structure (Java)

This file is the authoritative as-is snapshot of the mobile test project. Use it to decide whether to CREATE a new
file/package or EDIT an existing one. It is NOT aspirational — only list a package or file after it actually exists
on disk.

> **First use:** this skill ships with placeholder names (`my-mobile-tests`, root package `com.myproject.mobiletests`).
> The first time you generate code, scan the real project (the `pom.xml` that depends on
> `solutions.bellatrix:bellatrix.android` and/or `bellatrix.ios`), replace the placeholders below with the real
> artifact, root package, app constants, and packages, and empty the example tables. If a listed file does not exist
> yet, treat the tree as the target layout: CREATE it, then record what is really on disk.
>
> **Maintenance rule:** whenever you add a page object package, section, custom component, model, or tests
> sub-package as part of a generation workflow, update the corresponding table in the same change. The agent owns
> keeping this tree accurate.

---

## Top-Level Layout

Root package: `com.myproject.mobiletests` (folders under `src/test/java/com/myproject/mobiletests/`).

```
my-mobile-tests/
├─ pom.xml                                           # solutions.bellatrix:bellatrix.android:1.0 and/or bellatrix.ios:1.0 (test scope), JUnit 5
├─ src/main/resources/application.properties         # environment=dev
├─ src/main/resources/testFrameworkSettings.dev.json # androidSettings / iOSSettings — lifecycle, timeouts, app path, serviceUrl
└─ src/test/java/com/myproject/mobiletests/
   ├─ common/ManualTestCase.java      # @ManualTestCase("TC-XXX") for SPECTRA traceability
   ├─ common/Constants.java           # compile-time constants for @ExecutionApp(appPath = …, appPackage = …)
   ├─ models/                         # Records, enums and data classes (platform-neutral)
   ├─ android/components/             # Custom Android components (overrides / new)
   ├─ android/pages/{screen}/         # Android page objects — {Screen}.java + Map.java + Asserts.java
   ├─ android/pages/shared/           # Reusable Android page-object sections (composition)
   ├─ android/tests/{featurearea}/    # Android test classes grouped by feature area
   ├─ ios/components/                 # Custom iOS components
   ├─ ios/pages/{screen}/             # iOS page objects — {Screen}.java + Map.java + Asserts.java
   ├─ ios/pages/shared/               # Reusable iOS sections
   └─ ios/tests/{featurearea}/        # iOS test classes grouped by feature area
```

A single-platform project keeps only its own `android/` or `ios/` subtree (and only its own dependency and settings
section).

### Key facts
- **Platforms:** Android (UiAutomator2) — page base `AndroidPage<Map, Asserts>`, test base `AndroidTest`,
  `solutions.bellatrix.android.infrastructure.ExecutionApp`; iOS (XCUITest) — `IOSPage<Map, Asserts>`, `IOSTest`,
  `solutions.bellatrix.ios.infrastructure.ExecutionApp`.
- **Appium endpoint:** `http://127.0.0.1:4723` (`serviceUrl` in each settings section) — started by the user, never
  by the agent or the tests.
- **App under test:** `Constants.ANDROID_APP_PATH` / `Constants.APP_PACKAGE` / `Constants.IOS_APP_PATH` (fill in from
  the real project).
- **Global lifecycle (config):** `restart every time`.
- **Class-level lifecycle (generated classes default):** `Lifecycle.REUSE_IF_STARTED` — declared speed intent that
  overrides the config per test class (honored on Android; iOS 1.0 still restarts per test — see
  `bellatrix-mobile-test-structure-and-lifecycle`).
- **Build check:** `mvn -q test-compile` (never `mvn test` — the user runs tests).

### Required project wiring (`pom.xml`)

```xml
<properties>
  <!-- BELLATRIX-Java 1.0 is built for Java 19. Use source/target: the default maven-compiler-plugin of Maven 3.8
       ignores maven.compiler.release and compiles for Java 5 -->
  <maven.compiler.source>19</maven.compiler.source>
  <maven.compiler.target>19</maven.compiler.target>
  <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
</properties>

<dependencies>
  <dependency>
    <groupId>solutions.bellatrix</groupId>
    <artifactId>bellatrix.android</artifactId>   <!-- and/or bellatrix.ios -->
    <version>1.0</version>
    <scope>test</scope>
  </dependency>
  <dependency>
    <groupId>org.junit.jupiter</groupId>
    <artifactId>junit-jupiter-engine</artifactId>
    <version>5.9.2</version>
    <scope>test</scope>
  </dependency>
  <dependency>
    <groupId>org.junit.jupiter</groupId>
    <artifactId>junit-jupiter-params</artifactId>   <!-- only for data-driven tests -->
    <version>5.9.2</version>
    <scope>test</scope>
  </dependency>
</dependencies>
```

BELLATRIX-Java is not on Maven Central: the user installs it (`mvn install` of the BELLATRIX-Java repository) or
points the project at their own repository. Do not change the dependency coordinates. Maven must run on **JDK 19 or
later** (`mvn -v`).

---

## Root Files

### `common/Constants.java`

```java
package com.myproject.mobiletests.common;

public final class Constants {
    // Android
    public static final String ANDROID_APP_PATH = "${user.home}/apps/shop.apk";
    public static final String APP_PACKAGE = "com.example.shop";
    public static final String MAIN_ACTIVITY = ".MainActivity";
    public static final String ANDROID_VERSION = "14";
    public static final String ANDROID_DEVICE = "Android Emulator";

    // iOS (an @ExecutionApp appPath is used verbatim on iOS — no ${…} expansion)
    public static final String IOS_APP_PATH = "/path/to/Shop.app";
    public static final String BUNDLE_ID = "com.example.shop";
    public static final String IOS_VERSION = "17.5";
    public static final String IOS_DEVICE = "iPhone 15";

    private Constants() {
    }
}
```

### `common/ManualTestCase.java`

Create it if it does not exist (string-based, metadata only — BELLATRIX-Java has no built-in string variant):

```java
package com.myproject.mobiletests.common;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ManualTestCase {
    String value();
}
```

It is not `@Repeatable` on purpose: one SPECTRA test case per test method.

### `src/main/resources/`

`application.properties` (`environment=dev`) and `testFrameworkSettings.dev.json` (`androidSettings` /
`iOSSettings`) — full key list in `bellatrix-mobile-app-services`.

---

## `android/pages/` and `ios/pages/` — Page Objects (3-class pattern)

Each screen gets its own package with three classes: `Map.java` (component methods / locators), `{Screen}.java`
(extends `AndroidPage<Map, Asserts>` / `IOSPage<Map, Asserts>`; workflows), `Asserts.java` (extends
`PageAsserts<Map>`; assertion methods using `validate…`).

| Package | Platform | Purpose |
|---------|----------|---------|
| `android/pages/login/` *(example — replace)* | Android | Login screen — user name, password, remember me, error message |

**Package:** `com.myproject.mobiletests.android.pages.<screen>` / `com.myproject.mobiletests.ios.pages.<screen>`
**Base classes:** `solutions.bellatrix.android.pages.AndroidPage` / `PageMap` / `PageAsserts`;
`solutions.bellatrix.ios.pages.IOSPage` / `PageMap` / `PageAsserts`

---

## `pages/shared/` — Reusable Page-Object Sections

Plain classes (NOT `AndroidPage` / `IOSPage`) that group locators and workflows shared by several screens (bottom
navigation, toolbar, number pad). Screens expose them via composition — a
`public BottomNavigationSection navigation() { return new BottomNavigationSection(); }` method on the screen's
`Map` — and delegate from their action methods. Each section creates its components through
`new ComponentCreateService()` of its platform.

| File | Used by | Purpose |
|------|---------|---------|
| *(none yet)* | | |

**Package:** `com.myproject.mobiletests.android.pages.shared` / `com.myproject.mobiletests.ios.pages.shared`
**When to CREATE a new section:** two or more screens expose the same locator set AND the same workflow.

---

## `components/` — Custom Components

Custom BELLATRIX components live here when a built-in is missing behavior or needs an override. Extend the closest
built-in (`Button`, `ComboBox`, …) of the SAME platform and add or override public methods; extend
`AndroidComponent` / `IOSComponent` only for read-only containers.

| File | Extends | Why |
|------|---------|-----|
| *(none yet)* | | |

**Package:** `com.myproject.mobiletests.android.components` / `com.myproject.mobiletests.ios.components`
**Usage in Map classes:** `create().byId(SpinnerComboBox.class, "com.example.shop:id/country")`.

---

## `models/` — Data Models and Enums

Plain records, data classes and enums shared across page objects of both platforms. No component or BELLATRIX types
here — pure data.

| File | Purpose |
|------|---------|
| *(none yet)* | |

**Package:** `com.myproject.mobiletests.models`

---

## `tests/` — Test Classes (grouped by feature area)

One file per feature area and platform. One test method per SPECTRA TC. Every method carries
`@ManualTestCase("TC-XXX")`.

| Package | File | Feature area |
|---------|------|--------------|
| *(none yet)* | | |

**Package:** `com.myproject.mobiletests.android.tests.<featurearea>` / `…ios.tests.<featurearea>` (the `package`
line must match the folder)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a screen NOT in the Pages table (for its platform) | **CREATE** `pages/{screen}/` with `{Screen}.java`, `Map.java`, `Asserts.java` | `android/pages/` or `ios/pages/` |
| Test case targets an existing screen | **EDIT** that screen's Map / page / Asserts classes; append new methods | `…/pages/{screen}/` |
| Feature area NOT in the Tests table | **CREATE** `tests/{featurearea}/{App}{FeatureArea}Tests.java` | `…/tests/` |
| Feature area exists | **EDIT** — add a new `@Test` method to the existing class | `…/tests/{featurearea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** a custom component | `…/components/` |
| Same locator block + workflow on ≥2 screens | **CREATE** a section and expose it from each screen's `Map` | `…/pages/shared/` |
| Need a shared enum / record / data class | **EDIT** an existing one if it fits, else **CREATE** | `models/` |
| `common/ManualTestCase.java` / `common/Constants.java` missing | **CREATE** once (code above) | `common/` |

---

## Sync

Your agent's instructions may reference this skill. Whenever you add a package, section, component, model, or test
file, ALSO update this skill's tables in the same change.
