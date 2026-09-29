# Shared by the installer and diagnostic collector. Compatible with Windows PowerShell 5.1.
function Test-BrokenArrowFolder([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    return Test-Path -LiteralPath ([IO.Path]::Combine($Path, 'BrokenArrow.exe')) -PathType Leaf -ErrorAction SilentlyContinue
}

function Get-SteamGameCandidates([string[]]$SteamRoots) {
    $libraries = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($steamRoot in $SteamRoots) {
        if ([string]::IsNullOrWhiteSpace($steamRoot)) { continue }
        [void]$libraries.Add($steamRoot)
        $vdf = [IO.Path]::Combine($steamRoot, 'steamapps\libraryfolders.vdf')
        if (Test-Path -LiteralPath $vdf -PathType Leaf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                [void]$libraries.Add($match.Groups[1].Value.Replace('\\', '\'))
            }
        }
    }
    foreach ($library in $libraries) {
        $manifest = [IO.Path]::Combine($library, 'steamapps\appmanifest_1604270.acf')
        if (Test-Path -LiteralPath $manifest -PathType Leaf) {
            $match = [regex]::Match((Get-Content -LiteralPath $manifest -Raw), '"installdir"\s+"([^"]+)"')
            if ($match.Success) { [IO.Path]::Combine($library, 'steamapps\common\' + $match.Groups[1].Value) }
        }
        [IO.Path]::Combine($library, 'steamapps\common\Broken Arrow')
    }
}

function Find-BrokenArrow([string]$ExplicitPath, [string]$PackageRoot, [switch]$NonInteractive) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $explicit = $ExplicitPath.Trim().Trim('"')
        if (-not (Test-BrokenArrowFolder $explicit)) { throw 'The supplied game folder does not contain BrokenArrow.exe. Check -GameDir.' }
        return (Resolve-Path -LiteralPath $explicit).ProviderPath
    }
    $roots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        try {
            $item = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
            if ($item.SteamPath) { $roots += $item.SteamPath }
            if ($item.InstallPath) { $roots += $item.InstallPath }
        } catch { }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { $roots += Join-Path $base 'Steam' }
    }
    $candidates = @($PackageRoot, (Split-Path -Parent $PackageRoot))
    $candidates += @(Get-SteamGameCandidates $roots)
    $candidates += @('D:\Games\Broken Arrow', 'D:\SteamLibrary\steamapps\common\Broken Arrow', 'E:\SteamLibrary\steamapps\common\Broken Arrow')
    foreach ($candidate in $candidates) {
        if (Test-BrokenArrowFolder $candidate) { return (Resolve-Path -LiteralPath $candidate).ProviderPath }
    }
    if ($NonInteractive) { throw 'Broken Arrow was not found. Supply -GameDir with the folder containing BrokenArrow.exe.' }
    Write-Host 'Broken Arrow was not found automatically.'
    $answer = (Read-Host 'Paste the game folder containing BrokenArrow.exe, then press Enter').Trim().Trim('"')
    if (-not (Test-BrokenArrowFolder $answer)) { throw 'That folder does not contain BrokenArrow.exe. Run the installer again with the correct folder.' }
    return (Resolve-Path -LiteralPath $answer).ProviderPath
}
