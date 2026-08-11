using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

internal static class OverlayLocalization
{
    private static readonly (string Pt, string En)[] DesktopTexts =
    [
        ("PERFIS DE LAYOUT", "LAYOUT PROFILES"), ("Novo", "New"),
        ("Duplicar", "Duplicate"), ("Renomear", "Rename"), ("Excluir", "Delete"),
        ("Importar perfil", "Import profile"), ("Exportar perfil", "Export profile"),
        ("Exportar diagnóstico", "Export diagnostics"), ("Aplicar preset", "Apply preset"),
        ("VISIBILIDADE", "VISIBILITY"), ("FUNDO", "BACKGROUND"), ("ESCALA", "SCALE"),
        ("Dashboard RedFox", "RedFox dashboard"), ("Inputs", "Driver inputs"),
        ("Session / Flags", "Session / Flags"), ("Fuel / Virtual Energy", "Fuel / Virtual Energy"),
        ("Race Control / Damage", "Race Control / Damage"),
        ("PERFIL, ESTRATÉGIA E DESEMPENHO", "PROFILE, STRATEGY & PERFORMANCE"),
        ("Tema", "Theme"), ("Preto", "Black"), ("Alto contraste", "High contrast"),
        ("Visão de cores segura", "Color-vision safe"), ("Personalizado", "Custom"),
        ("Atualização (Hz)", "Refresh rate (Hz)"), ("Grade magnética (px)", "Snap grid (px)"),
        ("Reserva combustível (voltas)", "Fuel reserve (laps)"),
        ("Nome na dashboard", "Dashboard title"),
        ("Cor principal personalizada", "Custom accent color"),
        ("Cor de fundo personalizada", "Custom background color"),
        ("Formato hexadecimal, por exemplo #42D3A6", "Hexadecimal format, for example #42D3A6"),
        ("Formato hexadecimal, por exemplo #0A0F1A", "Hexadecimal format, for example #0A0F1A"),
        ("As cores personalizadas entram em uso ao selecionar o tema Personalizado.",
            "Custom colors are used when the Custom theme is selected."),
        ("Reserva VE (%)", "VE reserve (%)"), ("Voltas restantes (0 = auto)", "Remaining laps (0 = auto)"),
        ("Máx. stint (0 = auto)", "Max stint (0 = auto)"), ("Perda por pit (s)", "Pit loss (s)"),
        ("Jogos de pneus (0 = livre)", "Tire sets (0 = unrestricted)"),
        ("Limite de desgaste (%)", "Wear limit (%)"), ("Troca de pneus (s)", "Tire change (s)"),
        ("Duração restante manual (min)", "Manual remaining duration (min)"),
        ("Volta manual (s)", "Manual lap time (s)"),
        ("Consumo manual (L/volta)", "Manual consumption (L/lap)"),
        ("Tanque manual (L)", "Manual tank capacity (L)"),
        ("Valores 0 mantém o cálculo automático. Voltas manuais têm prioridade sobre a duração.",
            "Zero keeps automatic calculation. Manual laps take priority over duration."),
        ("Opacidade global do fundo", "Global background opacity"),
        ("Densidade visual", "Visual density"), ("Automática", "Automatic"),
        ("Compacta", "Compact"), ("Normal", "Normal"), ("Expandida", "Expanded"),
        ("Histórico dos pedais (s)", "Pedal history (s)"),
        ("Alertas prioritários", "Priority alerts"), ("Reduzir movimento", "Reduce motion"),
        ("Texto da dashboard", "Dashboard text"), ("Texto das torres", "Timing tower text"),
        ("Texto dos inputs", "Inputs text"),
        ("Máx. carros no Live Standings", "Max cars in Live Standings"),
        ("Carros de cada lado no Relative", "Cars on each side in Relative"),
        ("As escalas internas alteram a legibilidade sem mudar a posição nem o tamanho externo de cada overlay.",
            "Internal scales change readability without moving or resizing each overlay."),
        ("Restaurar perfil", "Restore profile"), ("Fechar", "Close"), ("Aplicar", "Apply"),
        ("Arrastar barra", "Drag toolbar"), ("Perfil de layout ativo", "Active layout profile"),
        ("Abrir configuração completa", "Open full configuration"),
        ("Liberar movimento e redimensionamento", "Enable movement and resizing"),
        ("Travar o overlay e impedir alterações acidentais", "Lock overlay and prevent accidental changes"),
    ];

    public static void Apply(DependencyObject root, string? language)
    {
        if (root is TextBlock text && text.GetBindingExpression(TextBlock.TextProperty) is null)
        {
            text.Text = Translate(language, text.Text);
        }
        if (root is ContentControl content && content.Content is string value)
        {
            content.Content = Translate(language, value);
        }
        if (root is HeaderedContentControl header && header.Header is string headerValue)
        {
            header.Header = Translate(language, headerValue);
        }
        if (root is FrameworkElement element && element.ToolTip is string tooltip)
        {
            element.ToolTip = Translate(language, tooltip);
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Apply(VisualTreeHelper.GetChild(root, index), language);
        }
    }

    private static string Translate(string? language, string text)
    {
        var common = OverlayText.TranslateExact(language, text);
        if (!string.Equals(common, text, StringComparison.Ordinal)) return common;
        foreach (var pair in DesktopTexts)
        {
            if (string.Equals(text, pair.Pt, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, pair.En, StringComparison.OrdinalIgnoreCase))
            {
                return OverlayText.Normalize(language) == OverlayText.EnglishUnitedStates ? pair.En : pair.Pt;
            }
        }
        return text;
    }
}
