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
- Fixed LMU's `int.MaxValue` unlimited-lap sentinel being displayed and used
  as a real lap count in timed-session fuel projections.
- Expanded Fuel Strategy with current Virtual Energy, energy consumption per
  lap, and estimated energy range for LMGT3 and other supported cars.
- Rebuilt the main dashboard as the RedFox Racing display with progressive
  shift lights, ABS/TC activation lights, large gear, lap timing, brake bias,
  engine temperatures, fuel, delta, position, and tire temperatures.
- Added the official TC, TC Slip, TC Cut, and ABS setup levels, uniform
  dashboard scaling, Bahnschrift typography, and a nearly opaque background.

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
