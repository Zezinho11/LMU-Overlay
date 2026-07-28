# Layout system

## User contract

Every desktop widget is independently movable and resizable. The user may place
widgets anywhere inside the visible LMU presentation area, including any corner.
The application exposes two explicit interaction states:

- **Edit mode:** widgets accept pointer input and expose drag and resize handles.
- **Race mode:** widgets are locked and the entire overlay is click-through.

Switching to race mode must never leave an interactive invisible surface over
the game.

## Persisted model

Layout profiles store normalized coordinates relative to the LMU client area,
alongside minimum pixel dimensions:

- widget identifier and widget-specific configuration;
- normalized `x`, `y`, `width`, and `height`;
- user scale and opacity;
- visibility, z-order, and lock state;
- anchor and optional snap preferences;
- source monitor, resolution, and DPI metadata;
- schema version for future migrations.

Normalized geometry keeps a layout useful when the game resolution changes.
Monitor and DPI metadata allow deterministic restoration and migration.

## Placement behavior

Edit mode supports:

- free dragging;
- resizing from edges and corners;
- optional snapping to the game bounds, a configurable grid, and other widgets;
- keyboard nudging for precise placement;
- reset of one widget or the complete profile;
- safe-area clamping and an always-available recovery command.

Minimum dimensions are declared by each widget. Aspect ratio is not forced
unless the widget opts into it.

## Desktop and VR

The shared layout domain describes widget identity, size intent, visibility, and
presentation settings. Each host owns its coordinate adapter:

- desktop maps normalized geometry into the LMU client area;
- SteamVR maps the same widget composition onto overlay textures and stores VR
  transforms separately.

Desktop pixel coordinates are never reused as SteamVR world-space transforms.
This separation lets users keep independent desktop and VR profiles while the
telemetry and widget implementations remain shared.

## Acceptance checks

1. A widget can be placed and resized in every screen corner.
2. Restarting the app restores the same layout.
3. Locked mode does not capture clicks, hover, focus, or keyboard input.
4. Resolution, DPI, fullscreen mode, and monitor changes keep widgets visible.
5. Corrupt or incompatible profile data falls back to a recoverable default.
