#Requires -Version 5.1
param([string]$TestRoot = (Join-Path $env:TEMP ('BALobby-public-package-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$checks = 0
function Require($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Invoke-Installer([string]$Package, [string]$Game, [string]$Backups) {
    $ErrorActionPreference = 'Continue'
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Package 'install.ps1') -GameDir $Game -BackupRoot $Backups -NonInteractive 2>&1
    $result = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    return @{ ExitCode = $result; Output = (($output | Out-String) -replace '\s+', ' ') }
}
function Read-Manifest([string]$Package) { Get-Content -LiteralPath (Join-Path $Package 'manifest.json') -Raw | ConvertFrom-Json }
function Write-Manifest([string]$Package, $Manifest) { [IO.File]::WriteAllText((Join-Path $Package 'manifest.json'), ($Manifest | ConvertTo-Json -Depth 6)) }
function Snapshot([string]$Folder) {
    return ((Get-ChildItem -LiteralPath $Folder -File -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }) -join "`n")
}

New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
$fixtureDll = Join-Path $TestRoot 'built-alpha.dll'
[IO.File]::WriteAllText($fixtureDll, 'New alpha DLL fixture: never install this into a real game.')
$package = Join-Path $TestRoot 'Release with spaces'
& (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir $package -BuildDll $fixtureDll -AllowDirty
$manifest = Read-Manifest $package
Require ($manifest.SchemaVersion -eq 1 -and $manifest.Version -eq '3.1.0-alpha.1') 'versioned release manifest'
Require ($manifest.Validation.Status -eq 'NotReported') 'unreported checks are not claimed as passing'
Require ($manifest.SourceCommit -match '^[0-9a-f]{40}$') 'source commit recorded'
Require (-not (Test-Path -LiteralPath (Join-Path $package 'dist')) -and -not (Test-Path -LiteralPath (Join-Path $package 'rollback'))) 'package has one current DLL and no stale install paths'
Require (@($manifest.Files | Where-Object Path -match '\.(?:dll|exe)$').Count -eq 1) 'no loader or game assemblies shipped'
foreach ($file in $manifest.Files) {
    Require ((Get-FileHash -LiteralPath (Join-Path $package $file.Path)).Hash -eq $file.SHA256) ('package hash: ' + $file.Path)
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package + '.zip')
try {
    foreach ($file in $manifest.Files) {
        $entry = $zip.GetEntry($file.Path.Replace('\', '/'))
        Require ($null -ne $entry) ('ZIP entry: ' + $file.Path)
        $stream = $entry.Open()
        try { Require ((Get-FileHash -InputStream $stream).Hash -eq $file.SHA256) ('ZIP hash: ' + $file.Path) }
        finally { $stream.Dispose() }
    }
} finally { $zip.Dispose() }
$rejected = $false
try { & (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir $package -BuildDll $fixtureDll -AllowDirty } catch { $rejected = $true }
Require $rejected 'packaging refuses to mix files with a previous build'
$badReport = Join-Path $TestRoot 'wrong-validation.json'
[IO.File]::WriteAllText($badReport, '{"Version":"3.1.0-alpha.1","BuildSucceeded":true,"DllSHA256":"wrong"}')
$rejected = $false
try { & (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir (Join-Path $TestRoot 'Wrong report') -BuildDll $fixtureDll -ValidationReport $badReport -AllowDirty } catch { $rejected = $true }
Require $rejected 'validation results must match the packaged DLL'
$goodReport = Join-Path $TestRoot 'matching-validation.json'
$fixtureValidation = [ordered]@{ Version = '3.1.0-alpha.1'; SourceCommit = (& git -C $repo rev-parse HEAD).Trim(); BuildSucceeded = $true; DllSHA256 = (Get-FileHash -LiteralPath $fixtureDll).Hash; ManagedChecksPassed = 17; HarmonyRegistrationPassed = $true; DiagnosticFixturesPassed = $true; PublicPackageFixturesPassed = $true; CompletedUtc = [DateTime]::UtcNow.ToString('o') }
[IO.File]::WriteAllText($goodReport, ($fixtureValidation | ConvertTo-Json))
$reportedPackage = Join-Path $TestRoot 'Reported package'
& (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir $reportedPackage -BuildDll $fixtureDll -ValidationReport $goodReport -AllowDirty
$reportedManifest = Read-Manifest $reportedPackage
Require ($reportedManifest.Validation.ManagedChecksPassed -eq 17 -and $reportedManifest.Validation.HarmonyRegistrationPassed) 'manifest records supplied matching verification results'
Require ($reportedManifest.Validation.DiagnosticFixturesPassed -and $reportedManifest.Validation.PublicPackageFixturesPassed) 'manifest records installer and collector fixture results'
$fixtureValidation.SourceCommit = 'wrong'
[IO.File]::WriteAllText($goodReport, ($fixtureValidation | ConvertTo-Json))
$rejected = $false
try { & (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir (Join-Path $TestRoot 'Stale checks') -BuildDll $fixtureDll -ValidationReport $goodReport -AllowDirty } catch { $rejected = $true }
Require $rejected 'validation source commit must match the release source'
$fixtureValidation.SourceCommit = (& git -C $repo rev-parse HEAD).Trim()
$fixtureValidation.HarmonyRegistrationPassed = $false
[IO.File]::WriteAllText($goodReport, ($fixtureValidation | ConvertTo-Json))
$rejected = $false
try { & (Join-Path $repo 'scripts\Package-UI.ps1') -OutputDir (Join-Path $TestRoot 'Failed checks') -BuildDll $fixtureDll -ValidationReport $goodReport -AllowDirty } catch { $rejected = $true }
Require $rejected 'failed registration results cannot produce a release package'

# This fixture compiles an owned tiny executable with Unity-style version metadata.
# No original game executable or assembly is copied into the test package.
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler is required for the installer fixture.' }
$game = Join-Path $TestRoot 'Game with spaces'
$backups = Join-Path $TestRoot 'Backups'
New-Item -ItemType Directory -Path $game,(Join-Path $game 'Mods'),(Join-Path $game 'MelonLoader\net6'),(Join-Path $game 'UserData\BALocalSkirmish') -Force | Out-Null
$program = Join-Path $TestRoot 'Fixture.cs'
$fixtureSource = @'
using System.Reflection;
[assembly: AssemblyFileVersion("2022.3.62.0")]
[assembly: AssemblyInformationalVersion("2022.3.62f3")]
class Fixture { static void Main(string[] args) { if (args.Length > 0) System.Threading.Thread.Sleep(60000); } }
'@
[IO.File]::WriteAllText($program, $fixtureSource)
& $compiler /nologo /target:exe ('/out:' + (Join-Path $game 'BrokenArrow.exe')) $program
if ($LASTEXITCODE -ne 0) { throw 'Could not compile the fixture game executable.' }
$originalDll = Join-Path $game 'Mods\BALocalSkirmish.dll'
$settings = Join-Path $game 'UserData\BALocalSkirmish\lobby.json'
$loader = Join-Path $game 'MelonLoader\net6\MelonLoader.dll'
$launcher = Join-Path $game 'Launch-Offline.bat'
$gameAsset = Join-Path $game 'OriginalAsset.dat'
[IO.File]::WriteAllText($originalDll, 'Old installed mod fixture')
[IO.File]::WriteAllText($settings, '{"SchemaVersion":1,"RememberedDifficulty":3}')
[IO.File]::WriteAllText($loader, 'Existing loader fixture')
[IO.File]::WriteAllText($launcher, 'Existing launcher fixture')
[IO.File]::WriteAllText($gameAsset, 'Original game asset fixture')
[IO.File]::WriteAllText((Join-Path $game 'MelonLoader\Latest.log'), '[fixture] Game Version: 1.2.0')
$oldHash = (Get-FileHash -LiteralPath $originalDll).Hash
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$preservedHashes = @{}
foreach ($path in @($loader, $launcher, $gameAsset, (Join-Path $game 'BrokenArrow.exe'))) { $preservedHashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
$result = Invoke-Installer $package $game $backups
Require ($result.ExitCode -eq 0) ('Windows PowerShell 5.1 install succeeds: ' + $result.Output)
Require ((Get-FileHash -LiteralPath $originalDll).Hash -eq (Get-FileHash -LiteralPath $fixtureDll).Hash) 'installs the exact new top-level DLL'
Require ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'saved setup preserved'
foreach ($path in $preservedHashes.Keys) { Require ((Get-FileHash -LiteralPath $path).Hash -eq $preservedHashes[$path]) ('preserved: ' + [IO.Path]::GetFileName($path)) }
$backup = @(Get-ChildItem -LiteralPath $backups -Directory)[0].FullName
Require ((Get-FileHash -LiteralPath (Join-Path $backup 'BALocalSkirmish.dll')).Hash -eq $oldHash) 'old mod backup verified'
Require ((Get-FileHash -LiteralPath (Join-Path $backup 'lobby.json')).Hash -eq $settingsHash) 'setup backup verified'
Require (-not $backup.StartsWith((Join-Path $game 'Mods'), [StringComparison]::OrdinalIgnoreCase)) 'backups stored outside Mods'

$before = Snapshot $game
$result = Invoke-Installer $package $game (Join-Path $game 'Mods\BadBackup')
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('outside Mods')) 'backup path inside Mods refused'
Require ((Snapshot $game) -eq $before) 'rejected backup path leaves game untouched'
[IO.File]::AppendAllText((Join-Path $package 'BALocalSkirmish.dll'), 'corrupt')
$result = Invoke-Installer $package $game $backups
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('verification failed')) 'corrupt release DLL refused'
Require ((Snapshot $game) -eq $before) 'corrupt package leaves game untouched'
Copy-Item -LiteralPath $fixtureDll -Destination (Join-Path $package 'BALocalSkirmish.dll') -Force
$manifest = Read-Manifest $package
$manifest.Files += [PSCustomObject]@{ Path = '../outside.dll'; SHA256 = ('0' * 64) }
Write-Manifest $package $manifest
$result = Invoke-Installer $package $game $backups
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('invalid or duplicate')) 'manifest traversal path refused'
Require ((Snapshot $game) -eq $before) 'invalid manifest leaves game untouched'
$manifest.Files = @($manifest.Files | Where-Object Path -ne '../outside.dll')
Write-Manifest $package $manifest

$running = Start-Process -FilePath (Join-Path $game 'BrokenArrow.exe') -ArgumentList 'hold' -WindowStyle Hidden -PassThru
try {
    $result = Invoke-Installer $package $game $backups
    Require ($result.ExitCode -ne 0 -and $result.Output.Contains('Close Broken Arrow')) 'running game refused'
    Require ((Snapshot $game) -eq $before) 'running game is not modified'
} finally { if (-not $running.HasExited) { Stop-Process -Id $running.Id -Force } }

[IO.File]::WriteAllText((Join-Path $game 'MelonLoader\Latest.log'), '[fixture] Game Version: 1.2.2')
$beforeWrongVersion = Snapshot $game
$result = Invoke-Installer $package $game $backups
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('other than supported v1.2.0')) 'known unsupported game version refused'
Require ((Snapshot $game) -eq $beforeWrongVersion) 'unsupported version leaves game untouched'
[IO.File]::WriteAllText((Join-Path $game 'MelonLoader\Latest.log'), '[fixture] Game Version: 1.2.0')
$badGame = Join-Path $TestRoot 'Unity6 Game'
New-Item -ItemType Directory -Path $badGame,(Join-Path $badGame 'MelonLoader\net6') -Force | Out-Null
[IO.File]::WriteAllText($program, $fixtureSource.Replace('2022.3.62f3', '6000.0.1f1'))
& $compiler /nologo /target:exe ('/out:' + (Join-Path $badGame 'BrokenArrow.exe')) $program
Copy-Item -LiteralPath $loader -Destination (Join-Path $badGame 'MelonLoader\net6\MelonLoader.dll')
$badSnapshot = Snapshot $badGame
$result = Invoke-Installer $package $badGame $backups
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('different or unknown Unity version')) 'incompatible Unity engine refused'
Require ((Snapshot $badGame) -eq $badSnapshot) 'incompatible engine leaves game untouched'

$freshGame = Join-Path $TestRoot 'First mod install'
New-Item -ItemType Directory -Path $freshGame,(Join-Path $freshGame 'MelonLoader\net6') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $game 'BrokenArrow.exe') -Destination $freshGame
Copy-Item -LiteralPath $loader -Destination (Join-Path $freshGame 'MelonLoader\net6\MelonLoader.dll')
$freshBackups = Join-Path $TestRoot 'First install backups'
$result = Invoke-Installer $package $freshGame $freshBackups
Require ($result.ExitCode -eq 0) ('first mod install succeeds without previous DLL/settings: ' + $result.Output)
Require ((Get-FileHash -LiteralPath (Join-Path $freshGame 'Mods\BALocalSkirmish.dll')).Hash -eq (Get-FileHash -LiteralPath $fixtureDll).Hash) 'first install uses current DLL'
Require (-not (Test-Path -LiteralPath (Join-Path $freshGame 'UserData\BALocalSkirmish\lobby.json'))) 'first install does not invent settings'
$freshBackup = @(Get-ChildItem -LiteralPath $freshBackups -Directory)[0].FullName
Require (-not (Get-Content -LiteralPath (Join-Path $freshBackup 'backup.json') -Raw | ConvertFrom-Json).PreviousModPresent) 'first-install backup record identifies no previous mod'
$missingLoader = Join-Path $TestRoot 'No loader'
New-Item -ItemType Directory -Path $missingLoader -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $game 'BrokenArrow.exe') -Destination $missingLoader
$missingSnapshot = Snapshot $missingLoader
$result = Invoke-Installer $package $missingLoader $backups
Require ($result.ExitCode -ne 0 -and $result.Output.Contains('existing compatible MelonLoader')) 'missing loader is reported without downloading software'
Require ((Snapshot $missingLoader) -eq $missingSnapshot) 'missing loader leaves game untouched'

$sourceCheckout = Invoke-Installer $repo $game $backups
Require ($sourceCheckout.ExitCode -ne 0 -and $sourceCheckout.Output.Contains('source checkout')) 'source checkout cannot silently install dist 2.6.3'
$beforeWrapper = Snapshot $game
& $env:ComSpec /d /c ('call "' + (Join-Path $package 'install.bat') + '" -GameDir "' + $game + '" -BackupRoot "' + $backups + '" -NonInteractive <NUL')
Require ($LASTEXITCODE -eq 0) 'double-click batch wrapper installs successfully'
Require ((Snapshot $game) -eq $beforeWrapper) 'reinstall preserves game/settings contents'
Write-Host ('PASS: ' + $checks + ' public package/installer checks: manifest and ZIP integrity, correct DLL, verified backups, preservation, running/unsupported games, corrupt packages and source checkout refusal.')
Write-Host ('Fixture files kept at: ' + $TestRoot)
