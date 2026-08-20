# LMU Overlay

Extensible telemetry overlay for **Le Mans Ultimate**, designed around the game's
official shared-memory interface and a strict anti-cheat-safe boundary.

> Project status: `0.8.0` — complete semantic customization, persistent
> per-track personal timing, Strategy Engine hardening, SteamVR parity and
> runtime resilience.
> SteamVR desktop-feature parity is implemented; extended live validation on
> multiple headsets remains part of release qualification.

## Safety boundary

The project reads the documented `LMU_Data` shared-memory map with
`MemoryMappedFileRights.Read`. It does not inject code, hook graphics APIs, inspect
game process memory, simulate input, modify game files, or write to LMU shared
memory. See [the complete safety model](docs/eac/safety-model.md).

No software can promise future EAC allow-list status. Every release must be
revalidated after LMU, EAC, SteamVR, or overlay changes.

## Architecture

The codebase is divided into independent Domain, shared-memory, Configuration,
Timing, Strategy, Widgets, Application, DirectX, Desktop and SteamVR assemblies.
Desktop and VR consume the same presentation-neutral state instead of carrying
separate racing logic. See [module boundaries](docs/architecture/module-boundaries.md).
An architecture test prevents circular/platform dependencies and the return of
large composition-root or facade files.

## Current capabilities

- Typed domain and telemetry-source boundary.
- Derived LMU shared-memory layout constants.
- Read-only, coherent snapshot reader.
- Minimal JSON compatibility probe.
- Normalized session, player, standings, weather, inputs, fuel, hybrid-energy,
  gap, flag, and pit-state snapshots.
- Derived HUD metrics and an asynchronous polling service.
- Background telemetry runtime with automatic reconnect, failure isolation,
  health counters, and a render-thread-safe latest-frame buffer.
- Event-driven shared-memory capture through the official `LMU_Data_Event`,
  with an 8 ms safety timeout, coherent counter checks, a direct read-only
  mapped pointer and reused buffers.
- Native low-latency dashboard rendered by Win32, Direct3D 11, Direct2D,
  DirectWrite and DirectComposition. New telemetry snapshots travel directly
  from the dedicated event-driven capture thread to a latest-frame GPU renderer;
  WPF remains the editor, configuration UI and automatic compatibility fallback.
- Native Live Standings and Relative surfaces share a Direct3D device, preserve
  the approved wider timing-tower appearance and transparent unused area, and redraw only
  for new LMU scoring data or layout changes. Their WPF versions remain available
  automatically in edit mode and as a graphics compatibility fallback.
- Duplicate LMU frames are reused without reparsing, while new player telemetry
  follows an allocation-light path and the graph emits at most one visual point
  per horizontal pixel.
- The native driver-input surface uses a transparent steering-wheel sprite that
  rotates with the existing steering signal and uploads a pre-multiplied bitmap
  directly to Direct2D at resource creation time.
- Clean out-lap S2 and S3 times seed provisional sector-delta references. Clean
  personal sectors are persisted per track and vehicle model, allowing a saved
  S1 reference on the first flying lap. Without an honest reference the dash
  displays `NEW` instead of comparing against a pit-contaminated S1.
- Valid personal-best laps are stored locally per track, driver and vehicle
  model. Optimal is persisted under the same identity and only replaced by a
  faster valid value. Independent sector records are replaced only when the
  corresponding sector of a new officially valid best lap is faster.
  On sector completion the dash shows the new sector and its PB delta for four
  seconds, then returns to the saved PB-sector values.
- Reproducible anonymized fixture, parser checks, and Windows CI.
- Header provenance and compatibility matrix without redistributing proprietary
  Studio 397 files.
- Movable/resizable RedFox dashboard, Live Standings, Relative, Fuel & Virtual
  Energy strategy, Race Control/damage, inputs, and session/weather/flag widgets.
- Session type and remaining time on Live Standings, qualifying-specific
  best-lap gaps by class, per-car tire/Virtual Energy status, and the official LMU Timing
  history optimal lap on the dashboard.
- Weighted race strategy with conservative consumption, configurable reserves,
  manual race distance, stint limits, multi-stop and pit-loss projection.
- Per-profile widget scale, theme, refresh rate, magnetic grid, privacy-safe
  diagnostics export, and local crash logging.
- Shared Brazilian Portuguese/English localization stored per profile and
  rendered consistently by Desktop, DirectComposition and SteamVR hosts.
