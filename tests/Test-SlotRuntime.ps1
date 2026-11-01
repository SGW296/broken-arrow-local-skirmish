param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
& $Dotnet run --project (Join-Path $PSScriptRoot 'SlotRuntimeRegression/SlotRuntimeRegression.csproj') -c Release --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Runtime slot regression failed (exit $LASTEXITCODE)." }
