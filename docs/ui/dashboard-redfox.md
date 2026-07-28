# RedFox Racing dashboard direction

This document records the approved visual direction for the main dashboard.
The supplied motorsport display is a layout reference, not an asset to copy.

## Brand

- Display `REDFOX RACING` in the top header.
- Do not display Porsche or another vehicle manufacturer as the overlay brand.
- Preserve a dark, compact motorsport-instrument appearance.

## Primary hierarchy

1. A wide shift-light strip at the top.
2. Large centered gear.
3. Speed, lap, session state, and current lap time around the gear.
4. Delta and predicted/best time where the official telemetry supports them.
5. Four tire temperatures in a compact 2x2 block.
6. Fuel and the existing strategy projection.
7. ABS and traction-control indicators with clear inactive, armed, and active
   states.

## Existing data to preserve

- Speed, gear, RPM, position, lap, fuel, and delta.
- Front-left, front-right, rear-left, and rear-right tire temperatures.
- Track/session context.
- Throttle, brake, and steering remain available in their independent widget.

## Additional indicators

### Shift lights

Use the normalized engine RPM fraction already available to the widget model.
The initial renderer may use progressive green, amber, red, and blue segments.
Thresholds must later become configurable per car.

### ABS and traction control

Use the existing `AbsActive` and `TractionControlActive` values. Never infer an
activation that the official telemetry does not report.

### Additional official fields

- Engine oil temperature.
- Engine water temperature.
- Front brake bias, derived from the API's documented rear-brake fraction.
- Current, last, and best lap times.

Oil and water pressure, time of day, and predicted lap time remain
reference-only. They may be added only after their official LMU API fields and
semantics are verified. Until then, omit them instead of showing invented
values.

## Layout behavior

- Keep the dashboard as one independent movable and resizable box.
- Scale typography, indicators, and spacing with the box while preserving
  minimum readable sizes.
- Continue to support click-through race mode and the persistent edit mode.
- Keep the renderer-independent state reusable by the future SteamVR host.
