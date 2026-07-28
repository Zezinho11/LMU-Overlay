# Live validation evidence

## 2026-07-28 — baseline

- LMU numeric game version: `14000`
- Track: `Circuit de Spa-Francorchamps`
- Session code: `1` (practice)
- Player vehicle: `Manthey DK Engineering 2026 #91:WEC`
- Active vehicles: `1`
- Scored vehicles: `1`
- Result: coherent read, `Connected`, process exit code `0`

The test used the documented `LMU_Data` mapping with read rights only. No game
process handle, code injection, graphics hook, input synthesis, or memory write
was used.

Player identity and raw live telemetry were not retained. The committed fixture
is synthetic/anonymized and reproduces the validated layout without personal
data.

## 2026-07-28 — desktop overlay

The Phase 2 desktop host rendered live telemetry over the active Spa practice
session after being launched in the same interactive Windows desktop as LMU.

The validation exposed a case where `LMU_Data` was connected but the launching
session could not expose the LMU window handle. The host now falls back to the
primary screen bounds in that condition. It still does not open a game process
handle, install a graphics hook, or change the read-only safety boundary.

## 2026-07-28 — RealRoad and weather panel

A fresh read-only snapshot from the live Spa session validated the newly bound
official scoring fields:

- track grip level: `2` (`MEDIUM`);
- cloud darkness: `0`;
- rain intensity: `0`;
- average path wetness: `0`;
- ambient temperature: `15 C`;
- track temperature: approximately `31.7 C`.

The values were coherent with the visible dry, clear session and confirm the
derived offsets for average path wetness and `mTrackGripLevel`.
