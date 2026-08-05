# ADR 0005: Native dashboard renderer

- Status: Accepted
- Date: 2026-08-05

## Context

The dashboard contains RPM, gear, shift lights, pedal inputs and a scrolling
trace. Those values are latency-sensitive and can arrive much faster than the
WPF dispatcher can reliably measure, lay out and redraw a large visual tree.
Increasing the shared-memory polling rate alone therefore did not remove the
visible pauses.

## Decision

Render the latency-critical dashboard in a separate topmost, click-through
Win32 window backed by Direct3D 11, Direct2D, DirectWrite and DirectComposition.
The telemetry capture thread publishes only the newest immutable dashboard
snapshot to an above-normal-priority renderer thread. Frames are replaced, not
queued, so an old backlog can never accumulate. Presentation is synchronized
to the display through a premultiplied-alpha composition swap chain.

WPF remains responsible for configuration, edit mode and lower-frequency
strategic widgets. It also remains an automatic fallback if native graphics
initialization fails. In edit mode the WPF dashboard is shown for moving and
resizing; the native surface resumes when the layout is locked.

Live Standings and Relative use independent DirectComposition surfaces backed
by one additional shared Direct3D device. This avoids a full-screen transparent
swap chain and pays only for the pixels occupied by each timing tower. Their
states are rebuilt only when LMU replaces the immutable standings collection;
profile changes can reposition either surface without recalculating race data.
The WPF timing towers remain the exact edit-mode and failure fallback views.

## Safety boundary

The native renderer is still an external desktop window. It does not inject a
DLL, hook DirectX inside LMU, read game-process memory, simulate input or write
to the game's shared-memory map. High-frequency telemetry remains read-only
through the documented `LMU_Data` shared-memory interface. The theoretical
optimal uses one read-only localhost request per second to
`/rest/watch/standings/history`, the same history endpoint used by LMU's
packaged Live Timing UI; it never sends a command or writes data.

## Consequences

- The critical path no longer depends on the WPF dispatcher or retained visual
  tree.
- Dashboard presentation can follow each new LMU telemetry frame without
  blocking shared-memory capture.
- DirectX resources and third-party binding licenses become release concerns.
- Layout editing deliberately uses the WPF representation rather than making
  the native click-through window interactive.
