# LMU Overlay

Extensible telemetry overlay for **Le Mans Ultimate**, designed around the game's
official shared-memory interface and a strict anti-cheat-safe boundary.

> Project status: Phase 0 / API probe. This repository does not yet provide an
> end-user overlay.

## Safety boundary

The project reads the documented `LMU_Data` shared-memory map with
`MemoryMappedFileRights.Read`. It does not inject code, hook graphics APIs, inspect
game process memory, simulate input, modify game files, or write to LMU shared
memory. See [the complete safety model](docs/eac/safety-model.md).

No software can promise future EAC allow-list status. Every release must be
revalidated after LMU, EAC, SteamVR, or overlay changes.

## Phase 0 deliverables

- Typed domain and telemetry-source boundary.
- Derived LMU shared-memory layout constants.
- Read-only, coherent snapshot reader.
- Minimal JSON compatibility probe.
- Parser tests and Windows CI.
- Header provenance and compatibility matrix without redistributing proprietary
  Studio 397 files.

## Prerequisites

- Windows 10 1809 or newer, x64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Le Mans Ultimate with its shared-memory plugin enabled.

## Run the probe

Start LMU, enter a session, then:

```powershell
dotnet run --project tools/LmuOverlay.LmuProbe
```

The probe prints a JSON snapshot and returns:

- `0`: compatible live snapshot;
- `1`: LMU unavailable, access denied, incompatible, or unstable snapshot;
- `2`: unsupported operating system.

## Build and test

```powershell
dotnet restore
dotnet build --no-restore --configuration Release
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

## Legal note

Le Mans Ultimate, Studio 397, Steam, SteamVR, and Easy Anti-Cheat are trademarks
of their respective owners. This is an independent project and is not affiliated
with or endorsed by those organizations.

The installed LMU SDK headers explicitly restrict redistribution. They are not
present in this repository. Constants here are independently maintained bindings
derived for interoperability and tied to exact local header hashes.

## License

No open-source license has been selected yet. Until one is added, all rights are
reserved. Public visibility alone does not grant reuse rights.
