# Performance and soak testing

Telemetry polling uses a monotonic start-to-start schedule. The duration of a
successful shared-memory read is deducted from the next wait instead of being
added to the polling interval, preventing accumulated telemetry lag.

Run the default 30-second gate:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest
```

For a pre-release endurance run, use a longer duration:

```powershell
dotnet run --configuration Release --project tools/LmuOverlay.SoakTest -- --duration-seconds 3600
```

The gate checks effective polling cadence, average and maximum read time,
process-wide allocations per read, CPU use and working set. CI and release jobs
run a short five-second version; the longer run remains part of manual release
validation on representative Windows hardware.
