---
description: As-is snapshot of the BELLATRIX Java web test project (Maven) plus the CREATE-vs-EDIT decision table. Ground every CREATE vs EDIT decision on it and refresh it whenever a pages/, sections/, components/, models/, or tests/ package is added.
---
# my-project-web-tests — Current Project Structure (Java)

This file is the authoritative as-is snapshot of what exists in the web test project. Use it to decide whether to CREATE a new file/package or EDIT an existing one. It is NOT aspirational — only add an entry after the folder/file is actually on disk.

> **First use:** replace `my-project-web-tests` / `myproject.webtests` with the real Maven project folder and root package, and fill the tables from the actual tree (search `src/test/java/**/pages/*/`, `**/sections/*/`, `**/components/*.java`, `**/models/*.java`, `**/tests/**/*.java`).
>
> **Maintenance rule:** whenever you add a page object package, section, custom component, model, or tests sub-package during a generation workflow, update the matching table here in the same change. The agent owns keeping this tree accurate.

---

## Top-Level Layout

Root package: `myproject.webtests` (folders under `src/test/java/myproject/webtests/`).

```
my-project-web-tests/
├─ pom.xml                                            # solutions.bellatrix:bellatrix.web:1.0 (test scope), JUnit 5
├─ src/main/resources/application.properties          # environment=dev
├─ src/main/resources/testFrameworkSettings.dev.json  # webSettings — browser, lifecycle, timeouts, baseUrl, gridSettings
└─ src/test/java/myproject/webtests/
   ├─ common/ManualTestCase.java      # @ManualTestCase("TC-XXX") for SPECTRA traceability
   ├─ components/                     # Custom BELLATRIX components (overrides / new)
   ├─ models/                         # Records, data classes and enums used by page objects
   ├─ pages/{page}/                   # Page objects — {PageName}.java + Map.java + Asserts.java
   ├─ sections/{section}/             # Reusable sections (WebSection) — header, footer, nav
   └─ tests/{featurearea}/            # Test classes grouped by feature area
```

### Key facts
- **Base URL:** `https://example.com/` (`webSettings.baseUrl` — update to the system under test)
- **Browser / driver:** local browser (`webSettings.executionType: "regular"`) or a Selenium Grid / cloud provider via a `webSettings.gridSettings[]` entry whose `providerName` equals `executionType`
- **Global lifecycle (config):** `restart every time`
- **Class-level lifecycle (generated classes default):** `Lifecycle.REUSE_IF_STARTED` — overridden per class/test when state must not leak
- **Page base class:** `WebPage<Map, Asserts>`; **section base class:** `WebSection<Map, Asserts>`; **test base class:** `solutions.bellatrix.web.infrastructure.junit.WebTest` (TestNG: `…infrastructure.testng.WebTest`)
- **Locator preference:** `data-testid` → static id → name → aria-label → label/placeholder → tag+text → structural CSS → positional XPath
- **Build check:** `mvn -q test-compile` (never `mvn test` — the user runs tests)

### Required project wiring (`pom.xml`)

BELLATRIX-Java is not on Maven Central — `solutions.bellatrix:bellatrix.web:1.0` must be installed into the local repository from the BELLATRIX-Java source (`mvn install -DskipTests -pl bellatrix.web -am`). The project targets Java 19, so Maven must run on **JDK 19 or later** (`mvn -v` shows the Java version). JUnit 5 and TestNG arrive transitively; `junit-jupiter-params` (for `@ParameterizedTest`) does not.

```xml
<properties>
    <maven.compiler.source>19</maven.compiler.source>
    <maven.compiler.target>19</maven.compiler.target>
    <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
    <bellatrix.version>1.0</bellatrix.version>
</properties>

<dependencies>
    <dependency>
        <groupId>solutions.bellatrix</groupId>
        <artifactId>bellatrix.web</artifactId>
        <version>${bellatrix.version}</version>
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
        <artifactId>junit-jupiter-params</artifactId>
        <version>5.9.2</version>
        <scope>test</scope>
    </dependency>
</dependencies>

<build>
    <plugins>
        <plugin>
            <groupId>org.apache.maven.plugins</groupId>
            <artifactId>maven-compiler-plugin</artifactId>
            <version>3.11.0</version>
            <configuration>
                <release>19</release>
            </configuration>
        </plugin>
        <plugin>
            <groupId>org.apache.maven.plugins</groupId>
            <artifactId>maven-surefire-plugin</artifactId>
            <version>3.0.0-M5</version>
        </plugin>
    </plugins>
</build>
```

### Configuration files

`application.properties` (classpath root) selects the environment; BELLATRIX then loads `testFrameworkSettings.<env>.json` from the classpath root (`-Denvironment=…` overrides the file):

