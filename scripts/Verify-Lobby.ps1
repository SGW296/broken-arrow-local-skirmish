param(
    [string]$GameDir,
    [string]$ReportPath = (Join-Path $PSScriptRoot '../artifacts/validation.json'),
    [switch]$ManagedOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Get-Content -LiteralPath (Join-Path $repo 'src/LocalSkirmish.cs') -Raw
$versionMatch = [regex]::Match($source, 'PublicVersion\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) { throw 'PublicVersion was not found.' }
$version = $versionMatch.Groups[1].Value
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$results = @()
$buildSucceeded = $false
$harmonyPassed = $false
$dllHash = $null
Push-Location $repo
try {
    if (-not $ManagedOnly) {
        if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Supply -GameDir for release validation, or use -ManagedOnly.' }
        & dotnet build -c Release 'src/BALocalSkirmish.csproj' "-p:GameDir=$GameDir"
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
        $buildSucceeded = $true
        $dllHash = (Get-FileHash -LiteralPath 'src/bin/Release/BALocalSkirmish.dll' -Algorithm SHA256).Hash
    }
    $managed = @('Test-Lobby.ps1', 'Test-TeamRoster.ps1', 'Test-Preview.ps1',
        'Test-SlotPolicy.ps1', 'Test-SlotRuntime.ps1', 'Test-NativeAiProfiles.ps1',
        'Test-NativeStyle.ps1', 'Test-WaveDiagnostics.ps1', 'Test-DeckSelection.ps1')
    foreach ($name in $managed) {
        $output = @(& (Join-Path 'tests' $name) 6>&1)
        foreach ($line in $output) { Write-Host $line }
        if ($LASTEXITCODE -ne 0) { throw "$name failed." }
        $assertions = [regex]::Matches(($output -join "`n"), '(?m)^PASS: (\d+) ')
        if ($assertions.Count -ne 1) { throw "$name did not report exactly one assertion total." }
        $results += [ordered]@{ Suite = $name; ChecksPassed = [int]$assertions[0].Groups[1].Value }
    }
    if (-not $ManagedOnly) {
        & './tests/Test-PatchRegistration.ps1' -GameDir $GameDir
        if ($LASTEXITCODE -ne 0) { throw 'Harmony registration failed.' }
        $harmonyPassed = $true
    }
    & './tests/Test-Diagnostics.ps1'
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic collector fixture failed.' }
    & './tests/Test-PublicPackage.ps1'
    if ($LASTEXITCODE -ne 0) { throw 'Public installer/package fixture failed.' }
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source commit.' }
    $total = ($results | ForEach-Object { $_.ChecksPassed } | Measure-Object -Sum).Sum
    $report = [ordered]@{
        Version = $version
        SourceCommit = $commit
        CompletedUtc = [DateTime]::UtcNow.ToString('o')
        BuildSucceeded = $buildSucceeded
        ManagedChecksPassed = [int]$total
        HarmonyRegistrationPassed = $harmonyPassed
        DiagnosticFixturesPassed = $true
        PublicPackageFixturesPassed = $true
        DllSHA256 = $dllHash
        Suites = $results
        InGameTested = $false
        Scope = 'Managed fixtures and temporary-file tests; Unity/IL2CPP live acceptance is recorded separately.'
    }
    $absoluteReport = [IO.Path]::GetFullPath($ReportPath)
    New-Item -ItemType Directory -Path (Split-Path $absoluteReport -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($absoluteReport, ($report | ConvertTo-Json -Depth 6))
    Write-Host "PASS: $total managed assertions; diagnostic and public package fixtures passed."
    Write-Host "Validation report: $absoluteReport"
} finally { Pop-Location }
