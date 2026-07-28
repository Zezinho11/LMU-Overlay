# ADR 0002: Read-only LMU boundary

- Status: accepted
- Date: 2026-07-28

## Decision

Consume LMU only through the documented `LMU_Data` mapping, opened with read
rights. Never reproduce the sample's writable lock protocol.

## Rationale

This yields useful live telemetry without entering the game process or modifying
game-owned state. Coherence is checked by comparing update counters before and
after each copy, retrying a bounded number of times.

## Consequences

A snapshot can be discarded if LMU updates it during the copy. We prefer a missed
frame over a write capability or potentially torn data.
