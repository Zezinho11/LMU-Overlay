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

The optional phone dashboard follows the same latest-frame rule. Serialization
is capped at 30 Hz and each browser receives only the newest JSON snapshot over
one server-sent-event stream. Direct physical steering is sampled in the fast
telemetry path without locks, writes, force feedback, or per-frame discovery.

Run the default 30-second gate:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest
```

For a pre-release endurance run, use a longer duration:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest -- --duration-seconds 3600
```

The gate checks effective capture cadence, average and maximum read time,
process-wide allocations per read, CPU use, absolute working set and working-set
growth. Runtime diagnostics in both Desktop and SteamVR also report p99 read
latency, stale-frame age, event wakeups, timeouts, duplicate snapshots,
published frames and presentation recovery attempts.

CI and release jobs run a short five-second version. A scheduled Windows job
runs the one-hour gate nightly with a 64 MB growth ceiling, while manual runs on
representative hardware remain part of release qualification.
