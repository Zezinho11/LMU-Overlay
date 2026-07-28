# Desktop prototype quick start

The Phase 2 desktop prototype is an external Windows application. It never
injects into LMU, installs a graphics hook, opens an LMU process handle, or
writes to game memory.

## Run

1. Build the solution with the .NET 10 SDK.
2. Start `LmuOverlay.Desktop.exe`.
3. Start Le Mans Ultimate normally.
4. The overlay aligns itself to the LMU client area and displays live shared
   memory telemetry when the map is available.

The application lives in the Windows notification area. It automatically hides
when both the LMU window and telemetry are unavailable. If telemetry is connected
but the current Windows session cannot expose the LMU window handle, the overlay
uses the primary screen bounds as a safe borderless/fullscreen fallback.

## Position and resize

Open the tray icon menu and select **Editar layout**. Drag the widget from its
body and resize it from the orange lower-right handle. Movement snaps to nearby
screen edges. Select **Bloquear overlay** when finished.

Locked mode applies the Windows click-through extended style to the complete
overlay window, so the game receives pointer input normally. Double-clicking the
tray icon toggles edit mode. **Restaurar layout** recovers the default placement.

The normalized layout is stored at:

`%LOCALAPPDATA%\LMU Overlay\layout.json`

Normalized geometry preserves placement across common resolution changes and
is clamped to keep the widget visible.

## Current vertical-slice scope

- one live diagnostic widget;
- transparent, topmost external overlay;
- LMU client-area tracking and auto-hide;
- free movement, corner resizing, edge snap, reset, and persistence;
- explicit edit and locked click-through modes;
- reconnect when LMU shared memory becomes available.

Visual polish and the complete widget set remain later Phase 2/3 work.
