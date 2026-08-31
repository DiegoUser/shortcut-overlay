# Shows or hides the Recorda calendar.
#
# There is no window handle to find and no keystroke to synthesize: Recorda is single-
# instanced, so activating it again does not start a second process - the launch is redirected
# to the one already running. That instance decides what to do with it: come to the front, or
# go back to the tray when it was already the window in front. So this one key both summons
# and dismisses the calendar, and this script does not need to know which.
#
# The decision lives over there rather than here, and that was measured rather than preferred.
# Reading the foreground window from PowerShell needs Add-Type, which invokes the C# compiler,
# and ActionRunner starts a fresh powershell.exe per keypress: 237 ms became 550 ms, which
# stops feeling like a shortcut.
#
# The app spends its life hidden in the tray with reminders pending, so "not running" is not
# the normal case. If nothing is running yet this starts it, which is also correct.
#
# TODO - this wants to be one action for every app, not one script per app.
#   Nothing below is specific to a calendar. Any single-instanced app can be driven this way,
#   and several here could be: the whole body is "activate this AUMID, and say something useful
#   if it is not installed". The shape it wants is a single show-app.ps1 taking the identity
#   through 'opciones' in perfiles.json, the way toggle-audio-output.ps1 already takes its two
#   device names - so binding a new app becomes a config line and no new file.
#
#   Two things to settle before doing it, and neither is hard, but both are easy to discover
#   the wrong way round:
#
#   - An app that is NOT single-instanced will start a second copy every press. The generic
#     action cannot tell from the outside, so that is a property of the app being bound, and
#     it belongs written down beside the binding rather than assumed.
#   - Toggling only works for apps that treat re-activation as "show or hide me". An ordinary
#     app just comes to the front, which is still useful and is the honest default; the
#     generic action should promise that much and no more.
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
