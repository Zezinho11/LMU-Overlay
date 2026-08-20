using System.Globalization;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public sealed class SteamVrConfigurationForm : Form
{
    private readonly SteamVrProfileStore _store;
    private readonly string _language;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        BackgroundColor = Color.FromArgb(10, 14, 20),
        BorderStyle = BorderStyle.None,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    public SteamVrConfigurationForm(SteamVrProfileStore store)
    {
        _store = store;
        _language = new DesktopProfileSettingsReader().Load().Language;
        Text = L("RedFox Racing - Layout SteamVR", "RedFox Racing - SteamVR layout");
        ClientSize = new Size(980, 470);
        MinimumSize = new Size(820, 390);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(10, 14, 20);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        ConfigureGrid();
        var runtime = LmuOverlay.Core.VrRuntimeProbe.Detect();
        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(14, 10, 14, 4),
            ForeColor = Color.White,
            Text = L(
                $"OVERLAYS STEAMVR · {runtime.Detail}\r\nAjuste cada painel; o host aplica o arquivo salvo em tempo real.",
                $"STEAMVR OVERLAYS · {runtime.Detail}\r\nAdjust each panel; the host applies the saved file in real time."),
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(17, 24, 34),
        };
        buttons.Controls.Add(Button(OverlayText.Get(_language, OverlayTextKey.Save), (_, _) => SaveProfile()));
        buttons.Controls.Add(Button(L("Exportar diagnóstico", "Export diagnostics"), (_, _) => ExportDiagnostics()));
        buttons.Controls.Add(Button(L("Compacto", "Compact"), (_, _) => LoadProfile(SteamVrProfile.Compact)));
        buttons.Controls.Add(Button(L("Padrão", "Default"), (_, _) => LoadProfile(SteamVrProfile.Default)));
        Controls.Add(_grid);
        Controls.Add(header);
        Controls.Add(buttons);
        LoadProfile(_store.Load());
    }

    private void ConfigureGrid()
    {
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(27, 36, 50);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(199, 213, 225);
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(13, 19, 28);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(15, 118, 110);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.GridColor = Color.FromArgb(49, 61, 75);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Widget", HeaderText = L("Painel", "Panel"), ReadOnly = true, Width = 170,
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Visible", HeaderText = L("Visível", "Visible"), Width = 65,
        });
        AddNumberColumn("Width", L("Largura (m)", "Width (m)"), 105);
        AddNumberColumn("Distance", L("Distância (m)", "Distance (m)"), 115);
        AddNumberColumn("Horizontal", "Horizontal (m)", 115);
        AddNumberColumn("Vertical", "Vertical (m)", 105);
        AddNumberColumn("Opacity", L("Opacidade", "Opacity"), 95);
    }

    private void AddNumberColumn(string name, string header, int width) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            ValueType = typeof(float),
        });

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(15, 118, 110),
            ForeColor = Color.White,
            Margin = new Padding(5, 2, 5, 2),
        };
        button.Click += click;
        return button;
    }

    private void LoadProfile(SteamVrProfile profile)
    {
        _grid.Rows.Clear();
        Add("Dashboard", profile.Dashboard);
        Add(OverlayText.Get(_language, OverlayTextKey.DriverInputs), profile.Inputs);
        Add(OverlayText.Get(_language, OverlayTextKey.LiveStandings), profile.LiveStandings);
        Add(OverlayText.Get(_language, OverlayTextKey.Relative), profile.Relative);
        Add(OverlayText.Get(_language, OverlayTextKey.FuelAndEnergy), profile.FuelStrategy);
        Add(OverlayText.Get(_language, OverlayTextKey.SessionWeather), profile.SessionFlags);
        Add(OverlayText.Get(_language, OverlayTextKey.RaceControl), profile.RaceControl);
        Add(OverlayText.Get(_language, OverlayTextKey.PriorityAlert), profile.PriorityAlert);
    }

    private void Add(string name, SteamVrWidgetPlacement placement) =>
        _grid.Rows.Add(
            name,
            placement.Visible,
            Number(placement.WidthMeters),
            Number(placement.DistanceMeters),
            Number(placement.HorizontalOffsetMeters),
            Number(placement.VerticalOffsetMeters),
            Number(placement.Opacity));

    private void SaveProfile()
    {
        try
        {
            var original = _store.Load();
            var profile = original with
            {
                Dashboard = Read(0, original.Dashboard),
                Inputs = Read(1, original.Inputs),
                LiveStandings = Read(2, original.LiveStandings),
                Relative = Read(3, original.Relative),
                FuelStrategy = Read(4, original.FuelStrategy),
                SessionFlags = Read(5, original.SessionFlags),
                RaceControl = Read(6, original.RaceControl),
                PriorityAlert = Read(7, original.PriorityAlert),
            };
            _store.Save(profile);
            MessageBox.Show(
                this,
                L("Layout SteamVR salvo. O host em execução aplicará as mudanças automaticamente.",
                    "SteamVR layout saved. The running host will apply changes automatically."),
                "RedFox Racing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, L("Não foi possível salvar", "Unable to save"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportDiagnostics()
    {
        using var dialog = new SaveFileDialog
        {
            Title = L("Exportar diagnóstico SteamVR", "Export SteamVR diagnostics"),
            Filter = "JSON (*.json)|*.json",
            FileName = $"lmu-overlay-steamvr-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            AddExtension = true,
            DefaultExt = "json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var written = SteamVrDiagnosticsWriter.TryWrite(
            dialog.FileName,
            new LmuOverlay.Core.TelemetryRuntimeHealth(
                0, 0, 0, 0, 0, 0, null, "Configuration-only report."),
            new(false, 0, null, "Configuration-only report; start the host for live health."),
            _store.Load());
        MessageBox.Show(this,
            written ? L("Diagnóstico exportado.", "Diagnostics exported.")
                    : L("Não foi possível exportar o diagnóstico.", "Unable to export diagnostics."),
            "RedFox Racing", MessageBoxButtons.OK,
            written ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private SteamVrWidgetPlacement Read(int rowIndex, SteamVrWidgetPlacement fallback)
    {
        var row = _grid.Rows[rowIndex];
        return new SteamVrWidgetPlacement(
            Convert.ToBoolean(row.Cells["Visible"].Value, CultureInfo.InvariantCulture),
            Parse(row, "Width", fallback.WidthMeters),
            Parse(row, "Distance", fallback.DistanceMeters),
            Parse(row, "Vertical", fallback.VerticalOffsetMeters),
            Parse(row, "Horizontal", fallback.HorizontalOffsetMeters),
            Parse(row, "Opacity", fallback.Opacity)).Sanitize();
    }

    private static float Parse(DataGridViewRow row, string column, float fallback)
    {
        var text = Convert.ToString(row.Cells[column].Value, CultureInfo.CurrentCulture);
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
            return current;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        return fallback;
    }

    private static string Number(float value) =>
        value.ToString("0.00", CultureInfo.CurrentCulture);

    private string L(string portuguese, string english) =>
        OverlayText.Normalize(_language) == OverlayText.EnglishUnitedStates ? english : portuguese;
}
