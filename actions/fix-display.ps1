# Re-applies the display topology after resuming from sleep.
#
# The monitor is connected over DisplayPort, which drops its hot-plug detect signal during
# power saving. On resume the GPU sometimes fails to re-enumerate the output, leaving the
# screen black while the PC stays awake. Switching to Duplicate and back forces a full mode
# set, which recovers it. This is the scripted equivalent of Win+P, Duplicate, PC screen only.
#
# The original suppressed every error with $ErrorActionPreference = 'SilentlyContinue'. On a
# script whose whole job is to recover a black screen, hiding the reason it failed is the
# worst possible default.

$displaySwitch = Join-Path $env:SystemRoot 'System32\DisplaySwitch.exe'

if (-not (Test-Path -LiteralPath $displaySwitch)) {
    throw "DisplaySwitch.exe not found at $displaySwitch"
}

Start-Process -FilePath $displaySwitch -ArgumentList '/clone' -Wait

# Let the driver settle before switching back.
Start-Sleep -Seconds 2

Start-Process -FilePath $displaySwitch -ArgumentList '/internal' -Wait
