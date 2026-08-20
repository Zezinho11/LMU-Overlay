using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using LmuOverlay.Application;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

public partial class OverlayWindow
{
    private void UpdateSessionFlags(SessionFlagsWidgetState state)
    {
        string T(OverlayTextKey key) => OverlayText.Get(_profile.Settings.Language, key);
        if (!state.Available)
        {
            SessionNameText.Text = T(OverlayTextKey.Session);
            SessionMetaText.Text = "--:--  ·  LAP --";
            GripValueText.Text = "UNKNOWN";
            GripValueText.Foreground = System.Windows.Media.Brushes.LightGray;
            GripCard.BorderBrush = NeutralCardBrush;
            WeatherIconText.Text = "☁";
            WeatherNameText.Text = T(OverlayTextKey.NoData);
            WeatherDetailText.Text = "RAIN --%  ·  WET --%";
            WeatherCard.BorderBrush = NeutralCardBrush;
            FlagCardText.Text = T(OverlayTextKey.NoData);
            FlagCard.Background = NeutralCardBrush;
            AmbientTemperatureText.Text = "--°C";
            TrackTemperatureText.Text = "--°C";
            WetnessText.Text = "--%";
            return;
        }

        SessionNameText.Text = $"{state.SessionName} · {state.PhaseName}";
        var remaining = state.RemainingSeconds > 0
            ? TimeSpan.FromSeconds(state.RemainingSeconds).ToString(
                state.RemainingSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
            : "--:--";
        var lap = state.MaximumLaps > 0
            ? $"{state.CurrentLap}/{state.MaximumLaps}"
            : state.CurrentLap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SessionMetaText.Text = $"{remaining}  ·  LAP {lap}";

        var gripBrush = state.TrackGripLevel switch
        {
            0 => GripGreenBrush,
            1 => GripLightBrush,
            2 => GripMediumBrush,
            3 => GripHeavyBrush,
            >= 4 => GripSaturatedBrush,
            _ => NeutralCardBrush,
        };
        GripValueText.Text = state.TrackGripName;
        GripValueText.Foreground = gripBrush;
        GripCard.BorderBrush = gripBrush;

        WeatherIconText.Text = state.WeatherCondition switch
        {
            WeatherConditionKind.Clear => "☀",
            WeatherConditionKind.PartlyCloudy => "☀☁",
            WeatherConditionKind.Cloudy => "☁",
            WeatherConditionKind.Overcast => "☁☁",
            WeatherConditionKind.LightRain => "☂",
            WeatherConditionKind.Rain => "☂☂",
            WeatherConditionKind.HeavyRain => "☔",
            _ => "☁",
        };
        WeatherNameText.Text = state.WeatherName;
        WeatherDetailText.Text =
            $"RAIN {state.RainIntensity:P0}  ·  WET {state.AveragePathWetness:P0}";
        WeatherIconText.Foreground = state.WeatherCondition switch
        {
            WeatherConditionKind.Clear => System.Windows.Media.Brushes.Gold,
            WeatherConditionKind.PartlyCloudy =>
                System.Windows.Media.Brushes.LightSkyBlue,
            WeatherConditionKind.Cloudy or WeatherConditionKind.Overcast =>
                System.Windows.Media.Brushes.LightGray,
            WeatherConditionKind.LightRain or
            WeatherConditionKind.Rain or
            WeatherConditionKind.HeavyRain =>
                System.Windows.Media.Brushes.DeepSkyBlue,
            _ => System.Windows.Media.Brushes.LightGray,
        };
        WeatherCard.BorderBrush = WeatherIconText.Foreground;

        FlagCardText.Text = OverlayText.TranslateExact(_profile.Settings.Language, state.FlagName);
        FlagCard.Background = state.FlagName switch
        {
            "GREEN" => FlagGreenBrush,
            "YELLOW" => FlagYellowBrush,
            "RED" => FlagRedBrush,
            _ => NeutralCardBrush,
        };
        AmbientTemperatureText.Text =
            $"{state.AmbientTemperatureCelsius:0}°C";
        TrackTemperatureText.Text =
            $"{state.TrackTemperatureCelsius:0}°C";
        WetnessText.Text = $"{state.AveragePathWetness:P0}";
    }

    private void UpdateFuelStrategy(FuelStrategyWidgetState state)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        if (!state.Available)
        {
            FuelStatusText.Text = "NO DATA";
            FuelCurrentText.Text = "--.- L";
            EnergyCurrentText.Text = "--%";
            FuelTableCurrentText.Text = "--.- L";
            FuelUsageText.Text = "--.-- L";
            FuelRangeText.Text = "--.-";
            FuelRangeTimeText.Text = "-- MIN";
            EnergyTableCurrentText.Text = "--%";
            EnergyUsageText.Text = "--.-%";
            EnergyRangeText.Text = "--.-";
            EnergyRangeTimeText.Text = "-- MIN";
            FinishTargetText.Text = "-- LAPS / -- MIN";
            FuelFinishText.Text = "--.- L / --.- L";
            EnergyFinishText.Text = "--% / --%";
            StrategyPlanText.Text = "STRATEGY LEARNING";
            StrategyPitPlanText.Text = "PIT --";
            StrategyTirePlanText.Text = "TIRES --";
            StrategyAlternativeText.Text = "ALT --";
            FlagScenarioText.Text = "FLAGS · LEARNING";
            WeatherScenarioText.Text = "WEATHER · LEARNING";
            TrafficScenarioText.Text = "TRAFFIC · LEARNING";
            FuelSamplesText.Text = "WAITING FOR TELEMETRY";
            return;
        }

        FuelStatusText.Text = state.Status;
        FuelStatusText.Foreground = state.Status switch
        {
            "SHORT" => Brush(palette.Critical),
            "MARGINAL" => Brush(palette.Attention),
            "GOOD" => Brush(palette.Positive),
            _ => Brush(palette.SecondaryText),
        };
        FuelCurrentText.Text = state.EffectiveFuelCapacityLiters > 0
            ? $"{state.FuelLiters:0.0} / {state.EffectiveFuelCapacityLiters:0.0} L"
            : $"{state.FuelLiters:0.0} L";
        EnergyCurrentText.Text = $"{state.VirtualEnergyFraction:P0}";
        FuelTableCurrentText.Text = $"{state.FuelLiters:0.0} L";
        FuelUsageText.Text = state.Learning
            ? "LEARNING"
            : $"{state.ProjectedConsumptionLitersPerLap:0.00} L";
        FuelRangeText.Text = state.Learning
            ? "--.-"
            : $"{state.EstimatedRangeLaps:0.0}";
        FuelRangeTimeText.Text = state.Learning
            ? "-- MIN"
            : FormatStrategyMinutes(state.EstimatedRangeTimeSeconds);
        EnergyTableCurrentText.Text = $"{state.VirtualEnergyFraction:P0}";
        EnergyUsageText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.AverageVirtualEnergyFractionPerLap:P1}"
            : "LEARNING";
        EnergyRangeText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.EstimatedVirtualEnergyRangeLaps:0.0}"
            : "--.-";
        EnergyRangeTimeText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? FormatStrategyMinutes(state.EstimatedVirtualEnergyRangeTimeSeconds)
            : "-- MIN";
        FinishTargetText.Text =
            $"{state.EstimatedLapsToFinish} LAPS / " +
            FormatStrategyMinutes(state.EstimatedTimeToFinishSeconds);
        FuelFinishText.Text = state.Learning
            ? "--.- L / --.- L"
            : $"{state.RequiredFuelLiters:0.0} L / " +
              $"{state.FuelMarginLiters:+0.0;-0.0;0.0} L";
        EnergyFinishText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.RequiredVirtualEnergyFraction:P0} / " +
              $"{state.VirtualEnergyMarginFraction:+0.0%;-0.0%;0.0%}"
            : "--% / --%";
        FuelFinishText.Foreground = state.FuelMarginLiters < 0
            ? Brush(palette.Critical)
            : Brush(palette.Positive);
        EnergyFinishText.Foreground =
            state.AverageVirtualEnergyFractionPerLap > 0 &&
            state.VirtualEnergyMarginFraction < 0
                ? Brush(palette.Critical)
                : Brush(palette.Information);
        StrategyPlanText.Text = state.PlanSummary;
        StrategyPitPlanText.Text = $"PIT  {state.PitPlan}";
        StrategyTirePlanText.Text = $"TIRES  {state.TirePlan}";
        StrategyAlternativeText.Text = state.EstimatedPitStops > 0
            ? $"FINAL FILL  +{state.FinalFuelToAddLiters:0.0} L · " +
              $"NRG {state.FinalVirtualEnergyTargetFraction:P0} · FINISH {state.FinishProbability:P0}"
            : $"NO FINAL FILL REQUIRED · FINISH {state.FinishProbability:P0}";
        FlagScenarioText.Text = state.FuelSavePlan;
        FuelSaveLapPlanText.Text = state.FuelSaveLapPlan;
        WeatherScenarioText.Text = state.FuelSaveVirtualEnergyTargetPerLap > 0
            ? $"{state.FuelSavePitPlan} · NRG TARGET {state.FuelSaveVirtualEnergyTargetPerLap:P1}/LAP"
            : state.FuelSavePitPlan;
        TrafficScenarioText.Text = $"TIRES  {state.FuelSaveTirePlan}";
        FuelSamplesText.Text = state.Learning
            ? "COMPLETE A LAP TO CALCULATE"
            : $"PIT L{state.SuggestedPitLap} ({state.LapsUntilPit} LAPS)  " +
              $"· SAVE {state.RequiredFuelSavingFraction:P0}  " +
              $"· ADD {state.FuelToAddLiters:0.0} L  " +
              $"· CONF {state.Confidence} ({state.Samples}/12)";
    }

    private void UpdateRaceControl(RaceControlWidgetState state)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        RaceAttentionText.Text = !state.Available
            ? "NO DATA"
            : state.RequiresAttention ? "ATTENTION" : "CLEAR";
        RaceAttentionText.Foreground = state.RequiresAttention
            ? Brush(palette.Critical)
            : Brush(palette.Positive);
        RacePenaltyText.Text = state.PenaltyStatus;
        RacePenaltyText.Foreground = state.OutstandingPenalties > 0
            ? Brush(palette.Critical)
            : Brush(palette.PrimaryText);
        RacePitLapText.Text = $"{state.PitStatus} · {state.LapStatus}";
        RaceDamageText.Text = state.DamageStatus == "OK"
            ? "OK"
            : $"{state.DamageStatus} · {state.ImpactStatus}";
        RaceDamageText.Foreground = state.HasCriticalDamage
            ? Brush(palette.Critical)
            : state.RequiresAttention
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
        RaceFlagText.Text = $"FLAG {state.FlagStatus}";
        RaceSystemsText.Text = state.SystemsStatus;
        RaceControlHeader.Background = state.HasCriticalDamage
            ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Critical, 0.55))
            : state.RequiresAttention
                ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Attention, 0.55))
                : Brush(palette.Card);
    }

    private void UpdatePriorityAlert(
        DashboardWidgetState dashboard,
        SessionFlagsWidgetState session,
        FuelStrategyWidgetState fuel,
        RaceControlWidgetState raceControl)
    {
        if (!_profile.Settings.ShowPriorityAlerts || !_profile.PriorityAlert.Visible)
        {
            PriorityAlert.Visibility = Visibility.Collapsed;
            return;
        }

        string T(OverlayTextKey key) => OverlayText.Get(_profile.Settings.Language, key);
        (OverlayAlertSeverity Severity, string Icon, string Text, string Detail)? alert = null;
        if (dashboard.Available)
        {
            var hottestTire = new[]
            {
                dashboard.TireTemperatures.FrontLeftCelsius,
                dashboard.TireTemperatures.FrontRightCelsius,
                dashboard.TireTemperatures.RearLeftCelsius,
                dashboard.TireTemperatures.RearRightCelsius,
            }.Max();
            var maximumWear = 1 - new[]
            {
                dashboard.TireWear.FrontLeftFraction,
                dashboard.TireWear.FrontRightFraction,
                dashboard.TireWear.RearLeftFraction,
                dashboard.TireWear.RearRightFraction,
            }.Min();
            maximumWear = Math.Clamp(maximumWear, 0, 1);

            alert = raceControl.HasCriticalDamage
                ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.CriticalDamage), raceControl.DamageStatus)
                : raceControl.OutstandingPenalties > 0
                    ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.Penalty), raceControl.PenaltyStatus)
                    : session.FlagName == "RED"
                        ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.RedFlag), T(OverlayTextKey.SessionStopped))
                        : fuel.Available && !fuel.Learning && fuel.Status == "SHORT"
                            ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.EnergyShortfall), fuel.PlanSummary)
                            : TireTemperatureClassifier.Classify(hottestTire) == TireTemperatureBand.Critical
                                ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.TireTemperature), $"{T(OverlayTextKey.Hottest)} {hottestTire:0}°C")
                                : maximumWear >= _profile.Settings.TireWearLimitPercent / 100
                                    ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.TireWear), $"{T(OverlayTextKey.Maximum)} {maximumWear:P0}")
                                    : session.FlagName == "YELLOW"
                                        ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.YellowFlag), T(OverlayTextKey.NoSafetyCarAssumption))
                                        : session.RainIntensity >= 0.02
                                            ? (OverlayAlertSeverity.Attention, "☂", session.WeatherName, $"RAIN {session.RainIntensity:P0}")
                                            : dashboard.SpeedLimiterActive
                                                ? (OverlayAlertSeverity.Information, "P", T(OverlayTextKey.PitLimiter), T(OverlayTextKey.Active))
                                                : null;
        }

        if (alert is null && IsEditMode)
        {
            alert = (
                OverlayAlertSeverity.Information,
                "i",
                T(OverlayTextKey.PriorityAlert),
                "FULL PUSH · TYRES · ENERGY");
        }

        if (alert is null)
        {
            PriorityAlert.Visibility = Visibility.Collapsed;
            return;
        }

        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var color = alert.Value.Severity switch
        {
            OverlayAlertSeverity.Critical => palette.Critical,
            OverlayAlertSeverity.Attention => palette.Attention,
            _ => palette.Information,
        };
        PriorityAlertIcon.Text = alert.Value.Icon;
        PriorityAlertText.Text = alert.Value.Text;
        PriorityAlertDetail.Text = alert.Value.Detail;
        PriorityAlert.BorderBrush = new System.Windows.Media.SolidColorBrush(color);
        PriorityAlert.Background = new System.Windows.Media.SolidColorBrush(
            OverlayVisualSystem.WithOpacity(palette.Background, 0.96));
        PriorityAlert.Visibility = Visibility.Visible;
    }
}
