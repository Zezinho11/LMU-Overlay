# SteamVR preview quick start

The SteamVR host is a separate executable. It uses Valve's documented OpenVR
overlay interface and never injects into LMU or hooks the game's renderer.

1. Start SteamVR and confirm that the headset is detected.
2. Start LMU and enter a session if live telemetry is desired.
3. Run `LmuOverlay.SteamVr.exe` from the release folder.
4. Five independent RedFox panels appear head-locked in front of the headset:
   Dashboard, Live Standings, Relative, Fuel Strategy and Session/Weather.
5. Close the console or press `Ctrl+C` to remove the panel cleanly.

The host creates `%LOCALAPPDATA%\LMU Overlay\steamvr-profile.json` on first run.
Each widget has independent `Visible`, `WidthMeters`, `DistanceMeters`,
`VerticalOffsetMeters`, `HorizontalOffsetMeters` and `Opacity` values. Edit the
file while the host is closed, then restart it to apply the layout.

The first multi-widget preview reuses the desktop state factories for dashboard,
standings, relative, fuel strategy and session/weather. An in-headset editor,
controller interaction and the remaining smaller widget surfaces are later
SteamVR milestones.

The host locates `openvr_api.dll` beside the executable or in the user's Steam
installation. SteamVR must be running before the overlay host starts.
