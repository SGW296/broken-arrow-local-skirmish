#Requires -Version 5.1
param([string]$GameDir, [string]$OutputDir, [switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts\Find-BrokenArrow.ps1')
$gameRoot = Find-BrokenArrow -ExplicitPath $GameDir -PackageRoot $PSScriptRoot -NonInteractive:$NonInteractive
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $PSScriptRoot 'diagnostics' }
$reportRoot = Join-Path ([IO.Path]::GetFullPath($OutputDir)) ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add('BA Local Skirmish diagnostic report')
$lines.Add('Collected: ' + (Get-Date -Format o))
$lines.Add('Game folder: ' + $gameRoot)
$lines.Add('Collector does not change game files, decks, settings, or processes. Nothing is uploaded.')
$lines.Add('Please reproduce the problem, then collect before the next game launch replaces Latest.log.')
$lines.Add('')
$lines.Add('Installed mod files (SHA256):')
$mods = Join-Path $gameRoot 'Mods'
if (Test-Path -LiteralPath $mods) {
    foreach ($file in Get-ChildItem -LiteralPath $mods -Filter '*.dll' -File | Sort-Object Name) {
        $lines.Add($file.Name + ' | ' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
    }
} else { $lines.Add('Mods folder missing.') }
$log = Join-Path $gameRoot 'MelonLoader\Latest.log'
if (Test-Path -LiteralPath $log -PathType Leaf) {
    $lines.Add('Log timestamp: ' + (Get-Item -LiteralPath $log).LastWriteTime.ToString('o'))
    if ((Get-Item -LiteralPath $log).Length -le 20MB) {
        Copy-Item -LiteralPath $log -Destination (Join-Path $reportRoot 'Latest.log')
    } else {
        $head = @(Get-Content -LiteralPath $log -TotalCount 250)
        $tail = @(Get-Content -LiteralPath $log -Tail 4000)
        @($head + '[Collector: large log trimmed; middle omitted.]' + $tail) | Set-Content -LiteralPath (Join-Path $reportRoot 'Latest.log') -Encoding UTF8
        $lines.Add('Large log trimmed to its first 250 and last 4000 lines.')
    }
    $trace = @(Select-String -LiteralPath (Join-Path $reportRoot 'Latest.log') -Pattern 'Local.?Skirmish|Custom.?Nations|MelonLoader|Harmony|Error|Exception' | ForEach-Object { $_.Line })
    [IO.File]::WriteAllLines((Join-Path $reportRoot 'deck-and-spawn-trace.txt'), [string[]]$trace, [Text.Encoding]::UTF8)
} else { $lines.Add('Latest.log missing. Launch modded, reproduce the issue, then collect again.') }
$lines.Add('')
$lines.Add('Tester notes to add: map name; player deck; AI names and selected decks; whether those cards have selected transports; which unit moved slowly; other installed mods.')
$lines.Add('Expected evidence: one load binding, per-player selected deck assignments, and AI spawn lines with replacement unit IDs. Missing or discarded bindings require investigation.')
$lines.Add('Review the report before sharing: logs may contain local paths and player/deck names.')
$lines | Set-Content -LiteralPath (Join-Path $reportRoot 'summary.txt') -Encoding UTF8
Write-Host ''
Write-Host ('Diagnostic report saved to: ' + $reportRoot)
Write-Host 'Send summary.txt, Latest.log, and deck-and-spawn-trace.txt to the mod author after reviewing them.'
Write-Host 'Nothing was uploaded and no game files were changed.'
