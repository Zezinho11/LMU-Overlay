# Roadmap

The prioritized hardening work and its research basis are recorded in the
[2026-08-05 product and technical audit](audits/2026-08-05-product-technical-audit.md).

## Phase 0 — foundation (current)

- [x] Verify official LMU shared-memory headers and provenance.
- [x] Establish the no-injection, read-only EAC safety boundary.
- [x] Create the solution, contracts, bindings, parser, probe, tests, and CI.
- [x] Validate the probe against a live LMU session.

## Phase 1 — telemetry core (current)

- [x] Add typed telemetry, scoring, weather, energy, flag, and pit-state bindings.
- [x] Add immutable normalized snapshots and bounded coherent reads.
- [x] Add update scheduling and derived HUD metrics.
- [x] Add an anonymized replay fixture and compatibility evidence.
- [x] Add Race Control/damage state from verified official fields.
- [x] Add configurable stint limits, race-distance override, multi-stop and
  pit-loss projection.
- [x] Add stateful fuel consumption and finish projection.
- [x] Capture clean out-lap S2/S3 references and persist clean personal sector
  bests per track and vehicle model for first-flying-lap deltas.
- [x] Persist complete personal-best laps per track, driver and vehicle model,
  keeping all displayed PB sectors tied to the same record lap.
- [x] Add a bounded, versioned and privacy-safe normalized telemetry recorder,
  deterministic replay source and sequential regression fixtures.

## Phase 2 — desktop vertical slice

- [x] Build the external transparent click-through Windows host.
- [x] Add an explicit edit mode in which every widget can be freely moved and
  resized, plus a locked race mode that is fully click-through.
- [x] Add snapping to screen edges.
- [x] Persist position, size, opacity, and visibility in a layout profile.
- [x] Persist scale and lock state; normalized layouts adapt across monitor,
  resolution, and DPI changes.
- [x] Recover off-screen widgets after game display or resolution changes.
- [x] Add configurable snapping to a grid.
- [x] Add snapping to nearby widgets.
- [x] Add named layout profiles with create, duplicate, rename, delete, switch,
  and portable import/export.
- [x] Add RedFox, black, and high-contrast theme tokens.
- [x] Add the diagnostic, dashboard, inputs, standings, relative, and
  session/flags widgets.
- [x] Expose and test read-time and memory budgets in privacy-safe diagnostics.
- [x] Add stable start-to-start telemetry pacing plus automated read-time,
  cadence, allocation, working-set, and CPU soak-test gates.
- [x] Move telemetry acquisition off the UI thread, reuse shared-memory buffers,
  isolate read failures, and expose runtime health counters.
- [x] Replace fixed high-frequency polling with `LMU_Data_Event` wakeups,
  counter-driven scoring refresh, direct read-only mapped copies and an 8 ms
  recovery timeout.
- [x] Move the latency-critical dashboard from WPF to a native Win32,
  Direct3D 11, Direct2D/DirectWrite and DirectComposition renderer while
  retaining WPF as the editor and compatibility fallback.
- [x] Move driver inputs to a latest-frame native DirectComposition surface and
  add a GPU-backed steering-wheel sprite without changing input semantics.

## Phase 3 — product coverage

- [x] Add Race Control/damage coverage from currently verified official fields.
- [ ] Add further non-streamer widgets only after official fields are verified
  and live-tested.
- Add multiclass live standings: full player-class field and compact leaders
  with timing and pit state for every other class.
- [x] Add the initial multiclass standings, Relative, and Session/Flags
  coverage.
- [x] Add the ten-car dynamic timing tower with P1 pinning, player window,
  abbreviated drivers, car silhouettes, last laps, and intervals.
- [x] Add the initial fuel strategy with learned consumption and finish margin.
- [x] Add configuration UX, profile import/export, diagnostics, and signed
  packaging/update foundations.
- [ ] Add localization.
- [x] Add the first configuration window with widget visibility, opacity, and
  live application.
- [x] Add named profiles and profile import/export.
- [x] Add an always-interactive floating toolbar for direct profile switching,
  configuration, edit mode, and explicit layout locking.
- [x] Add advanced weighted fuel/energy projection, fuel-saving target, pit-lap
  recommendation, fuel-to-add amount, reserve, and confidence.
- [x] Add self-contained Windows packaging, checksums, CI artifacts, and
  tag-driven public GitHub Releases.
- [x] Add machine-readable manifests, an explicit license, privacy-safe
  diagnostics, crash logs, and optional Authenticode signing.

## Phase 4 — SteamVR

- [x] Implement a separate SteamVR `IVROverlay` host with runtime discovery,
  clean lifecycle, a head-relative transform and raw texture submission.
- [ ] Reuse every widget/layout surface through submitted textures. The initial
  multi-widget preview now reuses the shared dashboard, standings, relative,
  fuel-strategy and session/weather states with independent persistent VR
  placement; smaller widget parity remains.
- Add world/dashboard transforms, opacity/scale controls, interaction, and VR
  performance tests.

## Phase 5 — hardening and growth

- Expand compatibility testing, telemetry recordings, accessibility, release
  signing, security review, and EAC/LMU regression checks.
- Evaluate an OpenXR adapter without coupling it to the core.

## Definition of done for every phase

- Safety boundary remains intact.
- Compatibility evidence is recorded.
- Release build has no warnings.
- Automated checks and relevant live tests pass.
- Documentation and changelog match the implementation.
