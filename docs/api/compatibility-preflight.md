# Compatibility preflight and extensibility

This layer prepares both Desktop and SteamVR for LMU updates without guessing a
new telemetry contract. It does not bypass or modify Easy Anti-Cheat.

## Startup policy

`GameCompatibilityProbe` reads Steam app `2399420`, records the installed and
target build IDs, and hashes the official headers already present under
`Support/SharedMemoryInterface`. Headers are never copied into the project or a
diagnostic report.

- The known `SharedMemoryInterface.hpp` hash enables layout v1.
- A missing installation/header is reported, while portable and fixture use can
  still start disconnected.
- An installed but unknown header fails telemetry closed as
  `IncompatibleLayout`; it is never parsed optimistically.
- Personal timing data is namespaced by Steam build and header hash, preserving
  older JSON entries while preventing a new physics generation from silently
  reusing them.

Desktop displays the result in Configuration and exports it in the privacy-safe
diagnostic JSON. SteamVR prints the preflight at startup and includes the same
section in its live or configuration-only diagnostic.

## Extensible vehicle catalog

Users and maintainers can add aliases without rebuilding the application at
`%LOCALAPPDATA%/LMU Overlay/vehicle-catalog.json`:

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "tokens": ["EXACT LMU VEHICLE NAME", "MODEL ALIAS"],
      "manufacturer": "Manufacturer",
      "code": "MFR",
      "color": "#2F80ED"
    }
  ]
}
```

External entries take priority over embedded entries. Unknown vehicle strings
use a neutral fallback and are recorded once per process in
`%LOCALAPPDATA%/LMU Overlay/diagnostics/unknown-vehicles.txt`.

## Tire temperature profiles

Optional thresholds live at
`%LOCALAPPDATA%/LMU Overlay/tire-temperature-profiles.json`:

```json
{
  "version": 1,
  "entries": [
    {
      "vehicleClass": "LMGT3",
      "vehicleModel": "*",
      "compound": "SOFT",
      "thresholds": {
        "coldToWarming": 60,
        "warmingToOptimal": 75,
        "optimalToHot": 100,
        "hotToCritical": 115
      }
    }
  ]
}
```

Blank values and `*` are wildcards. The most specific class/model/compound
match wins. Invalid or absent catalogs fall back to the established global
profile. WPF, DirectComposition and SteamVR consume the same resolver.

## Strategy isolation

Fuel/Virtual Energy learning resets on track, session, lap rollback, vehicle or
LMU game-version changes. Implausible fuel/energy samples and contaminated pit
or out laps remain rejected. No learned strategy is persisted across builds.

## VR backend selection

`VrRuntimeProbe` reports whether SteamVR is installed/running and which OpenXR
runtime is registered. Production remains the documented external SteamVR
`IVROverlay` backend.

The isolated `OpenXrExperimental` selection is intentionally capability-gated.
OpenXR core does not provide a universal cross-application overlay. The project
will not install or inject an API layer into LMU. `--vr-backend openxr` therefore
falls back to SteamVR when allowed; `--no-vr-fallback` fails safely without
affecting Desktop.

## Security and release evidence

`scripts/audit-eac-safety.ps1` blocks process-memory APIs, remote-thread/hook
APIs, packet-capture packages and writable shared-memory access. CI and Release
publish its JSON evidence. Releases also retain SHA-256 checksums, SPDX SBOM,
GitHub provenance/SBOM attestations and optional Authenticode signing.

## Offline qualification

Automated tests cover unknown/pending builds, append-only and truncated layouts,
generation-isolated timing stores, external vehicle/tire catalogs, strategy
generation resets, safe OpenXR fallback, Desktop/SteamVR presentation parity,
visual baselines and telemetry soak behavior. Live LMU and physical-headset
qualification remain separate release gates.
