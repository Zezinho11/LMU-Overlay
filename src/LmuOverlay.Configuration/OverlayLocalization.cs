namespace LmuOverlay.Configuration;

public enum OverlayTextKey
{
    Language, PortugueseBrazil, English, Theme, Dashboard, DriverInputs,
    LiveStandings, Relative, FuelAndEnergy, SessionWeather, RaceControl,
    Current, Last, Best, Optimal, Position, Lap, Delta, Fuel, VirtualEnergy,
    BrakeBias, Rpm, Sectors, TyreTempWear, Telemetry, Throttle, Brake, Clutch,
    Steering, Compound, Manufacturer, Number, Driver, LastLap, Interval, Gap,
    Resource, UsagePerLap, Range, Time, ToFinish, NeedMargin, Strategy,
    Session, Flag, Grip, Weather, Air, Track, Rain, Wetness, Penalty,
    Pit, Leader, Waiting, NoData, Clear, Attention, Short, Good,
    ConfigureWidgets, EditLayout, LockOverlay, UnlockOverlay, RestoreLayout,
    Exit, Connected, Disconnected, Profile, Save, Cancel, Settings, Edit, Editing, Lock, Locked, RefreshRate,
    BackgroundOpacity, VisualDensity, PriorityAlerts, ReduceMotion,
    Practice, Qualifying, Race, Green, Yellow, Red, PitLimiter, Active,
    TireTemperature, TireWear, PriorityAlert, Systems, Damage, CriticalDamage,
    RedFlag, SessionStopped, EnergyShortfall, Hottest, Maximum, YellowFlag,
    NoSafetyCarAssumption
}

public static class OverlayText
{
    public const string PortugueseBrazil = "pt-BR";
    public const string EnglishUnitedStates = "en-US";

