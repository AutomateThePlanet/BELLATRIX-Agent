#!/usr/bin/env pwsh
# Packs the `bellatrix` CLI into ./artifacts/nuget and installs or updates it as a global dotnet tool from there.
# Package id: Bellatrix.Cli; command: bellatrix. Use it to try a local build before it is on NuGet.org.
param(
    [string]$Configuration = "Release",
    [string]$Feed = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "artifacts/nuget")
)
$ErrorActionPreference = "Stop"
$proj = "$PSScriptRoot/Bellatrix.Cli.csproj"

if (-not (Test-Path $Feed)) { New-Item -ItemType Directory -Path $Feed | Out-Null }

Write-Host "Packing bellatrix CLI ($Configuration) -> $Feed ..."
dotnet pack $proj -c $Configuration -o $Feed
if ($LASTEXITCODE -ne 0) { throw "pack failed ($LASTEXITCODE)" }

$version = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$installed = (dotnet tool list --global) -match '^bellatrix\.cli\s'
if ($installed) {
    dotnet tool update --global --add-source $Feed Bellatrix.Cli --version $version
} else {
    dotnet tool install --global --add-source $Feed Bellatrix.Cli --version $version
}
if ($LASTEXITCODE -ne 0) { throw "tool install/update failed ($LASTEXITCODE)" }
Write-Host "[OK] bellatrix $version installed. Try: bellatrix --version"
