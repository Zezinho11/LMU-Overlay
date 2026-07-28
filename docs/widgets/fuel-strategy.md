# Fuel strategy widget

The fuel strategy widget learns consumption from completed player laps and
turns the current fuel value into an actionable race projection.

## Current fields

- Current fuel in liters.
- Current Virtual Energy as a percentage, including LMGT3 usage.
- Rolling average consumption over the last five valid completed laps.
- Rolling Virtual Energy consumption and estimated energy range in laps.
- Estimated fuel range in laps.
- Estimated laps remaining from lap limit or session time.
- Fuel required to finish, including a one-lap reserve.
- Positive or negative finish margin and a GOOD, MARGINAL, or SHORT status.
- Fuel and Virtual Energy range expressed both in laps and estimated minutes,
  using the latest valid lap time (or best lap as a fallback).
- Virtual Energy required to finish and its positive or negative finish margin.

The widget displays LEARNING until it records its first valid completed lap.
An increase in fuel is treated as a refueling event and is not added as a
consumption sample. Its baseline is reset when the track, session code, or lap
counter changes backwards.

The desktop renderer presents every value on one fixed strategy table: current
Fuel and Virtual Energy, average usage per lap, range in laps and minutes,
finish target, required amounts, margins, sample count, and reserve. It does not
use alternate pages or hide one resource behind a selector.

Virtual Energy is read from the official LMU `mVirtualEnergy` telemetry field.
The installed API and live telemetry expose it as a normalized fraction, so the
widget renders it as a percentage. Energy refills are excluded from the rolling
per-lap consumption in the same way as physical refueling.

## Interaction

Fuel Strategy is a separate box with the same edit-mode behavior as the other
desktop widgets: drag, resize, edge snap, normalized persistence, and
click-through race mode.

## Safety boundary

The tracker consumes immutable snapshots produced from the official read-only
LMU shared-memory API. It does not write to the game, inject code, install
graphics hooks, or access LMU process memory.

## Planned extensions

- Configurable reserve and rolling-average window.
- Manual race-lap override for timed sessions.
- Stint length, pit window, energy allocation, and driver-time projections.
- Caution-lap consumption profiles.
- Reuse by the future SteamVR renderer.
