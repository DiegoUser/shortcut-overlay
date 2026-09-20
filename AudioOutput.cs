using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Atajos;

/// <summary>One active playback endpoint, as Windows reports it.</summary>
/// <param name="Name">The friendly name shown in the sound settings.</param>
/// <param name="IsDefault">Whether this is the endpoint Windows is currently playing through.</param>
public sealed record AudioEndpoint(string Name, bool IsDefault);

/// <summary>
/// Reads which playback device Windows is using right now.
///
/// Read-only on purpose. Switching stays in actions\toggle-audio-output.ps1, which is what the
/// key actually runs; this side only has to describe the current state. It lives in the app
/// rather than in a script because the overlay opens instantly and a subtitle that arrives a
/// third of a second later — the cost of starting PowerShell — would move the layout under the
/// pointer after the panel is already on screen.
///
/// Only documented APIs are used here: MMDeviceEnumerator is the supported way to *read* the
/// default endpoint. The undocumented interface (IPolicyConfig) is only needed to *set* it, and
/// it stays in the script.
/// </summary>
public static class AudioOutput
{
    private const int Render = 0;         // eRender: playback, not capture
    private const int StateActive = 1;    // DEVICE_STATE_ACTIVE
    private const int Multimedia = 1;     // eMultimedia role
    private const int StgmRead = 0;

    private static readonly Guid EnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static PropertyKey FriendlyNameKey = new()
    {
        FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        PropertyId = 14,
    };

    /// <summary>
    /// Every active playback device, with the current default flagged.
    /// </summary>
    public static List<AudioEndpoint> List()
    {
        var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(EnumeratorClsid)!)!;

        // Not fatal when it fails: a machine with no playback device at all still has to
        // produce a list rather than an exception.
        string? defaultId = null;
        if (enumerator.GetDefaultAudioEndpoint(Render, Multimedia, out IMMDevice current) == 0)
        {
            current.GetId(out defaultId);
        }

        Marshal.ThrowExceptionForHR(
            enumerator.EnumAudioEndpoints(Render, StateActive, out IMMDeviceCollection collection));

        Marshal.ThrowExceptionForHR(collection.GetCount(out int count));

        List<AudioEndpoint> endpoints = [];

        for (int i = 0; i < count; i++)
        {
            Marshal.ThrowExceptionForHR(collection.Item(i, out IMMDevice device));
            Marshal.ThrowExceptionForHR(device.GetId(out string id));
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StgmRead, out IPropertyStore store));
            Marshal.ThrowExceptionForHR(store.GetValue(ref FriendlyNameKey, out PropVariant value));

            try
            {
                endpoints.Add(new AudioEndpoint(value.AsString() ?? id, id == defaultId));
            }
            finally
            {
                // The string was allocated by the callee with CoTaskMemAlloc, so the GC knows
                // nothing about it. This runs on every overlay open; leaking it would add up.
                PropVariantClear(ref value);
            }
        }

        return endpoints;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    /// <summary>
    /// Only the two fields a VT_LPWSTR needs are named. The explicit size matters: PropVariantClear
    /// writes over the whole 24-byte structure, so a buffer shaped to just these fields would let
    /// it run past the end.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public short VarType;
        [FieldOffset(8)] public IntPtr Pointer;

        public readonly string? AsString() =>
            Pointer == IntPtr.Zero ? null : Marshal.PtrToStringUni(Pointer);
    }
}
