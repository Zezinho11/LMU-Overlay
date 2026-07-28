# Relative widget

The Relative widget shows the nearest cars around the player in official
standing order. Its renderer-independent state includes overall position,
driver, class, signed relative gap, lap difference, player highlight, and pit
state.

The first implementation shows three cars ahead and three behind. Same-lap gaps
are derived from each car's official gap-to-leader value. Lap differences remain
explicit so a lapped car is never presented as a normal seconds-only gap.

Future live validation will compare this derived ordering against busy
multiclass sessions and replace derived gaps with a more precise official value
when the shared-memory interface exposes one.
