# LMU API compatibility matrix

Bindings are derived from headers installed with the user's legitimate LMU copy.
The proprietary headers themselves must never be committed or redistributed.

## Baseline captured 2026-07-28

| Artifact | SHA-256 |
|---|---|
| `SharedMemoryInterface.hpp` | `194ff1ab39030bc811540931c8b9817258727252c9a4b35fa4734bbaa16d4ddc` |
| `InternalsPlugin.hpp` | `9b6ee8cf610fa5049b18df580a9a9bc9ebb91346fc466584d576a6442abcf68f` |

Installed source location:
`Le Mans Ultimate/Support/SharedMemoryInterface/`.

## Derived layout v1

Packing is 4 bytes. The shared object is 324,820 bytes and supports 104 vehicles.

| Field | Absolute offset | Size/stride |
|---|---:|---:|
| Update events | 0 | 16 × 4 |
| Game version (`long`, numeric) | 64 | 4 |
| Scoring block | 1,632 | 126,832 |
| Track name | 1,632 | 64 |
| Session code | 1,696 | 4 |
| Scored vehicle count | 1,736 | 4 |
| Average path wetness | 1,964 | 8 |
| Track grip level | 1,981 | 1 |
| Cloud coverage | 1,982 | 1 |
| Vehicle scoring array | 2,192 | 584 per vehicle |
| Telemetry block | 128,464 | 196,356 |
| Active vehicle count | 128,464 | 1 |
| Player vehicle index | 128,465 | 1 |
| Player-has-vehicle flag | 128,466 | 1 |
| Vehicle telemetry array | 128,468 | 1,888 per vehicle |
| Vehicle name within telemetry item | item + 32 | 64 |

## Phase 1 binding coverage

| Group | Bound data |
|---|---|
| Session | elapsed/end time, maximum laps, lap length, phase, realtime state |
| Weather/RealRoad | cloud, rain, ambient/track temperatures, wind, min/max/average path wetness, track grip level |
| Player | lap, speed, gear/RPM, inputs, fuel, battery/SoC, regen, virtual energy, per-wheel tire temperature/wear |
| Driver aids | limiter, ABS, TC, invalid-lap state |
| Standings | position, laps, sector, lap distance/times, gaps, class |
| Race state | pit state, flags, yellow, garage, DRS, penalties |

The parser exposes only normalized immutable records. Raw offsets remain internal
to the LMU adapter.

## Compatibility policy

1. Record installed header hashes before updating bindings.
2. Generate/validate native `sizeof` and `offsetof` values in a local-only tool.
3. Update this matrix and golden parser fixtures.
4. Run the live probe against LMU with plugins enabled.
5. Mark a release compatible only after stable snapshots are observed in menu,
   practice, qualifying, race, replay, and session transitions.

Unknown layouts fail closed as `IncompatibleLayout` or `InvalidData`.

See [live validation evidence](live-validation.md).
