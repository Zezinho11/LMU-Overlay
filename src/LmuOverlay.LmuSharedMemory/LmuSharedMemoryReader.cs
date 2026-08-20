using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;
using LmuOverlay.Core;

namespace LmuOverlay.LmuSharedMemory;

[SupportedOSPlatform("windows")]
public sealed unsafe class LmuSharedMemoryReader :
    ILmuTelemetrySource,
    IWaitableTelemetrySource
{
    private readonly MemoryMappedFile? _map;
    private readonly MemoryMappedViewAccessor? _view;
    private readonly byte[] _buffer = new byte[LmuApiLayoutV1.ObjectSize];
    private readonly byte[] _playerBuffer = new byte[LmuApiLayoutV1.VehicleTelemetrySize];
    private readonly LmuConnectionState? _startupFailureState;
    private readonly string _startupFailureDetail = string.Empty;
    private readonly NamedEventWaitHandle? _updateEvent;
    private WaitHandle[]? _waitHandles;
    private byte* _viewPointer;
    private bool _pointerAcquired;
    private LmuTelemetrySnapshot? _lastSnapshot;
    private long _lastFullParseTimestamp;

    public LmuSharedMemoryReader()
    {
        var compatibility = GameCompatibilityProbe.Detect();
        if (compatibility.State == GameCompatibilityState.UnknownLayout)
        {
            _startupFailureState = LmuConnectionState.IncompatibleLayout;
            _startupFailureDetail = compatibility.Detail;
            return;
        }

        try
        {
            _map = MemoryMappedFile.OpenExisting(
                LmuApiLayoutV1.MapName,
                MemoryMappedFileRights.Read);
            _view = _map.CreateViewAccessor(
                0,
                LmuApiLayoutV1.ObjectSize,
                MemoryMappedFileAccess.Read);
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _viewPointer);
            _viewPointer += (nint)_view.PointerOffset;
            _pointerAcquired = true;
            _updateEvent = NamedEventWaitHandle.TryOpen(
                LmuApiLayoutV1.EventName);
        }
        catch (FileNotFoundException)
        {
            _startupFailureState = LmuConnectionState.Disconnected;
            _startupFailureDetail =
                "LMU_Data is unavailable. Start Le Mans Ultimate and enable its shared-memory plugin.";
        }
        catch (UnauthorizedAccessException)
        {
            _startupFailureState = LmuConnectionState.AccessDenied;
            _startupFailureDetail = "Windows denied read-only access to LMU_Data.";
        }
    }

    public TelemetryUpdateWaitResult WaitForUpdate(
        WaitHandle cancellation,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        if (_updateEvent is null)
        {
            return cancellation.WaitOne(timeout)
                ? TelemetryUpdateWaitResult.Cancelled
                : TelemetryUpdateWaitResult.TimedOut;
        }

        _waitHandles ??= [cancellation, _updateEvent];
        return WaitHandle.WaitAny(_waitHandles, timeout) switch
        {
            0 => TelemetryUpdateWaitResult.Cancelled,
            1 => TelemetryUpdateWaitResult.Signaled,
            _ => TelemetryUpdateWaitResult.TimedOut,
        };
    }

    public LmuProbeSnapshot ReadProbeSnapshot()
    {
        var snapshot = ReadTelemetrySnapshot();
        return new(
            snapshot.State,
            snapshot.GameVersion == 0
                ? string.Empty
                : snapshot.GameVersion.ToString(CultureInfo.InvariantCulture),
            snapshot.Session?.TrackName ?? string.Empty,
            snapshot.Session?.SessionCode ?? 0,
            snapshot.Player?.VehicleName ?? string.Empty,
            snapshot.ActiveVehicles,
            snapshot.ScoredVehicles,
            snapshot.Player is not null,
            snapshot.CapturedAt,
            snapshot.Detail);
    }

    public LmuTelemetrySnapshot ReadTelemetrySnapshot()
    {
        if (_startupFailureState is { } startupState)
        {
            return LmuTelemetrySnapshot.Unavailable(
                startupState,
                _startupFailureDetail);
        }

        if (_map is null)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "LMU shared-memory map was not opened.");
        }

        if (_view is null)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "LMU shared-memory view was not opened.");
        }

        var now = Stopwatch.GetTimestamp();
        var scoringSequence = ReadUInt32(
            LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));
        var requiresFullRead = _lastSnapshot is null ||
            scoringSequence != _lastSnapshot.ScoringSequence ||
            _lastFullParseTimestamp == 0 ||
            now - _lastFullParseTimestamp >= Stopwatch.Frequency / 5;
        if (!requiresFullRead && TryReadPlayerTelemetryBlock() is { } fastSnapshot)
        {
            _lastSnapshot = fastSnapshot;
            return fastSnapshot;
        }

        return ReadFullSnapshot();
    }

    private LmuTelemetrySnapshot ReadFullSnapshot()
    {
        _ = _view ?? throw new ObjectDisposedException(nameof(LmuSharedMemoryReader));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var scoringBefore = ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));

            var bytesRead = CopyToBuffer(0, _buffer);
            if (bytesRead != _buffer.Length)
            {
                return LmuTelemetrySnapshot.Unavailable(
                    LmuConnectionState.IncompatibleLayout,
                    $"Expected {_buffer.Length} bytes, read {bytesRead}.");
            }

            var scoringAfter = ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));

            // Scoring and telemetry are independent producer streams. Requiring
            // both counters to remain stable while copying the whole mapping made
            // fast telemetry collisions trigger a one-second reconnect. Guard the
            // scoring copy here, then overlay a separately guarded player block.
            if (scoringBefore == scoringAfter)
            {
                _lastSnapshot = LmuSnapshotParser.ParseTelemetry(_buffer);
                _lastFullParseTimestamp = Stopwatch.GetTimestamp();
                if (_lastSnapshot.State == LmuConnectionState.Connected &&
                    TryReadPlayerTelemetryBlock() is { } playerSnapshot)
                {
                    _lastSnapshot = playerSnapshot;
                }

                return _lastSnapshot;
            }
        }

        return LmuTelemetrySnapshot.Unavailable(
            LmuConnectionState.InvalidData,
            "LMU updated scoring during three consecutive read attempts.");
    }

    private LmuTelemetrySnapshot? TryReadPlayerTelemetryBlock()
    {
        if (_view is null || _lastSnapshot?.Player is not { } previousPlayer)
        {
            return null;
        }

        var activeVehicles = ReadByte(LmuApiLayoutV1.ActiveVehiclesOffset);
        var reportedIndex = ReadByte(LmuApiLayoutV1.PlayerVehicleIndexOffset);
        var hasReportedVehicle = ReadBoolean(LmuApiLayoutV1.PlayerHasVehicleOffset);
        var playerIndex = ResolvePlayerIndex(
            activeVehicles,
            reportedIndex,
            hasReportedVehicle,
            previousPlayer.VehicleId);
        if (playerIndex < 0)
        {
            return null;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var telemetryBefore = ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));
            var bytesRead = CopyToBuffer(
                LmuApiLayoutV1.VehicleTelemetryOffset(playerIndex),
                _playerBuffer);
            var telemetryAfter = ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));
            if (bytesRead == _playerBuffer.Length && telemetryBefore == telemetryAfter)
            {
                var sourceElapsedTime = LmuSnapshotParser.ReadPlayerElapsedTime(
                    _playerBuffer);
                if (double.IsFinite(sourceElapsedTime) &&
                    sourceElapsedTime == previousPlayer.ElapsedTime)
                {
                    return _lastSnapshot;
                }

                return LmuSnapshotParser.ParsePlayerTelemetryBlock(
                    _playerBuffer,
                    _lastSnapshot,
                    telemetryAfter);
            }
        }

        return null;
    }

    private int ResolvePlayerIndex(
        int activeVehicles,
        int reportedIndex,
        bool hasReportedVehicle,
        int playerVehicleId)
    {
        if (_view is null)
        {
            return -1;
        }

        if (hasReportedVehicle && reportedIndex < activeVehicles &&
            ReadInt32(
                LmuApiLayoutV1.VehicleTelemetryOffset(reportedIndex) +
                LmuApiLayoutV1.TelemetryVehicleIdOffset) == playerVehicleId)
        {
            return reportedIndex;
        }

        for (var index = 0; index < activeVehicles; index++)
        {
            if (ReadInt32(
                    LmuApiLayoutV1.VehicleTelemetryOffset(index) +
                    LmuApiLayoutV1.TelemetryVehicleIdOffset) == playerVehicleId)
            {
                return index;
            }
        }

        return -1;
    }

    private int CopyToBuffer(int offset, byte[] destination)
    {
        if (!_pointerAcquired || _viewPointer == null)
        {
            return 0;
        }

        new ReadOnlySpan<byte>(_viewPointer + offset, destination.Length)
            .CopyTo(destination);
        return destination.Length;
    }

    private byte ReadByte(int offset) => *(_viewPointer + offset);

    private bool ReadBoolean(int offset) => ReadByte(offset) != 0;

    private int ReadInt32(int offset) =>
        Unsafe.ReadUnaligned<int>(_viewPointer + offset);

    private uint ReadUInt32(int offset) =>
        Unsafe.ReadUnaligned<uint>(_viewPointer + offset);

    public void Dispose()
    {
        _updateEvent?.Dispose();
        if (_pointerAcquired && _view is not null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _viewPointer = null;
            _pointerAcquired = false;
        }
        _view?.Dispose();
        _map?.Dispose();
    }

    private sealed class NamedEventWaitHandle : WaitHandle
    {
        private const uint Synchronize = 0x00100000;

        private NamedEventWaitHandle(IntPtr handle) =>
            SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: true);

        public static NamedEventWaitHandle? TryOpen(string name)
        {
            var handle = OpenEvent(Synchronize, false, name);
            return handle == IntPtr.Zero
                ? null
                : new NamedEventWaitHandle(handle);
        }

        [DllImport("kernel32.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr OpenEvent(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            string name);
    }
}
