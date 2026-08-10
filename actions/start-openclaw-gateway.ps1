# Starts the OpenClaw gateway in its workspace.
#
# The original wrapped C:\ScriptsHome\openclaw-gateway.cmd. That indirection is dropped here
# so the project carries its own copy of what the action does and does not depend on a folder
# outside it, which is what portability to another machine requires.
#
# The window is deliberately visible: the gateway logs to it, and hiding it would mean the
# process is running with nowhere to see what it is doing.

$workspace = Join-Path $env:USERPROFILE '.openclaw\workspace'

if (-not (Test-Path -LiteralPath $workspace)) {
    throw "OpenClaw workspace not found at $workspace"
}

if (-not (Get-Command 'openclaw' -ErrorAction SilentlyContinue)) {
    throw 'The openclaw command is not on PATH. Install it with: npm i -g openclaw'
}

Start-Process 'cmd.exe' -ArgumentList '/k', 'openclaw gateway' -WorkingDirectory $workspace
