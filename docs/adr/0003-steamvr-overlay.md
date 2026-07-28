# ADR 0003: SteamVR overlay host

- Status: planned
- Date: 2026-07-28

## Decision

Implement VR as a separate presentation host using SteamVR's documented
`IVROverlay` API. Reuse normalized telemetry, widget state, layout, and texture
rendering from the desktop host.

## Rationale

This supports SteamVR without injecting into LMU and leaves room for a later
OpenXR adapter. The host boundary prevents VR concerns from contaminating core
telemetry or widgets.

## Consequences

SteamVR lifecycle, texture submission, transforms, controller interaction, and
performance require their own integration tests. VR is not part of Phase 0.
