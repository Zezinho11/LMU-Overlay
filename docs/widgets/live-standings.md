# Live standings multiclass widget

## Purpose

Provide a fast multiclass race overview inspired by endurance timing screens,
without copying proprietary visual assets.

## Information hierarchy

The widget displays one section per vehicle class in a WEC-style timing tower:

- the complete widget dynamically fits up to twelve cars in one class, or
  eleven cars when additional class bands are required;
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
- every visible row shows the official front/rear compound reported for that
  vehicle and its telemetry Virtual Energy percentage; pit state replaces this compact
  status while the car is in pit lane.

The top bar shows the official session kind and remaining session time. During
qualifying, the lap column changes to `BEST LAP` and the interval is the
difference between each car's official best lap and the fastest official best
lap in its class. Pit state does not replace that qualifying gap.

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
runtime vehicle ID is never misrepresented as a race number. Manufacturer
badges cross-reference each scoring row with the official telemetry vehicle
model by vehicle ID, then use a known three-letter code and brand color. During
a temporary model-metadata gap they also resolve the official scoring vehicle
name, so `BMW`, `FER`, `AST` and the other known codes remain visible. A
genuinely unknown model renders `---` rather than misrepresenting a team-name
abbreviation as a manufacturer.

Virtual Energy and compound names are joined by official vehicle ID from each
corresponding LMU telemetry block. The same rule applies to the compact P1 row
for every non-player class: it displays that leader's own energy and compound,
never values copied from the player.

The timing tower uses a wider 500x410 design surface without side rails, a
branding header, or a horizontal footer. The session header and column headings
are followed directly by the colored class band. Existing narrow layouts are
migrated to the wider footprint while preserving their right edge and avoiding
overlap with Relative.

## Layout and configuration

The widget is independently movable, resizable, hideable, and lockable. Future
configuration will include:

- expanded or compact player-class rows;
- columns to display;
- class colors and abbreviations;
- configurable maximum visible cars;
- last-lap versus best-lap emphasis;
- optional track-condition header.

## Acceptance checks

1. The tower fills its vertical design surface without overflowing and always
   includes the player's class P1 and player.
2. Every other class contains exactly its leader.
3. Pit entry and exit update without rebuilding the complete overlay.
4. Missing timing values never appear as valid zero times.
5. The moving window follows position changes without losing P1.
