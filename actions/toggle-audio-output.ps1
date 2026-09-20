# Switches the default audio output between two devices.
#
# Replaces an earlier version that synthesized Win+Ctrl+V to open the sound flyout so the
# device could be picked by hand. That could never work from here: actions always run while
# Ctrl+Shift are physically held, so the injected chord arrives as Win+Ctrl+Shift+V and
# matches nothing. No action in this project may synthesize keystrokes.
#
# Switching directly is better anyway. It removes the manual step entirely.
#
# Windows exposes no supported API to set the default endpoint. IPolicyConfig is undocumented
# but has been stable since Windows 7 and is what every tool in this space uses. Nothing needs
# to be installed.

# The two devices come from the key's "opciones" in perfiles.json, so they are written down
# once and the overlay can show the same pair it is about to switch between. Matched against
# the device name, case-insensitive; partial text is enough.
#
# Not marked Mandatory on purpose: this runs with stdin redirected and no console, so a
# mandatory parameter would leave PowerShell waiting on a prompt nobody can see. Better to
# fail immediately with a line that says what is missing.
param(
    [string]$DeviceA,
    [string]$DeviceB
)

if (-not $DeviceA -or -not $DeviceB) {
    throw "This action needs two device names in 'opciones' in perfiles.json, for example [""Realtek"", ""HyperX""]."
}

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int Item(int index, out IMMDevice device);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr iface);
    [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int state);
}

[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetAt(int index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}

[StructLayout(LayoutKind.Sequential)]
struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;
}

[StructLayout(LayoutKind.Explicit)]
struct PropVariant
{
    [FieldOffset(0)] public short VarType;
    [FieldOffset(8)] public IntPtr Pointer;

    public string AsString()
    {
        return Pointer == IntPtr.Zero ? null : Marshal.PtrToStringUni(Pointer);
    }
}

// Undocumented. Only SetDefaultEndpoint is used; the earlier entries exist so that the one
// that matters lands on the right vtable slot. They are never called.
[Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDeviceFormat();
    [PreserveSig] int ResetDeviceFormat();
    [PreserveSig] int SetDeviceFormat();
    [PreserveSig] int GetProcessingPeriod();
    [PreserveSig] int SetProcessingPeriod();
    [PreserveSig] int GetShareMode();
    [PreserveSig] int SetShareMode();
    [PreserveSig] int GetPropertyValue();
    [PreserveSig] int SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
    [PreserveSig] int SetEndpointVisibility();
}

public class AudioDevice
{
    public string Id;
    public string Name;
    public bool IsDefault;
    public override string ToString() { return Name; }
}

public static class AudioOutput
{
    private const int Render = 0;
    private const int StateActive = 1;
    private const int StgmRead = 0;

    // eConsole, eMultimedia, eCommunications. All three are set so the switch is complete:
    // leaving one behind means some apps keep using the previous device.
    private static readonly int[] Roles = { 0, 1, 2 };

    private static readonly Guid EnumeratorClsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid PolicyConfigClsid = new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    private static PropertyKey FriendlyNameKey = new PropertyKey
    {
        FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        PropertyId = 14
    };

    public static List<AudioDevice> List()
    {
        var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(EnumeratorClsid));

        string defaultId = null;
        IMMDevice current;
        if (enumerator.GetDefaultAudioEndpoint(Render, 1, out current) == 0)
        {
            current.GetId(out defaultId);
        }

        IMMDeviceCollection collection;
        Marshal.ThrowExceptionForHR(
            enumerator.EnumAudioEndpoints(Render, StateActive, out collection));

        int count;
        Marshal.ThrowExceptionForHR(collection.GetCount(out count));

        var devices = new List<AudioDevice>();

        for (int i = 0; i < count; i++)
        {
            IMMDevice device;
            Marshal.ThrowExceptionForHR(collection.Item(i, out device));

            string id;
            Marshal.ThrowExceptionForHR(device.GetId(out id));

            IPropertyStore store;
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StgmRead, out store));

            PropVariant value;
            Marshal.ThrowExceptionForHR(store.GetValue(ref FriendlyNameKey, out value));

            devices.Add(new AudioDevice
            {
                Id = id,
                Name = value.AsString(),
                IsDefault = (id == defaultId)
            });
        }

        return devices;
    }

    public static void SetDefault(string deviceId)
    {
        var config = (IPolicyConfig)Activator.CreateInstance(
            Type.GetTypeFromCLSID(PolicyConfigClsid));

        foreach (int role in Roles)
        {
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, role));
        }
    }
}
'@

$devices = [AudioOutput]::List()
$current = $devices | Where-Object { $_.IsDefault } | Select-Object -First 1

function Find-Device($fragment) {
    $devices | Where-Object { $_.Name -like "*$fragment*" } | Select-Object -First 1
}

$a = Find-Device $DeviceA
$b = Find-Device $DeviceB

if (-not $a -and -not $b) {
    throw "Neither '$DeviceA' nor '$DeviceB' is available. Active outputs: " + (($devices | ForEach-Object { $_.Name }) -join '; ')
}

# Anything other than A goes to A, so a third device that sneaks in as default still lands
# somewhere predictable instead of doing nothing. KeyStatus.cs mirrors this rule to draw the
# arrow in the panel; changing the direction here means changing it there too.
if ($current -and $current.Name -like "*$DeviceA*") {
    $target = $b
    $missing = $DeviceB
}
else {
    $target = $a
    $missing = $DeviceA
}

if (-not $target) {
    throw "'$missing' is not available. Active outputs: " + (($devices | ForEach-Object { $_.Name }) -join '; ')
}

[AudioOutput]::SetDefault($target.Id)
