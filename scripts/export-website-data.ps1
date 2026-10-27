<#
.SYNOPSIS
  Copies the generated CLI data into the website's docs section. Never hand-edit the copies.

.DESCRIPTION
  Writes, into <Website>/src/data/:
    bellatrix-cli-help.json  = the output of `bellatrix help --json` of the installed (released) CLI
    support-matrix.json      = docs/support-matrix.json of this repo
  The website's /agent/ pages render the CLI reference, the per-agent init layouts and the support matrix from these
  two files. Run it after each release (the CLI version must match this repo's csproj), then rebuild the site.

.EXAMPLE
  powershell -File scripts/export-website-data.ps1 -Website <path-to-website-repo>
  powershell -File scripts/export-website-data.ps1 -Website <path-to-website-repo> -Exe .\src\Bellatrix.Cli\bin\Release\net8.0\bellatrix.exe
#>
param(
    [Parameter(Mandatory = $true)][string]$Website,
    [string]$Exe = "bellatrix"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$dataDir = Join-Path $Website "src\data"
if (-not (Test-Path $dataDir)) { throw "Website data folder not found: $dataDir" }

$csproj = [IO.File]::ReadAllText((Join-Path $repo "src\Bellatrix.Cli\Bellatrix.Cli.csproj"), $utf8)
$expected = [regex]::Match($csproj, "<Version>([^<]+)</Version>").Groups[1].Value
$help = [string](& $Exe help --json)
if ($LASTEXITCODE -ne 0) { throw "'$Exe help --json' failed." }
$version = ($help | ConvertFrom-Json).version
if ($version -ne $expected) { throw "'$Exe' is $version but this repo is ${expected}: install the released CLI first (dotnet tool update -g Bellatrix.Cli)." }

[IO.File]::WriteAllText((Join-Path $dataDir "bellatrix-cli-help.json"), $help.Trim() + "`n", $utf8)
Copy-Item (Join-Path $repo "docs\support-matrix.json") (Join-Path $dataDir "support-matrix.json") -Force
Write-Host "Exported bellatrix $version help + support matrix to $dataDir"
