using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace OCCMissionGoals;

public static class ThemeManager
{
    private static bool _isDark;
    private static string _accentHex = "#4CAF50";
    private static Color _accentColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private static string _currentTheme = DefaultThemeName;

    /// <summary>默认主题名，配置缺失时回退到它。</summary>
    public const string DefaultThemeName = "默认主题";

    /// <summary>主题文件列表（显示名，磁盘路径）。显示名取自主题 XAML 内的 __theme_name。</summary>
    private static readonly List<(string Name, string File)> _themes = new();

    public static bool IsDark => _isDark;

    /// <summary>当前主题色（规范化后的 #RRGGBB）。</summary>
    public static string AccentColorHex => _accentHex;

    /// <summary>预设主题色（#RRGGBB），供设置页与搜索下拉框共用。</summary>
    public static IReadOnlyList<string> AccentPresets { get; } = new[]
    {
        "#4CAF50", "#8BC34A", "#009688", "#00BCD4",
        "#2196F3", "#3F51B5", "#9C27B0", "#E91E63",
        "#FF5722", "#FF9800", "#795548", "#607D8B"
    };

    /// <summary>可用主题样式名（供设置下拉框使用）。</summary>
    public static IReadOnlyList<string> ThemeNames => _themes.Select(t => t.Name).ToList();

    /// <summary>当前选中的主题样式名。</summary>
    public static string CurrentThemeName => _currentTheme;

    static ThemeManager()
    {
        LoadThemes();
    }

    /// <summary>扫描 Themes 目录，载入所有主题 XAML（每个文件含 Light.* / Dark.* 两套配色）。</summary>
    private static void LoadThemes()
    {
        _themes.Clear();

        var dir = Path.Combine(AppContext.BaseDirectory, "Themes");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.xaml").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = ReadThemeName(file);
                if (string.IsNullOrWhiteSpace(name))
                    name = Path.GetFileNameWithoutExtension(file);
                _themes.Add((name, file));
            }
        }

        // 没有任何主题文件时退化为一个占位主题（不会应用配色）。
        if (_themes.Count == 0)
            _themes.Add((DefaultThemeName, string.Empty));
    }

    private static string? ReadThemeName(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var rd = (ResourceDictionary)XamlReader.Load(stream);
            return rd["__theme_name"] as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>选择主题样式（仅记录选择，真正应用由 ApplyTheme 完成）。</summary>
    public static void SetThemeStyle(string name)
    {
        _currentTheme = ResolveTheme(name).Name;
    }

    private static (string Name, string File) ResolveTheme(string name)
    {
        if (_themes.Count == 0) return (DefaultThemeName, string.Empty);
        var entry = _themes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
        return entry.Name is null ? _themes[0] : entry;
    }

    public static void ToggleTheme() => ApplyTheme(!_isDark);

    /// <summary>应用当前主题样式的深色/浅色配色，并刷新主题色派生画刷。</summary>
    public static void ApplyTheme(bool dark)
    {
        _isDark = dark;

        var entry = ResolveTheme(_currentTheme);
        if (!string.IsNullOrEmpty(entry.File))
            ApplyPalette(entry.File, dark);

        // 主题切换后重新派生主题色，使选中态跟随明暗主题。
        ApplyAccentDerived(_accentColor);
    }

    /// <summary>
    /// 读取主题 XAML，把其中的 Light.* / Dark.* 画刷按当前明暗模式复制到应用资源。
    /// 键去掉 "Light." / "Dark." 前缀，与界面里 {DynamicResource ForegroundBrush} 等保持一致。
    /// </summary>
    private static void ApplyPalette(string file, bool dark)
    {
        ResourceDictionary rd;
        try
        {
            using var stream = File.OpenRead(file);
            rd = (ResourceDictionary)XamlReader.Load(stream);
        }
        catch
        {
            return;
        }

        var prefix = dark ? "Dark." : "Light.";
        var resources = Application.Current.Resources;

        foreach (var keyObj in rd.Keys)
        {
            if (keyObj is not string key || !key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            resources[key[prefix.Length..]] = rd[keyObj];
        }
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
