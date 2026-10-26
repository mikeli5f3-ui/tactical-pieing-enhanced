# Tactical Pieing Enhanced — 战术切角增强

Source and build instructions for **MovingLean v0.3**, a Ready or Not ordinary-lean enhancement intended for single-player.

Nexus mod page: https://www.nexusmods.com/readyornot/mods/9062

The uploaded file is currently quarantined pending Nexus review. This repository supplies inspectable source and build instructions; it does not establish approval or an independent malware assessment.

## What the mod changes

The supported executable uses fixed positive and negative ordinary-lean target constants of +15 and -15. The mod redirects four specific instruction references to private constants in process memory: the positive target, negative target, associated collision-range argument, and release comparison. The default target is 24 and the allowed setting is 15–30. These are internal game values, not measured degrees or centimeters.

The on-disk game executable and saves are not patched. Existing native collision-check calls remain. Multiplayer is not tested or supported.

## Source layout

- `Source/MovingLeanAuto.cs`: the .NET Framework x64 companion, process guards, executable checks, runtime patching, rollback, and synthetic process tests.
- `Source/build_bridge.py`: Python stdlib emitter for the minimal x64 native launch bridge. The exported Lua entry launches the sibling companion using `CreateProcessW`; DllMain only stores the module handle. Imports are `GetModuleFileNameW`, `CreateProcessW`, and `CloseHandle`.
- `Source/Build.ps1`: build script.
- `Source/BridgeTest.cs` and `Source/BridgeChild.cs`: isolated native launch fixtures.
- `Source/README.md`: additional implementation and test details.
- `Package/ue4ss/Mods/MovingLean/Scripts/main.lua`: relative-path UE4SS loader entry.
- `Package/ue4ss/Mods/MovingLean/LeanAmount.txt`: default setting, 24.

No compiled executable, DLL, UE4SS distribution, original game file, credential, or user runtime log is included.

## Build on Windows x64

Requirements: Python 3, Windows PowerShell, and the installed Microsoft .NET Framework C# compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

From the repository root:

```powershell
powershell -NoProfile -File ".\Source\Build.ps1" -Python "C:\Path\To\python.exe"
```

Replace the Python path with the installed interpreter. No Python packages or downloaded native compiler are needed. The script generates:

```text
Package/ue4ss/Mods/MovingLean/bin/MovingLeanAuto.exe
Package/ue4ss/Mods/MovingLean/bin/MovingLeanBridge.dll
```

Existing output binaries are backed up before rebuilding. Inspect sources and build commands before execution. Compiler versions and metadata can affect binary hashes; the checksums below identify the original uploaded release, not a promise of byte-identical rebuilds.

## Optional verification

The companion is a Windows-subsystem executable. Diagnostic output is recorded under `%LOCALAPPDATA%/MovingLean/Logs`.

```powershell
Start-Process -FilePath ".\Package\ue4ss\Mods\MovingLean\bin\MovingLeanAuto.exe" -ArgumentList "--self-test" -Wait
```

This creates only a synthetic child process and checks native execution of 15 → 24 → 15, instruction verification, restore behavior, and repeated toggling. It does not start or access Ready or Not.

`--verify-only <GAME_EXE>` validates only the supplied disk executable's hash, instruction sequences, and original constant references. It does not apply a game-process patch.

## Startup and guards

UE4SS loads the Lua entry, which loads the native bridge. The bridge starts a windowless companion directly as a child of the game. The companion validates the parent process name, executable SHA256, and original instructions before writing. A per-game-PID mutex rejects duplicate helpers. The helper exits with the game; it does not register a Windows startup task or service.

The companion has no network, download, credential-access, or privilege-elevation implementation. It writes diagnostics to its per-user log directory.

## Supported release

Windows x64 Steam build **25784851**. Supported game EXE SHA256:

`ff445331e7144a2dc79ec50c2e6fd42f2a2e7b49ae7b03ae1a6076dcfc769262`

Different executable hashes are rejected. Do not remove guards to force compatibility after an update.

Original uploaded archive SHA256:

`d04a12df23cd6bc77d79b5308c0072417212cc4058fc647d06bde4caa0cdd136`

Original `MovingLeanAuto.exe` SHA256:

`3a2345eb097067b61ea6bb31b421c06acdf8f3d07680ceb34eb824a02077d491`

Original `MovingLeanBridge.dll` SHA256:

`1dd563f082ea88a554ade602afa49632a8fd419502d0207b83370ade3801aa38`

## Review scope

The standalone v0.2 gameplay behavior was confirmed by the initial user. The v0.3 automatic startup and application path was separately tested. This is limited testing, not exhaustive weapon, animation, collision, or mod compatibility validation.

UE4SS is an external prerequisite: https://github.com/UE4SS-RE/RE-UE4SS
