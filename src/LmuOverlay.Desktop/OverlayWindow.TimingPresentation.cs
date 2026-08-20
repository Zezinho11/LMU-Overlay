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
    private void UpdateStandings(LiveStandingsWidgetState standings)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        SetText(StandingsSessionText, OverlayText.TranslateExact(_profile.Settings.Language, standings.SessionName));
        SetText(StandingsClockText, FormatSessionTime(standings.SessionRemainingSeconds));
        SetText(
            StandingsLapHeading,
            OverlayText.Get(
                _profile.Settings.Language,
                standings.IsQualifying ? OverlayTextKey.Best : OverlayTextKey.LastLap));
        var structureKey = string.Join(
            "|",
            standings.Classes.Select(category =>
                $"{category.ClassName}:{string.Join(',', category.Rows.Select(row => row.CarNumber))}"));
        if (structureKey == _standingsStructureKey)
        {
            var childIndex = 0;
            foreach (var category in standings.Classes)
            {
                if (StandingsRows.Children[childIndex++] is Border header)
                {
                    header.Background = category.IsPlayerClass
                        ? Brush(palette.Critical)
                        : Brush(palette.Card);
                    if (header.Child is TextBlock label)
                    {
                        label.Text = category.ClassName.ToUpperInvariant();
                    }
                }

                for (var rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
                {
                    if (StandingsRows.Children[childIndex++] is Grid rowGrid)
                    {
                        UpdateStandingsRow(rowGrid, category.Rows[rowIndex], rowIndex);
                    }
                }
            }

            return;
        }

        StandingsRows.Children.Clear();
        _standingsStructureKey = structureKey;

        foreach (var category in standings.Classes)
        {
            StandingsRows.Children.Add(new Border
            {
                Height = 18,
                Background = category.IsPlayerClass
                    ? Brush(palette.Critical)
                    : Brush(palette.Card),
                Child = new TextBlock
                {
                    Text = category.ClassName.ToUpperInvariant(),
                    Foreground = Brush(palette.PrimaryText),
                    FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 9,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(9, 0, 0, 0),
                },
            });

            for (var rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
            {
                StandingsRows.Children.Add(CreateStandingsRow(
                    category.Rows[rowIndex],
                    rowIndex));
            }
        }

        ApplyTextScale(LiveStandingsWidget, _profile.Settings.TimingTextScale);
        ApplySurfaceOpacity(
            LiveStandingsWidget,
            _profile.LiveStandings.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private Grid CreateStandingsRow(
        LiveStandingsRowState row,
        int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var grid = new Grid
        {
            Height = 25,
            Background = row.IsPlayer
                ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Accent, 0.55))
                : rowIndex % 2 == 0
                    ? Brush(palette.Background)
                    : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.55)),
            ToolTip = $"{row.DriverName} · {row.VehicleName}",
        };
        foreach (var width in new[] { 36d, 50d, 40d, 62d, 104d, 76d })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(width),
            });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddStandingsText(
            grid,
            row.ClassPosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            0,
            row.ClassPosition == 1
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText),
            FontWeights.Bold,
            11);
        var manufacturerBadge = new Border
        {
            Background = CarIconBrush(row.VehicleModel),
            Width = 42,
            Height = 19,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ManufacturerAbbreviation(row.VehicleModel),
                Foreground = Brush(palette.PrimaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(manufacturerBadge, 1);
        grid.Children.Add(manufacturerBadge);
        AddStandingsText(
            grid,
            row.CarNumber,
            2,
            Brush(palette.PrimaryText),
            FontWeights.Bold,
            9);
        AddStandingsText(
            grid,
            row.DriverAbbreviation,
            3,
            Brush(palette.PrimaryText),
            FontWeights.Bold,
            10);
        AddStandingsText(
            grid,
            FormatLapTime(row.LastLapTimeSeconds),
            4,
            Brush(palette.PrimaryText),
            FontWeights.SemiBold,
            9);
        AddStandingsText(
            grid,
            FormatStandingsInterval(row),
            5,
            row.IsInPitLane && !row.IsQualifying
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText),
            FontWeights.SemiBold,
            10,
            System.Windows.HorizontalAlignment.Right,
            new Thickness(0, 0, 4, 0));
        AddStandingsText(
            grid,
            FormatTireEnergy(row),
            6,
            row.IsInPitLane
                ? Brush(palette.Attention)
                : TireCompoundBrush(row.TireCompound),
            FontWeights.Bold,
            9,
            System.Windows.HorizontalAlignment.Center);
        return grid;
    }

    private void UpdateStandingsRow(
        Grid grid,
        LiveStandingsRowState row,
        int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        grid.Background = row.IsPlayer
            ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Accent, 0.55))
            : rowIndex % 2 == 0
                ? Brush(palette.Background)
                : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.55));
        grid.ToolTip = $"{row.DriverName} · {row.VehicleName}";
        var texts = grid.Children.OfType<TextBlock>().ToArray();
        if (texts.Length >= 6)
        {
            texts[0].Text = row.ClassPosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            texts[0].Foreground = row.ClassPosition == 1
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
            texts[1].Text = row.CarNumber;
            texts[2].Text = row.DriverAbbreviation;
            texts[3].Text = FormatLapTime(row.LastLapTimeSeconds);
            texts[4].Text = FormatStandingsInterval(row);
            texts[4].Foreground = row.IsInPitLane && !row.IsQualifying
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
            texts[5].Text = FormatTireEnergy(row);
            texts[5].Foreground = row.IsInPitLane
                ? Brush(palette.Attention)
                : TireCompoundBrush(row.TireCompound);
        }

        var badge = grid.Children.OfType<Border>().FirstOrDefault();
        if (badge is not null)
        {
            badge.Background = CarIconBrush(row.VehicleModel);
            if (badge.Child is TextBlock badgeText)
            {
                badgeText.Text = ManufacturerAbbreviation(row.VehicleModel);
            }
        }
    }

    private static void AddStandingsText(
        Grid grid,
        string text,
        int column,
        System.Windows.Media.Brush foreground,
        FontWeight fontWeight,
        double fontSize,
        System.Windows.HorizontalAlignment alignment =
            System.Windows.HorizontalAlignment.Center,
        Thickness? margin = null)
    {
        var element = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = fontWeight,
            FontSize = fontSize,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin ?? new Thickness(0),
        };
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static string FormatStandingsInterval(LiveStandingsRowState row)
    {
        if (row.IsQualifying)
        {
            return row.ClassPosition == 1
                ? "LEADER"
                : row.IntervalSeconds >= 0 && double.IsFinite(row.IntervalSeconds)
                    ? $"+{row.IntervalSeconds:0.000}"
                    : "--.---";
        }

        if (row.IsInPitLane)
        {
            return "PIT";
        }

        if (row.ClassPosition == 1)
        {
            return "LEADER";
        }

        if (row.IntervalLaps > 0)
        {
            return $"+{row.IntervalLaps} L";
        }

        return row.IntervalSeconds > 0 && double.IsFinite(row.IntervalSeconds)
            ? $"+{row.IntervalSeconds:0.000}"
            : "--.---";
    }

    private static string FormatTireEnergy(LiveStandingsRowState row)
    {
        if (row.IsInPitLane)
        {
            return "PIT";
        }

        var compound = TireCompoundCode(row.TireCompound);
        var energy = row.VirtualEnergyFraction is >= 0 and <= 1 &&
                     double.IsFinite(row.VirtualEnergyFraction)
            ? $"{row.VirtualEnergyFraction:P0}"
            : "--%";
        return $"{compound}  |  {energy}";
    }

    private static string TireCompoundCode(string compound)
    {
        var value = compound.Trim().ToUpperInvariant();
        return value switch
        {
            _ when value.Contains("SOFT", StringComparison.Ordinal) => "S",
            _ when value.Contains("MED", StringComparison.Ordinal) => "M",
            _ when value.Contains("HARD", StringComparison.Ordinal) => "H",
            _ when value.Contains("WET", StringComparison.Ordinal) => "W",
            _ when value.Contains("INTER", StringComparison.Ordinal) => "I",
            { Length: > 0 } => value[..Math.Min(3, value.Length)],
            _ => "--",
        };
    }

    private static System.Windows.Media.Brush TireCompoundBrush(string compound)
    {
        var code = TireCompoundCode(compound);
        return code switch
        {
            "S" => System.Windows.Media.Brushes.Red,
            "M" => System.Windows.Media.Brushes.Gold,
            "H" => System.Windows.Media.Brushes.White,
            "W" => System.Windows.Media.Brushes.DeepSkyBlue,
            "I" => System.Windows.Media.Brushes.LimeGreen,
            _ => System.Windows.Media.Brushes.Gainsboro,
        };
    }

    private static System.Windows.Media.Brush CarIconBrush(string vehicleName)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter
            .ConvertFromString(VehicleCatalog.Resolve(vehicleName).Color)!;
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string ManufacturerAbbreviation(string vehicleName)
    {
        return VehicleCatalog.Resolve(vehicleName).Code;
    }

}
