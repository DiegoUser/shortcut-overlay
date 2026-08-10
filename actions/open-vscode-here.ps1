# Opens VS Code in the folder of the active File Explorer window.
#
# Two defects of the original are fixed here. It called `code`, which resolves to code.cmd
# and flashes a console window even from a hidden host, so this launches Code.exe directly.
# And it had no fallback: with no Explorer window open, $path came back empty and the script
# ended without opening anything and without saying why.

. (Join-Path $PSScriptRoot '_lib\Get-ExplorerPath.ps1')

$path = Get-ExplorerPath

if (-not $path) { $path = [Environment]::GetFolderPath('Desktop') }

$candidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Microsoft VS Code\Code.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft VS Code\Code.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft VS Code\Code.exe')
)

$code = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not $code) {
    # Thrown rather than swallowed: the overlay logs a failed action, and a missing editor
    # is exactly the kind of thing worth seeing in that log.
    throw 'VS Code not found. Looked in: ' + ($candidates -join '; ')
}

Start-Process -FilePath $code -ArgumentList $path
