param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
& $Dotnet run --project (Join-Path $PSScriptRoot 'SlotPolicyRegression/SlotPolicyRegression.csproj') -c Release --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Slot policy regression failed (exit $LASTEXITCODE)." }
