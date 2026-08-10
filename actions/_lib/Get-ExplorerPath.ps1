# Returns the folder path of the File Explorer window the user is actually looking at.
#
# The original scripts took the first window Shell.Application happened to return, which is
# arbitrary: with several Explorer windows open it could hand back one from another monitor.
# This matches the foreground window handle first, and only then falls back to any window.
#
# Returns $null when no Explorer folder can be resolved. Callers decide what to do about it,
# because "open a terminal somewhere sensible" and "open an editor somewhere sensible" are
# not necessarily the same answer.

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class ForegroundWindow
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
'@

function Get-ExplorerPath {
    param(
        # Which window counts as focused. Defaults to the real one; overridable because a
        # test cannot make Explorer the foreground window on demand -- Windows blocks
        # SetForegroundWindow from a background process, so the matching would never be
        # exercised otherwise.
        [IntPtr] $ForegroundHandle = [ForegroundWindow]::GetForegroundWindow()
    )

    $shell = New-Object -ComObject Shell.Application
    $windows = @($shell.Windows())

    if ($windows.Count -eq 0) { return $null }

    # The window with focus is the one the user means.
    foreach ($window in $windows) {
        $path = Get-WindowFolder $window
        if ($path -and ([IntPtr]$window.HWND) -eq $ForegroundHandle) { return $path }
    }

    # No Explorer window in focus, so any open one beats giving up.
    foreach ($window in $windows) {
        $path = Get-WindowFolder $window
        if ($path) { return $path }
    }

    return $null
}

function Get-WindowFolder($window) {
    # Shell.Windows() also returns Internet Explorer windows and windows that are still
    # opening, and touching .Document on those throws. A failure here just means "not a
    # usable Explorer window".
    try {
        $path = $window.Document.Folder.Self.Path
    }
    catch {
        return $null
    }

    if ($path -and (Test-Path -LiteralPath $path)) { return $path }

    return $null
}
