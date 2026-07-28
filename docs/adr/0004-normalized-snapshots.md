# ADR 0004: Normalized immutable snapshots

- Status: accepted
- Date: 2026-07-28

## Decision

Copy each coherent LMU frame into application-owned memory, parse it immediately,
and expose only immutable domain records to the rest of the application.

## Rationale

Widgets must not depend on proprietary structure layouts, mapped-memory lifetime,
or partially updated frames. A normalized boundary allows deterministic tests,
replay fixtures, desktop/SteamVR reuse, and layout-version fail-closed behavior.

## Consequences

The adapter owns all offsets and encoding rules. Derived metrics remain pure
functions of a snapshot. Presentation hosts consume the polling service and
never receive the raw shared-memory buffer.
