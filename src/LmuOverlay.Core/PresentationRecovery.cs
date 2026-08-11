namespace LmuOverlay.Core;

[Flags]
public enum PresentationFeatures
{
    None = 0,
    Dashboard = 1 << 0,
    Inputs = 1 << 1,
    LiveStandings = 1 << 2,
    Relative = 1 << 3,
    FuelAndVirtualEnergy = 1 << 4,
    SessionWeatherFlags = 1 << 5,
    RaceControl = 1 << 6,
    PriorityAlerts = 1 << 7,
    ThemesAndCustomColors = 1 << 8,
    TypographyAndDensity = 1 << 9,
    PersistentTimingReferences = 1 << 10,
    OfficialOptimal = 1 << 11,
    RuntimeRecovery = 1 << 12,
    RuntimeHealth = 1 << 13,
    AccessibleColorPalette = 1 << 14,
    Localization = 1 << 15,
}

public static class PresentationFeatureSet
{
    public const PresentationFeatures Required =
        PresentationFeatures.Dashboard |
        PresentationFeatures.Inputs |
        PresentationFeatures.LiveStandings |
        PresentationFeatures.Relative |
        PresentationFeatures.FuelAndVirtualEnergy |
        PresentationFeatures.SessionWeatherFlags |
        PresentationFeatures.RaceControl |
        PresentationFeatures.PriorityAlerts |
        PresentationFeatures.ThemesAndCustomColors |
        PresentationFeatures.TypographyAndDensity |
        PresentationFeatures.PersistentTimingReferences |
        PresentationFeatures.OfficialOptimal |
        PresentationFeatures.RuntimeRecovery |
        PresentationFeatures.RuntimeHealth |
        PresentationFeatures.AccessibleColorPalette |
        PresentationFeatures.Localization;
}

public sealed record PresentationHostHealth(
    bool Available,
    long RecoveryAttempts,
    DateTimeOffset? LastRecoveredAt,
    string LastError);

public static class PresentationRecoveryPolicy
{
    public static TimeSpan DelayForFailure(int consecutiveFailures)
    {
        var exponent = Math.Clamp(consecutiveFailures - 1, 0, 4);
        return TimeSpan.FromMilliseconds(250 * (1 << exponent));
    }
}
