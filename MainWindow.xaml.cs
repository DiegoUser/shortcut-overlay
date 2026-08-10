using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Atajos;

public partial class MainWindow : Window
{
    // Extended window styles.
    //
    // WS_EX_TRANSPARENT is deliberately absent: the cells are clickable. It belonged to the
    // original always-on-screen design, where a panel that swallowed clicks would have been
    // in the way all day. This panel only exists while it is being used.
    //
    // WS_EX_NOACTIVATE now does more than avoid stealing the caret. Clicking a cell must not
    // make this window the foreground one, or actions like "open a terminal in the current
    // Explorer folder" would resolve against the overlay instead of Explorer.
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;   // no alt-tab entry
    private const int WS_EX_NOACTIVATE = 0x08000000;   // never steals focus

    // Hotkey modifiers.
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_SHIFT = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_SPACE = 0x20;
    private const uint VK_1 = 0x31;
    private const uint VK_Q = 0x51;

    private const int WM_HOTKEY = 0x0312;

    // Hotkey ids. Action keys occupy 11..16, one per slot.
    private const int HOTKEY_CYCLE = 1;
    private const int HOTKEY_EXIT = 2;
    private const int HOTKEY_ACTION_BASE = 11;
    private const int SlotCount = 6;

    /// <summary>Distance from the working-area edges, in device-independent pixels.</summary>
    private const double MarginFromEdge = 24;

    /// <summary>
    /// How long Ctrl+Shift must be held before the panel opens.
    ///
    /// This delay is what keeps the overlay out of the way of everything else. Our action
    /// hotkeys only exist while the panel is open, so an ordinary Ctrl+Shift+1 in Excel or
    /// Ctrl+Shift+Space in VS Code is released long before we register anything. Opening
    /// the panel has to be deliberate, not something fingers do by accident.
    /// </summary>
    private static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(350);

    /// <summary>How often the Ctrl+Shift state is sampled.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ProfileStore _store = new();
    private readonly DispatcherTimer _holdTimer;

    private IntPtr _handle;
    private bool _isShown;
    private DateTime? _heldSince;

    /// <summary>
    /// Whether hovering the panel is currently allowed to keep it open. Only true once the
    /// pointer has been outside since the panel opened, so a panel that happens to appear
    /// under a resting cursor still closes when Ctrl+Shift is released.
    /// </summary>
    private bool _hoverCanHold;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _store;

        // Creates the HWND without showing the window, so hotkeys and polling are live from
        // startup even though the overlay is hidden.
        new WindowInteropHelper(this).EnsureHandle();

