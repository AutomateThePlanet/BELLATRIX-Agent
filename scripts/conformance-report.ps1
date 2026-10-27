<#
.SYNOPSIS
  Aggregates agent-conformance results (Level 2) into docs/conformance-report.md, and optionally writes the
  evidence into docs/support-matrix.json.

.DESCRIPTION
  Data-driven: the columns are the agents in scripts/conformance/agents.json, the rows are every language x domain in
  scripts/conformance/languages.json (a domain the language does not support is "MISSING (framework)").
  Inputs: artifacts/conformance/<agent>-<language>.json as written by scripts/agent-conformance.ps1. No input file for a
  cell = NOT RUN. The report has no timestamp of its own, so re-running it on the same inputs gives the same file.

  Cell = DISCOVERY / DELEGATION:
    PASS / FAIL (n/m probes)   the probe verdicts (discovery is per agent x language, delegation per domain)
    NOT RUN                    no result, or no delegation probe for that domain
    SKIPPED-NOT-INSTALLED | SKIPPED-OUTDATED | SKIPPED-NOT-LOGGED-IN | ERROR   from the harness
  Label when both pass: "READY candidate"; "READY (synthetic fixtures)" when any delegation probe of that domain ran on
  a fixture marked synthetic - never plain READY on synthetic evidence.

  -UpdateMatrix writes { discovery, delegation, conformanceLabel } into each cell's evidence in docs/support-matrix.json.
  It never changes a cell's status: promoting a cell stays a reviewed edit (scripts/gen-readme-matrix.ps1 then
  regenerates the README).

.EXAMPLE
  powershell -File scripts/conformance-report.ps1
  powershell -File scripts/conformance-report.ps1 -InDir artifacts/conformance -UpdateMatrix
