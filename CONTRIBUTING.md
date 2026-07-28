# Contributing

The contribution workflow will open after the project license is selected.

For now:

1. Open an issue describing the change and its safety implications.
2. Keep the game boundary read-only.
3. Do not commit proprietary LMU headers, game assets, credentials, binaries, or
   recorded telemetry containing personal data.
4. Add tests for every binding or parser change.
5. Run restore, Release build, and all tests before submitting a change.

Changes touching process access, rendering injection, input synthesis, game files,
network interception, or undocumented interfaces are out of scope.
