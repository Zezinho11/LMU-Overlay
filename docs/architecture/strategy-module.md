# Strategy module

Race strategy is a separate, UI-independent assembly:

```text
LmuOverlay.Domain
        ↓
LmuOverlay.Strategy
  ├── Learning/RobustSampleWindow
  ├── Learning/StrategyLearningModel
  └── Planning/
      ├── SessionHorizonCalculator
      ├── FullPushStrategyCalculator
      ├── FuelSaveStrategyCalculator
      ├── TireStrategyCalculator
      └── StrategyPlannerFacade
        ↓
LmuOverlay.Widgets/FuelStrategyTracker (coordinator/facade)
        ├── Desktop
        └── SteamVR
```

## Boundaries

- `LmuOverlay.Strategy` has no WPF, DirectX or SteamVR dependency.
- Learning owns rolling fuel, Virtual Energy, pace and per-corner tire samples.
- Capacity estimation owns the live 100% NRG observation and paired-lap fallback.
- Planning owns stint, stop, fuel-save and tire-service calculations.
- Session horizon owns timed/lap-limited distance calculation.
- `FuelStrategyTracker` keeps snapshot/session coordination and maps the result to
  the existing widget state. This preserves the public Desktop/VR contract.
- Desktop and SteamVR never calculate strategy independently.

## Safe extension points

- Change robust sample behavior in `Learning/RobustSampleWindow.cs`.
- Change fuel/NRG/tire learning in `Learning/StrategyLearningModel.cs`.
- Change timed-session projection in `Planning/SessionHorizonCalculator.cs`.
- Change Full Push in `Planning/FullPushStrategyCalculator.cs`.
- Change Fuel Save in `Planning/FuelSaveStrategyCalculator.cs`.
- Change per-corner tire scheduling in `Planning/TireStrategyCalculator.cs`.
- Change UI text/layout only in the Desktop or SteamVR renderer.

Persisted BEST, sector and OPTIMAL stores are outside this dependency graph and
must not be migrated, cleared or rewritten by strategy changes.
