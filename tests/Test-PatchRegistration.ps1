param(
    [string]$GameDir = 'D:\Games\Broken Arrow',
    [string]$ModDll = (Join-Path $PSScriptRoot '../src/bin/Release/BALocalSkirmish.dll')
)
$ErrorActionPreference = 'Stop'
$projectDir = Join-Path $PSScriptRoot 'HarmonyRegistration'
$fixtureDir = Join-Path $projectDir 'obj'
New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
& (Join-Path $PSScriptRoot 'Test-DeckSelection.ps1') -ExportHarness (Join-Path $fixtureDir 'SelectionFixture.cs')
dotnet run --project (Join-Path $projectDir 'HarmonyRegistration.csproj') -c Release "-p:GameDir=$GameDir" -- (Resolve-Path -LiteralPath $ModDll).Path
if ($LASTEXITCODE -ne 0) { throw "Patch registration regression failed (exit $LASTEXITCODE)." }
