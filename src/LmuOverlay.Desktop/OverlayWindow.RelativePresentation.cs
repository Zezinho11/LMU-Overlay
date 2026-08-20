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
    private void UpdateRelative(RelativeWidgetState relative)
    {
        var structureKey = string.Join(
            "|",
            relative.Rows.Select(row => $"{row.CarNumber}:{row.DriverName}"));
        if (structureKey == _relativeStructureKey && relative.Rows.Count > 0)
        {
            for (var index = 0; index < relative.Rows.Count; index++)
            {
                if (RelativeRows.Children[index] is Grid grid)
                {
                    UpdateRelativeRow(grid, relative.Rows[index], index);
                }
            }

            return;
        }

        RelativeRows.Children.Clear();
        _relativeStructureKey = structureKey;
        if (relative.Rows.Count == 0)
        {
            var palette = OverlayVisualSystem.Resolve(_profile.Settings);
            RelativeRows.Children.Add(new TextBlock
            {
                Text = "WAITING FOR PLAYER",
                Foreground = Brush(palette.SecondaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Height = 40,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            ApplyTextScale(RelativeWidget, _profile.Settings.TimingTextScale);
            return;
        }

        var rowHeight = Math.Min(37, 386d / relative.Rows.Count);
        for (var index = 0; index < relative.Rows.Count; index++)
        {
            var row = CreateRelativeRow(relative.Rows[index], index);
            row.Height = rowHeight;
            RelativeRows.Children.Add(row);
        }

        ApplyTextScale(RelativeWidget, _profile.Settings.TimingTextScale);
        ApplySurfaceOpacity(
            RelativeWidget,
            _profile.Relative.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private Grid CreateRelativeRow(RelativeRowState row, int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var playerBackground = Brush(palette.PrimaryText);
        var darkText = Brush(palette.Background);
        var foreground = row.IsPlayer
            ? darkText
            : Brush(palette.PrimaryText);
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        var grid = new Grid
        {
            Height = 37,
            Background = row.IsPlayer
                ? playerBackground
                : rowIndex % 2 == 0
                    ? Brush(palette.Background)
                    : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.7)),
            ToolTip =
                $"P{row.OverallPosition} · {row.DriverName} · {row.VehicleClass}",
        };
        foreach (var width in new[] { 52d, 60d })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(width),
            });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(118),
        });

        var numberBadge = new Border
        {
            Width = 44,
            Height = 25,
            CornerRadius = new CornerRadius(3, 0, 0, 3),
            Background = classBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.OverallPosition.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                Foreground = Brush(palette.PrimaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(numberBadge, 0);
        grid.Children.Add(numberBadge);

        var classBadge = new Border
        {
            Width = 54,
            Height = 25,
            CornerRadius = new CornerRadius(0, 3, 3, 0),
            Background = Brush(palette.PrimaryText),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.ClassAbbreviation,
                Foreground = classBrush,
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(classBadge, 1);
        grid.Children.Add(classBadge);

        var driver = new TextBlock
        {
            Text = row.DriverDisplayName,
            Foreground = foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 3, 0),
        };
        Grid.SetColumn(driver, 2);
        grid.Children.Add(driver);

        var gap = new TextBlock
        {
            Text = FormatRelativeGap(row),
            Foreground = row.IsInPitLane
                ? row.IsPlayer
                    ? darkText
                    : Brush(palette.Attention)
                : foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(gap, 3);
        grid.Children.Add(gap);
        return grid;
    }

    private void UpdateRelativeRow(Grid grid, RelativeRowState row, int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var playerBackground = Brush(palette.PrimaryText);
        var darkText = Brush(palette.Background);
        var foreground = row.IsPlayer ? darkText : Brush(palette.PrimaryText);
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        grid.Background = row.IsPlayer
            ? playerBackground
            : rowIndex % 2 == 0
                ? Brush(palette.Background)
                : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.7));
        grid.ToolTip = $"P{row.OverallPosition} · {row.DriverName} · {row.VehicleClass}";
        var badges = grid.Children.OfType<Border>().ToArray();
        if (badges.Length >= 2)
        {
            badges[0].Background = classBrush;
            if (badges[0].Child is TextBlock number)
            {
                number.Text = row.OverallPosition.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            if (badges[1].Child is TextBlock className)
            {
                className.Text = row.ClassAbbreviation;
                className.Foreground = classBrush;
            }
        }

        var texts = grid.Children.OfType<TextBlock>().ToArray();
        if (texts.Length >= 2)
        {
            texts[0].Text = row.DriverDisplayName;
            texts[0].Foreground = foreground;
            texts[1].Text = FormatRelativeGap(row);
            texts[1].Foreground = row.IsInPitLane && !row.IsPlayer
                ? Brush(palette.Attention)
                : foreground;
        }
    }

    private static string FormatRelativeGap(RelativeRowState row)
    {
        if (row.IsInPitLane)
        {
            return "PIT";
        }

        return row.RelativeLaps switch
        {
            > 0 => $"+{row.RelativeLaps}L",
            < 0 => $"{row.RelativeLaps}L",
            _ when row.IsPlayer => "0.0",
            _ when double.IsFinite(row.RelativeGapSeconds) =>
                row.RelativeGapSeconds.ToString("+0.0;-0.0;0.0"),
            _ => "--.-",
        };
    }

    private static System.Windows.Media.Brush RelativeClassBrush(
        string classAbbreviation) =>
        classAbbreviation switch
        {
            "GT3" => Brush(0, 225, 112),
            "HYP" => Brush(244, 32, 55),
            "P2" => Brush(45, 123, 225),
            _ => Brush(125, 141, 160),
        };
}
