using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Atajos;

/// <summary>
/// Runs the action script bound to a key.
///
/// The overlay launches PowerShell itself instead of going through a .bat: a .bat opens a
/// console window, and the flash it produces on every keypress is exactly the kind of
/// visible seam this project is trying to remove.
/// </summary>
public static class ActionRunner
{
    /// <summary>
    /// Raised on a background thread once an action's process has exited. The overlay uses it
    /// to re-read the state a key reports, since an action that changes something is exactly
    /// the moment the line describing it goes stale.
    /// </summary>
    public static event Action? ActionFinished;

    /// <summary>
    /// Runs a binding's action. Does nothing for an unassigned key, which is a normal state
    /// rather than an error.
    /// </summary>
    public static void Run(KeyBinding binding)
    {
        if (binding.IsFree) return;

        string path = Path.Combine(AppPaths.ActionsDir, binding.Action);

        if (!File.Exists(path))
        {
            Log($"{binding.Key} -> '{binding.Action}' does not exist in {AppPaths.ActionsDir}");
            return;
        }

        try
        {
            Start(binding, path);
        }
        catch (Exception ex)
        {
            Log($"{binding.Key} -> '{binding.Action}' failed to start: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts the script and watches how it ends.
    ///
    /// Capturing stderr here means the action scripts do not each need their own logging:
    /// they can just throw, and the reason lands in the log. A script that fails silently
    /// looks identical to a key that is not bound, which is the hardest kind of bug to chase.
    /// </summary>
    private static void Start(KeyBinding binding, string path)
    {
        ProcessStartInfo info = new()
        {
            FileName = "powershell.exe",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppPaths.ActionsDir,
        };

        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");

        // Actions are launched through a wrapper rather than directly, so that a failure
        // reaches the log as one readable line instead of a multi-line PowerShell banner in
        // the OEM code page. See _lib\run-action.ps1.
        info.ArgumentList.Add(Path.Combine(AppPaths.ActionsDir, "_lib", "run-action.ps1"));
        info.ArgumentList.Add("-Script");
        info.ArgumentList.Add(path);

        // Whatever the key declares in "opciones", in order, forwarded to the script as
        // positional arguments. A script that takes none simply gets none. This is what keeps
        // a toggle's two device names written down once instead of once here and once there.
        foreach (string option in binding.Options) info.ArgumentList.Add(option);

        Process process = new() { StartInfo = info, EnableRaisingEvents = true };
        List<string> errors = [];

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) errors.Add(e.Data.Trim());
        };

        process.Exited += (_, _) =>
        {
            if (process.ExitCode != 0 || errors.Count > 0)
            {
                string detail = errors.Count > 0
                    ? string.Join(" | ", errors)
                    : "(no error output)";

                Log($"{binding.Key} -> '{binding.Action}' exit={process.ExitCode}: {detail}");
            }

            process.Dispose();

            // After Dispose, so a slow subscriber cannot hold on to the process object. It
            // fires even when the action failed: a partial change still leaves the panel wrong.
            ActionFinished?.Invoke();
        };

        process.Start();
        process.BeginErrorReadLine();
    }

    private static void Log(string message) => Atajos.Log.Write(message);
}
