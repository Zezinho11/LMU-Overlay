# Live standings multiclass widget

## Purpose

Provide a fast multiclass race overview inspired by endurance timing screens,
without copying proprietary visual assets.

## Information hierarchy

The widget displays one section per vehicle class in a WEC-style timing tower:

- the complete widget dynamically fits thirteen or fourteen visible cars,
  depending on how many class bands are required;
- the player's class fills every row left after compact leaders from the other
  two race classes are included;
- P1 remains fixed while a moving window follows the player up and down the
  class order;
- every other class shows only its current leader;
- the player's row receives a persistent highlight;
- every row shows class position, a three-character manufacturer badge, an
  explicit race number when available, a three-character driver abbreviation,
  last lap, interval to the previous car, and pit-lane state;
- compact non-player-class leaders always show their time and pit indicator.

The player's class appears first. Other classes follow in a stable order so the
screen does not jump unnecessarily while positions change.

## Data rules

Class order is derived from the normalized standings snapshot, sorted by the
official overall position within each class. A pit indicator is active when the
vehicle is reported in pits or has a non-`None` pit state.

If a time or interval is unavailable, the widget renders a neutral placeholder
rather than inventing a value. Car numbers are extracted from explicit
`#number`, `CAR`, `NO`, `NUM` or standalone number tokens in the official
vehicle name. Tokens that belong to the vehicle model are excluded, and the
runtime vehicle ID is never misrepresented as a race number. Manufacturer badges cross-reference each
scoring row with the official telemetry vehicle model by vehicle ID, then use a
known three-letter code and brand color. Unknown models render `---` rather than
misrepresenting a team-name abbreviation as a manufacturer.

The timing tower uses a narrow 260x410 vertical design surface without side
rails, a branding header, or a horizontal footer. The column headings are
followed directly by the colored class band. Existing version-4 layouts using
the former wide default are migrated to the narrow right-aligned footprint.

## Layout and configuration

The widget is independently movable, resizable, hideable, and lockable. Future
configuration will include:

- expanded or compact player-class rows;
- columns to display;
- class colors and abbreviations;
- configurable maximum visible cars;
- last-lap versus best-lap emphasis;
- optional session clock and track-condition header.

## Acceptance checks

1. The tower fills its vertical design surface without overflowing and always
   includes the player's class P1 and player.
2. Every other class contains exactly its leader.
3. Pit entry and exit update without rebuilding the complete overlay.
4. Missing timing values never appear as valid zero times.
5. The moving window follows position changes without losing P1.
