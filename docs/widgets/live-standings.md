# Live standings multiclass widget

## Purpose

Provide a fast multiclass race overview inspired by endurance timing screens,
without copying proprietary visual assets.

## Information hierarchy

The widget displays one section per vehicle class:

- the player's class shows every available car in class order;
- every other class shows only its current leader;
- the player's row receives a persistent highlight;
- every row may show driver, vehicle, class position, completed laps, gap,
  last lap, best lap, and pit-lane state;
- compact non-player-class leaders always show their time and pit indicator.

The player's class appears first. Other classes follow in a stable order so the
screen does not jump unnecessarily while positions change.

## Data rules

Class order is derived from the normalized standings snapshot, sorted by the
official overall position within each class. A pit indicator is active when the
vehicle is reported in pits or has a non-`None` pit state.

If a time or gap is unavailable, the widget renders a neutral placeholder rather
than inventing a value. Car numbers are extracted only from reliable telemetry
fields; vehicle names remain the fallback.

## Layout and configuration

The widget is independently movable, resizable, hideable, and lockable. Future
configuration will include:

- expanded or compact player-class rows;
- columns to display;
- class colors and abbreviations;
- maximum visible player-class cars with scrolling or paging;
- last-lap versus best-lap emphasis;
- optional session clock and track-condition header.

## Acceptance checks

1. The player's class contains all available cars with correct class positions.
2. Every other class contains exactly its leader.
3. Pit entry and exit update without rebuilding the complete overlay.
4. Missing timing values never appear as valid zero times.
5. Updates do not reorder class sections except when the player's class changes.
