# SteamVR quick start

The SteamVR host is a separate executable. It uses Valve's documented OpenVR
overlay interface and never injects into LMU or hooks the game's renderer.

1. Start SteamVR and confirm that the headset is detected.
2. Start LMU and enter a session if live telemetry is desired.
3. Run `LmuOverlay.SteamVr.exe` from the release folder.
4. Seven permanent RedFox panels appear head-locked in front of the headset:
   Dashboard, Inputs, Live Standings, Relative, Fuel & Virtual Energy,
   Session/Weather and Race Control. A separate Priority Alert surface appears
   only when the same desktop warning rules require attention.
5. Close the console or press `Ctrl+C` to remove the panel cleanly.

The host creates `%LOCALAPPDATA%\LMU Overlay\steamvr-profile.json` on first run.
Each widget has independent `Visible`, `WidthMeters`, `DistanceMeters`,
`VerticalOffsetMeters`, `HorizontalOffsetMeters` and `Opacity` values. Edit the
file is still portable, but manual editing is not required. Run
`LmuOverlay.SteamVr.exe --configure-vr` for a graphical editor. Saving applies
to an already-running VR host within one second.

The VR host uses the desktop widget rules for telemetry, track-relative gaps,
multiclass timing, fuel/Virtual Energy strategy, flags, weather, penalties,
damage, persistent personal-best laps and sectors, and LMU's official Timing
optimal. The active desktop profile also controls theme, custom colors, title,
text scale, timing population, strategy inputs, pedal history and update rate.

Dashboard and Inputs render at the selected 60-120 Hz rate. Timing surfaces
render at 10 Hz and strategy/session surfaces at 5 Hz because their source data
does not change at physics cadence. If SteamVR is started later or restarted,
the host waits and reconnects automatically.

The host locates `openvr_api.dll` beside the executable or in the user's Steam
installation or the runtime paths registered in `openvrpaths.vrpath`. SteamVR
may be started before or after the overlay host.

For the same privacy-safe health export available on desktop, start the VR host
with an explicit destination:

```powershell
LmuOverlay.SteamVr.exe --diagnostics .\steamvr-diagnostics.json
```

The file updates every ten seconds and includes p99 read latency, stale-frame
age and compositor recovery attempts without driver, track or telemetry values.
