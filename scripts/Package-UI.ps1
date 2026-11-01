#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = '3.1.0-alpha.1',
    [string]$OutputDir,
    [string]$BuildDll,
    [string]$ValidationReport,
    [switch]$AllowDirty
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Supply a release version such as 3.1.0-alpha.1.' }
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $repo ('artifacts\Local-Skirmish-Lobby-' + $Version) }
if ([string]::IsNullOrWhiteSpace($BuildDll)) { $BuildDll = Join-Path $repo 'src\bin\Release\BALocalSkirmish.dll' }
if (-not (Test-Path -LiteralPath $BuildDll -PathType Leaf)) { throw 'Build the Release DLL first; see docs/MODDING.md.' }
$buildHash = (Get-FileHash -LiteralPath $BuildDll -Algorithm SHA256).Hash
$commit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not record the source commit. Package from a Git checkout.' }
$branch = (& git -C $repo rev-parse --abbrev-ref HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not record the source branch.' }
$dirty = -not [string]::IsNullOrWhiteSpace((& git -C $repo status --porcelain --untracked-files=no | Out-String))
if ($dirty -and -not $AllowDirty) { throw 'Commit tracked source changes before creating a release package. -AllowDirty is available for local fixture packages only.' }
$versionSources = [ordered]@{ PublicVersion = $null; DllProductVersion = $null }
$sourceText = Get-Content -LiteralPath (Join-Path $repo 'src\LocalSkirmish.cs') -Raw
$publicVersionMatch = [regex]::Match($sourceText, 'PublicVersion\s*=\s*"([^"]+)"')
if ($publicVersionMatch.Success) { $versionSources.PublicVersion = $publicVersionMatch.Groups[1].Value }
$versionSources.DllProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($BuildDll)).ProductVersion
if (-not $AllowDirty -and ($versionSources.PublicVersion -ne $Version -or
    $versionSources.DllProductVersion -notmatch ('^' + [regex]::Escape($Version) + '(?:\+|$)'))) {
    throw 'The requested package version does not match source PublicVersion and built DLL product version. Build and verify the intended release again.'
}
$validation = [ordered]@{ Status = 'NotReported' }
if (-not [string]::IsNullOrWhiteSpace($ValidationReport)) {
    $report = Get-Content -LiteralPath $ValidationReport -Raw | ConvertFrom-Json
    if ($report.Version -ne $Version -or $report.SourceCommit -ne $commit -or $report.DllSHA256 -ne $buildHash -or $report.BuildSucceeded -ne $true -or
        $report.HarmonyRegistrationPassed -ne $true -or $report.DiagnosticFixturesPassed -ne $true -or
        $report.PublicPackageFixturesPassed -ne $true -or [long]$report.ManagedChecksPassed -le 0) {
        throw 'The validation report does not describe this version and DLL. Run verification again.'
    }
    $validation = [ordered]@{
        Status = 'Reported'
        BuildSucceeded = $report.BuildSucceeded
        ManagedChecksPassed = $report.ManagedChecksPassed
        HarmonyRegistrationPassed = $report.HarmonyRegistrationPassed
        DiagnosticFixturesPassed = $report.DiagnosticFixturesPassed
        PublicPackageFixturesPassed = $report.PublicPackageFixturesPassed
        CompletedUtc = $report.CompletedUtc
    }
}
$packagePath = [IO.Path]::GetFullPath($OutputDir)
if ((Test-Path -LiteralPath $packagePath) -and @(Get-ChildItem -LiteralPath $packagePath -Force).Count -gt 0) {
    throw 'The output folder is not empty. Use a new output folder; packaging never deletes previous builds.'
}
if (Test-Path -LiteralPath ($packagePath + '.zip')) { throw 'The output ZIP already exists. Choose a new output folder.' }
$files = [ordered]@{
    'BALocalSkirmish.dll' = $BuildDll
    'LICENSE' = (Join-Path $repo 'LICENSE')
    'README.md' = (Join-Path $repo 'docs\INSTALL-LOBBY.md')
    'install.ps1' = (Join-Path $repo 'install.ps1')
    'install.bat' = (Join-Path $repo 'install.bat')
    'collect-diagnostics.ps1' = (Join-Path $repo 'collect-diagnostics.ps1')
    'collect-diagnostics.bat' = (Join-Path $repo 'collect-diagnostics.bat')
    'scripts\Find-BrokenArrow.ps1' = (Join-Path $repo 'scripts\Find-BrokenArrow.ps1')
}
foreach ($note in @('ALPHA-VALIDATION.md', 'COMPATIBILITY.md')) {
    $notePath = Join-Path $repo ('docs\' + $note)
    if (Test-Path -LiteralPath $notePath -PathType Leaf) { $files[$note] = $notePath }
}
$changes = Join-Path $repo 'CHANGELOG.md'
if (Test-Path -LiteralPath $changes -PathType Leaf) { $files['CHANGELOG.md'] = $changes }
foreach ($sourcePath in $files.Values) { if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw ('A required package input is missing: ' + $sourcePath) } }
if (-not $AllowDirty) {
    $repoPrefix = [IO.Path]::GetFullPath($repo).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    foreach ($relative in $files.Keys) {
        if ($relative -eq 'BALocalSkirmish.dll') { continue }
        $sourceRelative = [IO.Path]::GetFullPath($files[$relative]).Substring($repoPrefix.Length)
        & git -C $repo ls-files --error-unmatch -- $sourceRelative 1>$null 2>$null
        if ($LASTEXITCODE -ne 0) { throw ('Commit the release input before packaging: ' + $sourceRelative) }
    }
}
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
foreach ($relative in $files.Keys) {
    $target = Join-Path $packagePath $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $files[$relative] -Destination $target
}
$manifest = [ordered]@{
    SchemaVersion = 1
    Version = $Version
    SourceCommit = $commit
    SourceBranch = $branch
    SourceTreeState = $(if ($dirty) { 'dirty' } else { 'clean' })
    FixturePackage = [bool]$AllowDirty
    VersionSources = $versionSources
    Upstream = 'https://github.com/BovineOverlord/broken-arrow-local-skirmish'
    Target = 'Broken Arrow v1.2.0, existing compatible MelonLoader, net6.0/x64; offline only'
    Validation = $validation
    LiveValidation = 'See ALPHA-VALIDATION.md for the recorded live coverage and limitations.'
    Files = @($files.Keys | Sort-Object | ForEach-Object { [ordered]@{
        Path = $_.Replace('\', '/')
        SHA256 = (Get-FileHash -LiteralPath (Join-Path $packagePath $_) -Algorithm SHA256).Hash
    } })
}
[IO.File]::WriteAllText((Join-Path $packagePath 'manifest.json'), ($manifest | ConvertTo-Json -Depth 6))
$zipPath = $packagePath + '.zip'
Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath $zipPath
Write-Host ('Package: ' + $packagePath)
Write-Host ('ZIP: ' + $zipPath)
Write-Host ('DLL SHA256: ' + $buildHash)
