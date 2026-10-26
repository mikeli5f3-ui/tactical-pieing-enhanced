# Builds use only Python stdlib and the installed .NET Framework compiler.
param([string]$Python = "python")
$ErrorActionPreference = "Stop"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $PSScriptRoot "MovingLeanAuto.cs"
$bridge = Join-Path $PSScriptRoot "build_bridge.py"
$pythonCommand = Get-Command $Python -ErrorAction Stop
$pythonPath = $pythonCommand.Source
foreach ($path in @($compiler, $source, $bridge, $pythonPath)) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing: $path" }
}
$target = Join-Path (Split-Path $PSScriptRoot -Parent) "Package\ue4ss\Mods\MovingLean\bin"
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in @("MovingLeanAuto.exe", "MovingLeanBridge.dll")) {
    $existing = Join-Path $target $name
    if (Test-Path -LiteralPath $existing) {
        Copy-Item -LiteralPath $existing -Destination ($existing + ".backup_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
    }
}
& $pythonPath $bridge (Join-Path $target "MovingLeanBridge.dll")
if ($LASTEXITCODE -ne 0) { throw "Bridge build failed" }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$target\MovingLeanAuto.exe" $source
if ($LASTEXITCODE -ne 0) { throw "Core build failed" }
