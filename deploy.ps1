# Publishes Atajos and installs it into its own folder, away from bin\Debug.
#
# The install directory is a hardcoded path outside %LOCALAPPDATA% on purpose. When this
# script is run from a shell hosted inside an MSIX package (a Claude Code session is one),
# Windows silently redirects %LOCALAPPDATA% into that package's private storage at
# ...\Packages\<package>\LocalCache\Local. The deploy then appears to succeed, and reads
# back correctly from inside the same container, but the files do not exist for any process
# outside it, and Windows discards that storage on package update or reset. Using a path
# that is never virtualized removes the whole failure mode.
#
# Development and daily use must not share a directory. While the app is running its .exe is
# locked, so every rebuild fails, and a dotnet clean would delete the app being used.
#
# Usage:
#   .\deploy.ps1                 install and restart
#   .\deploy.ps1 -SelfContained  bundle the .NET runtime, for a machine without it
#   .\deploy.ps1 -NoStart        install without launching
#   .\deploy.ps1 -NoAutostart    skip creating the startup shortcut

[CmdletBinding()]
param(
    [switch]$SelfContained,
    [switch]$NoStart,
    [switch]$NoAutostart
)

$ErrorActionPreference = 'Stop'

# Warn if this shell's %LOCALAPPDATA% is redirected into an MSIX package.
#
# A shell hosted inside a package container (a Claude Code session is one) has its
# %LOCALAPPDATA% writes redirected to ...\Packages\<package>\LocalCache\Local. Files written
# there read back correctly from inside the same container but do not exist for any process
# outside it, and Windows discards that storage on package update or reset.
#
# Two tests that look right are useless here. Comparing the path string fails because
# %LOCALAPPDATA% still reads as the real path. Calling GetCurrentPackageFullName fails
# because the redirection can come from the silo rather than from token package identity,
# so the process reports no package. Only writing a probe and looking for it works.
$probeName = "atajos-deploy-probe-$PID-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).tmp"
$probePath = Join-Path $env:LOCALAPPDATA $probeName
$redirectedInto = $null
try {
    Set-Content -LiteralPath $probePath -Value 'probe' -ErrorAction Stop
    $packagesRoot = Join-Path $env:USERPROFILE 'AppData\Local\Packages'
    if (Test-Path -LiteralPath $packagesRoot) {
        $redirectedInto = Get-ChildItem -LiteralPath $packagesRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName "LocalCache\Local\$probeName" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
    }
}
finally {
    Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
}

if ($redirectedInto) {
    Write-Warning "This shell's %LOCALAPPDATA% is redirected into a package container."
    Write-Warning "  probe surfaced at: $redirectedInto"
    Write-Warning "The install path below is hardcoded outside %LOCALAPPDATA%, so this deploy is safe,"
    Write-Warning "but do not reintroduce %LOCALAPPDATA% here or the install will land in package storage."
}

$projectDir  = $PSScriptRoot
$installDir  = 'C:\Scripts\Atajos'
$publishDir  = Join-Path $projectDir 'bin\Publish'
$startupLink = Join-Path ([Environment]::GetFolderPath('Startup')) 'Atajos.lnk'
$exePath     = Join-Path $installDir 'Atajos.exe'

# Written by the user or by the running app. A deploy must never overwrite these.
$preserve = @('perfiles.json', 'perfil-activo.txt', 'atajos.log', 'atajos.log.1')

Write-Host "Publicando..." -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$publishArgs = @(
    'publish', (Join-Path $projectDir 'Atajos.csproj'),
    '-c', 'Release',
    '-o', $publishDir,
    '--nologo',
    '-v', 'quiet'
)

if ($SelfContained) {
    $publishArgs += @('-r', 'win-x64', '--self-contained', 'true')
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Stop the running instance, or the .exe cannot be replaced.
$running = Get-Process -Name 'Atajos' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Cerrando la instancia en ejecucion..." -ForegroundColor Cyan
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 600
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null

Write-Host "Instalando en $installDir" -ForegroundColor Cyan

Get-ChildItem $publishDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($publishDir.Length).TrimStart('\')
    $target   = Join-Path $installDir $relative

    # Config and state survive a deploy. Losing an edited perfiles.json to an install would
    # be the most annoying possible bug in this project.
    if (($preserve -contains $relative) -and (Test-Path -LiteralPath $target)) {
        Write-Host "  conservado: $relative" -ForegroundColor DarkGray
        return
    }

    $targetDir = Split-Path $target
    if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }

    Copy-Item $_.FullName $target -Force
}

if (-not $NoAutostart) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($startupLink)
    $link.TargetPath = $exePath
    $link.WorkingDirectory = $installDir
    $link.IconLocation = $exePath
    $link.Description = 'Atajos'
    $link.Save()
    Write-Host "Arranque automatico: $startupLink" -ForegroundColor Cyan
}

if (-not $NoStart) {
    Start-Process -FilePath $exePath -WorkingDirectory $installDir
    Write-Host "Iniciado." -ForegroundColor Green
}

Write-Host ""
Write-Host "Listo." -ForegroundColor Green
Write-Host "  App:    $exePath"
Write-Host "  Config: $(Join-Path $installDir 'perfiles.json')"
