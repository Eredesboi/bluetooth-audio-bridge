using System.Runtime.InteropServices;

namespace BluetoothAudioBridge;

/// <summary>
/// Per-app output device, the same setting as Windows Settings › Sound › Volume mixer.
/// There is no public API for it: this uses the undocumented AudioPolicyConfig factory
/// (same approach as EarTrumpet / SoundSwitch). It may break in a future Windows build;
/// the volume mixer always works as a fallback.
/// </summary>
public static class AudioRouting
{
    // Two IIDs for the same vtable: Windows 11 21H2+ (build 21390) and older.
    static readonly Guid IidNew = new("ab3d4648-e242-459f-b02f-541c70306324");
    static readonly Guid IidOld = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");

    // Stub methods are never called; they only keep the vtable layout right (IInspectable + 19 others).
    [ComImport, Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFactoryNew
    {
        void GetIids(); void GetRuntimeClassName(); void GetTrustLevel(); // IInspectable
        void M1(); void M2(); void M3(); void M4(); void M5(); void M6(); void M7(); void M8(); void M9(); void M10();
        void M11(); void M12(); void M13(); void M14(); void M15(); void M16(); void M17(); void M18(); void M19();
        [PreserveSig] int Set(uint pid, int flow, int role, IntPtr deviceId);
        [PreserveSig] int Get(uint pid, int flow, int role, out IntPtr deviceId);
    }

    [ComImport, Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFactoryOld
    {
        void GetIids(); void GetRuntimeClassName(); void GetTrustLevel(); // IInspectable
        void M1(); void M2(); void M3(); void M4(); void M5(); void M6(); void M7(); void M8(); void M9(); void M10();
        void M11(); void M12(); void M13(); void M14(); void M15(); void M16(); void M17(); void M18(); void M19();
        [PreserveSig] int Set(uint pid, int flow, int role, IntPtr deviceId);
        [PreserveSig] int Get(uint pid, int flow, int role, out IntPtr deviceId);
    }

    [DllImport("combase.dll")] static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] static extern int WindowsCreateString(string s, int len, out IntPtr h);
    [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr h);
    [DllImport("combase.dll")] static extern IntPtr WindowsGetStringRawBuffer(IntPtr h, out int len);

    static Func<uint, int, IntPtr, int>? _set;
    static GetFn? _get;
    delegate int GetFn(uint pid, int flow, int role, out IntPtr id);

    static bool Init()
    {
        if (_set != null) return true;
        const string cls = "Windows.Media.Internal.AudioPolicyConfig";
        WindowsCreateString(cls, cls.Length, out var hCls);
        try
        {
            var iid = IidNew;
            if (RoGetActivationFactory(hCls, ref iid, out var p) == 0)
            {
                var f = (IFactoryNew)Marshal.GetObjectForIUnknown(p);
                _set = (pid, role, h) => f.Set(pid, 0, role, h);
                _get = (uint pid, int flow, int role, out IntPtr id) => f.Get(pid, flow, role, out id);
                return true;
            }
            iid = IidOld;
            if (RoGetActivationFactory(hCls, ref iid, out p) == 0)
            {
                var f = (IFactoryOld)Marshal.GetObjectForIUnknown(p);
                _set = (pid, role, h) => f.Set(pid, 0, role, h);
                _get = (uint pid, int flow, int role, out IntPtr id) => f.Get(pid, flow, role, out id);
                return true;
            }
            return false;
        }
        finally { WindowsDeleteString(hCls); }
    }

    // Fails (false) until this process has an audio session, i.e. until the phone stream plays: callers retry.
    // mmDeviceId = MMDevice.ID ("{0.0.0.00000000}.{guid}"), or null for the system default.
    public static bool SetOutput(string? mmDeviceId)
    {
        if (!Init()) return false;
        var s = mmDeviceId == null ? null : $@"\\?\SWD#MMDEVAPI#{mmDeviceId}#{{e6327cad-dcec-4949-ae8a-991e976a79d2}}";
        IntPtr h = IntPtr.Zero;
        if (s != null) WindowsCreateString(s, s.Length, out h);
        try
        {
            var ok = true;
            foreach (var role in new[] { 0, 1, 2 }) // console, multimedia, communications
                ok &= _set!((uint)Environment.ProcessId, role, h) == 0;
            return ok;
        }
        finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
    }

    public static string? GetOutput()
    {
        if (!Init() || _get!((uint)Environment.ProcessId, 0, 1, out var h) != 0 || h == IntPtr.Zero) return null;
        var buf = WindowsGetStringRawBuffer(h, out var len);
        var s = len == 0 ? null : Marshal.PtrToStringUni(buf, len);
        WindowsDeleteString(h);
        return s;
    }
}
