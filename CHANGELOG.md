# Changelog

## Unreleased

- Added the first functional desktop overlay vertical slice.
- Added LMU window alignment, transparent click-through race mode, and tray
  controls.
- Added freely movable and resizable diagnostic telemetry widget with normalized
  persisted placement and off-screen clamping.
- Added desktop layout tests and user documentation.
- Added a primary-screen fallback when telemetry is connected but the LMU
  window handle is unavailable.
- Validated the first live desktop render over an active Spa session.
- Added independent, movable, resizable, and persistent Relative and
  Session/Flags widgets.
- Added renderer-independent session, weather, race-phase, and flag state.
- Added a stateful fuel-strategy widget with rolling per-lap consumption,
  range, finish projection, reserve, margin, and refueling detection.

All notable changes will be documented here.

## Unreleased

### Added

- Initial .NET 10 solution structure.
- Read-only LMU shared-memory adapter and compatibility probe.
- Parser and domain tests.
- EAC safety model, API compatibility matrix, ADRs, and Windows CI.
- Normalized session, weather, player telemetry, and standings snapshots.
- Inputs, fuel, hybrid-energy, gaps, flags, pit state, and derived HUD metrics.
- Asynchronous telemetry polling service.
- Anonymized Spa single-vehicle fixture and expanded offset/parser checks.
