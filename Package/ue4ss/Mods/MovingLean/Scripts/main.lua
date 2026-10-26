-- No UObject traversal, offsets, tick hooks, or game-property writes here.
-- The native bridge launches the sibling hidden helper in the current process context.
local src = debug.getinfo(1, "S").source:gsub("\\", "/"):gsub("^@", "")
local base = assert(src:match("^(.*)/Scripts/main%.lua$"), "MovingLean: cannot resolve mod path")
local bridge, err = package.loadlib(base .. "/bin/MovingLeanBridge.dll", "luaopen_MovingLeanBridge")
if not bridge then
    print("[MovingLean] Bridge unavailable; no patch requested: " .. tostring(err) .. "\n")
    return
end
local ok, failure = pcall(bridge)
if not ok then
    print("[MovingLean] Bridge failed: " .. tostring(failure) .. "\n")
    return
end
print("[MovingLean] Automatic patch requested. Verified result is logged under LOCALAPPDATA/MovingLean/Logs.\n")
