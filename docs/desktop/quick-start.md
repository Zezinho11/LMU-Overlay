# Desktop prototype quick start

The desktop overlay is an external Windows application. It never
injects into LMU, installs a graphics hook, opens an LMU process handle, or
writes to game memory.

## Run

1. Build the solution with the .NET 10 SDK.
2. Start `LmuOverlay.Desktop.exe`.
3. Start Le Mans Ultimate normally.
4. The overlay aligns itself to the LMU client area and displays live shared
   memory telemetry when the map is available.

For a public release, extract the self-contained Windows x64 ZIP and run
`LmuOverlay.Desktop.exe`. A separate .NET installation is not required.

The application lives in the Windows notification area. It automatically hides
when both the LMU window and telemetry are unavailable. If telemetry is connected
but the current Windows session cannot expose the LMU window handle, the overlay
uses the primary screen bounds as a safe borderless/fullscreen fallback.

## Floating toolbar

A compact `RFX` toolbar remains interactive above the game even while the main
overlay is click-through. It provides:

- direct layout-profile switching;
- **AJUSTES** to open widget visibility, opacity, profile import/export, and
  reset controls;
- **EDITAR** to enable widget movement and resizing;
- **TRAVAR** to save the layout and return the main overlay to click-through
  race mode.

The green **TRAVADO** state confirms that accidental pointer movement cannot
change widget geometry. Drag the toolbar itself by its `RFX` handle. It remains
inside the LMU client bounds when the game resolution changes.

## Position and resize

Open the tray icon menu and select **Editar layout**. Drag the widget from its
body and resize it from the orange lower-right handle. Movement snaps to nearby
screen edges. Select **Bloquear overlay** when finished.

Locked mode applies the Windows click-through extended style to the complete
main overlay window, so the game receives pointer input normally. The independent
toolbar remains clickable so the layout can be unlocked without returning to the
notification area. Double-clicking the tray icon also toggles edit mode.
**Restaurar layout** recovers the default placement.

The normalized layout is stored at:

`%LOCALAPPDATA%\LMU Overlay\layout.json`

Normalized geometry preserves placement across common resolution changes and
is clamped to keep the widget visible.

## Current scope

- RedFox dashboard, Live Standings, Relative, Fuel & Virtual Energy strategy,
  inputs, and session/weather/flags;
- transparent, topmost external overlay;
- LMU client-area tracking and auto-hide;
- free movement, corner resizing, edge snap, reset, and persistence;
- explicit edit and locked click-through modes;
- background polling and automatic reconnect when LMU shared memory becomes
  available.

Extended live-race scenario validation and SteamVR remain future work.
