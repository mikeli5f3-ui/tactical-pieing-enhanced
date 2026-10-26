# Building Moving Lean Enhanced

Player releases require no compiler or Python. The companion is a .NET Framework x64 Windows-subsystem executable.

Build prerequisites: Python 3 and the Windows .NET Framework C# compiler. No downloaded native compiler, C++ SDK, UE4SS SDK, Lua headers, or Python packages are needed to build the release files.

Run `Source/Build.ps1 -Python <path-to-python.exe>` from an extracted source package. It creates `Package/ue4ss/Mods/MovingLean/bin`. Existing build binaries are backed up before rebuilding. Copy `Scripts/main.lua`, `enabled.txt`, and `LeanAmount.txt` from the main download when assembling a fresh player package.

`build_bridge.py` emits a position-independent x64 PE DLL. DllMain records its module handle only. The exported Lua entry has the standard `int(lua_State*)` signature, makes no Lua API calls, and returns zero results. It resolves its own file path, starts the sibling companion with `CreateProcessW` and `CREATE_NO_WINDOW`, and closes the resulting process/thread handles. It never patches memory or inspects Unreal objects itself.

`MovingLeanAuto.cs` checks that its parent process is the supported game executable, validates the executable hash and instruction bytes, and applies the four relative-constant changes. The game path is derived from that parent process. A per-PID named mutex prevents duplicate helpers.

Core tests: run the companion with `--self-test`; it creates only its own synthetic test process. `--verify-only <GAME_EXE>` performs a read-only disk-file check. Neither mode starts the game. Both modes write results under `%LOCALAPPDATA%/MovingLean/Logs`.

Optional native launch tests: compile `BridgeTest.cs` as a console x64 executable and `BridgeChild.cs` as a Windows-subsystem x64 executable. In an isolated test directory, name the latter `MovingLeanAuto.exe` beside a copy of `MovingLeanBridge.dll`; the host loads the DLL and calls its export. The child reports its path and whether a console exists. Do not replace the player companion with this fixture.

The assembly sites and executable hash are intentionally build-specific. After a game update, review the actual native logic and update the guards and tests before distributing a compatible build. Do not remove the guards to force application.
