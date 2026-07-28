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
- Added the first widget configuration window with independent visibility and
  opacity controls, live apply, persistence, and layout restore.
- Added named layout profiles with live switching, create, duplicate, rename,
  guarded deletion, backward-compatible migration, and portable JSON
  import/export.
- Rebuilt Live Standings as a scalable ten-car timing tower with a pinned P1,
  moving player window, abbreviated driver names, manufacturer-colored car
  silhouettes, explicit race numbers, last laps, intervals, and pit status.
- Reworked the RedFox dashboard around the approved physical display reference
  with a dark bezel, side LEDs, boxed telemetry, a larger central gear, and a
  spacious 2x2 tire-temperature block.
- Narrowed Live Standings into a rail-free vertical tower and replaced car
  outlines with compact three-letter manufacturer badges.
- Removed the redundant Live Standings branding header and connected each
  manufacturer badge to the official per-vehicle telemetry model and brand
  color, with a neutral unknown fallback.
- Lifted the dashboard tire-temperature label above the 2x2 readings to prevent
  overlap with the front tire values.
- Made Live Standings fill its available vertical space with up to fourteen
  cars, assigning all spare rows to the player's current race class while
  retaining compact leaders from the other classes.
- Rebuilt Fuel Strategy as a single-page race strategy table inspired by LMU's
  in-car menu, showing Fuel and Virtual Energy together with current levels,
  per-lap usage, range in laps and minutes, finish need, margins, samples, and
  reserve.
- Rebuilt Relative as a 260x410 timing tower matching Live Standings, with a
  centered nine-car window, explicit race numbers, compact driver names,
  colored GT3/Hypercar/LMP2 badges, player highlight, lap gaps, and pit state.
- Added four tire-shaped dashboard indicators beside FL/FR/RL/RR temperatures,
  with independent cold, warming, optimal, hot, and critical colors driven by
  official carcass telemetry.
- Rebuilt Session/Flags as a matching three-card panel with official
  Green/Light/Medium/Heavy/Saturated RealRoad grip, intensity-aware weather
  icons, rain and path wetness, separate temperatures, and full-color
  green/yellow/red flag cards.

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
