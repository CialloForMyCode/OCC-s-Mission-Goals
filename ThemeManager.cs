using System.Windows;
using System.Windows.Media;

namespace OCCMissionGoals;

public static class ThemeManager
{
    private static bool _isDark;
    private static string _accentHex = "#4CAF50";
    private static Color _accentColor = Color.FromRgb(0x4C, 0xAF, 0x50);

    public static bool IsDark => _isDark;

    /// <summary>当前主题色（规范化后的 #RRGGBB）。</summary>
    public static string AccentColorHex => _accentHex;

    // 亮色
    private static readonly (string Key, string Color)[] LightPalette =
    {
        ("ForegroundBrush",       "#8a8a8a"),
        ("BackgroundBrush",       "#f3f3f3"),
        ("MainBorderBrush",       "#808a8a8a"),
        ("CardBackgroundBrush",   "#FFFFFF"),
        ("CardBorderBrush",       "#ebebeb"),
        ("SearchBackgroundBrush", "#e7e7e7"),
        ("SearchBorderBrush",     "#e7e7e7"),
        ("SearchFocusBgBrush",    "#FFFFFF"),
        ("SearchFocusBorderBrush","#c0c0c0"),
        ("SearchHoverBorderBrush","#d8d8d8"),
        ("SubtleBackgroundBrush", "#e6e6e6"),
        ("HoverBackgroundBrush",  "#e8e8e8"),
        ("PressedBackgroundBrush","#d0d0d0"),
        ("SelectedBackgroundBrush","#b0b0b0"),
        ("WinControlHoverBrush",  "#e0e0e0"),
        ("WinControlPressedBrush","#c0c0c0"),
        ("MenuPopupBackgroundBrush","#FFFFFF"),
        ("MenuPopupBorderBrush",  "#c8c8c8"),
        ("DisabledForegroundBrush","#b0b0b0"),
        ("IconStrokeBrush",       "#8a8a8a"),
        ("RunButton",             "#8a8a8a"),
        ("SelectedForegroundBrush","#FFFFFF"),
        ("TerminalBackgroundBrush","#F5F5F5"),
        ("TerminalForegroundBrush","#1E1E1E"),
        ("TerminalScrollBarBrush", "#C0C0C0"),
        ("TerminalScrollBarHoverBrush","#A0A0A0"),
        ("TerminalSelectionBrush", "#ADD6FF"),
    };

    // 暗色
    private static readonly (string Key, string Color)[] DarkPalette =
    {
        ("ForegroundBrush",       "#b0b0b0"),
        ("BackgroundBrush",       "#0e0e11"),
        ("MainBorderBrush",       "#80222225"),
        ("CardBackgroundBrush",   "#18181b"),
        ("CardBorderBrush",       "#222225"),
        ("SearchBackgroundBrush", "#202025"),
        ("SearchBorderBrush",     "#222225"),
        ("SearchFocusBgBrush",    "#18181b"),
        ("SearchFocusBorderBrush","#3a3a40"),
        ("SearchHoverBorderBrush","#2a2a2f"),
        ("SubtleBackgroundBrush", "#2a2a30"),
        ("HoverBackgroundBrush",  "#2a2a30"),
        ("PressedBackgroundBrush","#3a3a40"),
        ("SelectedBackgroundBrush","#696976"),
        ("WinControlHoverBrush",  "#2a2a30"),
        ("WinControlPressedBrush","#3a3a40"),
        ("MenuPopupBackgroundBrush","#18181b"),
        ("MenuPopupBorderBrush",  "#3a3a40"),
        ("DisabledForegroundBrush","#555555"),
        ("IconStrokeBrush",       "#b0b0b0"),
        ("RunButton",             "#b0b0b0"),
        ("SelectedForegroundBrush","#FFFFFF"),
        ("TerminalBackgroundBrush","#1E1E1E"),
        ("TerminalForegroundBrush","#D4D4D4"),
        ("TerminalScrollBarBrush", "#424242"),
        ("TerminalScrollBarHoverBrush","#555555"),
        ("TerminalSelectionBrush", "#264f78"),
    };

    public static void ToggleTheme() => ApplyTheme(!_isDark);

    public static void ApplyTheme(bool dark)
    {
        _isDark = dark;
        var palette = _isDark ? DarkPalette : LightPalette;
        var resources = Application.Current.Resources;

        foreach (var (key, colorHex) in palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            resources[key] = new SolidColorBrush(color);
        }

        // 主题切换后重新派生主题色，使选中态跟随明暗主题。
        ApplyAccentDerived(_accentColor);
    }

    /// <summary>
    /// 设置主题色（#RRGGBB 或颜色名），并派生 hover / pressed / dark / light 四个变体画刷。
    /// 非法输入时回退到默认绿色 #4CAF50。
    /// </summary>
    public static void ApplyAccentColor(string hex)
    {
        var color = ParseColor(hex) ?? Color.FromRgb(0x4C, 0xAF, 0x50);
        _accentColor = color;
        _accentHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        ApplyAccentDerived(color);
    }

    /// <summary>
    /// 由主题色派生 hover / pressed / dark / light 四个变体。
    /// dark / light 同时跟随当前明暗主题：亮色主题用浅色调背景 + 深色调文字，
    /// 暗色主题用主题色 40% 不透明度的半透明淡色背景 + 近白文字，
    /// 让选中态在深色界面中含蓄清晰，既不偏暗也不偏白。
    /// </summary>
    private static void ApplyAccentDerived(Color color)
    {
        var resources = Application.Current.Resources;
        resources["PrimaryBrush"]        = new SolidColorBrush(color);
        resources["PrimaryHoverBrush"]   = new SolidColorBrush(Blend(color, Colors.Black, 0.12));
        resources["PrimaryPressedBrush"] = new SolidColorBrush(Blend(color, Colors.Black, 0.24));

        if (_isDark)
        {
            resources["PrimaryLightBrush"] = new SolidColorBrush(color) { Opacity = 0.40 };
            resources["PrimaryDarkBrush"]  = new SolidColorBrush(Blend(color, Colors.White, 0.85));
        }
        else
        {
            resources["PrimaryLightBrush"] = new SolidColorBrush(Blend(color, Colors.White, 0.72));
            resources["PrimaryDarkBrush"]  = new SolidColorBrush(Blend(color, Colors.Black, 0.40));
        }
    }

    private static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return (Color)ColorConverter.ConvertFromString(value.Trim());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把 <paramref name="color"/> 向 <paramref name="target"/> 按比例 t (0~1) 混合。</summary>
    private static Color Blend(Color color, Color target, double t)
    {
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        return Color.FromRgb(Mix(color.R, target.R), Mix(color.G, target.G), Mix(color.B, target.B));
    }
}
