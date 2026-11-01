param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
& $Dotnet run --project (Join-Path $PSScriptRoot 'WaveDiagnosticsRegression/WaveDiagnosticsRegression.csproj') -c Release --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Wave diagnostics regression failed (exit $LASTEXITCODE)." }
