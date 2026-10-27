<#
.SYNOPSIS
  Replaces the conformance snapshot fixtures (tests/fixtures/snapshots/) with LIVE recordings.

.DESCRIPTION
  Run when the drivers are up. This script never starts, stops or probes WinAppDriver / Appium / an emulator; a domain
  whose driver is not reachable is reported as SKIPPED and its fixture is left as it is.

    desktop  `bellatrix desktop start-session --app calculator` -> `get-page-source` -> desktop-calculator.page-source.xml
    mobile   `bellatrix mobile start-session --platform android --app <apk>` -> `get-page-source` -> android-apidemos.page-source.xml
    web      `bellatrix web start-session --headless` -> navigate the demo shop -> `snapshot` -> web-demo-shop.json

  Then, for desktop/mobile, the fixture JSON is regenerated from the new XML by the production parser
  (`BELLATRIX_UPDATE_FIXTURES=1 dotnet test --filter FixtureTests`), the answer key is marked "synthetic": false with
  "recordedWith", and the answer-key tests run again WITHOUT the update flag. Any target that no longer resolves to
  exactly one node is listed: fix its findArgs/expected in the .answers.json (the live app may differ from the
  synthetic page source), then re-run `dotnet test --filter FixtureTests`.

  Review the diff before committing: a recording contains whatever the app showed at the time.

.EXAMPLE
  powershell -File scripts/record-fixtures.ps1 -Domain web
  powershell -File scripts/record-fixtures.ps1 -Domain all -Apk .\ApiDemos-debug.apk
