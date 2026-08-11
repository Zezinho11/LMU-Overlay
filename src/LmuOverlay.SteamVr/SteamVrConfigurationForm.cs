using System.Globalization;

namespace LmuOverlay.SteamVr;

public sealed class SteamVrConfigurationForm : Form
{
    private readonly SteamVrProfileStore _store;
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
        Text = "RedFox Racing - SteamVR Layout";
        ClientSize = new Size(980, 470);
        MinimumSize = new Size(820, 390);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(10, 14, 20);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        ConfigureGrid();
        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(14, 10, 14, 4),
            ForeColor = Color.White,
            Text = "STEAMVR OVERLAYS\r\nAjuste cada painel; o host aplica o arquivo salvo em tempo real.",
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(17, 24, 34),
        };
        buttons.Controls.Add(Button("Salvar", (_, _) => SaveProfile()));
        buttons.Controls.Add(Button("Compacto", (_, _) => LoadProfile(SteamVrProfile.Compact)));
        buttons.Controls.Add(Button("Padrão", (_, _) => LoadProfile(SteamVrProfile.Default)));
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
            Name = "Widget", HeaderText = "Painel", ReadOnly = true, Width = 170,
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Visible", HeaderText = "Visível", Width = 65,
        });
        AddNumberColumn("Width", "Largura (m)", 105);
        AddNumberColumn("Distance", "Distância (m)", 115);
        AddNumberColumn("Horizontal", "Horizontal (m)", 115);
        AddNumberColumn("Vertical", "Vertical (m)", 105);
        AddNumberColumn("Opacity", "Opacidade", 95);
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
        Add("Inputs", profile.Inputs);
        Add("Live Standings", profile.LiveStandings);
        Add("Relative", profile.Relative);
        Add("Fuel & Virtual Energy", profile.FuelStrategy);
        Add("Session / Weather", profile.SessionFlags);
        Add("Race Control", profile.RaceControl);
        Add("Priority Alert", profile.PriorityAlert);
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
                "Layout SteamVR salvo. O host em execução aplicará as mudanças automaticamente.",
                "RedFox Racing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Não foi possível salvar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
}
