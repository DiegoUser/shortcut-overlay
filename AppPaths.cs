using System.IO;

namespace Atajos;

/// <summary>
/// Every path the app uses, resolved from the folder the executable lives in.
///
/// Nothing is hardcoded to a machine, which is the whole point: copy the output folder to
/// another PC and the shortcuts work there. It also means the app never reaches into
/// C:\Scripts, so the old iCUE setup keeps running untouched alongside this one.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = AppContext.BaseDirectory;

    /// <summary>Hand-edited configuration. Never written by the app.</summary>
    public static string Config { get; } = Path.Combine(Root, "perfiles.json");

    /// <summary>Which profile is active. Machine-written state, so it lives on its own.</summary>
    public static string ActiveProfile { get; } = Path.Combine(Root, "perfil-activo.txt");

    public static string ActionsDir { get; } = Path.Combine(Root, "actions");

    /// <summary>Failures only. Actions run with no window, so this is the only place they surface.</summary>
    public static string Log { get; } = Path.Combine(Root, "atajos.log");

    /// <summary>The previous log, kept across one rollover.</summary>
    public static string PreviousLog { get; } = Path.Combine(Root, "atajos.log.1");

    public static string Icon { get; } = Path.Combine(Root, "atajos.ico");
}
