# ADR 0003: SteamVR overlay host

- Status: accepted; foundation implemented
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

The first host implements the separate OpenVR lifecycle, `IVROverlay_028`, raw
RGBA texture submission, a head-relative transform, clean teardown and a small
telemetry preview. Full widget texture composition, profile controls,
interaction and headset performance validation remain incremental milestones.
