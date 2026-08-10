# Opens a command prompt in the folder of the active File Explorer window.
#
# Falls back to the Desktop rather than doing nothing: a key that opens a terminal
# somewhere is more useful than a key that silently does not respond.

. (Join-Path $PSScriptRoot '_lib\Get-ExplorerPath.ps1')

$path = Get-ExplorerPath

if (-not $path) { $path = [Environment]::GetFolderPath('Desktop') }

Start-Process 'cmd.exe' -WorkingDirectory $path