#>
param(
    [string]$InDir = "",
    [string]$OutFile = "",
    [string]$AgentTable = "",
    [switch]$UpdateMatrix
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
if (-not $InDir) { $InDir = Join-Path $repo "artifacts\conformance" }
if (-not $OutFile) { $OutFile = Join-Path $repo "docs\conformance-report.md" }
if (-not $AgentTable) { $AgentTable = Join-Path $PSScriptRoot "conformance\agents.json" }
$agentRows = @((Get-Content $AgentTable -Raw | ConvertFrom-Json).agents)
$langTable = Get-Content (Join-Path $PSScriptRoot "conformance\languages.json") -Raw | ConvertFrom-Json

function Read-Result($agentId, $languageId) {
    $path = Join-Path $InDir "$agentId-$languageId.json"
    if (-not (Test-Path $path)) { return $null }
    return ([string](Get-Content $path -Raw)) | ConvertFrom-Json
}

function Get-Cell($result, $domainId) {
    $cell = [ordered]@{ discovery = "NOT RUN"; delegation = "NOT RUN"; label = $null; synthetic = $false }
    if (-not $result) { return $cell }
    if ($result.status -ne "RAN") { $cell.discovery = $result.status; $cell.delegation = $result.status; return $cell }
    if ($result.discovery) { $cell.discovery = $(if ($result.discovery.pass) { "PASS" } else { "FAIL ($($result.discovery.score))" }) }
    $probes = @($result.delegation | Where-Object { $_ -and $_.domain -eq $domainId })
    if ($probes.Count) {
        $passed = @($probes | Where-Object { $_.pass }).Count
        $cell.delegation = $(if ($passed -eq $probes.Count) { "PASS" } else { "FAIL ($passed/$($probes.Count))" })
        $cell.synthetic = [bool](@($probes | Where-Object { $_.synthetic }).Count)
    }
    if ($cell.discovery -eq "PASS" -and $cell.delegation -eq "PASS") {
        $cell.label = $(if ($cell.synthetic) { "READY (synthetic fixtures)" } else { "READY candidate" })
    }
    return $cell
}

# docs/support-matrix.json is written with 2-space indentation (the layout of Python's json.dumps(indent=2,
# ensure_ascii=False)), so an evidence update changes only the evidence lines. PS 5.1 ConvertTo-Json would reformat it.
function ConvertTo-MatrixJson($value, [int]$level = 0) {
    if ($null -eq $value) { return "null" }
    if ($value -is [bool]) { return $(if ($value) { "true" } else { "false" }) }
    if ($value -is [string]) {
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.Append([char]34)
        foreach ($ch in $value.ToCharArray()) {
            $code = [int]$ch
            if ($code -eq 34 -or $code -eq 92) { [void]$sb.Append([char]92).Append($ch) }
            elseif ($code -eq 10) { [void]$sb.Append([char]92).Append('n') }
            elseif ($code -eq 13) { [void]$sb.Append([char]92).Append('r') }
            elseif ($code -eq 9) { [void]$sb.Append([char]92).Append('t') }
            elseif ($code -lt 32) { [void]$sb.Append([char]92).Append('u').Append($code.ToString("x4")) }
            else { [void]$sb.Append($ch) }
        }
        [void]$sb.Append([char]34)
        return $sb.ToString()
    }
    if ($value -is [ValueType]) { return ([IFormattable]$value).ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    $pad = "  " * ($level + 1); $close = "  " * $level
    if ($value -is [Management.Automation.PSCustomObject]) {
        $props = @($value.PSObject.Properties)
        if (-not $props.Count) { return "{}" }
        $items = foreach ($p in $props) { $pad + (ConvertTo-MatrixJson $p.Name) + ": " + (ConvertTo-MatrixJson $p.Value ($level + 1)) }
        return "{`n" + ($items -join ",`n") + "`n$close}"
    }
    $array = @($value)
    if (-not $array.Count) { return "[]" }
    $items = foreach ($v in $array) { $pad + (ConvertTo-MatrixJson $v ($level + 1)) }
    return "[`n" + ($items -join ",`n") + "`n$close]"
}

$results = @{}
foreach ($ag in $agentRows) { foreach ($lang in $langTable.languages) { $results["$($ag.id)|$($lang.id)"] = Read-Result $ag.id $lang.id } }
$found = @($results.Values | Where-Object { $_ })

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Agent conformance report (Level 2)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> Generated by ``scripts/conformance-report.ps1`` from ``artifacts/conformance/*.json`` (written by")
[void]$sb.AppendLine("> ``scripts/agent-conformance.ps1``). Do not edit by hand. Result files read: $($found.Count).")
if ($found.Count) {
    $latest = @($found | ForEach-Object { $_.finishedAt } | Where-Object { $_ } | Sort-Object)[-1]
    $versions = @($found | ForEach-Object { $_.bellatrixVersion } | Where-Object { $_ } | Sort-Object -Unique) -join ", "
    [void]$sb.AppendLine("> Latest run finished $latest; bellatrix $versions.")
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Cell = **DISCOVERY / DELEGATION**. DISCOVERY: the agent lists every scaffolded ``bellatrix-*`` skill and")
[void]$sb.AppendLine("subagent. DELEGATION: for each answer-key target the reply matches the key, the trace shows ``find --snapshot``")
[void]$sb.AppendLine("(or the locator-specialist subagent), and no context-heavy call or direct snapshot read happened.")
[void]$sb.AppendLine("Both PASS = **READY candidate**; on a fixture marked synthetic = **READY (synthetic fixtures)**.")
[void]$sb.AppendLine("A label is evidence for ``docs/support-matrix.json``, not a status change by itself.")
[void]$sb.AppendLine("")
$header = "| Language | Domain | " + (($agentRows | ForEach-Object { $_.name }) -join " | ") + " |"
[void]$sb.AppendLine($header)
[void]$sb.AppendLine("|---|---|" + ("---|" * $agentRows.Count))
$cells = @()
foreach ($lang in $langTable.languages) {
    foreach ($dom in $langTable.domains) {
        $row = "| $($lang.name) | $($dom.id) |"
        foreach ($ag in $agentRows) {
            if ($lang.domains -notcontains $dom.id) { $row += " MISSING (framework) |"; continue }
            $c = Get-Cell $results["$($ag.id)|$($lang.id)"] $dom.id
            $text = "$($c.discovery) / $($c.delegation)"
            if ($c.label) { $text += " - **$($c.label)**" }
            $row += " $text |"
            $cells += [pscustomobject]@{ language = $lang.id; domain = $dom.id; agent = $ag.id; cell = $c }
        }
        [void]$sb.AppendLine($row)
    }
}
$notes = @($found | Where-Object { @($_.notes).Count } | ForEach-Object { "- ``$($_.agent)-$($_.language)``: " + (@($_.notes) -join "; ") })
if ($notes.Count) {
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Notes from the harness")
    [void]$sb.AppendLine("")
    foreach ($n in $notes) { [void]$sb.AppendLine($n) }
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($OutFile, $sb.ToString().Replace("`r`n", "`n"), $utf8)
Write-Host "Wrote $OutFile ($($found.Count) result files, $($cells.Count) cells)."

if ($UpdateMatrix) {
    $matrixPath = Join-Path $repo "docs\support-matrix.json"
    if (-not (Test-Path $matrixPath)) { throw "docs/support-matrix.json not found." }
    $matrix = [IO.File]::ReadAllText($matrixPath, $utf8) | ConvertFrom-Json
    $updated = 0
    foreach ($c in $cells) {
        $m = @($matrix.cells | Where-Object { $_.language -eq $c.language -and $_.domain -eq $c.domain -and $_.agent -eq $c.agent })
        if ($m.Count -ne 1) { continue }
        $ev = $m[0].evidence
        foreach ($pair in @(@("discovery", $c.cell.discovery), @("delegation", $c.cell.delegation), @("conformanceLabel", $c.cell.label))) {
            if ($ev.PSObject.Properties[$pair[0]]) { $ev.($pair[0]) = $pair[1] } else { $ev | Add-Member -NotePropertyName $pair[0] -NotePropertyValue $pair[1] }
        }
        $updated++
    }
    [IO.File]::WriteAllText($matrixPath, (ConvertTo-MatrixJson $matrix) + "`n", $utf8)
    Write-Host "Updated evidence for $updated cells in docs/support-matrix.json (statuses unchanged; review, then run scripts/gen-readme-matrix.ps1)."
}