        _holdTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = PollInterval };
        _holdTimer.Tick += OnPollHoldState;
        _holdTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        int exStyle = GetWindowLong(_handle, GWL_EXSTYLE);
        SetWindowLong(_handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        HwndSource.FromHwnd(_handle)?.AddHook(WndProc);

        // Deliberately not Ctrl+Shift based: that combination opens the panel, and the exit
        // shortcut should not make it flash on its way out.
        if (!RegisterHotKey(_handle, HOTKEY_EXIT, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_Q))
        {
            MessageBox.Show(
                "Could not register the exit shortcut Ctrl+Alt+Q — another app already owns it.\n\n" +
                "The overlay is click-through, so it cannot be reached with the mouse. " +
                "End the Atajos process from Task Manager to exit.",
                "Atajos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _holdTimer.Stop();

        if (_handle != IntPtr.Zero)
        {
            UnregisterPanelHotkeys();
            UnregisterHotKey(_handle, HOTKEY_EXIT);
        }

        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Watches for Ctrl+Shift being held.
    ///
    /// Polling rather than a WH_KEYBOARD_LL hook on purpose: RegisterHotKey cannot express a
    /// modifiers-only shortcut, and a low-level hook would put this process in the path of
    /// every keystroke on the machine — something game anti-cheat is right to be suspicious
    /// of. Reading two key states twenty times a second costs nothing and touches nothing.
    /// </summary>
    private void OnPollHoldState(object? sender, EventArgs e)
    {
        bool held = IsDown(VK_CONTROL) && IsDown(VK_SHIFT);

        if (_isShown && !IsMouseOver) _hoverCanHold = true;

        if (!held)
        {
            _heldSince = null;

            // Releasing the keys while pointing at the panel keeps it open, otherwise the
            // cells would be unclickable in practice: the panel would vanish on the way to
            // them. Moving the pointer away closes it.
            if (_isShown && !(IsMouseOver && _hoverCanHold)) SetOverlayShown(false);
            return;
        }

        if (_isShown) return;

        _heldSince ??= DateTime.UtcNow;

        if (DateTime.UtcNow - _heldSince >= HoldDelay) SetOverlayShown(true);
    }

    private void OnKeyCellClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: KeyBinding binding }) ActionRunner.Run(binding);
    }

    private static bool IsDown(uint key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        int id = wParam.ToInt32();

        if (id == HOTKEY_EXIT)
        {
            Close();
            handled = true;
        }
        else if (id == HOTKEY_CYCLE)
        {
            _store.CycleToNext();
            handled = true;
        }
        else if (id >= HOTKEY_ACTION_BASE && id < HOTKEY_ACTION_BASE + SlotCount)
        {
            RunSlot(id - HOTKEY_ACTION_BASE);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void RunSlot(int index)
    {
        IReadOnlyList<KeyBinding>? keys = _store.ActiveProfile?.Keys;

        if (keys is not null && index < keys.Count) ActionRunner.Run(keys[index]);
    }

    private void SetOverlayShown(bool shown)
    {
        _isShown = shown;

        if (shown)
        {
            // Captured before Show(): the panel belongs on the monitor of whatever the user
            // is working in, and this is the last moment that is unambiguous.
            IntPtr context = GetForegroundWindow();

            // Re-read the config on every show, so editing perfiles.json takes effect
            // without restarting the overlay.
            _store.Reload();

            Show();
            UpdateLayout();
            MoveToActiveMonitor(context);
            RegisterPanelHotkeys();

            // Armed only once the pointer leaves, so a panel opening under a resting cursor
            // does not get stuck on screen.
            _hoverCanHold = false;
        }
        else
        {
            UnregisterPanelHotkeys();
            Hide();
        }
    }

    /// <summary>
    /// Registers the shortcuts that only make sense while the panel is on screen. Holding
    /// them system-wide would take Ctrl+Shift+1..6 and Ctrl+Shift+Space away from every
    /// other app for the entire session.
    /// </summary>
    private void RegisterPanelHotkeys()
    {
        List<string> failed = [];

        for (int i = 0; i < SlotCount; i++)
        {
            if (!RegisterHotKey(_handle, HOTKEY_ACTION_BASE + i,
                    MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_1 + (uint)i))
            {
                failed.Add($"Ctrl+Shift+{i + 1} (error {Marshal.GetLastWin32Error()})");
            }
        }

        if (_store.CanCycle &&
            !RegisterHotKey(_handle, HOTKEY_CYCLE, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_SPACE))
        {
            failed.Add($"Ctrl+Shift+Space (error {Marshal.GetLastWin32Error()})");
        }

        // Without this, a shortcut another app already owns produces a panel whose keys
        // quietly do nothing, which is indistinguishable from a broken action.
        if (failed.Count > 0)
        {
            Log.Write("Could not register: " + string.Join(", ", failed) +
                      " -- another app already owns them");
        }
    }

    private void UnregisterPanelHotkeys()
    {
        for (int i = 0; i < SlotCount; i++) UnregisterHotKey(_handle, HOTKEY_ACTION_BASE + i);

        UnregisterHotKey(_handle, HOTKEY_CYCLE);
    }

    /// <summary>
    /// Puts the panel at the bottom-left of the monitor holding <paramref name="context"/>.
    ///
    /// Positioned through SetWindowPos in physical pixels rather than Window.Left/Top. WPF's
    /// logical units are scaled by one monitor's DPI, so on a mixed-DPI setup they land the
    /// window in the wrong place, and this machine has several monitors.
    /// </summary>
    private void MoveToActiveMonitor(IntPtr context)
    {
        System.Windows.Forms.Screen screen = context != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(context)
            : System.Windows.Forms.Screen.PrimaryScreen!;

        System.Drawing.Rectangle workArea = screen.WorkingArea;

        // Twice on purpose. Moving to a monitor with a different scale factor makes WPF
        // re-layout, so the size measured before the move can be stale by the time it lands.
        for (int pass = 0; pass < 2; pass++)
        {
            if (!GetWindowRect(_handle, out RECT bounds)) return;

            int height = bounds.Bottom - bounds.Top;
            int margin = (int)Math.Round(MarginFromEdge * GetDpiForWindow(_handle) / 96.0);

            SetWindowPos(_handle, IntPtr.Zero,
                workArea.Left + margin,
                workArea.Bottom - height - margin,
                0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
