param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../src/LobbyUI.cs') -Raw
function Get-MethodSource([string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 0
    for ($i = $open; $i -lt $source.Length; $i++) {
        if ($source[$i] -eq '{') { $depth++ }
        if ($source[$i] -eq '}') { $depth-- }
        if ($depth -eq 0) { return $source.Substring($start, $i - $start + 1) }
    }
    throw "Unclosed production method: $signature"
}
$methods = @(
    'static string ScenarioKey(', 'static string DeckKey(', 'int FindDeck(',
    'SetupSnapshot CaptureSetup(', 'void RestoreSetup(', 'void ReadSettings(',
    'void SaveCurrent(', 'void WriteSettings(', 'void SelectMap(',
    'List<int> FilteredMaps(', 'void SavePreset(', 'void LoadPreset(', 'void PickDeckForSlot(', 'void ResetDefaults('
) | ForEach-Object { Get-MethodSource $_ }
$source = Get-Content (Join-Path $PSScriptRoot '../src/TeamRosterUI.cs') -Raw
$methods += @('List<RosterTeam> ReadRoster(', 'int ActiveRosterCount(', 'int ClosedRosterCount(',
    'int ScenarioRosterCount(', 'string MatchRosterSummary(') | ForEach-Object { Get-MethodSource $_ }
$generated = "using System; using System.IO; using System.Collections.Generic; namespace BALocalSkirmish { public partial class Core {`n" + ($methods -join "`n") + "`n} }"
$fixture = Join-Path $PSScriptRoot 'LobbyRegression/obj/LobbyFixture.cs'
New-Item -ItemType Directory -Path (Split-Path $fixture -Parent) -Force | Out-Null
[IO.File]::WriteAllText($fixture, $generated)
& $Dotnet run --project (Join-Path $PSScriptRoot 'LobbyRegression/LobbyRegression.csproj') -c Release --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "Lobby regression failed (exit $LASTEXITCODE)." }
