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
    public void UpdateRuntimeHealth(TelemetryRuntimeHealth health) =>
        _runtimeHealth = health;

    public void UpdatePresentationHealth(DesktopPresentationHealth health) =>
        _presentationHealth = health;

    public void ExportDiagnostics(string destinationPath) =>
        DiagnosticsReportWriter.Write(
            destinationPath,
            _lastSnapshot,
            _runtimeHealth,
            _presentationHealth,
            _profile,
            ActiveProfileName);

    public void CapturePng(string destinationPath, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        OverlayCanvas.Width = width;
        OverlayCanvas.Height = height;
        OverlayCanvas.Measure(new System.Windows.Size(width, height));
        OverlayCanvas.Arrange(new Rect(0, 0, width, height));
        ApplyProfile();
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(OverlayCanvas);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(destinationPath);
        encoder.Save(stream);
    }

    public void SwitchProfile(string name)
    {
        _profile = _layoutStore.Switch(name);
        ApplyProfile();
    }

    public void CreateProfile(string name, bool duplicateCurrent)
    {
        _profile = _layoutStore.Create(
            name,
            duplicateCurrent ? _profile : LayoutProfile.Default);
        ApplyProfile();
    }

    public void RenameProfile(string newName) =>
        _layoutStore.Rename(ActiveProfileName, newName);

    public void DeleteActiveProfile()
    {
        _profile = _layoutStore.Delete(ActiveProfileName);
        ApplyProfile();
    }

    public void ExportActiveProfile(string destinationPath) =>
        _layoutStore.Export(ActiveProfileName, destinationPath);

    public void ImportProfile(string sourcePath)
    {
        var importedName = _layoutStore.Import(sourcePath);
        _profile = _layoutStore.Switch(importedName);
        ApplyProfile();
    }

    public void ApplyPreset(string name)
    {
        _profile = LayoutPresets.Create(name);
        ApplyProfile();
        _layoutStore.Save(_profile);
    }
}
