namespace LmuOverlay.Widgets;

public sealed record StrategyScenarioInput(
    EnduranceStrategyPlan Plan,
    int RemainingLaps,
    double CurrentFuelLiters,
    double ConsumptionLitersPerLap,
    double ConsumptionSigmaLiters,
    double ReferencePaceSeconds,
    double PaceSigmaSeconds,
    double PitLossSigmaSeconds,
    double ReserveFuelLiters,
    int Seed,
    int Iterations = 512);

public sealed record StrategyScenarioResult(
    bool Available,
    double FinishProbability,
    double ReserveProbability,
    double P10TimeSeconds,
    double MedianTimeSeconds,
    double P90TimeSeconds,
    string Summary);

public static class StrategyScenarioSimulator
{
    public static StrategyScenarioResult Simulate(StrategyScenarioInput input)
    {
        if (!input.Plan.Available || input.RemainingLaps <= 0 ||
            input.ConsumptionLitersPerLap <= 0 || input.ReferencePaceSeconds <= 0)
        {
            return new(false, 0, 0, 0, 0, 0, "SCENARIOS · LEARNING");
        }

        var count = Math.Clamp(input.Iterations, 64, 2_048);
        var random = new Random(input.Seed);
        var finish = 0;
        var reserve = 0;
        var times = new double[count];
        var suppliedFuel = Math.Max(0, input.CurrentFuelLiters) +
            input.Plan.FuelAtStopsLiters.Sum(value => Math.Max(0, value));
        for (var index = 0; index < count; index++)
        {
            var consumption = Math.Max(0.01,
                input.ConsumptionLitersPerLap + Gaussian(random) * Math.Max(0, input.ConsumptionSigmaLiters));
            var pace = Math.Max(1,
                input.ReferencePaceSeconds + Gaussian(random) * Math.Max(0, input.PaceSigmaSeconds));
            var pitLoss = Math.Max(0,
                input.Plan.Stops * Gaussian(random) * Math.Max(0, input.PitLossSigmaSeconds));
            var margin = suppliedFuel - consumption * input.RemainingLaps;
            if (margin >= 0) finish++;
            if (margin >= input.ReserveFuelLiters) reserve++;
            times[index] = input.RemainingLaps * pace +
                input.Plan.Stops * Math.Max(0, input.Plan.EstimatedRaceTimeSeconds -
                    input.RemainingLaps * input.ReferencePaceSeconds) /
                    Math.Max(1, input.Plan.Stops) + pitLoss;
        }

        Array.Sort(times);
        var finishProbability = finish / (double)count;
        var reserveProbability = reserve / (double)count;
        var p10 = Percentile(times, 0.10);
        var median = Percentile(times, 0.50);
        var p90 = Percentile(times, 0.90);
        return new(
            true,
            finishProbability,
            reserveProbability,
            p10,
            median,
            p90,
            $"SCENARIOS · FINISH {finishProbability:P0} · RESERVE {reserveProbability:P0} · " +
            $"RANGE {Format(p10)}–{Format(p90)}");
    }

    private static double Gaussian(Random random)
    {
        var first = Math.Max(double.Epsilon, random.NextDouble());
        var second = random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(first)) * Math.Cos(2 * Math.PI * second);
    }

    private static double Percentile(double[] sorted, double percentile) =>
        sorted[Math.Clamp((int)Math.Round((sorted.Length - 1) * percentile), 0, sorted.Length - 1)];

    private static string Format(double seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
