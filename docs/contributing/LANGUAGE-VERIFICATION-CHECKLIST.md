# BELLATRIX — Language Verification Checklist

Lets you repeat the compile checks yourself in a real Maven project and a real TypeScript project, with your own AI agent.
The automated compile checks used a separate local Maven repo and `file:` npm deps. This checklist uses your normal
setup.

**Rule for every step:** the agent may **build** (`mvn test-compile`, `npx tsc --noEmit`, `dotnet build`) but must
**never run the tests**. A compile error caused by a skill is a skill bug. Note the skill name and section.

## 0. Prerequisites
- [ ] `bellatrix --version` → `{"version":"2.5.0"}` or later
- [ ] Java: JDK 19+ (BELLATRIX-Java targets 19), Maven 3.8+, and BELLATRIX-Java 1.0 jars that are **current**
  (`mvn install` from a fresh https://github.com/AutomateThePlanet/BELLATRIX-Java checkout; the 2023 jars in an old `~/.m2` are stale)
- [ ] TypeScript: Node 18+, and the `@bellatrix/*` packages from a https://github.com/AutomateThePlanet/BELLATRIX-JavaScript checkout (e.g. `file:` deps)

## 1. Java (Maven sample)
For each domain, start in an empty folder with a minimal `pom.xml`, so auto-detect picks Java.
- [ ] `bellatrix init --agent claude --domains desktop` → JSON `"language":"java"`, 12 `bellatrix-desktop-*` skills
  + generator + locator specialist, no warnings
- [ ] Ask the agent: *"Using bellatrix-desktop-generate-code, create a Calculator page object and a test for TC-1001
  (`@ManualTestCase("TC-1001")`), following bellatrix-desktop-project-structure. Build only."* → `mvn test-compile`
  is green
- [ ] Repeat with `--domains web` (demo shop https://demos.bellatrix.solutions/) → green
- [ ] Repeat with `--domains mobile`: one Android page + test and one iOS page + test → green
- [ ] Generated code uses `@ManualTestCase("TC-…")` (project-defined, `String value()`), never `int`

## 2. TypeScript (npm sample)
Start in an empty folder with `package.json` + `tsconfig.json`, so auto-detect picks TypeScript.
- [ ] `bellatrix init --agent claude --domains web` → `"language":"typescript"`, 12 `bellatrix-web-*` skills + generator
- [ ] Ask the agent for a home/cart page object (Map + Asserts) and a test for TC-1001 → `npx tsc --noEmit` is green
- [ ] `bellatrix.config.ts` starts with `import type {} from '@bellatrix/web/test';` (avoids TS2313 inside the framework)
- [ ] `bellatrix init --agent claude --domains desktop,mobile` → warning *"BELLATRIX TypeScript supports web only …"*;
  only `execute-scenario`, `execute-spectra-test` and the locator specialist per domain

## 3. Mixed / explicit
- [ ] A folder with both `pom.xml` and `*.csproj` → `init` exits 2 and asks for `--language`
- [ ] `--language java` on a C# folder → Java skills, recorded in `.bellatrix/manifest.claude.json` (`"language":"java"`);
  a later `bellatrix update-skills` keeps Java without the flag

Record per cell (language × domain): GREEN / RED + the skill defect, if any.

## 4. Per-language live items (moved from LIVE-VERIFICATION-CHECKLIST §C in v.3)

The compile checks were automated during development: generated C# desktop, web and mobile projects build against
the current BELLATRIX source (https://github.com/AutomateThePlanet/BELLATRIX). What remains is the end-to-end agent experience on a real
app, which needs a driver.

### 4.1 C# (2.4.0)
- [ ] **Desktop:** in a repo with a BELLATRIX desktop test project and a SPECTRA TC, ask your agent to use
  `bellatrix-desktop-system-tests-generator` to automate the TC (Calculator). It runs the CLI live, delegates
  locators, generates the page object and test with `[ManualTestCase("TC-…")]`, runs `dotnet build`, and does **not**
  run the tests
- [ ] **Web:** the same with `bellatrix-web-system-tests-generator` against https://demos.bellatrix.solutions/
  (no Selenium server needed)
- [ ] **Mobile (Android):** the same with `bellatrix-mobile-system-tests-generator` against ApiDemos on an emulator.
  Generated Android locators use `CreateByDescription` for content-desc, never `CreateByAccessibilityId`
- [ ] Run the generated tests yourself; record pass/fail per domain

### 4.2 Java (2.5.0)
Compile checks were automated during development; repeat them with §1.
- [ ] **Desktop:** in a Maven repo with BELLATRIX-Java, `bellatrix init --agent claude --language java --domains desktop`, then
  ask your agent to use `bellatrix-desktop-system-tests-generator` to automate a SPECTRA TC against Calculator. It drives the
  CLI live, delegates locators, generates `{Window}` + `Map` + `Asserts` and a test with `@ManualTestCase("TC-…")`, runs
  `mvn test-compile`, and does **not** run tests. Note: BELLATRIX-Java desktop `byXPath` uses `By.tagName` at runtime. The
  skills steer to `byXPath`-free strategies; confirm the generated locators run
- [ ] **Web:** the same with `bellatrix-web-system-tests-generator` against https://demos.bellatrix.solutions/
- [ ] **Mobile:** Android (ApiDemos on an emulator) and, if you have a Mac, iOS. The skills document framework runtime
  pitfalls (validators read once; iOS "not exists" waits time out; `ComboBox.selectByText` NPE → `SpinnerComboBox`);
  confirm they hold on a device
- [ ] Run the generated tests yourself; record pass/fail per domain

### 4.3 TypeScript (2.5.0) — web only
- [ ] **Web:** `bellatrix init --agent claude --language typescript --domains web` in an npm project with `@bellatrix/web`;
  the generator automates a TC against the demo shop, writes Map/Asserts page + test, runs `npx tsc --noEmit`, never runs tests
- [ ] Run the generated test with the BELLATRIX runner yourself (Playwright controller); confirm the `getValue()`/
  `getMaxLength()` caveats in the validations skill match what you see
- [ ] `--domains desktop,mobile` with TypeScript: warning only + the 3 agnostic CLI skills; `execute-scenario` works live
  through the CLI (no code generation)
