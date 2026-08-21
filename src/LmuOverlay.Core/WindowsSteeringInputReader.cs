using System.Runtime.InteropServices;
using System.Diagnostics;

namespace LmuOverlay.Core;

public readonly record struct SteeringInputSample(
    bool Available,
    double NormalizedPosition,
    int DeviceId,
    string DeviceName);

/// <summary>
/// Read-only access to the physical steering axis exposed by Windows. This
/// never sends force feedback or input to the simulator.
/// </summary>
public sealed class WindowsSteeringInputReader
{
    private const uint JoyReturnX = 0x00000001;
    private const uint JoyErrorNone = 0;
    private static readonly string[] WheelKeywords =
    [
        "wheel", "volante", "fanatec", "moza", "simagic", "simucube",
        "thrustmaster", "logitech", "asetek", "cammus", "pxn",
    ];

    private readonly int _requestedDeviceId;
    private Device? _device;
    private long _nextDiscoveryAt;

    public WindowsSteeringInputReader(int requestedDeviceId = -1) =>
        _requestedDeviceId = Math.Clamp(requestedDeviceId, -1, 15);

    public SteeringInputSample Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        if (_device is null && Stopwatch.GetTimestamp() >= _nextDiscoveryAt)
        {
            _device = Discover();
            _nextDiscoveryAt = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 2;
        }

        if (_device is not { } device)
        {
            return default;
        }

        var state = new JoyInfoEx
        {
            Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
            Flags = JoyReturnX,
        };
        if (JoyGetPosEx((uint)device.Id, ref state) != JoyErrorNone)
        {
            _device = null;
            return default;
        }

        var span = Math.Max(1d, device.MaximumX - device.MinimumX);
        var normalized = ((state.X - device.MinimumX) / span * 2) - 1;
        normalized = Math.Clamp(normalized, -1, 1);
        if (Math.Abs(normalized) < 0.002)
        {
            normalized = 0;
        }
        return new(true, normalized, device.Id, device.Name);
    }

    public static IReadOnlyList<(int Id, string Name)> EnumerateDevices()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var devices = new List<(int, string)>();
        var count = Math.Min(16u, JoyGetNumDevs());
        for (var id = 0u; id < count; id++)
        {
            var caps = new JoyCaps();
            if (JoyGetDevCaps(id, ref caps, (uint)Marshal.SizeOf<JoyCaps>()) == JoyErrorNone)
            {
                devices.Add(((int)id, caps.ProductName?.Trim() ?? $"Device {id}"));
            }
        }
        return devices;
    }

    private Device? Discover()
    {
        var candidates = new List<Device>();
        foreach (var (id, name) in EnumerateDevices())
        {
            if (_requestedDeviceId >= 0 && id != _requestedDeviceId) continue;
            var caps = new JoyCaps();
            if (JoyGetDevCaps((uint)id, ref caps, (uint)Marshal.SizeOf<JoyCaps>()) != JoyErrorNone)
            {
                continue;
            }
            candidates.Add(new(id, name, caps.XMinimum, caps.XMaximum));
        }
        if (_requestedDeviceId >= 0) return candidates.FirstOrDefault();
        return candidates.FirstOrDefault(candidate => WheelKeywords.Any(keyword =>
                   candidate.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
               ?? (candidates.Count == 1 ? candidates[0] : null);
    }

    private sealed record Device(
        int Id,
        string Name,
        uint MinimumX,
        uint MaximumX);

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;
        public uint Flags;
        public uint X;
        public uint Y;
        public uint Z;
        public uint R;
        public uint U;
        public uint V;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort ManufacturerId;
        public ushort ProductId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ProductName;
        public uint XMinimum;
        public uint XMaximum;
        public uint YMinimum;
        public uint YMaximum;
        public uint ZMinimum;
        public uint ZMaximum;
        public uint ButtonCount;
        public uint PeriodMinimum;
        public uint PeriodMaximum;
        public uint RMinimum;
        public uint RMaximum;
        public uint UMinimum;
        public uint UMaximum;
        public uint VMinimum;
        public uint VMaximum;
        public uint Capabilities;
        public uint MaximumAxes;
        public uint Axes;
        public uint MaximumButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string RegistryKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string OemVxd;
    }

    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
    private static extern uint JoyGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "joyGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern uint JoyGetDevCaps(uint id, ref JoyCaps caps, uint size);

    [DllImport("winmm.dll", EntryPoint = "joyGetPosEx")]
    private static extern uint JoyGetPosEx(uint id, ref JoyInfoEx state);
}
