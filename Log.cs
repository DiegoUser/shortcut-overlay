using System.IO;

namespace Atajos;

/// <summary>
/// The one place anything gets written down.
///
/// Everything here runs with no window, so a failure that is not logged is a failure nobody
/// will ever see. The file is capped and rolled over rather than growing forever.
/// </summary>
public static class Log
{
    /// <summary>Roll over past this size. Errors are rare; this is months of them.</summary>
    private const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                RollIfTooBig();
                File.AppendAllText(AppPaths.Log, $"{DateTime.Now:s}  {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Losing a log line must never take the app down with it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Keeps one previous file. Two generations is enough to still have context after a
    /// rollover, without turning the install folder into an archive.
    /// </summary>
    private static void RollIfTooBig()
    {
        FileInfo file = new(AppPaths.Log);

        if (!file.Exists || file.Length < MaxBytes) return;

        File.Move(AppPaths.Log, AppPaths.PreviousLog, overwrite: true);
    }
}
