using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Atajos;

/// <summary>
/// The tray icon: the only visible proof that the app is running.
///
/// Without it the overlay is invisible until you remember the shortcut, there is no way to
/// quit except a keystroke you have to memorise, and a failure written to the log is a
/// failure nobody knows to go and read.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action onExit)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Abrir carpeta de configuración", null, (_, _) => Open(AppPaths.Root));
        menu.Items.Add("Ver registro de errores", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => onExit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Atajos — sostené Ctrl+Shift",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    /// <summary>
    /// Used to surface an error that would otherwise only exist in the log file.
    /// </summary>
    public void Notify(string title, string message)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = ToolTipIcon.Warning;
        _icon.ShowBalloonTip(5000);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (File.Exists(AppPaths.Icon))
            {
                // Asking for the tray's own size makes Windows pick the matching entry from
                // the .ico instead of rescaling whichever one it landed on.
                return new System.Drawing.Icon(AppPaths.Icon, SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Could not load {AppPaths.Icon}: {ex.Message}");
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void OpenLog()
    {
        if (!File.Exists(AppPaths.Log))
        {
            Notify("Atajos", "No hay errores registrados.");
            return;
        }

        Open(AppPaths.Log);
    }

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write($"Could not open {path}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Explicitly hidden first: a NotifyIcon that is only disposed can leave a ghost in
        // the tray until the user moves the pointer over it.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
