using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atajos;

/// <summary>One G key inside a profile, as the overlay draws it.</summary>
public sealed class KeyBinding(
    string key,
    string slot,
    string name,
    string icon,
    string action,
    IReadOnlyList<string> options,
    string statusKind)
    : INotifyPropertyChanged
{
    private string? _status;

    /// <summary>Config key: G1..G6.</summary>
    public string Key { get; } = key;

    /// <summary>
    /// The digit to press while the panel is open: 1..6. Shown instead of the G name
    /// because it is the one label that is true on every machine, with or without a
    /// keyboard that has G keys.
    /// </summary>
    public string Slot { get; } = slot;

    /// <summary>Short human name shown under the icon.</summary>
    public string Name { get; } = name;

    /// <summary>Segoe Fluent Icons glyph, already decoded from its hex code.</summary>
    public string Icon { get; } = icon;

    /// <summary>Action script the dispatcher will run. Empty means the key is unassigned.</summary>
    public string Action { get; } = action;

    /// <summary>
    /// Arguments handed to the action script, in order. They are also what a status provider
    /// reads, so a key that toggles between two things names them once and both the running
    /// and the describing use that same list.
    /// </summary>
    public IReadOnlyList<string> Options { get; } = options;

    /// <summary>Which <see cref="KeyStatus"/> provider draws the live line. Empty means none.</summary>
    public string StatusKind { get; } = statusKind;

    /// <summary>
    /// The live line under the name, refreshed every time the panel is shown. Null when the
    /// key declares no provider, or when reading the state failed.
    /// </summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (_status == value) return;

            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    public bool IsFree => string.IsNullOrWhiteSpace(Action);

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class Profile(string id, string name, IReadOnlyList<KeyBinding> keys)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public IReadOnlyList<KeyBinding> Keys { get; } = keys;
}

/// <summary>
/// Reads the same configuration the dispatcher reads, so the overlay can never describe a
/// key differently from what pressing it actually does.
///
/// perfiles.json is treated as read-only: it is hand-edited, and reserializing it would
/// destroy that formatting. Which profile is active is state, not configuration, so it
/// lives in its own one-line file that only this class writes.
/// </summary>
public sealed class ProfileStore : INotifyPropertyChanged
{
    private const string ConfigFile = "perfiles.json";

    private static readonly string ConfigPath = AppPaths.Config;
    private static readonly string StatePath = AppPaths.ActiveProfile;

    /// <summary>Shown when a key's icon code is missing or malformed (Segoe "Unknown").</summary>
    private const string FallbackIcon = "\uE9CE";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly string[] KeyOrder = ["G1", "G2", "G3", "G4", "G5", "G6"];

    private List<Profile> _profiles = [];
    private int _activeIndex;

    public ProfileStore() => Reload();

    /// <summary>The profile currently on screen, or null when the config could not be read.</summary>
    public Profile? ActiveProfile =>
        _profiles.Count == 0 ? null : _profiles[_activeIndex];

    /// <summary>
    /// Why the config could not be read, or null when everything loaded. Surfaced in the UI
    /// on purpose: a blank panel looks like a bug in the overlay, not a broken config file.
    /// </summary>
    public string? LoadError { get; private set; }

    public bool HasError => LoadError is not null;

    public bool CanCycle => _profiles.Count > 1;

    /// <summary>
    /// Re-reads the configuration from disk. Called every time the overlay is shown, so
    /// editing perfiles.json takes effect without restarting anything.
    /// </summary>
    public void Reload()
    {
        string? previousId = ActiveProfile?.Id;

        try
        {
            _profiles = ReadProfiles();
            LoadError = _profiles.Count == 0
                ? $"{ConfigFile} no define ningún perfil."
                : null;
        }
        catch (Exception ex)
        {
            _profiles = [];
            LoadError = $"No se pudo leer {ConfigPath}\n{ex.Message}";
        }

        _activeIndex = ResolveActiveIndex(previousId);
        RefreshStatus();
        RaiseAllChanged();
    }

    public void CycleToNext()
    {
        if (!CanCycle) return;

        _activeIndex = (_activeIndex + 1) % _profiles.Count;
        WriteActiveProfile(_profiles[_activeIndex].Id);
        RefreshStatus();
        RaiseAllChanged();
    }

