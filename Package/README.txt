MOVING LEAN ENHANCED v0.3

REQUIREMENTS
- Ready or Not, supported Windows x64 Steam build 25784851.
- A compatible UE4SS installation, installed separately.
- Microsoft .NET Framework 4.x (4.8 recommended).

INSTALL
1. Close the game. Stop using the old standalone MovingLean24 launcher.
2. Extract the included ue4ss folder into:
   Ready Or Not\ReadyOrNot\Binaries\Win64\
3. Start the game normally from Steam. No separate launcher or console is needed.

Already using the older UE4SS folder layout?
Place the MovingLean folder inside your active UE4SS Mods folder instead.
Do not replace UE4SS.dll, proxy DLLs, mods.txt, or another mod's files.
The included enabled.txt enables MovingLean automatically.

USE
Q/E and movement controls remain unchanged.
Default ordinary lean target: 24, increased from the game's 15.
Ctrl+Alt+F8 while the game has focus toggles the patch for the current session.
There is no on-screen toggle notification. Status is written to the log.

CONFIGURATION
MovingLean\LeanAmount.txt contains one number, default 24.
Allowed range: 15 to 30. Close the game before changing it.
These are internal target values, not measured degrees or centimeters.

DISABLE / UNINSTALL
Close the game. Rename MovingLean\enabled.txt to enabled.off to disable,
or remove only the MovingLean folder to uninstall. Game files need no repair.

HOW IT WORKS
This is a UE4SS Lua module with a small native launch bridge and a hidden helper.
The helper runs only alongside its parent game process and exits with the game.
It verifies the executable SHA256 and four original instruction sequences before
redirecting those instructions to private lean constants in process memory.
No disk executable patch, UObject scan, camera-rotation overlay, or save edit.
Existing native collision-check calls remain in place; the corresponding range
argument and release comparison are updated with the lean target.

SUPPORTED BUILD
Steam build 25784851.
EXE SHA256: ff445331e7144a2dc79ec50c2e6fd42f2a2e7b49ae7b03ae1a6076dcfc769262
Different or updated executables are rejected without applying the patch.
Designed for single-player; multiplayer is not tested or supported.

LOGS
%LOCALAPPDATA%\MovingLean\Logs\patcher_<process id>.log
AUTO ON ... four instructions verified = patch applied.
UE4SS.log's "Automatic patch requested" only confirms the launch request.

Do not manually run the hidden helper. It only accepts its parent game process.
This package does not bundle UE4SS or any original game files.

UE4SS: https://github.com/UE4SS-RE/RE-UE4SS
