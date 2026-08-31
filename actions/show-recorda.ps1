# Brings the Recorda calendar to the front.
#
# There is no window handle to find and no keystroke to synthesize: Recorda is single-
# instanced, so activating it again does not start a second process - the launch is redirected
# to the one already running, and that instance answers by showing its window. Which is what
# makes this a one-line action instead of a Win32 window hunt.
#
# The app spends its life hidden in the tray with reminders pending, so "not running" is not
# the normal case. If nothing is running yet this starts it, which is also correct.
#
# Note the identity: the package is still named Calendario even though the app is displayed as
# Recorda. Identity/Name is what the package family name derives from and it is deliberately
# frozen, because renaming it would orphan the app's startup task and toast registration.

$aumid = 'shell:AppsFolder\Calendario_zw2vjmatwqsva!App'

try {
    Start-Process $aumid
}
catch {
    # Only on the failure path, because Get-AppxPackage costs a few hundred milliseconds and
    # this action is meant to feel instant. Reaching here means the activation was refused, and
    # by far the likeliest reason is that the package is not registered on this machine.
    if (-not (Get-AppxPackage -Name Calendario -ErrorAction SilentlyContinue)) {
        throw 'Recorda is not installed: no package named Calendario is registered. Run deploy.ps1 in the CalendarioWinUi repository.'
    }

    throw "Recorda is registered but would not activate: $($_.Exception.Message)"
}
