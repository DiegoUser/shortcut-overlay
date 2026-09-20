using System.Collections.Generic;
using System.Linq;

namespace Atajos;

/// <summary>
/// Builds the live line a key shows under its name.
///
/// A key declares which provider it wants through "estado" in perfiles.json. Everything else
/// about the key is static text, so this is the one place where the panel says something the
/// config file does not already know.
/// </summary>
public static class KeyStatus
{
    /// <summary>Current default playback device, and the one the key would switch to.</summary>
    public const string AudioOutputKind = "salida-audio";

    /// <summary>Longest a device name may be before it is cut, in characters.</summary>
    private const int MaxNameLength = 13;

    /// <summary>
    /// Complaints already written to the log. This runs on every open of the panel, so a
    /// key that is misconfigured once would otherwise write the same line dozens of times a
    /// day and bury everything else in there.
    /// </summary>
    private static readonly HashSet<string> Reported = [];

    /// <summary>
    /// Resolves the status line, or null when the key declares no provider. Never throws:
    /// reading live state is a nicety, and a machine where it fails should still get a panel
    /// with a working key on it. Failures go to the log instead.
    /// </summary>
    public static string? Describe(string kind, IReadOnlyList<string> options)
    {
        if (string.IsNullOrWhiteSpace(kind)) return null;

        try
        {
            return kind switch
            {
                AudioOutputKind => DescribeAudioOutput(options),
                _ => Unknown(kind),
            };
        }
        catch (Exception ex)
        {
            Report($"estado '{kind}' could not be read: {ex.Message}");
            return null;
        }
    }

    private static string? Unknown(string kind)
    {
        Report($"estado '{kind}' is not a known provider");
        return null;
    }

    private static string? DescribeAudioOutput(IReadOnlyList<string> options)
    {
        if (options.Count != 2)
        {
            Report($"estado '{AudioOutputKind}' needs exactly two names in 'opciones', got {options.Count}");
            return null;
        }

        string first = options[0];
        string second = options[1];

        AudioEndpoint? current = AudioOutput.List().FirstOrDefault(d => d.IsDefault);

        if (current is null) return null;

        // The same rule as toggle-audio-output.ps1: anything that is not the first device
        // switches to the first one. The two have to agree, or the panel would announce a
        // switch that does not happen. The script runs with these exact names, passed to it
        // from this same key, so only the direction is mirrored here — not the device list.
        bool onFirst = Matches(current.Name, first);

        string from = onFirst ? first
            : Matches(current.Name, second) ? second
            : current.Name;

        return $"{Shorten(from)} → {Shorten(onFirst ? second : first)}";
    }

    /// <summary>Logs a complaint the first time it is made, and stays quiet about it after that.</summary>
    private static void Report(string message)
    {
        if (Reported.Add(message)) Log.Write(message);
    }

    /// <summary>Config holds a fragment, Windows holds the full name: "HyperX" vs "Auriculares (HyperX Cloud II)".</summary>
    private static bool Matches(string deviceName, string fragment) =>
        !string.IsNullOrWhiteSpace(fragment) &&
        deviceName.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Keeps a name inside the cell, which is 96 pixels wide. Applied to both sides so a long
    /// name in perfiles.json cannot push the line out either, not just to the driver names —
    /// those are whatever the manufacturer felt like writing, such as
    /// "Auriculares (HyperX Cloud III)".
    /// </summary>
    private static string Shorten(string name)
    {
        if (name.Length <= MaxNameLength) return name;

        string cut = name[..MaxNameLength];

        // Prefer breaking on a space, unless that throws away most of the name. Cutting
        // mid-word is ugly; cutting right after an opening parenthesis is worse.
        int space = cut.LastIndexOf(' ');
        if (space >= MaxNameLength / 2) cut = cut[..space];

        return cut.TrimEnd(' ', '(', '[', '-', ',', '.') + "…";
    }
}
