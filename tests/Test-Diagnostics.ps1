#Requires -Version 5.1
param([string]$TestRoot = (Join-Path $env:TEMP ('BASkirmish-diagnostics-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$game = Join-Path $TestRoot 'Game with spaces'
$reports = Join-Path $TestRoot 'Reports'
New-Item -ItemType Directory -Path $game,(Join-Path $game 'Mods'),(Join-Path $game 'MelonLoader') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'BrokenArrow.exe'),'fixture')
[IO.File]::WriteAllText((Join-Path $game 'Mods\BALocalSkirmish.dll'),'mod fixture')
$log = Join-Path $game 'MelonLoader\Latest.log'
[IO.File]::WriteAllLines($log, @('Unrelated line', '[Local Skirmish] AI selected deck: Armor', '[Error] Example failure'))
function Require($Condition, [string]$Message) { if (-not $Condition) { throw $Message }; Write-Host ('PASS ' + $Message) }
function Snapshot { (Get-ChildItem -LiteralPath $game -Recurse -File | Sort-Object FullName | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }) -join "`n" }
$before = Snapshot
$batch = Join-Path $repo 'collect-diagnostics.bat'
& $env:ComSpec /d /c ('call "' + $batch + '" -GameDir "' + $game + '" -OutputDir "' + $reports + '" -NonInteractive <NUL')
Require ($LASTEXITCODE -eq 0) 'double-click wrapper runs the collector under Windows PowerShell'
$report = @(Get-ChildItem -LiteralPath $reports -Directory)[0].FullName
Require ((Get-FileHash -LiteralPath $log).Hash -eq (Get-FileHash -LiteralPath (Join-Path $report 'Latest.log')).Hash) 'full log copied exactly'
$summary = Get-Content -LiteralPath (Join-Path $report 'summary.txt') -Raw
$hash = (Get-FileHash -LiteralPath (Join-Path $game 'Mods\BALocalSkirmish.dll')).Hash
Require ($summary.Contains($hash)) 'installed mod hash included'
$trace = Get-Content -LiteralPath (Join-Path $report 'deck-and-spawn-trace.txt') -Raw
Require ($trace.Contains('AI selected deck') -and $trace.Contains('Example failure') -and -not $trace.Contains('Unrelated line')) 'trace keeps relevant log entries'
Require ((Snapshot) -eq $before) 'game files and contents unchanged'
$emptyGame = Join-Path $TestRoot 'No log'
New-Item -ItemType Directory -Path $emptyGame -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $emptyGame 'BrokenArrow.exe'),'fixture')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'collect-diagnostics.ps1') -GameDir $emptyGame -OutputDir (Join-Path $TestRoot 'No log report') -NonInteractive
Require ($LASTEXITCODE -eq 0) 'missing log still produces a report'
$emptyReport = @(Get-ChildItem -LiteralPath (Join-Path $TestRoot 'No log report') -Directory)[0].FullName
Require ((Get-Content -LiteralPath (Join-Path $emptyReport 'summary.txt') -Raw).Contains('Latest.log missing')) 'missing log instructions included'
Write-Host ('Diagnostic fixtures kept at: ' + $TestRoot)
