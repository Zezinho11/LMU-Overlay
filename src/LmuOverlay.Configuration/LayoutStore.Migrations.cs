using System.IO;
using System.Text.Json;

namespace LmuOverlay.Configuration;

public sealed partial class LayoutStore
{
    private static LayoutProfile Sanitize(LayoutProfile profile)
    {
        var diagnostic = SanitizePlacement(profile.Diagnostic);
        if (profile.SchemaVersion < 10 &&
            diagnostic.Width <= 0.23 && diagnostic.Height <= 0.20)
        {
            diagnostic = diagnostic with
            {
                Width = 0.38,
                Height = 0.40,
                Opacity = Math.Max(0.96, diagnostic.Opacity),
            };
        }

        var liveStandings = SanitizePlacement(profile.LiveStandings);
        if (profile.SchemaVersion < 5 &&
            Math.Abs(liveStandings.Width - 0.25) < 0.001)
        {
            liveStandings = liveStandings with
            {
                X = Math.Abs(liveStandings.X - 0.72) < 0.001
                    ? 0.81
                    : liveStandings.X,
                Width = 0.16,
                Opacity = Math.Max(0.96, liveStandings.Opacity),
            };
        }

        var fuelStrategy = SanitizePlacement(profile.FuelStrategy);
        if (profile.SchemaVersion < 6 &&
            Math.Abs(fuelStrategy.Width - 0.22) < 0.001 &&
            Math.Abs(fuelStrategy.Height - 0.16) < 0.001)
        {
            fuelStrategy = fuelStrategy with
            {
                Width = 0.30,
                Height = 0.25,
                Opacity = Math.Max(0.96, fuelStrategy.Opacity),
            };
        }

        var relative = SanitizePlacement(profile.Relative);
        if (profile.SchemaVersion < 7 &&
            Math.Abs(relative.Width - 0.28) < 0.001 &&
            Math.Abs(relative.Height - 0.28) < 0.001)
        {
            relative = relative with
            {
                X = 0.64,
                Y = 0.05,
                Width = 0.16,
                Height = 0.40,
                Opacity = Math.Max(0.96, relative.Opacity),
            };
        }

        if (profile.SchemaVersion < 17)
        {
            liveStandings = ExpandTimingPanel(liveStandings);
            relative = ExpandTimingPanel(relative);
            if (Overlaps(liveStandings, relative))
            {
                relative = relative with
                {
                    X = Math.Max(0, liveStandings.X - relative.Width - 0.01),
                };
            }
        }

        var sessionFlags = SanitizePlacement(profile.SessionFlags);
        if (profile.SchemaVersion < 8 &&
            Math.Abs(sessionFlags.Width - 0.28) < 0.001 &&
            Math.Abs(sessionFlags.Height - 0.12) < 0.001)
        {
            sessionFlags = sessionFlags with
            {
                X = 0.33,
                Width = 0.30,
                Height = 0.18,
                Opacity = Math.Max(0.96, sessionFlags.Opacity),
            };
        }

        var inputs = SanitizePlacement(profile.Inputs);
        if (profile.SchemaVersion < 15 &&
            Math.Abs(inputs.X - 0.025) < 0.001 &&
            Math.Abs(inputs.Y - 0.47) < 0.001)
        {
            inputs = inputs with { Y = 0.66 };
        }

        var settings = SanitizeSettings(profile.Settings);
        if (profile.SchemaVersion < 16 && settings.RefreshRateHz <= 60)
        {
            settings = settings with { RefreshRateHz = 120 };
        }

        return profile with
        {
            SchemaVersion = LayoutProfile.CurrentSchemaVersion,
            Diagnostic = diagnostic,
            Inputs = inputs,
            LiveStandings = liveStandings,
            Relative = relative,
            SessionFlags = sessionFlags,
            FuelStrategy = fuelStrategy,
            RaceControl = SanitizePlacement(profile.RaceControl),
            Settings = settings,
        };
    }

    private static WidgetPlacement ExpandTimingPanel(WidgetPlacement placement)
    {
        if (placement.Width > 0.20 || placement.Height < 0.34)
        {
            return placement;
        }

        const double expandedWidth = 0.28;
        var x = placement.X >= 0.5
            ? placement.X + placement.Width - expandedWidth
            : placement.X;
        return placement with
        {
            X = Math.Clamp(x, 0, 1 - expandedWidth),
            Width = expandedWidth,
            Height = Math.Max(0.40, placement.Height),
        };
    }

    private static bool Overlaps(WidgetPlacement first, WidgetPlacement second) =>
        first.X < second.X + second.Width &&
        first.X + first.Width > second.X &&
        first.Y < second.Y + second.Height &&
        first.Y + first.Height > second.Y;
}