- Reproducible self-contained Windows x64 ZIP releases with SHA-256 checksums.
- Separate SteamVR host using the documented OpenVR `IVROverlay` API. Dashboard,
  Inputs, Live Standings, Relative, Fuel & Virtual Energy, Session/Weather and
  Race Control and the transient Priority Alert run as independent head-relative
  surfaces with the same live states, PB sectors, official optimal and strategy
  rules as desktop.
- SteamVR automatically reconnects after the runtime restarts, follows the
  active desktop visual/strategy profile, applies VR layout changes live and
  provides a graphical editor for each panel's visibility, metric size,
  distance, horizontal/vertical offset and opacity.
- Resolution-independent desktop layouts with fixed widget proportions,
  adaptive readability limits and Windows per-monitor DPI support from 720p
  through ultrawide and 4K. SteamVR keeps texture-native proportions and metric
  panel sizes, independent of the headset render resolution.
- Visual-density breakpoints, background-only opacity, tabular timing numbers,
  priority alerts, stable timing rows and factory presets for Race, Endurance,
  Qualifying, Multiclass, VR Compact and High Contrast.

## Prerequisites

- Windows 10 1809 or newer, x64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Le Mans Ultimate with its shared-memory plugin enabled.

## Run the probe

Start LMU, enter a session, then:

```powershell
dotnet run --project tools/LmuOverlay.LmuProbe
```

The probe prints the complete normalized snapshot plus derived metrics and returns:

- `0`: compatible live snapshot;
- `1`: LMU unavailable, access denied, incompatible, or unstable snapshot;
- `2`: unsupported operating system.

For repeatable regression diagnosis without keeping the game open, the
development toolchain also includes a bounded, privacy-safe telemetry recorder
and deterministic player. See the [telemetry replay guide](docs/telemetry-replay.md).

## Build and test

```powershell
dotnet restore
dotnet build --no-restore --configuration Release
dotnet run --no-build --configuration Release --project tests/LmuOverlay.Core.Tests
dotnet run --no-build --configuration Release --project tests/LmuOverlay.Domain.Tests
dotnet run --no-build --configuration Release --project tests/LmuOverlay.LmuSharedMemory.Tests
```

## Planned architecture

```text
LMU official shared memory
          |
Telemetry source adapter
          |
Normalized immutable snapshots
          |
Widget/runtime services
       /       \
Desktop host   SteamVR IVROverlay host
```

Desktop and VR are separate presentation hosts over the same normalized
telemetry runtime and widget rules. SteamVR uses the documented overlay API;
OpenXR remains an optional future adapter.

## Desktop application

The desktop host includes LMU-window alignment, auto-hide, tray controls,
independent widget movement and resizing, normalized profile persistence, profile
import/export, and a locked click-through race mode. See
[the desktop quick start](docs/desktop/quick-start.md).

The configuration UI controls visual density, background contrast, pedal
history and priority alerts. Widget opacity affects dark surfaces while text and
semantic status colors remain fully readable.

SteamVR calibration can be run before the host starts:

```powershell
LmuOverlay.SteamVr.exe --vr-preset compact --calibrate
```

Open the graphical SteamVR layout editor with:

```powershell
LmuOverlay.SteamVr.exe --configure-vr
```

Use `--vr-preset endurance` to restore the complete endurance arrangement.

Emergency isolation is available with `--safe-mode`; it disables native
rendering and the optional localhost Optimal endpoint while retaining the
read-only shared-memory WPF fallback. Individual integrations can also be
disabled in the active profile.

Generate all eight real SteamVR texture surfaces plus a deterministic simulated
HMD composition without connecting a headset:

```powershell
LmuOverlay.SteamVr.exe --capture-vr-baselines artifacts\vr-preview
```

Generate reproducible desktop screenshots for visual review with:

```powershell
.\scripts\capture-visual-baselines.ps1
```

## Download and release

Tagged releases are published as a self-contained Windows x64 ZIP, so end users
do not need to install the .NET SDK. Every archive includes a SHA-256 checksum.
See [the release guide](docs/release.md).

## Legal note

Le Mans Ultimate, Studio 397, Steam, SteamVR, and Easy Anti-Cheat are trademarks
of their respective owners. This is an independent project and is not affiliated
with or endorsed by those organizations.

The installed LMU SDK headers explicitly restrict redistribution. They are not
present in this repository. Constants here are independently maintained bindings
derived for interoperability and tied to exact local header hashes.

## Source license

The repository currently uses an all-rights-reserved license. Public visibility
alone does not grant permission to copy, modify, or redistribute the project.
