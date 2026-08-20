using LmuOverlay.Domain;
using LmuOverlay.Strategy.Learning;
using LmuOverlay.Strategy.Planning;

namespace LmuOverlay.Widgets;


public sealed partial class FuelStrategyTracker
{
    private readonly StrategyLearningModel _learning = new();
    private readonly RobustSampleWindow _rainSamples = new(20);
    private string _trackName = string.Empty;
    private string _vehicleIdentity = string.Empty;
    private int _gameVersion;
    private int _sessionCode = int.MinValue;
    private int _lastCompletedLaps = -1;
    private double _lapStartFuel;
    private double _previousFuel;
    private double _lapStartVirtualEnergy;
    private double _previousVirtualEnergy;
    private DateTimeOffset _lastRainSampleAt = DateTimeOffset.MinValue;
    private DateTimeOffset _previousSampleAt = DateTimeOffset.MinValue;
    private bool _lapContaminated;
    private double _lapStartRain;
    private int _scenarioCompletedLaps = int.MinValue;
    private StrategyScenarioResult _scenario = new(false, 0, 0, 0, 0, 0, "SCENARIOS · LEARNING");


}
