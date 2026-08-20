using LmuOverlay.Domain;
using LmuOverlay.Strategy.Learning;
using LmuOverlay.Strategy.Planning;

var window = new RobustSampleWindow(3);
window.Add(1);
window.Add(2);
window.Add(100);
window.Add(3);
Require(window.Count == 3 && window.Median == 3,
    "Rolling windows must evict old values and retain a robust median.");

var learning = new StrategyLearningModel();
learning.Reset(88, 1, new(0.10, 0.20, 0.10, 0.20));
var liveCapacity = learning.EstimateFuelCapacity(120, 0);
Require(liveCapacity.Publish && liveCapacity.Liters == 88,
    "A 100% NRG observation must override the larger physical tank.");

for (var lap = 1; lap <= 3; lap++)
{
    learning.AddCompletedLap(
        fuelUsed: 4.4,
        validFuel: true,
        energyUsed: 0.05,
        validEnergy: true,
        lapTimeSeconds: 120 + lap,
        validPace: true,
        currentTireWear: new(
            0.10 + lap * 0.01,
            0.20 + lap * 0.02,
            0.10 + lap * 0.01,
            0.20 + lap * 0.02),
        validTireWear: true);
}

Require(learning.FuelSampleCount == 3 &&
        Math.Abs(learning.FuelMedian - 4.4) < 0.0001,
    "Fuel learning must be isolated from presentation and planning.");
Require(Math.Abs(learning.TireWearPerLap.FrontRightFraction - 0.02) < 0.0001 &&
        Math.Abs(learning.TireWearPerLap.FrontLeftFraction - 0.01) < 0.0001,
    "Per-corner tire learning must preserve asymmetric degradation.");

var session = new LmuSessionSnapshot(
    "Spa", 1, LmuSessionKind.Race, LmuGamePhase.GreenFlag,
    0, 3600, 100, 7000, true, "Driver",
    new(0, 0, 20, 30, default, 0, 0, 0, 1));
Require(SessionHorizonCalculator.RemainingLaps(session, null, 37) == 63,
    "The horizon calculator must remain independent from the UI coordinator.");

Console.WriteLine("Strategy module checks passed.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
