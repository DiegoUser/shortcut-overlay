# Checks that LocalDrop is up, works out the LAN URL, and copies it to the clipboard.
#
# LocalDrop runs as the "LocalDrop" Windows service (deployed by LocalDrop's publish.cmd), so
# this action no longer launches an executable of its own. It used to start a separate copy on
# port 5000, which meant two installs, and the one behind this key silently fell behind every
# publish. Starting a stopped service needs elevation, so the action says so instead of trying.
# Failures still surface instead of falling through into a dialog with a URL that points at
# nothing.

$serviceName = 'LocalDrop'
$port        = 5001

function Test-LocalDropHealthy {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 3
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

if (-not (Test-LocalDropHealthy)) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    $reason = if (-not $service) {
        "the '$serviceName' Windows service is not installed"
    }
    elseif ($service.Status -ne 'Running') {
        "the '$serviceName' service is $($service.Status); start it from services.msc (needs admin)"
    }
    else {
        "the '$serviceName' service is running but nothing answers on port $port"
    }

    # The key press expects feedback, and the log alone would make it look like nothing happened.
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("LocalDrop is not available: $reason.", 'LocalDrop',
        'OK', 'Warning') | Out-Null
    throw "LocalDrop is not available: $reason"
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
