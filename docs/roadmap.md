# Roadmap

## Phase 0 — foundation (current)

- [x] Verify official LMU shared-memory headers and provenance.
- [x] Establish the no-injection, read-only EAC safety boundary.
- [x] Create the solution, contracts, bindings, parser, probe, tests, and CI.
- [ ] Validate the probe in live menu/session transitions.

## Phase 1 — telemetry core

- Complete typed bindings for telemetry, scoring, rules, weather, strategy, and
  pit state.
- Add immutable normalized snapshots, update scheduling, derived metrics, replay
  fixtures, structured logging, and a compatibility gate.

## Phase 2 — desktop vertical slice

- Build the external transparent click-through Windows host.
- Add layout profiles, scaling, theme tokens, and the first widgets: relative,
  standings, fuel/energy, tires/brakes, track map, flags, and stint timing.
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
