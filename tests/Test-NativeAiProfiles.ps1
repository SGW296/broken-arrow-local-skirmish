param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
& $Dotnet run --project (Join-Path $PSScriptRoot 'NativeAiProfileRegression/NativeAiProfileRegression.csproj') -c Release --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Native AI profile regression failed (exit $LASTEXITCODE)." }
