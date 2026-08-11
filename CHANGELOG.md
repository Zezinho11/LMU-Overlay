# Changelog

## 0.5.0 - 2026-08-11

- Added each car's official tire compound and telemetry Virtual Energy
  percentage to Live Standings, with a compact compound badge, energy-condition
  bar and explicit pit state. Other-class P1 rows use that leader's own value.
- Removed the redundant session type and clock from Relative, leaving only its
  position, class, driver and track-relative gap functionality.
- Corrected dashboard `OPTIMAL` to use the read-only standings history endpoint
  used by LMU Live Timing itself. The overlay now reproduces LMU's minimum valid
  S1 + S2 + S3 calculation once per second instead of approximating sector 3
  from shared-memory best-lap accumulators.
- Fixed layout corruption when locking the overlay: edit-mode geometry is now
  captured before WPF surfaces are collapsed for native DirectComposition, so
  dashboard, Live Standings and Relative retain their exact size and position.
- Widened Live Standings and Relative to a 500x410 timing-tower design, enlarged
  their timing typography and added automatic migration for existing narrow
  profiles without allowing the two panels to overlap.
- Added the official session kind and remaining time to Live Standings.
  Qualifying now displays each driver's best lap and its gap to the fastest
  official best lap in that vehicle class; race sessions retain race intervals
  and pit-state behavior.
- Added `OPTIMAL` to the dashboard by summing the three official best sector
  values already exposed by LMU's Timing data. The value remains unavailable
  until all three valid sector references exist.
- Migrated Live Standings and Relative from locked-race WPF visuals to two
  independent DirectComposition surfaces backed by one shared Direct3D 11
  device. Their approved vertical layouts, colors, badges, transparency,
  profile bounds and edit-mode WPF representations are preserved.
- Timing panels now rebuild and present only when LMU publishes a new immutable
  standings set or their layout changes, avoiding redundant sorting, visual
  tree reconstruction and GPU presentation while retaining latest-frame
  delivery without queues.
- Added a native low-latency dashboard renderer using a click-through Win32
  window, Direct3D 11, Direct2D/DirectWrite and a premultiplied-alpha
  DirectComposition flip swap chain. WPF remains the configuration/editor and
  automatically falls back if native graphics initialization fails.
- Connected the native renderer directly to the dedicated telemetry thread.
  RPM, gear, speed, shift lights, controls, tyres and pedal history no longer
  pass through WPF's UI or composition events, and only the newest frame is
  retained when the monitor refresh rate is lower than telemetry cadence.
- Moved telemetry capture from an asynchronous ThreadPool loop to a dedicated
  above-normal-priority thread with a monotonic 4 ms schedule and latest-frame
  delivery. This prevents LMU CPU load from delaying polling continuations and
  producing the freeze-then-jump pattern in the realtime dashboard.
- Changed dashboard tire readings to the mean of the three official
  `mTireInnerLayerTemperature` channels, which is the value shown by LMU's
  MFD Tyres page rather than surface or carcass temperature.
- Prevented fast telemetry updates from invalidating a full scoring read and
  triggering a one-second reconnect; scoring and player telemetry now use
  independent consistency guards.
- Replaced per-frame WPF pedal geometry with one reusable fixed-size raster and
  suppressed unchanged high-frequency visual-property writes.
- Reworked the pedal graph around LMU elapsed time and a fixed time axis, so
  historical lines slide without being rescaled when new samples arrive.
- Removed recursive opacity/brush reconstruction from live standings and
  relative updates, cached frozen brushes, and suppressed duplicate source
  frames and unchanged high-frequency text/shift-light assignments.
- Moved full scoring parsing and strategic UI refresh to the native 5 Hz
  scoring cadence while retaining the player dashboard on the fast telemetry
  path.
- Fixed a preview.9 regression that reused stale frames when LMU event counters
  remained unchanged. Player telemetry is now read every poll, and standings,
  relative and session surfaces refresh independently at 5 Hz.
- Added an allocation-light player-only telemetry parse path: unchanged LMU
  frames are reused, full multiclass state is refreshed at 5 Hz, and fresh
  player RPM, gear, speed, inputs and elapsed time stay on the fast path.
- Stopped rebuilding pedal geometry for duplicate source frames and downsampled
  long histories to the graph's actual pixel width.
- Added an out-lap sector reference tracker. Clean S2/S3 splits seed first-lap
  deltas; a pit-contaminated S1 is rejected until a complete clean S1 exists.
- Split the desktop render pipeline into a high-frequency dashboard path and a
  5 Hz strategic-widget path, removing the former 30 Hz dashboard bottleneck.
- Increased shared-memory polling to a 4 ms target and synchronized desktop
  rendering with WPF composition at a configurable 30-144 Hz (120 Hz default).
- Increased SteamVR dashboard delivery to approximately 60 Hz while keeping
  standings, relative and strategy surfaces on a lower-cost cadence.
- Enlarged tire-condition icons and readings, and changed Relative to identify
  each car by race position while Live Standings retains position and car number.
- Added a shared RedFox visual token system with contrast validation, tabular
  timing numerals and background-only opacity.
- Added automatic, compact, normal and expanded visual-density modes plus six
  factory layout presets.
- Added a central non-animated priority alert surface for penalties, critical
  damage, flags, strategy shortfalls, tire conditions, rain and pit limiter.
- Added tire-temperature hysteresis and short ABS/TC visual retention to prevent
  indicator flicker.
- Reused Live Standings and Relative rows when their structure is stable instead
  of rebuilding their visual trees on every scoring update.
- Added filled throttle/brake traces with configurable 3-10 second history.
- Made the configuration window scroll safely on smaller displays.
- Added SteamVR compact/endurance presets and guided metric calibration.

- Made every desktop widget preserve its native aspect ratio across 720p, 1080p,
  ultrawide, 1440p and 4K game windows.
- Added adaptive readability limits, safe on-screen clamping and sharper
  fractional text scaling while resizing.
- Added explicit per-monitor DPI normalization for Windows display scales such
  as 125%, 150% and 200%.
- Stopped rebuilding the full overlay layout on every telemetry frame when the
  LMU window bounds have not changed.
- Kept SteamVR widgets resolution-independent through compositor-native metric
  sizing and per-widget texture aspect ratios.

- Corrected telemetry pacing so read time no longer accumulates on top of every
  polling interval.
- Added automated cadence, allocation, CPU, working-set and read-latency soak
  gates for CI, releases and extended local validation.
- Treat yellow flags as incident warnings without Safety Car assumptions,
  speed-reduction assumptions or discounted pit loss.
- Added the first separate SteamVR host using Valve's documented OpenVR overlay
  lifecycle, head-relative transform and a live RedFox telemetry preview.
- Expanded SteamVR to five independent telemetry surfaces with per-widget
  visibility, width, distance, offsets and opacity in a persistent VR profile.

## 0.2.0-preview.1

- Added a movable Race Control and car-status widget for penalties, pit/lap
  state, flags, damage, impacts, limiter, and DRS.
- Bound verified official LMU fields for overheating, detached parts, dent
  severity, scheduled stops, impacts, and flat/detached wheels.
- Added configurable fuel and energy reserves, remaining-lap override, stint
  limits, multi-stop calculation, and projected pit loss.
- Added per-widget scale, profile themes, configurable refresh rate, magnetic
  grid, and snapping to nearby widgets.
- Added privacy-safe diagnostics, crash logs, performance budgets, explicit
  all-rights-reserved licensing, update manifests, and optional Authenticode
  signing foundations.

## 0.1.0-preview.1

- Initial public desktop preview.
