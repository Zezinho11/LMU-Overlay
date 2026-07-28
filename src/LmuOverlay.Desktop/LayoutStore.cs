using System.IO;
using System.Text.Json;

namespace LmuOverlay.Desktop;

public sealed class LayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public LayoutStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LMU Overlay",
            "layout.json");
    }

    public LayoutProfile Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return LayoutProfile.Default;
            }

            var profile = JsonSerializer.Deserialize<LayoutProfile>(
                File.ReadAllText(_path),
                JsonOptions);
            return profile is { SchemaVersion: LayoutProfile.CurrentSchemaVersion }
                ? Sanitize(profile)
                : LayoutProfile.Default;
        }
        catch (JsonException)
        {
            return LayoutProfile.Default;
        }
        catch (IOException)
        {
            return LayoutProfile.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return LayoutProfile.Default;
        }
    }

    public void Save(LayoutProfile profile)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Sanitize(profile), JsonOptions));
        File.Move(temporaryPath, _path, true);
    }

    private static LayoutProfile Sanitize(LayoutProfile profile)
    {
        var item = profile.Diagnostic;
        return profile with
        {
            Diagnostic = item with
            {
                X = Math.Clamp(item.X, 0, 0.95),
                Y = Math.Clamp(item.Y, 0, 0.95),
                Width = Math.Clamp(item.Width, 0.08, 1),
                Height = Math.Clamp(item.Height, 0.08, 1),
                Scale = Math.Clamp(item.Scale, 0.5, 2),
                Opacity = Math.Clamp(item.Opacity, 0.2, 1),
            },
        };
    }
}
