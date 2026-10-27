<#
  Self-test stand-in for an AI agent (scripts/conformance/selftest-agents.json). It costs nothing and proves the
  conformance pipeline end to end: prompt -> the agent's shell calls (`bellatrix find --snapshot`, traced) -> reply ->
  scoring. Behaves like a well-behaved agent unless BELLATRIX_FAKE_MODE says otherwise:
    good          list the manifest's names; delegate via `find`; reply with the answer key's first locator
    read-snapshot reply correctly but "read" the snapshot instead of using find (no trace line) -> must FAIL
    heavy         call `snapshot --inline`-like context-heavy verb (get-page-source) -> must FAIL
#>
param([Parameter(Mandatory = $true)][string]$Prompt)
$ErrorActionPreference = "Stop"
$exe = if ($env:BELLATRIX_SELFTEST_EXE) { $env:BELLATRIX_SELFTEST_EXE } else { "bellatrix" }
$mode = if ($env:BELLATRIX_FAKE_MODE) { $env:BELLATRIX_FAKE_MODE } else { "good" }

if ($Prompt -match "^List every skill") {
    $m = Get-Content ".bellatrix\manifest.claude.json" -Raw | ConvertFrom-Json
    $paths = @($m.files | ForEach-Object { $_.path })
    $skills = @($paths | Where-Object { $_ -match '/skills/([^/]+)/SKILL\.md$' } | ForEach-Object { $Matches[1] })
    $agents = @($paths | Where-Object { $_ -match '/agents/([^/]+)\.md$' } | ForEach-Object { $Matches[1] })
    Write-Output (@{ skills = $skills; agents = $agents } | ConvertTo-Json -Compress)
    exit 0
}

if ($Prompt -match "BELLATRIX (?<domain>\w+) UI snapshot .* saved at (?<path>\S+)\. .* find the locator for (?<desc>.+)\. Reply") {
    $domain = $Matches.domain; $path = $Matches.path; $desc = $Matches.desc
    $fixture = [IO.Path]::GetFileNameWithoutExtension($path)
    $key = Get-Content (Join-Path $PSScriptRoot "..\..\tests\fixtures\snapshots\$fixture.answers.json") -Raw | ConvertFrom-Json
    $target = @($key.targets | Where-Object { $_.description -eq $desc })[0]
    switch ($mode) {
        "good" { & $exe $domain find --snapshot $path @($target.findArgs) --json | Out-Null }
        "heavy" { & $exe $domain get-page-source --session nope --json | Out-Null }
        default { Get-Content $path -Raw | Out-Null }
    }
    Write-Output $target.expected[0]
    exit 0
}

Write-Output "unrecognized prompt"
exit 1
