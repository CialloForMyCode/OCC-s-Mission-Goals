using System.Windows;
using System.Windows.Media;

namespace OCCMissionGoals;

public static class ThemeManager
{
    private static bool _isDark;

    public static bool IsDark => _isDark;

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
        ("MenuIndicatorBrush",    "#a0c8e8"),
        ("AccentBrush",           "#0078d4"),
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
        ("MenuIndicatorBrush",    "#a0c8e8"),
        ("AccentBrush",           "#60cdff"),
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
    }
}