```properties
environment=dev
```

The full `webSettings` schema is in `bellatrix-web-app-services`. There is no assembly bootstrap file (C#'s `TestsInitialize.cs`) — the browser lifecycle plugin registered by `WebTest` starts and closes the browser.

### `common/ManualTestCase.java`

```java
package myproject.webtests.common;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Links an automated test to a SPECTRA manual test case for traceability.
 * Pure metadata (not JUnit's @ParameterizedTest) — SPECTRA's coverage scanner matches @ManualTestCase("TC-XXX").
 * Deliberately NOT @Repeatable: one SPECTRA test case per test method.
 */
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ManualTestCase {
    String value();
}
```

BELLATRIX-Java has no built-in string `ManualTestCase` (the getting-started sample's `int id()` version cannot hold `TC-210`). Test classes import it: `import myproject.webtests.common.ManualTestCase;`.

---

## `pages/` — Page Objects (3-class pattern)

Each page gets its own package with `{PageName}.java` (`extends WebPage<Map, Asserts>`: `getUrl()`, `waitForPageLoad()`, workflows), `Map.java` (`extends PageMap`: component methods), `Asserts.java` (`extends PageAsserts<Map>`: assertion methods using `validate*`).

| Package | URL | Purpose |
|---------|-----|---------|
| _(none yet — add a row when you create a page)_ | | |

**Package:** `myproject.webtests.pages.<page>` (lowercase page name without the `Page` suffix — `pages.cart` holds `CartPage`)
**Base classes:** `WebPage<Map, Asserts>`, `PageMap`, `PageAsserts<Map>` (`solutions.bellatrix.web.pages`)

---

## `sections/` — Reusable Page Sections

Sections are 3-class packages whose action class extends `WebSection<Map, Asserts>` (NOT `WebPage`) — they group locators and workflows shared by several pages (header, navigation, footer, cookie banner, mini-cart). Pages expose them from their `Map` — `public HeaderSection header() { return app().createSection(HeaderSection.class); }` — and delegate from their action methods.

| Package | Used by | Purpose |
|---------|---------|---------|
| _(none yet)_ | | |

**Package:** `myproject.webtests.sections.<section>`
**When to CREATE a new section:** when two or more pages expose the same locator set AND the same workflow around it. A single-page duplicate stays on the page as a region.

---

## `components/` — Custom Components

Custom BELLATRIX components live here when a built-in lacks behavior or needs an override. New component = extend `solutions.bellatrix.web.components.WebComponent`; behavior tweak = extend an existing component (e.g. `Button`) and override its public methods. Public no-argument constructor only.

| File | Extends | Why |
|------|---------|-----|
| _(none yet)_ | | |

**Package:** `myproject.webtests.components`
**Usage in Map classes:** `create().byCss(MyComponent.class, "[data-testid='…']")`.

---

## `models/` — Data Models and Enums

Records, plain data classes, and enums shared across page objects. No component or BELLATRIX types here — pure data.

| File | Purpose |
|------|---------|
| _(none yet)_ | |

**Package:** `myproject.webtests.models`

---

## `tests/` — Test Classes (grouped by feature area)

One file per feature area. One test method per SPECTRA TC. Every traced method carries `@ManualTestCase("TC-XXX")`.

| Package | File | Feature area |
|---------|------|--------------|
| _(none yet)_ | | |

**Package:** `myproject.webtests.tests.<featurearea>` (the `package` line must match the folder)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a page NOT in the Pages table above | **CREATE** `pages/{page}/` with `{PageName}.java`, `Map.java`, `Asserts.java` | `pages/` |
| Test case targets an existing page | **EDIT** that page's `Map` / page / `Asserts` classes; append new methods | `pages/{page}/` |
| A modal / drawer / wizard step with its own locators | **CREATE** its own page object (or a section if it appears on several pages) | `pages/` or `sections/` |
| Test case's feature area is NOT in the Tests table above | **CREATE** `tests/{featurearea}/{FeatureArea}Tests.java` | `tests/` |
| Test case's feature area exists | **EDIT** — add a new `@Test` method to the existing class | `tests/{featurearea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** custom component extending `WebComponent` or an existing component | `components/` |
| Same locator block + workflow duplicated across ≥2 pages | **CREATE** a `WebSection` package and expose it from each page's `Map` | `sections/` |
| Need a shared enum / record / data class | **EDIT** an existing model if it fits; otherwise **CREATE** a new file | `models/` |
| `common/ManualTestCase.java` missing | **CREATE** once (code above) | `common/` |

---

## Sync

Your agent's instructions may reference this skill. When you add a new package, page, section, component, model, or test class, ALSO update this skill's tables in the same change.
