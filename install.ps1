#Requires -Version 5.1
<#
Installs the public-alpha DLL into an existing offline MelonLoader setup.
The release ZIP supplies BALocalSkirmish.dll and manifest.json beside this script.
This script does not install a loader, edit game assets, or change launch shortcuts.
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$BackupRoot,
    [switch]$NonInteractive
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts\Find-BrokenArrow.ps1')

function Require-GameClosed {
    if (Get-Process -Name BrokenArrow -ErrorAction SilentlyContinue) {
        throw 'Close Broken Arrow before installing. No mod files have been replaced.'
    }
}

function Assert-PackageFiles([string]$Root, $Manifest) {
    if ($Manifest.SchemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace($Manifest.Version)) {
        throw 'The release manifest is missing or unsupported. Download a complete release ZIP.'
    }
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Manifest.Files) {
        $relative = [string]$file.Path
        if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or
            $relative -match '(^|[\\/])\.\.([\\/]|$)' -or $relative.Contains(':') -or
            -not $seen.Add($relative.Replace('/', '\'))) {
            throw 'The release manifest contains an invalid or duplicate file path.'
        }
        $path = [IO.Path]::GetFullPath((Join-Path $Root $relative))
        $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw ('A release file is missing: ' + $relative + '. Extract the entire ZIP first.')
        }
        if ($file.SHA256 -notmatch '^[0-9A-Fa-f]{64}$' -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.SHA256) {
            throw ('Release file verification failed: ' + $relative + '. Download a fresh release ZIP.')
        }
    }
    if (-not $seen.Contains('BALocalSkirmish.dll')) { throw 'The release manifest does not include BALocalSkirmish.dll.' }
}

$source = Join-Path $PSScriptRoot 'BALocalSkirmish.dll'
$manifestPath = Join-Path $PSScriptRoot 'manifest.json'
if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'This is a source checkout, not an installable release. Download and extract the public-alpha release ZIP, or build and package the source first. The old dist DLL is not installed.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-PackageFiles $PSScriptRoot $manifest
Require-GameClosed
$gameRoot = Find-BrokenArrow -ExplicitPath $GameDir -PackageRoot $PSScriptRoot -NonInteractive:$NonInteractive
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'MelonLoader\net6\MelonLoader.dll') -PathType Leaf)) {
    throw 'An existing compatible MelonLoader setup is required. This update does not install or replace MelonLoader. See the release README.'
}
$unityVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $gameRoot 'BrokenArrow.exe')).ProductVersion
if ([string]::IsNullOrWhiteSpace($unityVersion) -or $unityVersion -notmatch '^2022\.3\.62f3(?:\s|$)') {
    throw 'This alpha targets Broken Arrow v1.2.0 with Unity 2022.3.62f3. The executable has a different or unknown Unity version; installation stopped. Newer game builds require separate compatibility testing.'
}
$latestLog = Join-Path $gameRoot 'MelonLoader\Latest.log'
if (Test-Path -LiteralPath $latestLog -PathType Leaf) {
    $versionLine = Select-String -LiteralPath $latestLog -Pattern 'Game Version:\s*([^\s]+)' | Select-Object -First 1
    if ($versionLine -and $versionLine.Matches[0].Groups[1].Value -ne '1.2.0') {
        throw 'The latest MelonLoader log identifies a game version other than supported v1.2.0. Installation stopped; use a release tested for that game version.'
    }
}
$mods = Join-Path $gameRoot 'Mods'
$destination = Join-Path $mods 'BALocalSkirmish.dll'
$settings = Join-Path $gameRoot 'UserData\BALocalSkirmish\lobby.json'
if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path $gameRoot 'UserData\BALocalSkirmish\Backups'
}
$backupBase = [IO.Path]::GetFullPath($BackupRoot)
$modsFull = [IO.Path]::GetFullPath($mods).TrimEnd('\', '/')
if ($backupBase.Equals($modsFull, [StringComparison]::OrdinalIgnoreCase) -or
    $backupBase.StartsWith($modsFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a backup folder outside Mods so MelonLoader cannot load backup DLLs.'
}
$backup = Join-Path $backupBase ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$hadMod = Test-Path -LiteralPath $destination -PathType Leaf
$hadSettings = Test-Path -LiteralPath $settings -PathType Leaf
$oldHash = $null; $settingsHash = $null
if ($hadMod) {
    $oldHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $destination -Destination (Join-Path $backup 'BALocalSkirmish.dll')
    if ((Get-FileHash -LiteralPath (Join-Path $backup 'BALocalSkirmish.dll')).Hash -ne $oldHash) { throw 'The previous DLL backup could not be verified; installation stopped.' }
}
if ($hadSettings) {
    $settingsHash = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $settings -Destination (Join-Path $backup 'lobby.json')
    if ((Get-FileHash -LiteralPath (Join-Path $backup 'lobby.json')).Hash -ne $settingsHash) { throw 'The settings backup could not be verified; installation stopped.' }
}
$expectedHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$record = [ordered]@{
    Version = $manifest.Version
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
    GameFolder = $gameRoot
    PreviousModPresent = $hadMod
    PreviousDllSHA256 = $oldHash
    SettingsPresent = $hadSettings
    SettingsSHA256 = $settingsHash
    InstalledDllSHA256 = $expectedHash
    SourceCommit = $manifest.SourceCommit
}
[IO.File]::WriteAllText((Join-Path $backup 'backup.json'), ($record | ConvertTo-Json -Depth 4))
Require-GameClosed
New-Item -ItemType Directory -Path $mods -Force | Out-Null
try {
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $expectedHash) { throw 'The installed DLL did not match the verified release.' }
} catch {
    if ($hadMod) { Copy-Item -LiteralPath (Join-Path $backup 'BALocalSkirmish.dll') -Destination $destination -Force }
    elseif (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination -Force }
    throw
}
if ($hadSettings -and (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash -ne $settingsHash) {
    throw 'Settings changed during installation. The original settings are preserved in the backup folder.'
}
Write-Host ('Installed Local Skirmish Lobby ' + $manifest.Version + '.')
Write-Host ('Verified SHA256: ' + $expectedHash)
Write-Host ('Backup: ' + $backup)
Write-Host 'Your loader, launcher and original game files were preserved. Launch using your usual offline modded shortcut.'
