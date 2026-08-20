using LmuOverlay.Domain;

namespace LmuOverlay.Strategy.Learning;

public readonly record struct FuelCapacityEstimate(double Liters, bool Publish);

public sealed class StrategyLearningModel
{
    private const int MinimumCapacitySamples = 3;
    private readonly RobustSampleWindow _fuel = new();
    private readonly RobustSampleWindow _energy = new();
    private readonly RobustSampleWindow _balancedCapacity = new();
    private readonly RobustSampleWindow _pace = new();
    private readonly RobustSampleWindow _maximumTireWear = new();
    private readonly RobustSampleWindow _frontLeftWear = new();
    private readonly RobustSampleWindow _frontRightWear = new();
    private readonly RobustSampleWindow _rearLeftWear = new();
    private readonly RobustSampleWindow _rearRightWear = new();
    private LmuWheelWear _previousTireWear;
    private double _maximumObservedFuelLiters;
    private double _observedBalancedFuelCapacityLiters;

    public int FuelSampleCount => _fuel.Count;
    public double FuelMedian => _fuel.Median;
    public double FuelConservative => _fuel.Conservative;
    public double FuelSigma => _fuel.Sigma;
    public double EnergyMedian => _energy.Median;
    public double PaceMedian => _pace.Median;
    public double PaceSigma => _pace.Sigma;
    public double PaceTrend => _pace.Trend;
    public IEnumerable<double> FuelSamples => _fuel;
    public IEnumerable<double> EnergySamples => _energy;

    public double MaximumTireWearPerLap =>
        _maximumTireWear.Count >= 3 ? _maximumTireWear.Conservative : 0;

    public LmuWheelWear TireWearPerLap => new(
        WheelRate(_frontLeftWear),
        WheelRate(_frontRightWear),
        WheelRate(_rearLeftWear),
        WheelRate(_rearRightWear));

    public void Reset(double fuelLiters, double virtualEnergy, LmuWheelWear tireWear)
    {
        _fuel.Clear();
        _energy.Clear();
        _balancedCapacity.Clear();
        _pace.Clear();
        _maximumTireWear.Clear();
        _frontLeftWear.Clear();
        _frontRightWear.Clear();
        _rearLeftWear.Clear();
        _rearRightWear.Clear();
        _previousTireWear = tireWear;
        _maximumObservedFuelLiters = Math.Max(0, fuelLiters);
        _observedBalancedFuelCapacityLiters = virtualEnergy >= 0.999
            ? Math.Max(0, fuelLiters)
            : 0;
    }

    public void ObserveResources(double fuelLiters, double virtualEnergy)
    {
        _maximumObservedFuelLiters = Math.Max(
            _maximumObservedFuelLiters,
            Math.Max(0, fuelLiters));
        if (virtualEnergy >= 0.999 && fuelLiters > 0)
        {
            _observedBalancedFuelCapacityLiters = Math.Max(
                _observedBalancedFuelCapacityLiters,
                fuelLiters);
        }
    }

    public void AddCompletedLap(
        double fuelUsed,
        bool validFuel,
        double energyUsed,
        bool validEnergy,
        double lapTimeSeconds,
        bool validPace,
        LmuWheelWear currentTireWear,
        bool validTireWear)
    {
        if (validFuel) _fuel.Add(fuelUsed);
        if (validEnergy) _energy.Add(energyUsed);
        if (validFuel && validEnergy)
        {
            var capacity = fuelUsed / energyUsed;
            if (double.IsFinite(capacity) && capacity is >= 1 and <= 1000)
            {
                _balancedCapacity.Add(capacity);
            }
        }

        if (validPace) _pace.Add(lapTimeSeconds);
        if (validTireWear)
        {
            AddTireSample(
                _frontLeftWear,
                currentTireWear.FrontLeftFraction - _previousTireWear.FrontLeftFraction);
            AddTireSample(
                _frontRightWear,
                currentTireWear.FrontRightFraction - _previousTireWear.FrontRightFraction);
            AddTireSample(
                _rearLeftWear,
                currentTireWear.RearLeftFraction - _previousTireWear.RearLeftFraction);
            AddTireSample(
                _rearRightWear,
                currentTireWear.RearRightFraction - _previousTireWear.RearRightFraction);
            var maximumDelta = Maximum(currentTireWear) - Maximum(_previousTireWear);
            if (maximumDelta is > 0.00001 and < 0.25)
            {
                _maximumTireWear.Add(maximumDelta);
            }
        }

        _previousTireWear = currentTireWear;
    }

    public FuelCapacityEstimate EstimateFuelCapacity(
        double reportedCapacityLiters,
        double manualCapacityLiters)
    {
        var reported = manualCapacityLiters > 0
            ? Math.Clamp(manualCapacityLiters, 1, 1000)
            : reportedCapacityLiters;
        var balanced = _observedBalancedFuelCapacityLiters > 0
            ? _observedBalancedFuelCapacityLiters
            : _balancedCapacity.Count >= MinimumCapacitySamples
                ? _balancedCapacity.Median
                : double.PositiveInfinity;
        var capacity = Math.Min(
            reported > 0 ? reported : balanced,
            balanced);
        if (!double.IsFinite(capacity) || capacity <= 0) capacity = reported;
        capacity = Math.Max(capacity, _maximumObservedFuelLiters);
        if (reported > 0) capacity = Math.Min(capacity, reported);
        var publish = _observedBalancedFuelCapacityLiters > 0 ||
            _balancedCapacity.Count >= MinimumCapacitySamples ||
            manualCapacityLiters > 0;
        return new(capacity, publish);
    }

    private static void AddTireSample(RobustSampleWindow window, double wear)
    {
        if (double.IsFinite(wear) && wear is > 0.00001 and < 0.25)
        {
            window.Add(wear);
        }
    }

    private static double WheelRate(RobustSampleWindow window) =>
        window.Count >= 3 ? window.Conservative : 0;

    private static double Maximum(LmuWheelWear wear) => Math.Max(
        Math.Max(wear.FrontLeftFraction, wear.FrontRightFraction),
        Math.Max(wear.RearLeftFraction, wear.RearRightFraction));
}
