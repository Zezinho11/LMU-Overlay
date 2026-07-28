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
- [ ] Expand into rules streams and stateful fuel/stint strategy in Phase 4.

## Phase 2 — desktop vertical slice

- Build the external transparent click-through Windows host.
- Add an explicit edit mode in which every widget can be freely moved and
  resized, plus a locked race mode that is fully click-through.
- Add optional snapping to screen edges, corners, a grid, and nearby widgets.
- Persist position, size, scale, opacity, visibility, and lock state per layout
  profile, monitor, resolution, and DPI.
- Recover off-screen widgets after monitor, resolution, DPI, or game display
  mode changes.
- Add layout profiles, theme tokens, and the first diagnostic widget.
- Establish frame-time, allocation, and idle-CPU budgets.

## Phase 3 — product coverage

- Add the remaining non-streamer LMU Drive-style race, strategy, input,
  weather, incident, and session widgets.
- Add configuration UX, profile import/export, localization, diagnostics, and
  signed packaging/update foundations.

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
