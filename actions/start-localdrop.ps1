# Starts LocalDrop, works out the LAN URL, and copies it to the clipboard.
#
# Three things changed from the original. The executable path was hardcoded to one user's
# profile, so it could never work on another machine; it now builds from $env:USERPROFILE and
# says so when it is missing. The whole script ran under $ErrorActionPreference =
# 'SilentlyContinue', which hid every failure including "the server never came up". And the
# server-start timeout used to fall through silently into a dialog showing a URL that pointed
# at nothing.

$exe  = Join-Path $env:USERPROFILE 'Desktop\PublicarLocalDrop\LocalDrop.Api.exe'
$port = 5000

if (-not (Test-Path -LiteralPath $exe)) {
    throw "LocalDrop not found at $exe"
}

function Test-PortListening {
    [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}

if (-not (Test-PortListening)) {
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden

    $deadline = (Get-Date).AddSeconds(10)
    while (-not (Test-PortListening) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-PortListening)) {
        throw "LocalDrop was started but nothing is listening on port $port after 10 s"
    }
}

# The adapter that is up and has a default gateway is the one on the LAN. Without that
# filter a VPN or a virtual switch adapter can win and hand out an unreachable address.
$ip = (Get-NetIPConfiguration |
    Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
    Select-Object -First 1).IPv4Address.IPAddress

if (-not $ip) {
    $ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -like '192.168.*' -or $_.IPAddress -like '10.*' } |
        Select-Object -First 1).IPAddress
}

$localUrl = "http://localhost:$port"

if ($ip) {
    $networkUrl = "http://${ip}:${port}"
    Set-Clipboard -Value $networkUrl
    $networkLine = "Network: $networkUrl  (copied to clipboard)"
}
else {
    # Saying the address could not be found beats copying the string "IP-not-detected"
    # to the clipboard, which is what the original did.
    Set-Clipboard -Value $localUrl
    $networkLine = 'Network: LAN address could not be detected'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$form = New-Object System.Windows.Forms.Form
$form.Text = 'LocalDrop'
$form.FormBorderStyle = 'FixedDialog'
$form.StartPosition = 'CenterScreen'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.TopMost = $true
$form.ClientSize = New-Object System.Drawing.Size(400, 150)

$label = New-Object System.Windows.Forms.Label
$label.Location = New-Object System.Drawing.Point(16, 14)
$label.Size = New-Object System.Drawing.Size(368, 80)
$label.Text = "LocalDrop is running.`r`n`r`nLocal:   $localUrl`r`n$networkLine"
$form.Controls.Add($label)

$openBtn = New-Object System.Windows.Forms.Button
$openBtn.Text = 'Open in browser'
$openBtn.Size = New-Object System.Drawing.Size(150, 30)
$openBtn.Location = New-Object System.Drawing.Point(16, 105)
$openBtn.Add_Click({ Start-Process $localUrl })
$form.Controls.Add($openBtn)

$closeBtn = New-Object System.Windows.Forms.Button
$closeBtn.Text = 'Close'
$closeBtn.Size = New-Object System.Drawing.Size(100, 30)
$closeBtn.Location = New-Object System.Drawing.Point(284, 105)
$closeBtn.DialogResult = [System.Windows.Forms.DialogResult]::OK
$form.Controls.Add($closeBtn)

$form.AcceptButton = $openBtn
$form.CancelButton = $closeBtn
$form.ShowDialog() | Out-Null
$form.Dispose()