    /// <summary>
    /// Re-reads the live line of every key in the active profile. Called when the panel opens
    /// and again once an action finishes, so a key that changes the very state it reports
    /// shows the new one without the panel having to be reopened.
    /// </summary>
    public void RefreshStatus()
    {
        foreach (KeyBinding key in ActiveProfile?.Keys ?? [])
        {
            key.Status = KeyStatus.Describe(key.StatusKind, key.Options);
        }
    }

    private static List<Profile> ReadProfiles()
    {
        if (!File.Exists(ConfigPath))
            throw new FileNotFoundException($"El archivo no existe.", ConfigPath);

        ConfigDto? config = JsonSerializer.Deserialize<ConfigDto>(
            File.ReadAllText(ConfigPath), JsonOptions);

        if (config?.Perfiles is null) return [];

        return config.Perfiles
            .Select(p => new Profile(
                p.Id ?? "",
                string.IsNullOrWhiteSpace(p.Nombre) ? p.Id ?? "?" : p.Nombre,
                BuildKeys(p.Teclas)))
            .ToList();
    }

    /// <summary>
    /// Always produces the six keys in order, so a key missing from the JSON renders as
    /// free rather than silently shifting the grid.
    /// </summary>
    private static List<KeyBinding> BuildKeys(Dictionary<string, KeyDto>? teclas)
    {
        List<KeyBinding> keys = [];

        for (int i = 0; i < KeyOrder.Length; i++)
        {
            string key = KeyOrder[i];

            KeyDto? dto = null;
            teclas?.TryGetValue(key, out dto);

            keys.Add(new KeyBinding(
                key,
                (i + 1).ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(dto?.Nombre) ? "Libre" : dto.Nombre,
                DecodeIcon(dto?.Icono),
                dto?.Accion ?? "",
                dto?.Opciones ?? [],
                dto?.Estado ?? ""));
        }

        return keys;
    }

    /// <summary>Turns a hex code like "E767" into the glyph it names.</summary>
    private static string DecodeIcon(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return FallbackIcon;

        return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)
            ? char.ConvertFromUtf32(code)
            : FallbackIcon;
    }

    /// <summary>
    /// Keeps the same profile selected across a reload when it still exists, so editing the
    /// config does not silently jump the user back to the first profile.
    /// </summary>
    private int ResolveActiveIndex(string? previousId)
    {
        if (_profiles.Count == 0) return 0;

        string? wanted = previousId ?? ReadActiveProfileId();
        int index = _profiles.FindIndex(p => p.Id == wanted);

        return index >= 0 ? index : 0;
    }

    private static string? ReadActiveProfileId()
    {
        try
        {
            return File.Exists(StatePath) ? File.ReadAllText(StatePath).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes through a temporary file so the dispatcher can never read a half-written name
    /// if a key is pressed at the same moment.
    /// </summary>
    private static void WriteActiveProfile(string id)
    {
        try
        {
            string temp = StatePath + ".tmp";
            File.WriteAllText(temp, id);
            File.Move(temp, StatePath, overwrite: true);
        }
        catch (IOException)
        {
            // The overlay stays usable even if the state file cannot be written; the
            // dispatcher just keeps running the previously active profile.
        }
    }

    private void RaiseAllChanged()
    {
        foreach (string property in
                 new[] { nameof(ActiveProfile), nameof(LoadError), nameof(HasError), nameof(CanCycle) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Shapes matching perfiles.json. Kept private: nothing outside this class should know
    // that the configuration happens to be JSON.
    private sealed class ConfigDto
    {
        [JsonPropertyName("perfiles")]
        public List<ProfileDto>? Perfiles { get; set; }
    }

    private sealed class ProfileDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("nombre")] public string? Nombre { get; set; }
        [JsonPropertyName("teclas")] public Dictionary<string, KeyDto>? Teclas { get; set; }
    }

    private sealed class KeyDto
    {
        [JsonPropertyName("nombre")] public string? Nombre { get; set; }
        [JsonPropertyName("icono")] public string? Icono { get; set; }
        [JsonPropertyName("accion")] public string? Accion { get; set; }
        [JsonPropertyName("opciones")] public List<string>? Opciones { get; set; }
        [JsonPropertyName("estado")] public string? Estado { get; set; }
    }
}
