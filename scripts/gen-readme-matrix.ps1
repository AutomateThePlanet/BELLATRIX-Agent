<#
.SYNOPSIS
  Regenerates the README agent and support tables from the public fields of docs/support-matrix.json.

.DESCRIPTION
  docs/support-matrix.json is the single source of what BELLATRIX Agent supports. This script reads only its public
  fields (languages, domains, agents, support, footnotes), never `internal` (the maintainers' verification tracking),
  and rewrites:
    README.md                         <!-- provider-matrix:begin/end -->  one row per agents[] entry: what
                                                                           `bellatrix init --agent <id>` sets up
                                      <!-- support-matrix:begin/end -->   language x domain, one row per language,
                                                                           then the footnotes
    src/Bellatrix.Cli/Package/README.md   the "## What is supported" section (the NuGet README; no HTML markers)
  Everything is data-driven: adding an agent or a language is a JSON change.

  -Check exits 1 (and writes nothing) when a README is out of date; SupportMatrixReadmeTests enforces the same in CI.

.EXAMPLE
  powershell -File scripts/gen-readme-matrix.ps1
  powershell -File scripts/gen-readme-matrix.ps1 -Check
#>
param([switch]$Check)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$readmePath = Join-Path $repo "README.md"
$packagePath = Join-Path $repo "src\Bellatrix.Cli\Package\README.md"
$matrix = [IO.File]::ReadAllText((Join-Path $repo "docs\support-matrix.json"), $utf8) | ConvertFrom-Json
$yes = [string][char]0x2713
$no = [string][char]0x2014

$support = New-Object System.Collections.Generic.List[string]
$support.Add("| Language | " + (($matrix.domains | ForEach-Object { $_.name + $(if ($_.detail) { " ($($_.detail))" } else { "" }) }) -join " | ") + " |")
$support.Add("|---|" + ("---|" * @($matrix.domains).Count))
foreach ($language in $matrix.languages) {
    $marks = @(foreach ($domain in $matrix.domains) {
        $entry = @($matrix.support | Where-Object { $_.language -eq $language.id -and $_.domain -eq $domain.id })
        if ($entry.Count -ne 1) { throw "docs/support-matrix.json: expected exactly one support entry for $($language.id)/$($domain.id), found $($entry.Count)." }
        $(if ($entry[0].supported) { $yes } else { $no })
    })
    $support.Add("| $($language.name) | " + ($marks -join " | ") + " |")
}
foreach ($note in $matrix.footnotes) { $support.Add(""); $support.Add($note) }

$agents = New-Object System.Collections.Generic.List[string]
$agents.Add("| Agent | ``--agent`` | Instructions | Skills | Subagents (locator specialist + generator) |")
$agents.Add("|---|---|---|---|---|")
foreach ($agent in $matrix.agents) {
    $name = "**$($agent.fullName)**" + $(if ($agent.aka) { " ($($agent.aka))" } else { "" })
    $agents.Add("| $name | ``$($agent.id)`` | $($agent.instructions) | $($agent.skills) | $($agent.subagents) |")
}

function Get-NewLine([string]$text) { $(if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }) }
function Set-Block([string]$text, [string]$name, $body) {
    $nl = Get-NewLine $text
    $begin = "<!-- $($name):begin -->"
    $end = "<!-- $($name):end -->"
    $b = $text.IndexOf($begin); $e = $text.IndexOf($end)
    if ($b -lt 0 -or $e -lt $b) { throw "README.md has no $begin ... $end block." }
    $block = $begin + $nl + "<!-- generated from docs/support-matrix.json by scripts/gen-readme-matrix.ps1; do not edit by hand -->" + $nl + ($body -join $nl) + $nl
    return $text.Substring(0, $b) + $block + $text.Substring($e)
}
# The NuGet README gets no HTML markers: the section runs from its heading to the next "## " heading.
function Set-Section([string]$text, [string]$heading, $body) {
    $nl = Get-NewLine $text
    $b = $text.IndexOf($heading + $nl)
    if ($b -lt 0) { throw "src/Bellatrix.Cli/Package/README.md has no '$heading' section." }
    $e = $text.IndexOf($nl + "## ", $b + $heading.Length)
    if ($e -lt 0) { throw "src/Bellatrix.Cli/Package/README.md: '$heading' must be followed by another '## ' section." }
    return $text.Substring(0, $b) + $heading + $nl + $nl + ($body -join $nl) + $nl + $text.Substring($e)
}

$readme = [IO.File]::ReadAllText($readmePath, $utf8)
$package = [IO.File]::ReadAllText($packagePath, $utf8)
$updatedReadme = Set-Block (Set-Block $readme "provider-matrix" $agents) "support-matrix" $support
$updatedPackage = Set-Section $package "## What is supported" $support

if ($updatedReadme -eq $readme -and $updatedPackage -eq $package) { Write-Host "README support tables are up to date."; exit 0 }
if ($Check) { Write-Host "README support tables are out of date: run scripts/gen-readme-matrix.ps1"; exit 1 }
[IO.File]::WriteAllText($readmePath, $updatedReadme, $utf8)
[IO.File]::WriteAllText($packagePath, $updatedPackage, $utf8)
Write-Host "README.md and the package README regenerated from docs/support-matrix.json."
