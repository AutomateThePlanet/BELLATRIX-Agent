---
description: As-is snapshot of the BELLATRIX TypeScript web test project tree plus the CREATE-vs-EDIT decision table. Ground every CREATE vs EDIT decision on it and refresh it whenever a src/pages/, src/sections/, src/components/, src/models/, or tests/ entry is added.
---
# {project.dir} — Current Project Structure (TypeScript)

This file is the authoritative as-is snapshot of what exists in the web test project. Use it to decide whether to CREATE a new file/folder or EDIT an existing one. It is NOT aspirational — only add an entry after the folder/file is actually on disk.

> **First use:** `{project.dir}` is the folder of `domains.web.project.path` in `.bellatrix/config.json` (it holds `package.json`, `tsconfig.json`, and `bellatrix.config.ts`); keep the placeholder. Fill the tables from the actual tree (search `src/pages/*/`, `src/sections/*/`, `src/components/*.ts`, `src/models/*.ts`, `tests/**/*.test.ts`).
>
> **Maintenance rule:** whenever you add a page object folder, section, custom component, model, or tests sub-folder during a generation workflow, update the matching table here in the same change. The agent owns keeping this tree accurate.

---

## Top-Level Layout

```
{project.dir}/
├─ package.json                      # "type": "module"; scripts.test = "bellatrix ./tests filter --category CI"
├─ tsconfig.json                     # strict, TC39 decorators, moduleResolution "bundler" (see below)
├─ bellatrix.config.ts               # webSettings — controller, browser, headless, baseUrl, timeouts (ms); first line: import type {} from '@bellatrix/web/test'
├─ src/
│  ├─ Categories.ts                  # frozen category names (Smoke, Regression, CI)
│  ├─ attributes/ManualTestCase.ts   # @ManualTestCase('TC-XXX') for SPECTRA traceability
│  ├─ utilities/waits.ts             # waitForPartialUrl + waitUntilGone (framework gaps)
│  ├─ components/                    # Custom BELLATRIX components (overrides / new)
│  ├─ models/                        # Data types and option sets used by page objects
│  ├─ pages/{pagename}/              # Page objects — Map / Page / Asserts trio wired by @Page
│  ├─ pages/index.ts                 # barrel — every page exported here
│  └─ sections/{sectionname}/        # Reusable page sections (url-less WebPage) — header, footer, nav
└─ tests/{featurearea}/              # Test classes grouped by feature area — files MUST end in .test.ts
```

### Key facts
- **Base URL:** `https://example.com` (update to the system under test; `executionSettings.baseUrl` is opened when the browser starts)
- **Browser / controller:** `executionSettings.browserController: 'playwright'`, `browser: 'chrome'`, local (`executionType: 'local'`) or a grid/cloud via `remoteExecutionSettings`
- **Lifecycle:** fixed by the framework — one browser per test class (started before `beforeAll`, closed after `afterAll`); there is no per-class or per-test browser attribute
- **Page base class:** `WebPage<Map, Asserts>` + `@Page(Map, Asserts)`; **section:** url-less `WebPage`; **test base class:** `WebTest` (`@bellatrix/web/infrastructure`) with `@TestClass` / `@Test` from `@bellatrix/web/test`
- **Locator preference:** `data-testid` → static id → name → aria-label → label/placeholder → tag+text → structural CSS → positional XPath
- **Build check:** `{project.buildCommand}` (no emit; the runner executes `.ts` directly)
- **Run (the user, not the agent):** `npx bellatrix ./tests filter --testName <method>` / `--manualTestCase TC-XXX` / `--category Smoke`; Node ≥ 20.9

### Required project wiring

The `@bellatrix/*` packages are not on the public npm registry; reference them the way the team does (a `file:` path to a BELLATRIX-JavaScript checkout or an internal registry). `@bellatrix/runner` is only needed to run tests. The `file:` paths below assume the BELLATRIX-JavaScript checkout sits next to the project — adjust them (absolute paths work).

With `file:` dependencies, also add an `.npmrc` next to `package.json` — without it npm **symlinks** the packages and does not install their own dependencies, and `tsc` reports hundreds of errors inside `node_modules/@bellatrix/*`:

```ini
install-links=true
```

```json
{
  "name": "{project.dir}",
  "private": true,
  "type": "module",
  "dependencies": {
    "@bellatrix/core": "file:../BELLATRIX-JavaScript/@bellatrix/core",
    "@bellatrix/web": "file:../BELLATRIX-JavaScript/@bellatrix/web",
    "@bellatrix/extras-web": "file:../BELLATRIX-JavaScript/@bellatrix/extras/web",
    "@bellatrix/runner": "file:../BELLATRIX-JavaScript/@bellatrix/runner"
  },
  "devDependencies": {
    "typescript": "5.7.3",
    "@types/node": "22.13.1",
    "@types/selenium-webdriver": "4.1.28",
    "@types/stack-trace": "0.0.33"
  },
  "scripts": {
    "test": "bellatrix ./tests filter --category CI",
    "typecheck": "tsc --noEmit -p tsconfig.json"
  },
  "overrides": {
    "@playwright/test": "1.50.1",
    "playwright": "1.50.1",
    "playwright-core": "1.50.1",
    "selenium-webdriver": "4.28.1",
    "vitest": "3.0.5"
  }
}
```

