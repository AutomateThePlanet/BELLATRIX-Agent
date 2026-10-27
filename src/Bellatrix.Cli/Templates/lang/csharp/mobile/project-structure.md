# MyProject.MobileTests — Current Project Structure

This file is the authoritative as-is snapshot of the mobile test project. Use it to decide whether to
CREATE a new file/folder or EDIT an existing one. It is NOT aspirational — only list a folder or file
after it actually exists on disk.

> **First use:** this skill ships with placeholder names (`MyProject.MobileTests`). The first time you
> generate code, scan the real project (`*.csproj` that references `Bellatrix.Mobile`), replace the
> placeholders below with the real project name, namespaces, app constants, and folders, and empty the
> example tables.
>
> **Maintenance rule:** whenever you add a page object folder, section, custom component, model, or tests
> sub-folder as part of a generation workflow, update the corresponding table in the same change. The
> agent owns keeping this tree accurate.

---

## Top-Level Layout

```
MyProject.MobileTests/
├─ MyProject.MobileTests.csproj       # references Bellatrix.Mobile (+ NUnit or MSTest)
├─ TestsInitialize.cs                 # NUnit [SetUpFixture] — BELLATRIX bootstrap / teardown
├─ Constants.cs                       # app path, app id / bundle id, platform version, device, activity
├─ ManualTestCaseAttribute.cs         # [ManualTestCase("TC-XXX")] for SPECTRA traceability
├─ testFrameworkSettings.Debug.json   # mobileSettings — timeouts, lifecycle, Appium url, capabilities
├─ Components/                        # Custom BELLATRIX components (overrides / new)
├─ Models/                            # Enums and data classes used by page objects
├─ Pages/{ScreenName}/                # Page objects — 3-file partial class pattern
├─ Pages/Shared/                      # Reusable page-object sections (composition)
└─ Tests/{FeatureArea}/               # Test classes grouped by feature area
```

### Key facts
- **Platform:** Android (UiAutomator2) — page base `AndroidPage`, test base `AndroidTest`, attribute
  `[Android(...)]`. For iOS: `IOSPage`, `IOSTest`, `[IOS(...)]`.
- **Appium endpoint:** `http://127.0.0.1:4723` — started by the user, never by the agent
  (`shouldStartLocalService: "false"`).
- **App under test:** `Constants.AppPath` / `Constants.AppId` (fill in from the real project).
- **Global lifecycle (config):** `restart every time`.
- **Class-level lifecycle (generated classes default):** `Lifecycle.ReuseIfStarted` — speed
  optimization that overrides the config per test class.
- **Build:** `dotnet build MyProject.MobileTests/MyProject.MobileTests.csproj`

### Required project wiring (`*.csproj`)

```xml
<PropertyGroup>
  <!-- Match the TargetFramework of the Bellatrix.Mobile you reference (net10.0 for current BELLATRIX source). -->
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings> <!-- if disabled, add `using System;` yourself -->
  <IsPackable>false</IsPackable>
  <NoWarn>$(NoWarn);MSTEST0001</NoWarn>   <!-- MSTest analyzers arrive transitively via Bellatrix.Mobile -->
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="…" />
  <PackageReference Include="NUnit" Version="…" />
  <PackageReference Include="NUnit3TestAdapter" Version="…" />
  <PackageReference Include="Unity" Version="…" /> <!-- BELLATRIX's DI container; the GettingStarted samples reference it -->
  <!-- plus Bellatrix.Mobile: NuGet package or ProjectReference to Bellatrix.Mobile.csproj (+ Bellatrix.Core) -->
</ItemGroup>

<ItemGroup>
  <None Update="testFrameworkSettings.$(Configuration).json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Avoid a namespace segment named `Android` or `IOS` (e.g. `MyProject.MobileTests.Android`): it collides with the
`[Android]` / `[IOS]` attributes. Use `AndroidScreens` / `IOSScreens` style names.

### Both platforms?

Android and iOS page objects cannot share a class (different component types). Prefer one test project
per platform (`MyProject.AndroidTests`, `MyProject.IOSTests`) — as the BELLATRIX samples do. If both
must live in one project, keep them in separate folders and namespaces (e.g. `Pages/AndroidScreens/…`,
`Pages/IOSScreens/…`) and never import `Bellatrix.Mobile.Android` and `Bellatrix.Mobile.IOS` in the
same file.

---

## Root Files

### `Constants.cs`

```csharp
namespace MyProject.MobileTests;

public static class Constants
{
    public const string AppPath = @"AssemblyFolder\Apps\MyApp.apk";
    public const string AppId = "com.example.myapp";
    public const string PlatformVersion = "14";
    public const string DeviceName = "Android Emulator";
    public const string StartActivity = ".MainActivity";

