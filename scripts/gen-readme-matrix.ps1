<#
.SYNOPSIS
  Regenerates the README provider and support matrices from docs/support-matrix.json.

.DESCRIPTION
  docs/support-matrix.json is the single source of the language x domain x agent matrix and of each agent's file
  layout. This script rewrites two blocks of README.md:
    <!-- provider-matrix:begin/end -->  one row per agents[] entry: instructions, skills, subagents, delegation
    <!-- support-matrix:begin/end -->   one row per rows[] entry, one column per agents[] entry (the cells' status),
                                        then the legend/notes bullets
  Everything is data-driven: adding an agent or a language is a JSON change. A row whose cells are all the same
  MISSING status prints it once and arrows in the remaining columns.

  -Check exits 1 (and writes nothing) when README.md is out of date; SupportMatrixReadmeTests enforces the same in CI.

.EXAMPLE
  powershell -File scripts/gen-readme-matrix.ps1
  powershell -File scripts/gen-readme-matrix.ps1 -Check
#>
param([switch]$Check)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$readmePath = Join-Path $repo "README.md"
$matrix = [IO.File]::ReadAllText((Join-Path $repo "docs\support-matrix.json"), $utf8) | ConvertFrom-Json
$times = [string][char]0x00D7
$arrow = [string][char]0x2190

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("| Language $times domain | " + (($matrix.agents | ForEach-Object { $_.name }) -join " | ") + " | Evidence |")
$lines.Add("|---|" + ("---|" * @($matrix.agents).Count) + "---|")
foreach ($row in $matrix.rows) {
    $statuses = @(foreach ($agent in $matrix.agents) {
        $cell = @($matrix.cells | Where-Object { $_.language -eq $row.language -and $_.domain -eq $row.domain -and $_.agent -eq $agent.id })
        if ($cell.Count -ne 1) { throw "docs/support-matrix.json: expected exactly one cell for $($row.language)/$($row.domain)/$($agent.id), found $($cell.Count)." }
        $cell[0].status
    })
    $uniform = @($statuses | Sort-Object -Unique).Count -eq 1 -and $statuses[0] -like "MISSING*"
    $shown = $(if ($uniform) { @($statuses[0]) + @($arrow) * ($statuses.Count - 1) } else { $statuses })
    $lines.Add("| $($row.label) | " + ($shown -join " | ") + " | $($row.evidence) |")
}
$lines.Add("")
foreach ($entry in $matrix.legend) { $lines.Add("- **$($entry.status):** $($entry.meaning)") }
foreach ($note in $matrix.notes) { $lines.Add("- $note") }

$provider = New-Object System.Collections.Generic.List[string]
$provider.Add("| Agent | Instructions fragment | Skills | Subagents (locator specialist + generator) | Delegation mode |")
$provider.Add("|---|---|---|---|---|")
foreach ($agent in $matrix.agents) {
    $name = "**$($agent.fullName)**" + $(if ($agent.aka) { " ($($agent.aka))" } else { "" })
    $provider.Add("| $name | $($agent.instructions) | $($agent.skills) | $($agent.subagents) | $($agent.delegation) |")
}

$readme = [IO.File]::ReadAllText($readmePath, $utf8)
$nl = $(if ($readme.Contains("`r`n")) { "`r`n" } else { "`n" })
function Set-Block([string]$text, [string]$name, $body) {
    $begin = "<!-- $($name):begin -->"
    $end = "<!-- $($name):end -->"
    $b = $text.IndexOf($begin); $e = $text.IndexOf($end)
    if ($b -lt 0 -or $e -lt $b) { throw "README.md has no $begin ... $end block." }
    $block = $begin + $nl + "<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->" + $nl + ($body -join $nl) + $nl
    return $text.Substring(0, $b) + $block + $text.Substring($e)
}
$updated = Set-Block (Set-Block $readme "provider-matrix" $provider) "support-matrix" $lines

if ($updated -eq $readme) { Write-Host "README.md support matrix is up to date."; exit 0 }
if ($Check) { Write-Host "README.md support matrix is out of date: run scripts/gen-readme-matrix.ps1"; exit 1 }
[IO.File]::WriteAllText($readmePath, $updated, $utf8)
Write-Host "README.md support matrix regenerated from docs/support-matrix.json."
