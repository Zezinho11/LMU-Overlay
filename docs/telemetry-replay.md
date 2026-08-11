# Telemetry recorder and replay

The replay pipeline captures normalized immutable LMU Overlay snapshots. It does
not open a writable mapping, inject code, hook DirectX, automate the game, or
call an LMU endpoint. Live capture remains on the existing official read-only
shared-memory adapter.

## Privacy and realtime behavior

Every file produced by the tool is anonymized before it reaches disk:

- driver and player names become stable aliases such as `Driver 01`;
- live vehicle IDs and vehicle display names become recording-local values;
- runtime detail strings, which can contain local paths, are removed;
- capture timestamps use a Unix-epoch-relative timeline rather than the
  driver's wall-clock time;
- track, vehicle model, class, timing, controls, weather and telemetry remain
  available because they are required to reproduce bugs.

The capture callback only attempts to enqueue the newest immutable snapshot in
a bounded buffer. Disk I/O runs on a separate task. A saturated buffer drops
frames and reports the count instead of delaying telemetry capture or the
dashboard.

## Record a live sequence

Run LMU, enter the session to reproduce, and then run:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.TelemetryReplay -- `
  record --output .\artifacts\spa-timing.lmu-replay --duration-seconds 120
```

The file is NDJSON schema version 1: one header followed by monotonic frame
records. It is intentionally streamable so a long endurance capture does not
need to remain in memory.

Before sharing a fixture, confirm `Dropped frames: 0` and inspect it:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.TelemetryReplay -- `
  inspect --input .\artifacts\spa-timing.lmu-replay
```

## Replay

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.TelemetryReplay -- `
  play --input .\artifacts\spa-timing.lmu-replay --speed 1
```

`--speed` changes wall-clock playback only. Frame order, telemetry/scoring
sequences and the recorded normalized values do not change. The reusable
`ReplayTelemetrySource` implements the same read/wait contracts as the live
source, so regression tools can run through the normal `TelemetryRuntime`.

## Fail-closed validation

The reader rejects missing or duplicate headers, unsupported schema versions,
unknown entry types, non-monotonic sequences/timestamps, oversized entries,
captures longer than seven days and excessive frame counts. Files must be
treated as test input; they are never loaded automatically by the production
overlay.
