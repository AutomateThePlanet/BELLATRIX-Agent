<#
.SYNOPSIS
  Live CLI E2E suite (Level 1): checks prerequisites, enables only the domains this machine can run,
  runs tests/Bellatrix.Cli.E2E, and writes artifacts/e2e/e2e-report.json + e2e-summary.md.

.DESCRIPTION
  Never starts or stops WinAppDriver, Appium or a Selenium server - it only GETs their /status. A prerequisite
  that is not reachable is reported as SKIPPED, not FAILED. Every test step runs the INSTALLED `bellatrix` (or
  -Exe) as a separate process. The reattach tests need the trace log (BELLATRIX_TRACE), i.e. bellatrix >= 3.0.1.
  Windows PowerShell 5.1 compatible.

.EXAMPLE
  powershell -File scripts/run-e2e.ps1
  powershell -File scripts/run-e2e.ps1 -SeleniumUrl http://localhost:4444 -Apk .\ApiDemos-debug.apk
#>
param(
    [string]$Exe = "bellatrix",
    [string]$DriverUrl = "http://127.0.0.1:4723",
    [string]$SeleniumUrl = "http://localhost:4444",
    [string]$Apk = $(if ($env:BELLATRIX_E2E_APK) { $env:BELLATRIX_E2E_APK } else { "ApiDemos-debug.apk" }),
    [string]$OutDir = ""
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo "artifacts\e2e" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$prereqs = @()
function Add-Prereq($name, $ok, $detail, $gate) {
    $script:prereqs += [pscustomobject]@{ name = $name; status = $(if ($ok) { "OK" } else { "NOT-REACHABLE" }); detail = $detail; enables = $gate }
}
function Get-Json($url) {
    try { return Invoke-RestMethod -Uri $url -TimeoutSec 4 -UseBasicParsing } catch { return $null }
}

# 1. The CLI itself (required).
$version = $null
try { $version = ((& $Exe --version) | ConvertFrom-Json).version } catch { }
if (-not $version) { throw "'$Exe' not found or not runnable. Install the tool (src/Bellatrix.Cli/install.ps1) or pass -Exe." }
# Probe the trace log directly (dev builds may still carry the previous version number).
$probe = Join-Path ([IO.Path]::GetTempPath()) ("bellatrix-trace-probe-" + [guid]::NewGuid().ToString("N") + ".jsonl")
$env:BELLATRIX_TRACE = $probe
& $Exe --version | Out-Null
Remove-Item Env:\BELLATRIX_TRACE
$traceOk = Test-Path $probe
if ($traceOk) { Remove-Item $probe }
Add-Prereq "bellatrix" $true "version $version$(if (-not $traceOk) { ' (no BELLATRIX_TRACE support: reattach tests will fail; need >= 3.0.1)' })" "all"

# 2. The driver on 4723: WinAppDriver (desktop) or Appium (mobile; desktop too when McpSettings__DriverType=NovaWindows).
$status = Get-Json "$DriverUrl/status"
$desktop = $false; $appium = $false
if ($status -and $status.value -and $status.value.build) { $appium = $true; $kind = "Appium $($status.value.build.version)" }
elseif ($status -and $status.build) { $desktop = $true; $kind = "WinAppDriver $($status.build.version)" }
else { $kind = "nothing answering /status" }
if ($appium -and $env:McpSettings__DriverType -eq "NovaWindows") { $desktop = $true }
Add-Prereq "desktop driver ($DriverUrl)" $desktop $kind "BELLATRIX_E2E_DESKTOP"

# 3. Local web: Chrome installed (the CLI fetches the matching chromedriver itself).
$chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
            "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe") | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
Add-Prereq "Chrome (local driver)" ([bool]$chrome) $(if ($chrome) { $chrome } else { "chrome.exe not found" }) "BELLATRIX_E2E_WEB"

# 4. Remote web: a Selenium server.
$sel = Get-Json "$SeleniumUrl/status"
$selOk = [bool]($sel -and $sel.value -and $sel.value.ready)
Add-Prereq "Selenium server ($SeleniumUrl)" $selOk $(if ($selOk) { "ready" } else { "not reachable or not ready" }) "BELLATRIX_E2E_WEB_REMOTE"

