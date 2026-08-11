# Sector delta references

LMU scoring exposes sector 1, cumulative sector 2 and lap timing, while the
fast telemetry block exposes the current sector, elapsed time and lap-start
time. The dashboard combines both streams at sector transitions.

## Reference priority

1. Current-session personal clean best.
2. Saved personal clean best for the same track and vehicle model.
3. A clean S2 or S3 captured during the current out lap.
4. No reference. The dashboard shows `NEW` rather than a fabricated delta.

S1 is never seeded from an out lap that crosses the pit lane. On many tracks
the pit exit lies inside sector 1, so that value is not comparable with a full
flying sector. S2 and S3 are accepted only when their complete segment was
driven outside the pits.

References are stored in `%LOCALAPPDATA%\LMU Overlay\sector-references.json`.
Only track name, vehicle model, personal sector values and update time are
stored. Writes are atomic and never touch LMU files or shared memory.

The native Direct2D dashboard and WPF compatibility dashboard use the same
store. Concurrent updates merge the fastest valid value for each sector so one
surface cannot erase a reference captured by the other.

## Personal-best laps

Complete valid personal-best laps are stored separately in
`%LOCALAPPDATA%\LMU Overlay\personal-bests.json`. The key combines track/layout,
driver name and vehicle model, so different drivers and car categories never
share a record. This file belongs only to the local installation and is not
part of release or GitHub data.

A record is replaced only by a faster valid lap. Lap time, S1, S2 and S3 are
written as one atomic record and always originate from that same lap. The
tracker briefly waits for LMU's scoring update at start/finish so the official
last-lap sectors replace any high-frequency transition approximation.

At rest, the sector card displays the three sectors belonging to the saved PB
lap. After a sector is completed, its row displays the current sector time and
the difference from the corresponding PB-lap sector for four seconds. It then
returns automatically to the saved PB value. A new record becomes the baseline
only after the complete valid lap is confirmed.
