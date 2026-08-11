# ADR 0003: SteamVR overlay host

- Status: accepted; desktop parity implemented
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

The host implements the separate OpenVR lifecycle, `IVROverlay_028`, raw RGBA
texture submission, head-relative transforms, clean teardown and reconnect.
All desktop widget states have VR compositions, including high-frequency Inputs
and Dashboard, persistent timing references and the full strategy model. Metric
placement is stored independently from desktop pixels and can be edited through
a Windows configuration surface while the host is running. Controller-driven
placement and physical-headset release qualification remain later milestones.
