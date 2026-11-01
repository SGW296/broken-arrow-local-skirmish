param([string]$OutputDir = (Join-Path $PSScriptRoot '../../outputs/Local-Skirmish-Lobby-3.0.1'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$build = Join-Path $repo 'src/bin/Release/BALocalSkirmish.dll'
$original = Join-Path $repo 'dist/BALocalSkirmish.dll'
$previous = Join-Path $repo '../outputs/Local-Skirmish-Lobby-3.0.0/BALocalSkirmish.dll'
if (-not (Test-Path -LiteralPath $build)) { throw 'Build the Release DLL first; see docs/MODDING.md.' }
if (-not (Test-Path -LiteralPath $previous)) { throw 'The preserved 3.0.0 package is required to include the previous rollback DLL.' }
$packagePath = [IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $packagePath,(Join-Path $packagePath 'rollback'),(Join-Path $packagePath 'upstream-original') -Force | Out-Null
Copy-Item -LiteralPath $build -Destination (Join-Path $packagePath 'BALocalSkirmish.dll')
Copy-Item -LiteralPath $previous -Destination (Join-Path $packagePath 'rollback/BALocalSkirmish.dll')
Copy-Item -LiteralPath $original -Destination (Join-Path $packagePath 'upstream-original/BALocalSkirmish.dll')
Copy-Item -LiteralPath (Join-Path $repo 'docs/INSTALL-LOBBY.md') -Destination (Join-Path $packagePath 'README.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/UI-ITERATION.md') -Destination $packagePath
foreach($note in @('NATIVE-SKIRMISH-AUDIT.md','CLOSED-SLOT-ENGINE.md','NATIVE-AI-PROFILE-NOTES.md','NATIVE-UI-DESIGN.md','VALIDATION-3.0.0.md','VALIDATION-3.0.1.md','NORMAL-BASELINE.md')) {
    Copy-Item -LiteralPath (Join-Path $repo ('docs/'+$note)) -Destination $packagePath
}
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $packagePath
$commit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not record the source commit.' }
$manifest = [ordered]@{
    Version = '3.0.1-normal-baseline'
    SourceCommit = $commit
    SourceBranch = 'feature/normal-baseline'
    UpstreamCommit = '77ba5ae30bf4705b1119ae39a0a829708ede6b6b'
    Target = 'Broken Arrow v1.2.0, existing MelonLoader, net6.0/x64'
    InGameTested = $false
    ManagedChecksPassed = 679
    HarmonyRegistrationPassed = $true
    Files = @(Get-ChildItem -LiteralPath $packagePath -Recurse -File |
        Where-Object { $_.Name -ne 'manifest.json' } |
        Sort-Object FullName |
        ForEach-Object { [ordered]@{
            Path = [IO.Path]::GetRelativePath($packagePath, $_.FullName)
            SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        } })
}
[IO.File]::WriteAllText((Join-Path $packagePath 'manifest.json'), ($manifest | ConvertTo-Json -Depth 5))
$zipPath = $packagePath + '.zip'
Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath $zipPath -Force
Write-Host "Package: $packagePath"
Write-Host "ZIP: $zipPath"
