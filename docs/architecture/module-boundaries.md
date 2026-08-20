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

## Internal decomposition

Large host classes are split into cohesive partial adapters while retaining one
runtime instance and one state owner:

- Desktop separates frame composition, dashboard, pedal graph, standings,
  relative, session/strategy, themes, native surfaces and layout interaction.
- DirectComposition separates device/surface lifecycle, resources, drawing and
  Win32 interop for Dashboard and Inputs; Timing owns its reusable surface in a
  dedicated file.
- SteamVR keeps `Program.cs` as a three-line composition root and separates
  host helpers plus renderers for Dashboard/Inputs, Timing and Strategy/Session.
- LMU parsing is divided into entry points, session, standings, metadata,
  player, wheel/damage and primitive readers.
- Configuration separates operations, persistence, naming, migrations and
  settings sanitization under the `LmuOverlay.Configuration` namespace.
- Timing separates update, capture, valid-lap publication, references and reset;
  strategy separates state contracts, update orchestration and lifecycle.

`LmuOverlay.Architecture.Tests` enforces the dependency direction, platform-free
stable modules, small composition roots/facades and a 450-line source-file
budget. CI runs this gate before behavioral suites.

## Extension points

Add new telemetry fields to `Domain`, parse them in `LmuSharedMemory`, then
derive presentation-neutral state in `Widgets` or a dedicated policy assembly.
Only the final visual mapping belongs in Desktop/DirectX/SteamVR. This keeps new
widgets, OpenXR work and future strategy models from recreating a monolith.
