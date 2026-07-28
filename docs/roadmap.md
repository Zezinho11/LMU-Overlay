# Roadmap

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
- [ ] Expand into rules streams and full stint strategy in Phase 4.
- [x] Add stateful fuel consumption and finish projection.

## Phase 2 — desktop vertical slice

- [x] Build the external transparent click-through Windows host.
- [x] Add an explicit edit mode in which every widget can be freely moved and
  resized, plus a locked race mode that is fully click-through.
- [x] Add snapping to screen edges.
- [x] Persist position, size, opacity, and visibility in a layout profile.
- [ ] Extend persistence to scale, lock state, monitor, resolution, and DPI
  variants.
- [x] Recover off-screen widgets after game display or resolution changes.
- [ ] Add snapping to a grid and nearby widgets.
- [ ] Add named layout profiles and theme tokens.
- [x] Add the diagnostic, dashboard, inputs, standings, relative, and
  session/flags widgets.
- [ ] Establish automated frame-time, allocation, and idle-CPU budgets.

## Phase 3 — product coverage

- Add the remaining non-streamer LMU Drive-style race, strategy, input,
  weather, incident, and session widgets.
- Add multiclass live standings: full player-class field and compact leaders
  with timing and pit state for every other class.
- [x] Add the initial multiclass standings, Relative, and Session/Flags
  coverage.
- [x] Add the initial fuel strategy with learned consumption and finish margin.
- Add configuration UX, profile import/export, localization, diagnostics, and
  signed packaging/update foundations.
- [x] Add the first configuration window with widget visibility, opacity, and
  live application.
- [ ] Add named profiles and profile import/export.

## Phase 4 — SteamVR

- Implement the separate SteamVR `IVROverlay` host.
- Reuse the same widget/layout runtime via submitted textures.
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
