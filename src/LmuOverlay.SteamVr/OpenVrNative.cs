using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LmuOverlay.SteamVr;

public sealed class OpenVrNative : IDisposable
{
    private const string OverlayInterface = "FnTable:IVROverlay_028";
    private const int ApplicationOverlay = 2;
    private const uint HmdDeviceIndex = 0;
    private readonly nint _module;
    private readonly ShutdownDelegate _shutdown;
    private readonly FindOverlayDelegate _findOverlay;
    private readonly CreateOverlayDelegate _createOverlay;
    private readonly DestroyOverlayDelegate _destroyOverlay;
    private readonly OverlayFloatDelegate _setOverlayAlpha;
    private readonly OverlayFloatDelegate _setOverlayWidth;
    private readonly SetTrackedTransformDelegate _setTrackedTransform;
    private readonly OverlayOnlyDelegate _showOverlay;
    private readonly OverlayOnlyDelegate _hideOverlay;
    private readonly SetOverlayRawDelegate _setOverlayRaw;
    private readonly OverlayErrorNameDelegate _errorName;
    private readonly Dictionary<string, ulong> _handles = new(StringComparer.Ordinal);
    private bool _initialized;
    private bool _disposed;

    private OpenVrNative(nint module)
    {
        _module = module;
        var initialize = Export<InitDelegate>(module, "VR_InitInternal2");
        _shutdown = Export<ShutdownDelegate>(module, "VR_ShutdownInternal");
        var getInterface = Export<GetInterfaceDelegate>(module, "VR_GetGenericInterface");
        try
        {
            _ = initialize(out var error, ApplicationOverlay, nint.Zero);
            if (error != 0)
            {
                throw new InvalidOperationException($"SteamVR initialization failed ({error}).");
            }

            _initialized = true;
            var table = getInterface(OverlayInterface, out error);
            if (error != 0 || table == nint.Zero)
            {
                throw new InvalidOperationException(
                    $"SteamVR overlay interface {OverlayInterface} is unavailable ({error}).");
            }

            _findOverlay = Function<FindOverlayDelegate>(table, 0);
            _createOverlay = Function<CreateOverlayDelegate>(table, 1);
            _destroyOverlay = Function<DestroyOverlayDelegate>(table, 3);
            _errorName = Function<OverlayErrorNameDelegate>(table, 8);
            _setOverlayAlpha = Function<OverlayFloatDelegate>(table, 16);
            _setOverlayWidth = Function<OverlayFloatDelegate>(table, 22);
            _setTrackedTransform = Function<SetTrackedTransformDelegate>(table, 35);
            _showOverlay = Function<OverlayOnlyDelegate>(table, 43);
            _hideOverlay = Function<OverlayOnlyDelegate>(table, 44);
            _setOverlayRaw = Function<SetOverlayRawDelegate>(table, 62);
        }
        catch
        {
            if (_initialized)
            {
                _shutdown();
                _initialized = false;
            }

            NativeLibrary.Free(_module);
            throw;
        }
    }

    public static bool TryConnect(out OpenVrNative? openVr, out string detail)
    {
        openVr = null;
        var path = FindOpenVrLibrary();
        if (path is null)
        {
            detail = "openvr_api.dll was not found. Install or repair SteamVR.";
            return false;
        }

        try
        {
            openVr = new OpenVrNative(NativeLibrary.Load(path));
            detail = $"Connected through {path}";
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or DllNotFoundException or BadImageFormatException)
        {
            detail = exception.Message;
            openVr?.Dispose();
            openVr = null;
            return false;
        }
    }

    public void CreateOrReplaceOverlay(
        string key,
        string name,
        SteamVrOverlaySettings settings)
    {
        var safe = settings.Sanitize();
        var error = _findOverlay(key, out var handle);
        if (error != 0)
        {
            Check(_createOverlay(key, name, out handle), "CreateOverlay");
        }

        _handles[key] = handle;
        Check(_setOverlayAlpha(handle, safe.Opacity), "SetOverlayAlpha");
        Check(_setOverlayWidth(handle, safe.WidthMeters), "SetOverlayWidthInMeters");
        var transform = NativeMatrix34.From(SteamVrMatrix34.HeadLocked(safe));
        Check(
            _setTrackedTransform(handle, HmdDeviceIndex, ref transform),
            "SetOverlayTransformTrackedDeviceRelative");
    }

    public void SubmitRgba(string key, byte[] rgba, uint width, uint height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (!_handles.TryGetValue(key, out var handle))
        {
            throw new InvalidOperationException($"SteamVR overlay '{key}' is not configured.");
        }

        if (rgba.Length != checked(width * height * 4))
        {
            throw new ArgumentException("RGBA buffer size does not match its dimensions.", nameof(rgba));
        }

        var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            Check(
                _setOverlayRaw(handle, pinned.AddrOfPinnedObject(), width, height, 4),
                "SetOverlayRaw");
            Check(_showOverlay(handle), "ShowOverlay");
        }
        finally
        {
            pinned.Free();
        }
    }

    private void Check(int error, string operation)
    {
        if (error == 0)
        {
            return;
        }

        var pointer = _errorName(error);
        var name = pointer == nint.Zero
            ? error.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Marshal.PtrToStringUTF8(pointer) ?? error.ToString();
        throw new InvalidOperationException($"{operation} failed: {name}.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var handle in _handles.Values.Distinct())
        {
            _ = _hideOverlay(handle);
            _ = _destroyOverlay(handle);
        }
        _handles.Clear();

        if (_initialized)
        {
            _shutdown();
            _initialized = false;
        }

        if (_module != nint.Zero)
        {
            NativeLibrary.Free(_module);
        }
    }

    private static string? FindOpenVrLibrary()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "openvr_api.dll"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam",
                "openvr_api.dll"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Steam",
                "openvr_api.dll"),
        };
        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (steamKey?.GetValue("SteamPath") is string steamPath)
        {
            candidates.Insert(0, Path.Combine(steamPath, "openvr_api.dll"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static T Export<T>(nint module, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));

    private static T Function<T>(nint table, int index) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(
            Marshal.ReadIntPtr(table, index * IntPtr.Size));

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct NativeMatrix34
    {
        public float M0, M1, M2, M3, M4, M5, M6, M7, M8, M9, M10, M11;

        public static NativeMatrix34 From(SteamVrMatrix34 value) => new()
        {
            M0 = value.M0, M1 = value.M1, M2 = value.M2, M3 = value.M3,
            M4 = value.M4, M5 = value.M5, M6 = value.M6, M7 = value.M7,
            M8 = value.M8, M9 = value.M9, M10 = value.M10, M11 = value.M11,
        };
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint InitDelegate(out int error, int applicationType, nint startupInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ShutdownDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetInterfaceDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string version,
        out int error);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int FindOverlayDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        out ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateOverlayDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        out ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DestroyOverlayDelegate(ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint OverlayErrorNameDelegate(int error);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OverlayFloatDelegate(ulong handle, float value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetTrackedTransformDelegate(
        ulong handle,
        uint trackedDevice,
        ref NativeMatrix34 transform);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OverlayOnlyDelegate(ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetOverlayRawDelegate(
        ulong handle,
        nint buffer,
        uint width,
        uint height,
        uint bytesPerPixel);
}