    private static readonly IReadOnlyDictionary<OverlayTextKey, (string Pt, string En)> Texts =
        new Dictionary<OverlayTextKey, (string, string)>
        {
            [OverlayTextKey.Language] = ("Idioma", "Language"),
            [OverlayTextKey.PortugueseBrazil] = ("Português (Brasil)", "Portuguese (Brazil)"),
            [OverlayTextKey.English] = ("Inglês", "English"),
            [OverlayTextKey.Theme] = ("Tema", "Theme"),
            [OverlayTextKey.Dashboard] = ("Dashboard", "Dashboard"),
            [OverlayTextKey.DriverInputs] = ("Controles do piloto", "Driver inputs"),
            [OverlayTextKey.LiveStandings] = ("Classificação ao vivo", "Live standings"),
            [OverlayTextKey.Relative] = ("Relativo", "Relative"),
            [OverlayTextKey.FuelAndEnergy] = ("Combustível e energia virtual", "Fuel & virtual energy"),
            [OverlayTextKey.SessionWeather] = ("Sessão e clima", "Session & weather"),
            [OverlayTextKey.RaceControl] = ("Direção de prova", "Race control"),
            [OverlayTextKey.Current] = ("ATUAL", "CURRENT"),
            [OverlayTextKey.Last] = ("ÚLTIMA", "LAST"),
            [OverlayTextKey.Best] = ("MELHOR", "BEST"),
            [OverlayTextKey.Optimal] = ("ÓTIMA", "OPTIMAL"),
            [OverlayTextKey.Position] = ("POS", "POS"),
            [OverlayTextKey.Lap] = ("VOLTA", "LAP"),
            [OverlayTextKey.Delta] = ("DELTA", "DELTA"),
            [OverlayTextKey.Fuel] = ("COMBUSTÍVEL", "FUEL"),
            [OverlayTextKey.VirtualEnergy] = ("ENERGIA VIRTUAL", "VIRTUAL ENERGY"),
            [OverlayTextKey.BrakeBias] = ("BALANÇO DE FREIO", "BRAKE BIAS"),
            [OverlayTextKey.Rpm] = ("RPM", "RPM"),
            [OverlayTextKey.Sectors] = ("SETORES", "SECTORS"),
            [OverlayTextKey.TyreTempWear] = ("TEMP. / DESGASTE DOS PNEUS", "TYRE TEMP / WEAR"),
            [OverlayTextKey.Telemetry] = ("TELEMETRIA", "TELEMETRY"),
            [OverlayTextKey.Throttle] = ("ACEL", "THR"),
            [OverlayTextKey.Brake] = ("FREIO", "BRK"),
            [OverlayTextKey.Clutch] = ("EMBR", "CLU"),
            [OverlayTextKey.Steering] = ("VOLANTE", "STEERING"),
            [OverlayTextKey.Compound] = ("COMPOSTO", "COMPOUND"),
            [OverlayTextKey.Manufacturer] = ("MFR", "MFR"),
            [OverlayTextKey.Number] = ("Nº", "NO."),
            [OverlayTextKey.Driver] = ("PIL", "DRV"),
            [OverlayTextKey.LastLap] = ("ÚLTIMA VOLTA", "LAST LAP"),
            [OverlayTextKey.Interval] = ("INTERVALO", "INTERVAL"),
            [OverlayTextKey.Gap] = ("DIF.", "GAP"),
            [OverlayTextKey.Resource] = ("RECURSO", "RESOURCE"),
            [OverlayTextKey.UsagePerLap] = ("USO / VOLTA", "USAGE / LAP"),
            [OverlayTextKey.Range] = ("AUTONOMIA", "RANGE"),
            [OverlayTextKey.Time] = ("TEMPO", "TIME"),
            [OverlayTextKey.ToFinish] = ("ATÉ O FINAL", "TO FINISH"),
            [OverlayTextKey.NeedMargin] = ("NECESSÁRIO / MARGEM", "NEED / MARGIN"),
            [OverlayTextKey.Strategy] = ("ESTRATÉGIA", "STRATEGY"),
            [OverlayTextKey.Session] = ("SESSÃO", "SESSION"),
            [OverlayTextKey.Flag] = ("BANDEIRA", "FLAG"),
            [OverlayTextKey.Grip] = ("ADERÊNCIA", "GRIP"),
            [OverlayTextKey.Weather] = ("CLIMA", "WEATHER"),
            [OverlayTextKey.Air] = ("AR", "AIR"),
            [OverlayTextKey.Track] = ("PISTA", "TRACK"),
            [OverlayTextKey.Rain] = ("CHUVA", "RAIN"),
            [OverlayTextKey.Wetness] = ("UMIDADE", "WETNESS"),
            [OverlayTextKey.Penalty] = ("PENALIDADE", "PENALTY"),
            [OverlayTextKey.Pit] = ("PIT", "PIT"),
            [OverlayTextKey.Leader] = ("LÍDER", "LEADER"),
            [OverlayTextKey.Waiting] = ("AGUARDANDO", "WAITING"),
            [OverlayTextKey.NoData] = ("SEM DADOS", "NO DATA"),
            [OverlayTextKey.Clear] = ("LIVRE", "CLEAR"),
            [OverlayTextKey.Attention] = ("ATENÇÃO", "ATTENTION"),
            [OverlayTextKey.Short] = ("INSUFICIENTE", "SHORT"),
            [OverlayTextKey.Good] = ("OK", "GOOD"),
            [OverlayTextKey.ConfigureWidgets] = ("Configurar widgets", "Configure widgets"),
            [OverlayTextKey.EditLayout] = ("Editar layout", "Edit layout"),
            [OverlayTextKey.LockOverlay] = ("Travar overlay", "Lock overlay"),
            [OverlayTextKey.UnlockOverlay] = ("Destravar overlay", "Unlock overlay"),
            [OverlayTextKey.RestoreLayout] = ("Restaurar layout", "Restore layout"),
            [OverlayTextKey.Exit] = ("Sair", "Exit"),
            [OverlayTextKey.Connected] = ("CONECTADO", "CONNECTED"),
            [OverlayTextKey.Disconnected] = ("DESCONECTADO", "DISCONNECTED"),
            [OverlayTextKey.Profile] = ("Perfil", "Profile"),
            [OverlayTextKey.Save] = ("Salvar", "Save"),
            [OverlayTextKey.Cancel] = ("Cancelar", "Cancel"),
            [OverlayTextKey.Settings] = ("AJUSTES", "SETTINGS"),
            [OverlayTextKey.Edit] = ("EDITAR", "EDIT"),
            [OverlayTextKey.Editing] = ("EDITANDO", "EDITING"),
            [OverlayTextKey.Lock] = ("TRAVAR", "LOCK"),
            [OverlayTextKey.Locked] = ("TRAVADO", "LOCKED"),
            [OverlayTextKey.RefreshRate] = ("Taxa de atualização", "Refresh rate"),
            [OverlayTextKey.BackgroundOpacity] = ("Opacidade do fundo", "Background opacity"),
            [OverlayTextKey.VisualDensity] = ("Densidade visual", "Visual density"),
            [OverlayTextKey.PriorityAlerts] = ("Alertas prioritários", "Priority alerts"),
            [OverlayTextKey.ReduceMotion] = ("Reduzir movimento", "Reduce motion"),
            [OverlayTextKey.Practice] = ("Treino", "Practice"),
            [OverlayTextKey.Qualifying] = ("Classificação", "Qualifying"),
            [OverlayTextKey.Race] = ("Corrida", "Race"),
            [OverlayTextKey.Green] = ("VERDE", "GREEN"),
            [OverlayTextKey.Yellow] = ("AMARELA", "YELLOW"),
            [OverlayTextKey.Red] = ("VERMELHA", "RED"),
            [OverlayTextKey.PitLimiter] = ("LIMITADOR DE PIT", "PIT LIMITER"),
            [OverlayTextKey.Active] = ("ATIVO", "ACTIVE"),
            [OverlayTextKey.TireTemperature] = ("Temperatura dos pneus", "Tire temperature"),
            [OverlayTextKey.TireWear] = ("Desgaste dos pneus", "Tire wear"),
            [OverlayTextKey.PriorityAlert] = ("Alerta prioritário", "Priority alert"),
            [OverlayTextKey.Systems] = ("Sistemas", "Systems"),
            [OverlayTextKey.Damage] = ("Danos", "Damage"),
            [OverlayTextKey.CriticalDamage] = ("DANOS CRÍTICOS", "CRITICAL DAMAGE"),
            [OverlayTextKey.RedFlag] = ("BANDEIRA VERMELHA", "RED FLAG"),
            [OverlayTextKey.SessionStopped] = ("SESSÃO INTERROMPIDA", "SESSION STOPPED"),
            [OverlayTextKey.EnergyShortfall] = ("ENERGIA INSUFICIENTE", "ENERGY SHORTFALL"),
            [OverlayTextKey.Hottest] = ("MAIS QUENTE", "HOTTEST"),
            [OverlayTextKey.Maximum] = ("MÁXIMO", "MAXIMUM"),
            [OverlayTextKey.YellowFlag] = ("BANDEIRA AMARELA", "YELLOW FLAG"),
            [OverlayTextKey.NoSafetyCarAssumption] = ("SEM PREVISÃO DE SAFETY CAR", "NO SAFETY-CAR ASSUMPTION"),
        };

    public static string Normalize(string? language) =>
        string.Equals(language, EnglishUnitedStates, StringComparison.OrdinalIgnoreCase)
            ? EnglishUnitedStates
            : PortugueseBrazil;

    public static string Get(string? language, OverlayTextKey key)
    {
        var value = Texts[key];
        return Normalize(language) == EnglishUnitedStates ? value.En : value.Pt;
    }

    public static string TranslateExact(string? language, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        foreach (var pair in Texts.Values)
        {
            if (string.Equals(text.Trim(), pair.Pt, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text.Trim(), pair.En, StringComparison.OrdinalIgnoreCase))
            {
                return Normalize(language) == EnglishUnitedStates ? pair.En : pair.Pt;
            }
        }
        return text;
    }
}