The `overrides` block pins Playwright/Selenium to the framework's lock file — required: newer versions from the registry
(e.g. Playwright 1.64, selenium-webdriver 4.51) break the framework's own types (`TS2589`/`TS2339` in
`PlaywrightWebElement.ts`).

The framework ships `.ts` source and is type-checked as part of this program, so keep TypeScript at the version the framework pins (peer dependency `^5.7.3`) and keep the `@types/*` packages. If `tsc` reports errors INSIDE `node_modules/@bellatrix/*`:
- `TS2313 Type parameter 'T' has a circular constraint` in `WebComponent.ts` → the check order is wrong. `bellatrix.config.ts` must start with `import type {} from '@bellatrix/web/test';` and stay the FIRST entry of `include` (see the config in `bellatrix-web-app-services`).
- Hundreds of missing-module errors → the `.npmrc` `install-links=true` is missing (packages were symlinked).
- `TS2589` / `TS2339` in `PlaywrightWebElement.ts` → the `overrides` block is missing or was changed.

```json
{
  "compilerOptions": {
    "lib": ["ESNext", "DOM"],
    "module": "esnext",
    "target": "esnext",
    "moduleResolution": "bundler",
    "moduleDetection": "force",
    "noImplicitOverride": true,
    "experimentalDecorators": false,
    "emitDecoratorMetadata": false,
    "useDefineForClassFields": true,
    "strict": true,
    "skipLibCheck": true,
    "allowSyntheticDefaultImports": true,
    "forceConsistentCasingInFileNames": true,
    "allowJs": false,
    "noEmit": true
  },
  "include": ["bellatrix.config.ts", "src/**/*.ts", "tests/**/*.ts"]
}
```

`experimentalDecorators` MUST stay `false` (BELLATRIX uses TC39 decorators), `moduleResolution` must resolve package `exports` subpaths (`bundler`/`node16`/`nodenext`), `lib` must include `DOM`, and `bellatrix.config.ts` stays the first `include` entry.

### `src/Categories.ts`

```ts
export const Categories = Object.freeze({
    Smoke: 'Smoke',
    Regression: 'Regression',
    CI: 'CI',
} as const);
```

### `src/attributes/ManualTestCase.ts`

```ts
import { getMetadataFor } from '@bellatrix/core/utilities';
import type { BellatrixTest } from '@bellatrix/core/infrastructure';
import type { Method } from '@bellatrix/core/types';

const manualTestCaseKey = 'manualTestCase';

/**
 * Links an automated test to a SPECTRA manual test case for traceability.
 * Unlike BELLATRIX's @TestCase (data-driven parameterization), this decorator only stores
 * the ID in the test's metadata — enabling `bellatrix ./tests filter --manualTestCase TC-XXX`
 * and SPECTRA coverage scanning of `@ManualTestCase('TC-XXX')`.
 */
export function ManualTestCase<
    This extends BellatrixTest = BellatrixTest,
    ClassMethod extends Method<This> = Method<This>
>(id: string) {
    return function (testMethod: ClassMethod, context: ClassMethodDecoratorContext): void {
        if (context.kind !== 'method') {
            throw new Error('@ManualTestCase can only decorate test methods.');
        }

        const testMetadata = getMetadataFor(testMethod);
        const existing = testMetadata.customData.get(manualTestCaseKey);
        const ids = Array.isArray(existing) ? existing as string[] : [];
        ids.push(id);
        testMetadata.customData.set(manualTestCaseKey, ids);
    };
}
```

Import it in tests with a relative path: `import { ManualTestCase } from '../../src/attributes/ManualTestCase';`.

### `src/utilities/waits.ts`

The framework has no URL wait, and `wait.toNotExist()` / `wait.toNotBeVisible()` fail when the element is already gone (see `bellatrix-web-locators-and-waits`). These two helpers fill the gaps using only BELLATRIX APIs:

