# Session and flags widget

The Session/Flags widget presents the race-control context needed for quick
decisions without coupling rendering to the LMU shared-memory reader.

## Current fields

- Session kind and game phase.
- Green or yellow state, including full-course yellow.
- Red state when LMU reports the stopped race phase.
- Remaining session time when LMU exposes an end time.
- Current and maximum lap.
- Ambient and track temperatures.
- Official RealRoad grip level: Green, Light, Medium, Heavy, or Saturated.
- Clear, partly-cloudy, cloudy, overcast, light-rain, rain, and heavy-rain
  conditions derived from official cloud darkness and rain intensity.
- Official average racing-path wetness.

## Visual hierarchy

The desktop renderer uses one fixed panel matching the other RedFox widgets:

- a color-coded RealRoad grip card;
- a weather card with a condition icon, rain intensity, and path wetness;
- a flag card whose complete background changes between green, yellow, red,
  and neutral states;
- separate ambient-temperature, track-temperature, and path-wetness readings.

Weather icons scale rain from light to normal and heavy instead of using one
generic rainy symbol.

## Interaction

The widget is an independent desktop box. In edit mode it can be dragged,
resized, snapped to screen edges, and saved in the normalized layout profile.
In race mode it is click-through with the rest of the overlay.

## Data and safety boundary

The renderer consumes an immutable `SessionFlagsWidgetState`. Its factory
derives that state only from the read-only normalized telemetry snapshot.
It does not inject code, install hooks, read game process memory, or send
commands to LMU.

## Planned extensions

- Local, sector, blue, white, black, and checkered flag semantics when the
  official source exposes enough context.
- Safety-car and race-control messages.
- Session-specific visibility rules and alert sounds.
- Renderer reuse in the future SteamVR host.
