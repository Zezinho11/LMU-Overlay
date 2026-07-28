# Relative widget

The Relative widget shows the nearest cars around the player in official
standing order. Its renderer-independent state includes overall position,
official race number when explicitly available, compact driver name, class and
class abbreviation, signed relative gap, lap difference, player highlight, and
pit state.

The vertical tower shares the Live Standings 260x410 design proportion and
shows up to four cars ahead, the player, and four cars behind. GT3, Hypercar,
and LMP2 use distinct class badges. The player row is highlighted, pit state
replaces the gap with `PIT`, and missing official race numbers remain `--`.

Same-lap gaps are derived from each car's official gap-to-leader value. Lap
differences remain explicit so a lapped car is never presented as a normal
seconds-only gap.

Future live validation will compare this derived ordering against busy
multiclass sessions and replace derived gaps with a more precise official value
when the shared-memory interface exposes one.