```ts
import { App } from '@bellatrix/web/infrastructure';
import { ComponentsList, WebComponent } from '@bellatrix/web/components';
import { BellatrixSettings } from '@bellatrix/core/settings';

/** Waits until the current URL contains `partialUrl` (pageLoadTimeout). */
export async function waitForPartialUrl(app: App, partialUrl: string): Promise<void> {
    const { pageLoadTimeout, sleepInterval } = BellatrixSettings.get().webSettings.timeoutSettings;
    const deadline = Date.now() + pageLoadTimeout;
    let currentUrl = await app.browser.getUrl();
    while (!currentUrl.includes(partialUrl)) {
        if (Date.now() > deadline) {
            throw new Error(`URL '${currentUrl}' did not contain '${partialUrl}' within ${pageLoadTimeout} ms.`);
        }

        await app.sleep(sleepInterval);
        currentUrl = await app.browser.getUrl();
    }
}

/** Waits until a list locator matches nothing (validationTimeout). Pass a factory so every poll re-finds. */
export async function waitUntilGone<T extends WebComponent>(app: App, list: () => ComponentsList<T>): Promise<void> {
    const { validationTimeout, sleepInterval } = BellatrixSettings.get().webSettings.timeoutSettings;
    const deadline = Date.now() + validationTimeout;
    while ((await list().count()) > 0) {
        if (Date.now() > deadline) {
            throw new Error(`Elements were still present after ${validationTimeout} ms.`);
        }

        await app.sleep(sleepInterval);
    }
}
```

---

## `src/pages/` — Page Objects (Map / Page / Asserts trio)

Each page gets its own lower-case folder with `{PageName}Map.ts` (locator getters), `{PageName}.ts` (`@Page`, `url`, `waitForPageLoad()`, workflows), `{PageName}Asserts.ts` (assertion methods), and is exported from `src/pages/index.ts`.

| Folder | Page class | URL | Purpose |
|--------|-----------|-----|---------|
| _(none yet — add a row when you create a page)_ | | | |

**Base class:** `WebPage<Map, Asserts>` + `@Page(Map, Asserts)`

---

## `src/sections/` — Reusable Page Sections

Sections are url-less `WebPage` subclasses (same trio, same `@Page` wiring) that group locators and workflows shared by several pages (header, navigation, footer, cookie banner, mini-cart). Pages expose them through a getter — `get header(): HeaderSection { return this.app.createPage(HeaderSection); }` — and delegate from their action methods. Never `goTo` a section.

| Folder | Used by | Purpose |
|--------|---------|---------|
| _(none yet)_ | | |

**When to CREATE a new section:** when two or more pages expose the same locator set AND the same workflow around it. A single-page duplicate stays on the page as a region.

---

## `src/components/` — Custom Components

Custom BELLATRIX components live here when a built-in lacks behavior or needs an override. New component = `extends WebComponent<HTMLElementType>`; behavior tweak = extend an existing component (e.g. `Button`) and `override` its async methods. Every custom component carries `@BellatrixWebComponent`.

| File | Extends | Why |
|------|---------|-----|
| _(none yet)_ | | |

**Usage in Map classes:** `this.create(MyComponent).byCss("[data-testid='…']")`.

---

## `src/models/` — Data Types and Option Sets

Plain `type` aliases and frozen `as const` option objects shared across page objects. No component or BELLATRIX types here — pure data.

| File | Purpose |
|------|---------|
| _(none yet)_ | |

---

## `tests/` — Test Classes (grouped by feature area)

One `*.test.ts` file per feature area under a lower-case folder. One test method per SPECTRA TC. Every traced method carries `@ManualTestCase('TC-XXX')`.

| Folder | File | Feature area |
|--------|------|--------------|
| _(none yet)_ | | |

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a page NOT in the Pages table above | **CREATE** `src/pages/{pagename}/` with the 3 classes + export it from `src/pages/index.ts` | `src/pages/` |
| Test case targets an existing page | **EDIT** that page's Map/Page/Asserts; append new members | `src/pages/{pagename}/` |
| A modal / drawer / wizard step with its own locators | **CREATE** its own page object (or a section if it appears on several pages) | `src/pages/` or `src/sections/` |
| Test case's feature area is NOT in the Tests table above | **CREATE** `tests/{featurearea}/{FeatureArea}Tests.test.ts` | `tests/` |
| Test case's feature area exists | **EDIT** — add a new `@Test` method to the existing class | `tests/{featurearea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** a custom component (`@BellatrixWebComponent`) extending `WebComponent` or a built-in | `src/components/` |
| Same locator block + workflow duplicated across ≥2 pages | **CREATE** a url-less section and embed it via a getter on each page | `src/sections/` |
| Need a shared data type / option set | **EDIT** an existing model if it fits; otherwise **CREATE** a new file | `src/models/` |
| `src/attributes/ManualTestCase.ts` or `src/utilities/waits.ts` missing | **CREATE** it from the definition above before the first use | `src/` |

---

## Sync

Your agent's instructions may reference this skill. When you add a new folder, page, section, component, model, or test class, ALSO update this skill's tables in the same change.
