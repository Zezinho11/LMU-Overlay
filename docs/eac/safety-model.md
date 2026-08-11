# EAC safety model

## Goal

Minimize interaction with both Le Mans Ultimate and Easy Anti-Cheat by treating
the official LMU shared-memory interface and its packaged read-only localhost
Live Timing endpoint as the only game-data boundaries.

## Allowed interaction

- Open the named Windows mapping `LMU_Data`.
- Request `MemoryMappedFileRights.Read` only.
- Open `LMU_Data_Event` with `SYNCHRONIZE` access only and wait for producer
  notifications; never signal, reset, or modify the event.
- Copy documented telemetry into application-owned memory.
- Read `/rest/watch/standings/history` from LMU's localhost WebUI solely to
  reproduce the game's theoretical optimal-sector calculation.
- Render in a separate top-level desktop window.
- In VR, submit application-owned textures through SteamVR's documented
  `IVROverlay` API.

## Forbidden interaction

- DLL injection or code execution inside LMU/EAC processes.
- Graphics API, window procedure, or function hooking.
- `ReadProcessMemory`, `WriteProcessMemory`, process handles, or memory scanning.
- Shared-memory writes, even if the official sample demonstrates a write lock.
- Input automation, macros, packet interception, game-file patching, or EAC
  detection/evasion.
- Loading arbitrary native extensions into the trusted runtime.

## Enforcement

- The source adapter explicitly opens the map with read rights.
- The named update event is opened with synchronization rights only.
- The adapter interface exposes snapshots, not writable mapped memory.
- CI tests parser behavior without requiring the game.
- Dependency and release review checks the forbidden-interaction list.
- Compatibility is re-evaluated for every LMU header hash and major renderer or
  SteamVR change.

## Residual risk

EAC policy and detection behavior can change. A read-only external overlay reduces
risk but does not constitute approval by Epic Games or Studio 397. Distribution
must include a safe mode and a kill switch for integrations found incompatible.
