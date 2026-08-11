# Performance and soak testing

Telemetry capture waits on LMU's official `LMU_Data_Event` and reads as soon as
the producer signals new data. An 8 ms cancelable timeout is retained as a
safety net for a missing or unavailable event. Sources without an event use the
original monotonic start-to-start schedule, where read duration is deducted
from the next wait instead of accumulating telemetry lag.

The reader acquires a pointer to the read-only mapped view once, copies the
player block directly into a reused buffer and validates telemetry counters
before and after every copy. A scoring-counter change triggers an immediate full
parse; otherwise rival telemetry is refreshed at a bounded rate. Presentation
receives only the latest frame and does not build a render queue.

Run the default 30-second gate:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest
```

For a pre-release endurance run, use a longer duration:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest -- --duration-seconds 3600
```

The gate checks effective capture cadence, average and maximum read time,
process-wide allocations per read, CPU use and working set. Runtime diagnostics
also report event wakeups, timeouts, duplicate snapshots and published frames.
CI and release jobs
run a short five-second version; the longer run remains part of manual release
validation on representative Windows hardware.
