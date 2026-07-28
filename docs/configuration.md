# Widget configuration

The desktop tray menu exposes **Configurar widgets**, which opens the layout
and profile manager.

## Layout profiles

- Create an empty profile from the RedFox default layout.
- Duplicate the active profile, including every position, size, visibility,
  and opacity value.
- Rename profiles and switch between them without restarting the overlay.
- Delete profiles with confirmation; the final remaining profile is protected.
- Export one portable `*.lmu-layout.json` file.
- Import exported profiles, with automatic names such as `Profile (2)` when a
  profile with the same name already exists.

The layout previously stored in `layout.json` is migrated automatically into
the **Padrão** profile. Profile files carry explicit format and layout schema
versions so incompatible data is rejected instead of partially applied.

## Current controls

- Enable or disable Dashboard RedFox.
- Enable or disable Inputs.
- Enable or disable Live Standings.
- Enable or disable Relative.
- Enable or disable Session / Flags.
- Enable or disable Fuel / Virtual Energy.
- Set independent opacity from 20% to 100%.
- Apply changes without restarting LMU or the overlay.
- Restore the complete default layout.

Settings are written through the existing normalized layout store and remain
available on the next launch. Hidden widgets can always be re-enabled from the
configuration window because it is independent of the click-through overlay.

## Next configuration increment

- Resolution and monitor variants.
- Per-widget reset, scale, theme, and update-rate controls.