# 5. Mobile: Appium + an adb device + the ApiDemos APK.
# `adb devices` may start the adb server daemon, which would inherit (and hold open) a stdout pipe - and
# `Start-Process -Wait` waits for the daemon too. So: cmd /c with file redirection, bounded wait on cmd only.
$device = $null
$adbOut = Join-Path ([IO.Path]::GetTempPath()) "bellatrix-e2e-adb.txt"
if (Get-Command adb -ErrorAction SilentlyContinue) {
    $adbProc = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "adb devices > `"$adbOut`" 2>&1 < NUL" -NoNewWindow -PassThru
    if ($adbProc.WaitForExit(20000) -and (Test-Path $adbOut)) {
        $device = Get-Content $adbOut | Where-Object { $_ -match "`tdevice$" } | Select-Object -First 1
    }
}
$mobileOk = $appium -and [bool]$device -and (Test-Path $Apk)
$mobileDetail = "appium=$appium; adb device=$(if ($device) { ($device -split "`t")[0] } else { 'none' }); apk=$(if (Test-Path $Apk) { $Apk } else { "missing: $Apk" })"
Add-Prereq "Android (Appium + emulator + APK)" $mobileOk $mobileDetail "BELLATRIX_E2E_MOBILE"

# Gates: only what this machine can satisfy.
$env:BELLATRIX_E2E = "1"
$env:BELLATRIX_E2E_EXE = $Exe
$env:BELLATRIX_E2E_DESKTOP = $(if ($desktop) { "1" } else { "" })
$env:BELLATRIX_E2E_WEB = $(if ($chrome) { "1" } else { "" })
$env:BELLATRIX_E2E_WEB_REMOTE = $(if ($selOk) { "1" } else { "" })
$env:BELLATRIX_E2E_MOBILE = $(if ($mobileOk) { "1" } else { "" })
$env:BELLATRIX_E2E_SELENIUM_URL = $SeleniumUrl
$env:BELLATRIX_E2E_APK = $Apk

Write-Host "Prerequisites:"
$prereqs | ForEach-Object { Write-Host ("  [{0,-13}] {1} - {2}" -f $_.status, $_.name, $_.detail) }

# Run.
$trx = Join-Path $OutDir "e2e.trx"
if (Test-Path $trx) { Remove-Item $trx }
& dotnet test (Join-Path $repo "tests\Bellatrix.Cli.E2E\Bellatrix.Cli.E2E.csproj") --nologo `
    --logger "trx;LogFileName=$trx" --logger "console;verbosity=normal"
$testExit = $LASTEXITCODE

# Report.
$results = @()
if (Test-Path $trx) {
    [xml]$x = Get-Content $trx -Raw
    foreach ($r in $x.TestRun.Results.UnitTestResult) {
        $outcome = switch ($r.outcome) { "Passed" { "PASSED" } "Failed" { "FAILED" } default { "SKIPPED" } }
        $msg = $null
        if ($r.Output -and $r.Output.ErrorInfo) { $msg = [string]$r.Output.ErrorInfo.Message }
        elseif ($r.Output -and $r.Output.StdOut) { $msg = [string]$r.Output.StdOut }
        $ms = 0
        if ($r.duration) { $ms = [int]([timespan]::Parse($r.duration).TotalMilliseconds) }
        $results += [pscustomobject]@{ test = ($r.testName -replace '^Bellatrix\.Cli\.E2E\.', ''); outcome = $outcome; durationMs = $ms; message = $msg }
    }
}
$totals = [ordered]@{
    passed = @($results | Where-Object outcome -eq "PASSED").Count
    failed = @($results | Where-Object outcome -eq "FAILED").Count
    skipped = @($results | Where-Object outcome -eq "SKIPPED").Count
}
$report = [ordered]@{
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    bellatrixVersion = $version
    exe = $Exe
    machine = $env:COMPUTERNAME
    prerequisites = $prereqs
    results = $results
    totals = $totals
    dotnetTestExitCode = $testExit
}
$report | ConvertTo-Json -Depth 6 | Out-File -Encoding utf8 (Join-Path $OutDir "e2e-report.json")

$md = @("# BELLATRIX CLI live E2E - $((Get-Date).ToString('yyyy-MM-dd HH:mm'))", "",
        "bellatrix $version on $env:COMPUTERNAME - passed $($totals.passed), failed $($totals.failed), skipped $($totals.skipped)", "",
        "| Prerequisite | Status | Detail |", "|---|---|---|")
$md += $prereqs | ForEach-Object { "| $($_.name) | $($_.status) | $($_.detail) |" }
$md += @("", "| Test | Outcome | ms | Note |", "|---|---|---|---|")
$md += $results | Sort-Object test | ForEach-Object {
    $note = if ($_.message) { (($_.message -replace '\r?\n', ' ') -replace '\|', '/') } else { "" }
    if ($note.Length -gt 160) { $note = $note.Substring(0, 160) + "..." }
    "| $($_.test) | $($_.outcome) | $($_.durationMs) | $note |"
}
$md -join "`n" | Out-File -Encoding utf8 (Join-Path $OutDir "e2e-summary.md")

Write-Host ""
Write-Host "Report: $(Join-Path $OutDir 'e2e-report.json')"
Write-Host "Summary: $(Join-Path $OutDir 'e2e-summary.md')"
exit $(if ($totals.failed -gt 0) { 1 } else { 0 })
