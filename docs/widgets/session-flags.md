# Session and flags widget

The Session/Flags widget presents the race-control context needed for quick
decisions without coupling rendering to the LMU shared-memory reader.

## Current fields

- Session kind and game phase.
- Green or yellow state, including full-course yellow.
- Remaining session time when LMU exposes an end time.
- Current and maximum lap.
- Ambient and track temperatures.

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
