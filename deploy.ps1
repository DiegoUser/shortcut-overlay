# Publishes Atajos and installs it into its own folder, away from bin\Debug.
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

$projectDir  = $PSScriptRoot
$installDir  = Join-Path $env:LOCALAPPDATA 'Atajos'
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
