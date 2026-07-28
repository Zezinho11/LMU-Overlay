# LMU Overlay

Extensible telemetry overlay for **Le Mans Ultimate**, designed around the game's
official shared-memory interface and a strict anti-cheat-safe boundary.

> Project status: `0.1.0-preview.1` functional desktop preview with public
> Windows packaging.
> Extended live-race validation and SteamVR remain planned.

## Safety boundary

The project reads the documented `LMU_Data` shared-memory map with
`MemoryMappedFileRights.Read`. It does not inject code, hook graphics APIs, inspect
game process memory, simulate input, modify game files, or write to LMU shared
memory. See [the complete safety model](docs/eac/safety-model.md).

No software can promise future EAC allow-list status. Every release must be
revalidated after LMU, EAC, SteamVR, or overlay changes.

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
- Reused shared-memory view and read buffer for stable high-frequency polling.
- Reproducible anonymized fixture, parser checks, and Windows CI.
- Header provenance and compatibility matrix without redistributing proprietary
  Studio 397 files.
- Movable/resizable RedFox dashboard, Live Standings, Relative, Fuel & Virtual
  Energy strategy, Race Control/damage, inputs, and session/weather/flag widgets.
- Weighted race strategy with conservative consumption, configurable reserves,
  manual race distance, stint limits, multi-stop and pit-loss projection.
- Per-profile widget scale, theme, refresh rate, magnetic grid, privacy-safe
  diagnostics export, and local crash logging.
- Reproducible self-contained Windows x64 ZIP releases with SHA-256 checksums.

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

Desktop and VR are separate presentation hosts over the same widget model. The
SteamVR route uses the documented overlay API; OpenXR remains an optional future
adapter. See the project master plan alongside this repository.

## Desktop application

The desktop host includes LMU-window alignment, auto-hide, tray controls,
independent widget movement and resizing, normalized profile persistence, profile
import/export, and a locked click-through race mode. See
[the desktop quick start](docs/desktop/quick-start.md).

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
