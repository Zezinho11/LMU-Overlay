# Module boundaries

The source tree is intentionally split by responsibility. Presentation hosts do
not own telemetry parsing, timing persistence, strategy math or profile storage.

## Dependency flow

```text
LMU_Data (read-only)
  -> LmuOverlay.LmuSharedMemory
  -> LmuOverlay.Domain
  -> LmuOverlay.Application
       -> LmuOverlay.Widgets
            -> LmuOverlay.Timing
            -> LmuOverlay.Strategy
       -> LmuOverlay.Configuration
  -> Desktop / DirectX / SteamVR presentation hosts
```

Dependencies point toward stable data and policy modules. Desktop and SteamVR
are adapters at the edge; neither is a source of racing rules.

## Assemblies

| Assembly | Responsibility | Must not contain |
| --- | --- | --- |
| `LmuOverlay.Domain` | Immutable normalized telemetry contracts | Rendering, persistence, LMU byte offsets |
| `LmuOverlay.LmuSharedMemory` | Read-only map lifecycle, coherence and ABI parsing | UI or strategy decisions |
| `LmuOverlay.Configuration` | Profiles, placement, localization and sanitization | Telemetry polling or rendering |
| `LmuOverlay.Timing` | PB/optimal/sector validation and local persistence | Desktop or VR controls |
| `LmuOverlay.Strategy` | Learning windows, race horizon and push/save planners | Widget drawing |
| `LmuOverlay.Widgets` | Presentation-neutral widget states and factories | WPF, Direct2D or OpenVR APIs |
| `LmuOverlay.Application` | Shared composition used by every presentation host | Platform-specific drawing |
| `LmuOverlay.DirectX` | Low-latency desktop dashboard/input/timing adapters | LMU parsing and strategy rules |
| `LmuOverlay.Desktop` | WPF editor, fallback surfaces and host lifecycle | Duplicated telemetry/business rules |
| `LmuOverlay.SteamVr` | OpenVR overlay lifecycle and VR raster adapters | Desktop-only state models |

## Runtime rules

- Shared memory is opened read-only and copied into coherent snapshots.
- The capture loop publishes the latest immutable snapshot; renderers never
  poll or parse LMU independently.
- `EssentialOverlayFrameComposer` supplies the same dashboard, input, session
  and race-control semantics to desktop and VR.
- Stateful timing and strategy services are injected beside that compositor so
  their histories are not hidden in a renderer.
- Profile JSON has one schema and one sanitizer. SteamVR consumes the same
  `OverlayProfileSettings` used by desktop.
- Platform renderers may differ in drawing APIs, but parity tests protect the
  supported feature set and shared state contracts.

## Extension points

Add new telemetry fields to `Domain`, parse them in `LmuSharedMemory`, then
derive presentation-neutral state in `Widgets` or a dedicated policy assembly.
Only the final visual mapping belongs in Desktop/DirectX/SteamVR. This keeps new
widgets, OpenXR work and future strategy models from recreating a monolith.