    // iOS projects: set AppPath / PlatformVersion / DeviceName to the iOS values (.app path, iOS version,
    // simulator name) and add the bundle id, which the [IOS(...)] templates also use.
    public const string BundleId = "com.example.myapp";
}
```

### `ManualTestCaseAttribute.cs`

Create it if it does not exist (string-based, metadata only):

```csharp
using System;

namespace MyProject.MobileTests;

/// <summary>
/// Links an automated test to a SPECTRA manual test case for traceability.
/// Unlike NUnit's [TestCase], this attribute does not affect test parameterization —
/// it is purely a metadata marker used by SPECTRA coverage scanning.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ManualTestCaseAttribute : Attribute
{
    public ManualTestCaseAttribute(string testCaseId)
    {
        TestCaseId = testCaseId;
    }

    public string TestCaseId { get; }
}
```

(BELLATRIX also ships an `int`-based `Bellatrix.Data.Attributes.ManualTestCaseAttribute` — do not
import that namespace in test files.)

### `TestsInitialize.cs`

See `bellatrix-mobile-test-structure-and-lifecycle` → Assembly Bootstrap. One per project, in the
global namespace.

---

## `Pages/` — Page Objects (3-file partial class pattern)

Each screen gets its own folder with `{ScreenName}.Map.cs` (base class + locators), `.Actions.cs`
(workflows), `.Assertions.cs` (`Assert*` methods using `Validate*`).

| Folder | Platform | Purpose |
|--------|----------|---------|
| `Pages/LoginPage/` *(example — replace)* | Android | Login screen — user name, password, remember me, error message |

**Namespace:** `MyProject.MobileTests.Pages`
**Base class:** `AndroidPage` (override `PackageName`, `ActivityName`) / `IOSPage`

---

## `Pages/Shared/` — Reusable Page-Object Sections

Plain classes (NOT `AndroidPage` / `IOSPage`) that group locators and workflows shared by several
screens (bottom navigation, toolbar, number pad). Screens embed them via a
`public SectionName Section { get; } = new SectionName();` property on the Map partial and delegate
from their actions. Each section resolves `App` itself via
`ServicesCollection.Current.Resolve<AndroidApp>()` (`IOSApp` on iOS).

| File | Used by | Purpose |
|------|---------|---------|
| *(none yet)* | | |

**Namespace:** `MyProject.MobileTests.Pages.Shared`
**When to CREATE a new section:** two or more screens expose the same locator set AND the same workflow.

---

## `Components/` — Custom Components

Custom BELLATRIX components live here when a built-in is missing behavior or needs an override. Inherit
the closest built-in (`Button`, `ComboBox`, …) and add or override members; inherit `AndroidComponent` /
`IOSComponent` only for read-only containers.

| File | Extends | Why |
|------|---------|-----|
| *(none yet)* | | |

**Namespace:** `MyProject.MobileTests.Components`
**Usage in Map files:** `App.Components.CreateByDescription<ScrollingButton>("...")`.

---

## `Models/` — Data Models and Enums

Plain data classes and enums shared across page objects. No component or BELLATRIX types here.

| File | Purpose |
|------|---------|
| *(none yet)* | |

**Namespace:** `MyProject.MobileTests.Models`

---

## `Tests/` — Test Classes (grouped by feature area)

One file per feature area. One test method per SPECTRA TC. Every method carries
`[ManualTestCase("TC-XXX")]`.

| Folder | File | Feature area |
|--------|------|--------------|
| *(none yet)* | | |

**Namespace:** `MyProject.MobileTests.Tests` (check an existing file before deciding on nested
namespaces)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a screen NOT in the Pages table | **CREATE** `Pages/{ScreenName}/` with 3 partial files | `Pages/` |
| Test case targets an existing screen | **EDIT** that screen's Map/Actions/Assertions; append new members | `Pages/{ScreenName}/` |
| Feature area NOT in the Tests table | **CREATE** `Tests/{FeatureArea}/{App}{FeatureArea}Tests.cs` | `Tests/` |
| Feature area exists | **EDIT** — add a new `[Test]` method to the existing class | `Tests/{FeatureArea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** a custom component | `Components/` |
| Same locator block + workflow on ≥2 screens | **CREATE** a section and embed it via composition | `Pages/Shared/` |
| Need a shared enum / data class | **EDIT** an existing one if it fits, else **CREATE** | `Models/` |
| `ManualTestCaseAttribute.cs` / `Constants.cs` missing | **CREATE** at the project root | root |

---

## Sync

Your agent's instructions may reference this skill. Whenever you add a folder, section, component,
model, or test file, ALSO update this skill's tables in the same change.
