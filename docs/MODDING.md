# Modding guide: BA Local Skirmish

This local UI fork remembers settings in `UserData\BALocalSkirmish\lobby.json` and supports named
presets per scenario. See [UI-ITERATION.md](UI-ITERATION.md) for scope, behaviour and test limits.
This guide covers building from source.

## Building from source

Prerequisites:

- Windows, with the game installed.
- .NET SDK 6.0 or newer (`dotnet --version` to check): https://dotnet.microsoft.com/download
- MelonLoader installed in the game folder, and the game launched once modded. Running `install.bat` from
  the repo root installs MelonLoader for you, and the first modded launch produces the assemblies the
  project builds against.

Build:

```powershell
dotnet build -c Release src\BALocalSkirmish.csproj -p:GameDir="D:\Games\Broken Arrow"
```

Point `-p:GameDir` at your own install, or edit the default `<GameDir>` line in `src\BALocalSkirmish.csproj`.
The output `BALocalSkirmish.dll` lands in `src\bin\Release\`. Close the game, keep the original DLL
outside `Mods`, then replace only `Mods\BALocalSkirmish.dll`. Keep your existing loader and launcher.
`dist\BALocalSkirmish.dll` is retained as the upstream original, not this fork's output.

Checks (PowerShell, .NET SDK on PATH):

```powershell
./tests/Test-Lobby.ps1
./tests/Test-TeamRoster.ps1
./tests/Test-Preview.ps1
./tests/Test-SlotPolicy.ps1
./tests/Test-SlotRuntime.ps1
./tests/Test-NativeAiProfiles.ps1
./tests/Test-NativeStyle.ps1
./tests/Test-DeckSelection.ps1
./tests/Test-PatchRegistration.ps1 -GameDir 'C:\Games\Broken Arrow'
```

The lobby fixture runs production selection/persistence methods with managed stand-ins; it does not
start Unity. An in-game smoke checklist is in `UI-ITERATION.md`.
