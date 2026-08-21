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
- Enable or disable Race Control / Damage.
- Set independent opacity from 20% to 100%.
- Set independent scale from 0.5x to 2x.
- Select RedFox, black, high-contrast, or a custom theme.
- Select Brazilian Portuguese or English per profile. The same saved language
  is used by the WPF editor/fallback, native DirectComposition surfaces,
  toolbar and SteamVR surfaces/configuration editor.
- Customize the dashboard team name and the complete semantic palette using
  six-digit hexadecimal values such as `#42D3A6`: background, cards, accent,
  primary/secondary text, information, attention, critical and positive.
  Editing any color automatically selects the custom theme, and each valid
  field displays its own color preview on the input border.
- Compose the dashboard by independently showing or hiding the Sectors, Tyre
  Temp/Wear and Telemetry modules. Composition is stored in the profile and
  applied identically to WPF edit mode, native DirectComposition and SteamVR.
- Scale dashboard, timing-tower and inputs text independently from 0.80x to
  1.25x without moving or resizing the overlay surface.
- Choose 6-12 visible Live Standings cars and 2-5 track-relative cars on each
  side of the player.
- Select a 30-144 Hz UI refresh rate and a 0-50 px magnetic grid.
- Read the physical steering axis directly from the Windows game-controller
  interface. Device `-1` selects a recognized wheel automatically; the IDs and
  names detected by Windows are listed in the configuration window. A manual
  wheel range (for example 900 degrees) remains available for drivers whose
  firmware does not publish its physical range correctly. This source is
  read-only and automatically falls back to LMU telemetry when unavailable.
  Automatic range also reconciles the controller lock with LMU's active car
  lock. Set the manual range to `0` to use this synchronization.
- Enable placement across the complete Windows virtual desktop. After enabling
  **Permitir outros monitores**, unlock the overlay and drag/resize each widget
  normally onto another monitor; existing positions are rebased so enabling or
  disabling the option does not reset the layout.
- Enable the LAN dashboard and select its TCP port. The configuration window
  displays the token-protected URL to open on a phone connected to the same
  private network. The browser consumes latest-only server-sent frames at up to
  30 Hz, so a slow phone never builds a delayed telemetry queue.
- Shift lights use each vehicle's live engine maximum RPM and a per-model
  baseline. Clean full-throttle upshifts refine the target independently for
  that vehicle and gear. This is necessary because LMU's public shared-memory
  contract exposes RPM and rev limit, but not the native cockpit lamp bitfield.
- Configure reserves, manual remaining laps, maximum stint, pit loss, available
  tire sets, tire-wear limit and estimated tire-change time.
- Export a privacy-safe JSON diagnostic report.
- Apply changes without restarting LMU or the overlay.
- Restore the complete default layout.

Settings are written through the existing normalized layout store and remain
available on the next launch. Hidden widgets can always be re-enabled from the
configuration window because it is independent of the click-through overlay.
Profiles from schema 19 or older migrate to Brazilian Portuguese; unknown
language identifiers also fail safely to `pt-BR`.

Visual settings are applied both to the editable WPF preview and to the locked
DirectComposition renderers used during driving. The same profile is consumed
by SteamVR. Tire-temperature bands, manufacturer identities and flag-card
identity remain standardized; the general information/attention/critical
palette can be customized and is still protected by contrast validation.

The profile schema is intentionally renderer-neutral: palette tokens and
dashboard-module choices are data, rather than WPF-only properties. This is the
foundation for a later internal drag-and-drop dashboard builder without having
to create incompatible Desktop and VR layouts.

The built-in visual presets include Minimal, Broadcast and Endurance Pro in
addition to the race-mode layout presets. Applying a preset changes the active
profile only; it can then be refined and exported normally.

Normalized placement adapts to resolution and monitor size, including negative
virtual-desktop coordinates used by a monitor placed to the left or above the
primary display. Future increments may add per-display automatic profile
selection and snapping to nearby widgets.
