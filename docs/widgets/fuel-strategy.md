# Fuel strategy widget

The fuel strategy widget learns consumption from completed player laps and
turns the current fuel value into an actionable race projection.

## Current fields

- Current fuel in liters and the learned NRG-balanced maximum for the active
  car/track combination (for example `28.1 / 88.0 L`). The 88 L in this
  example is not a global constant.
- Current Virtual Energy as a percentage, including LMGT3 usage.
- Rolling robust consumption over the last twelve valid completed laps.
- Conservative fuel projection that adds a small variability buffer.
- Rolling Virtual Energy consumption and estimated energy range in laps.
- Estimated fuel range in laps.
- Estimated laps remaining from lap limit or session time.
- Fuel required to finish, including a one-lap reserve.
- Positive or negative finish margin and a GOOD, MARGINAL, or SHORT status.
- Fuel and Virtual Energy range expressed both in laps and estimated minutes,
  using the latest valid lap time (or best lap as a fallback).
- Virtual Energy required to finish and its positive or negative finish margin.
- Target consumption and required fuel-saving percentage.
- Usable laps until the next fuel/energy constraint, suggested pit lap, and
  fuel-to-add amount.
- LOW, MEDIUM, or HIGH confidence based on the number of valid samples.

The widget displays LEARNING until it records its first valid completed lap.
An increase in fuel is treated as a refueling event and is not added as a
consumption sample. Its baseline is reset when the track, session code, or lap
counter changes backwards.

The desktop renderer presents every value on one fixed strategy table: current
Fuel and Virtual Energy, projected usage per lap, range in laps and minutes,
finish target, required amounts, margins, pit target, saving target, confidence,
sample count, and reserve. It does not
use alternate pages or hide one resource behind a selector.

Virtual Energy is read from the official LMU `mVirtualEnergy` telemetry field.
The installed API and live telemetry expose it as a normalized fraction, so the
widget renders it as a percentage. Energy refills are excluded from the rolling
per-lap consumption in the same way as physical refueling.

For LMGT3 and Hypercar planning, the tracker pairs fuel and Virtual Energy
consumption from the same valid lap. `fuel used / NRG used` yields the
NRG-balanced fuel allocation at 100% for that car, circuit and current BoP. A
robust median of those paired samples is capped by the physical capacity
published by telemetry. Fuel and NRG then remain separate constraints: the
first one to reach its reserve determines stint length. Changing track,
vehicle, game generation or session resets the learning so one car's value can
never leak into another.

## Interaction

Fuel Strategy is a separate box with the same edit-mode behavior as the other
desktop widgets: drag, resize, edge snap, normalized persistence, and
click-through race mode.

## Safety boundary

The tracker consumes immutable snapshots produced from the official read-only
LMU shared-memory API. It does not write to the game, inject code, install
graphics hooks, or access LMU process memory.

Desktop and SteamVR reuse this same tracker and strategy plan; renderers do not
recalculate the numbers independently.
