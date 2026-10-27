#!/usr/bin/env pwsh
# Builds the model-free desktop CLI as a self-contained, single-file, ReadyToRun executable
# (parity with the Web server's build.ps1). Cold-start budget ≤1.5s motivates ReadyToRun.
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "$PSScriptRoot/dist"
)
$ErrorActionPreference = "Stop"
$proj = "$PSScriptRoot/Bellatrix.Cli.csproj"

Write-Host "Publishing bellatrix CLI ($Configuration / $Runtime) -> $Output"
dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=true `
    -o $Output

if ($LASTEXITCODE -ne 0) { throw "publish failed ($LASTEXITCODE)" }
Write-Host "[OK] Published to $Output/bellatrix.exe"

# Also produce the dotnet-tool package for `dotnet tool install`.
dotnet pack $proj -c $Configuration -o "$Output/nupkg"
if ($LASTEXITCODE -ne 0) { throw "pack failed ($LASTEXITCODE)" }
Write-Host "[OK] Packed tool nupkg to $Output/nupkg"
