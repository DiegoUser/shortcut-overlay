using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Atajos;

public partial class App : Application
{
    /// <summary>
    /// Local\ scopes the mutex to this login session, which is the right scope: two users on
    /// the same machine should each get their own overlay.
    /// </summary>
    private const string MutexName = @"Local\Atajos.SingleInstance";

    private Mutex? _instanceMutex;

    // The overlay starts hidden and is only shown by its shortcut, so nothing calls Show() at
    // startup. Held in fields so they are not collected while the app runs.
    private MainWindow? _overlay;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Silent on purpose. This happens when the startup shortcut fires and the app is
            // already running, which is not something worth interrupting anyone about.
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnFatalException;

        _overlay = new MainWindow();
        _tray = new TrayIcon(onExit: () => _overlay.Close());
    }

    /// <summary>
    /// An unexpected error on the UI thread is logged and swallowed rather than allowed to
    /// kill the process. A background utility that vanishes silently is worse than one that
    /// misses a keystroke: the shortcuts would simply stop working with no explanation. The
    /// balloon exists so the log actually gets read.
    /// </summary>
    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Write($"UNHANDLED (UI): {e.Exception}");
        e.Handled = true;

        _tray?.Notify("Atajos", "Ocurrió un error inesperado. Está anotado en el registro.");
    }

    /// <summary>
    /// Nothing can be recovered here, the process is going down either way. Logging it is the
    /// difference between a clue and a mystery.
    /// </summary>
    private void OnFatalException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Write($"FATAL: {e.ExceptionObject}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        if (_instanceMutex is not null)
        {
            // Only the instance that actually acquired it owns it.
            try { _instanceMutex.ReleaseMutex(); }
            catch (ApplicationException) { }

            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
