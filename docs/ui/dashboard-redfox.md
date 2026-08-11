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
5. Four tire temperatures and wear percentages in a compact 2x2 block.
6. Fuel and the existing strategy projection.
7. ABS and traction-control indicators with clear inactive, armed, and active
   states.

## Existing data to preserve

- Speed, gear, RPM, position, lap, fuel, and delta.
- Front-left, front-right, rear-left, and rear-right tire temperatures and
  accumulated wear.
- Track/session context.
- Throttle, brake, and steering remain available in their independent widget.

## Additional indicators

### Shift lights

Use the normalized engine RPM fraction already available to the widget model.
The initial renderer may use progressive green, amber, red, and blue segments.
Thresholds must later become configurable per car.

### ABS and traction control

Display the four official setup levels: primary TC, TC Slip, TC Cut, and ABS.
An available non-zero level uses the enabled color, while the existing
`AbsActive` and `TractionControlActive` values use a brighter actuation color.
Never infer an activation that the official telemetry does not report.

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
- Render the content on a fixed 720x300 design surface inside a uniform scaler,
  so arbitrary box sizes do not distort spacing or typography.
- Use a nearly opaque black background to cover the vehicle's original display.
- Continue to support click-through race mode and the persistent edit mode.
- Keep the renderer-independent state shared by the desktop and SteamVR hosts.
- Mirror the approved physical-display composition with a dark rounded bezel,
  blue side LEDs, centered shift lights, boxed timing and technical fields,
  a dominant central gear, and RedFox Racing branding.
- Keep tire temperatures permanently visible in a spacious 2x2 block ordered
  front-left/front-right over rear-left/rear-right.
- Keep the orange tire-temperature label above the 2x2 readings so it never
  obscures the front tire values.
- Place a compact tire-shaped indicator beside each FL/FR/RL/RR value. Color
  each wheel independently from official carcass temperature using the initial
  generic bands: cold below 60 C, warming below 75 C, optimal below 100 C, hot
  below 115 C, and critical at or above 115 C. Keep this classification in the
  renderer-independent widgets layer so future per-car thresholds and SteamVR
  rendering can reuse it.
- Render the official per-wheel `mWear` fraction beside each temperature as
  accumulated wear from 0% to 100%. Do not infer remaining life or grip loss:
  the LMU header explicitly states that wear is not necessarily proportional
  to grip loss.