#>
param(
    [ValidateSet("desktop", "web", "mobile", "all")][string]$Domain = "all",
    [string]$Exe = "bellatrix",
    [string]$Apk = $(if ($env:BELLATRIX_E2E_APK) { $env:BELLATRIX_E2E_APK } else { "ApiDemos-debug.apk" }),
    [string]$CalculatorAppId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
    [string]$WebUrl = "https://demos.bellatrix.solutions/",
    [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$fixtureDir = Join-Path $repo "tests\fixtures\snapshots"
$work = Join-Path ([IO.Path]::GetTempPath()) ("bellatrix-record-fixtures-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Force -Path $work | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Invoke-Cli([string[]]$arguments, [hashtable]$envVars = @{}) {
    # File redirection, never a pipe: a detached driver/daemon child could hold a pipe open (v.1 finding).
    $out = [IO.Path]::GetTempFileName(); $err = [IO.Path]::GetTempFileName()
    $quoted = ($arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = '/d /s /c ""' + (Get-Command $Exe).Source + '" ' + $quoted + ' > "' + $out + '" 2> "' + $err + '" < NUL"'
    $psi.WorkingDirectory = $work
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    foreach ($k in $envVars.Keys) { $psi.EnvironmentVariables[$k] = [string]$envVars[$k] }
    $p = [System.Diagnostics.Process]::Start($psi)
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) { try { & taskkill /PID $p.Id /T /F | Out-Null } catch { }; throw "timeout: bellatrix $($arguments -join ' ')" }
    $text = [string](Get-Content $out -Raw -ErrorAction SilentlyContinue)
    Remove-Item $out, $err -ErrorAction SilentlyContinue
    $json = $null; try { $json = $text | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ exitCode = $p.ExitCode; json = $json; text = $text }
}

function Test-Unreachable($r) { return ($r.exitCode -ne 0 -and "$($r.text)" -match 'driver_unreachable') }

$desktopEnv = @{ "McpSettings__Apps__0__Name" = "calculator"; "McpSettings__Apps__0__Path" = $CalculatorAppId }
$today = (Get-Date).ToString("yyyy-MM-dd")
$version = (Invoke-Cli @("--version")).json.version
$recorded = @(); $skipped = @()

function Save-PageSource($domainId, $startArgs, $envVars, $xmlName) {
    $start = Invoke-Cli $startArgs $envVars
    if (Test-Unreachable $start) { $script:skipped += "$domainId (driver not reachable)"; return $false }
    if ($start.exitCode -ne 0) { throw "$domainId start-session failed: $($start.text)" }
    try {
        Start-Sleep -Seconds 2
        $ps = Invoke-Cli @($domainId, "get-page-source", "--session", "rec", "--json") $envVars
        if ($ps.exitCode -ne 0 -or -not $ps.json.pageSource) { throw "$domainId get-page-source failed: $($ps.text)" }
        [IO.File]::WriteAllText((Join-Path $fixtureDir $xmlName), [string]$ps.json.pageSource, $utf8)
    } finally {
        $null = Invoke-Cli @($domainId, "close-session", "--session", "rec", "--json") $envVars
    }
    return $true
}

function Set-Recorded($fixture, $what) {
    $path = Join-Path $fixtureDir "$fixture.answers.json"
    $text = [IO.File]::ReadAllText($path)
    $text = $text -replace '"synthetic":\s*true', '"synthetic": false'
    $text = $text -replace '"recordedWith":\s*(null|"[^"]*")', ('"recordedWith": "' + $what + '"')
    [IO.File]::WriteAllText($path, $text, $utf8)
}

try {
    if ($Domain -in @("desktop", "all")) {
        if (Save-PageSource "desktop" @("desktop", "start-session", "--app", "calculator", "--session", "rec", "--json") $desktopEnv "desktop-calculator.page-source.xml") {
            Set-Recorded "desktop-calculator" "bellatrix desktop get-page-source ($version), $today"; $recorded += "desktop"
        }
    }
    if ($Domain -in @("mobile", "all")) {
        if (-not (Test-Path $Apk)) { $skipped += "mobile (APK not found: $Apk)" }
        elseif (Save-PageSource "mobile" @("mobile", "start-session", "--platform", "android", "--app", $Apk, "--session", "rec", "--json") @{} "android-apidemos.page-source.xml") {
            Set-Recorded "android-apidemos" "bellatrix mobile get-page-source ($version), $today"; $recorded += "mobile"
        }
    }
    if ($Domain -in @("web", "all")) {
        $start = Invoke-Cli @("web", "start-session", "--browser", "chrome", "--headless", "--width", "1366", "--height", "900", "--session", "rec", "--json")
        if ($start.exitCode -ne 0) { $skipped += "web (start-session failed: $($start.text.Trim()))" }
        else {
            try {
                $nav = Invoke-Cli @("web", "navigate", "--session", "rec", "--url", $WebUrl, "--json")
                if ($nav.exitCode -ne 0) { throw "web navigate failed: $($nav.text)" }
                $snap = Invoke-Cli @("web", "snapshot", "--session", "rec", "--json")
                if ($snap.exitCode -ne 0 -or -not $snap.json.path) { throw "web snapshot failed: $($snap.text)" }
                $src = $snap.json.path; if (-not [IO.Path]::IsPathRooted($src)) { $src = Join-Path $work $src }
                Copy-Item $src (Join-Path $fixtureDir "web-demo-shop.json") -Force
                Set-Recorded "web-demo-shop" "bellatrix web snapshot ($version), $today"; $recorded += "web"
            } finally {
                $null = Invoke-Cli @("web", "close-session", "--session", "rec", "--json")
            }
        }
    }
} finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

Write-Host "Recorded: $(if ($recorded.Count) { $recorded -join ', ' } else { 'nothing' })"
foreach ($s in $skipped) { Write-Host "SKIPPED: $s" }
if (-not $recorded.Count) { exit 0 }

$tests = Join-Path $repo "tests\Bellatrix.Cli.Tests"
if ($recorded | Where-Object { $_ -ne "web" }) {
    Write-Host "Regenerating fixture JSON from the recorded page source (production parser)..."
    $env:BELLATRIX_UPDATE_FIXTURES = "1"
    try { & dotnet test $tests --nologo -v q --filter "FullyQualifiedName~FixtureTests.SyntheticFixture" | Out-Null } finally { Remove-Item Env:\BELLATRIX_UPDATE_FIXTURES }
}
Write-Host "Re-running the answer-key tests..."
$log = & dotnet test $tests --nologo --filter "FullyQualifiedName~FixtureTests" --logger "console;verbosity=normal" 2>&1 | ForEach-Object { [string]$_ }
$failed = @($log | Where-Object { $_ -match '^\s*Failed .*FixtureTests\.' } | ForEach-Object { $_.Trim() })
if ($failed.Count) {
    Write-Host "Targets that no longer resolve (fix findArgs/expected in the .answers.json, then re-run):"
    $failed | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "All fixture and answer-key tests pass. Review 'git diff tests/fixtures/snapshots' before committing."
