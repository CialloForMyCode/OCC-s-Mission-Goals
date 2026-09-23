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

    /// <summary>内置默认主题的文件名。磁盘 Themes 目录里出现同名文件时忽略，以程序集内的为准。</summary>
    private const string DefaultThemeFileName = "Default.xaml";

    /// <summary>内置默认主题的资源路径，对应 csproj 中内嵌它的 Resource 项。</summary>
    private const string DefaultThemeResource = "/Themes/Default.xaml";

    /// <summary>主题文件列表（显示名，磁盘路径）。显示名取自主题 XAML 内的 __theme_name；
    /// 路径为 null 表示内置默认主题，从程序集资源读取，而不是磁盘文件。</summary>
    private static readonly List<(string Name, string? File)> _themes = new();

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

    /// <summary>当前主题样式推荐的强调色（#RRGGBB）；主题未指定时为 null。</summary>
    public static string? CurrentThemeAccent { get; private set; }

    static ThemeManager()
    {
        LoadThemes();
    }

    /// <summary>
    /// 载入主题列表：内置默认主题始终排在首位（取自程序集资源），随后扫描 Themes 目录中的外部主题
    /// （每个文件含 Light.* / Dark.* 两套配色）。
    /// </summary>
    private static void LoadThemes()
    {
        _themes.Clear();

        // 内置默认主题：打包在程序集内，Themes 目录为空或被删除时也不会丢失。
        _themes.Add((DefaultThemeName, null));

        var dir = Path.Combine(AppContext.BaseDirectory, "Themes");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.xaml").OrderBy(f => f, StringComparer.Ordinal))
            {
                // 磁盘上的 Default.xaml（旧版本残留或用户手动放回）一律忽略，避免出现重复的“默认主题”。
                if (string.Equals(Path.GetFileName(file), DefaultThemeFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = ReadThemeName(file);
                if (string.IsNullOrWhiteSpace(name))
                    name = Path.GetFileNameWithoutExtension(file);
                _themes.Add((name, file));
            }
        }
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

    /// <summary>
    /// 重新扫描 Themes 目录并重建主题列表（安装 / 卸载主题后调用）。
    /// 当前主题名若已不存在则回退到列表首个，并重新应用配色。
    /// </summary>
    public static void Reload()
    {
        LoadThemes();
        _currentTheme = ResolveTheme(_currentTheme).Name;
        ApplyTheme(_isDark);
    }

    private static (string Name, string? File) ResolveTheme(string name)
    {
        var entry = _themes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
        return entry.Name is null ? _themes[0] : entry;
    }

    public static void ToggleTheme() => ApplyTheme(!_isDark);

    /// <summary>应用当前主题样式的深色/浅色配色，并刷新主题色派生画刷。</summary>
    public static void ApplyTheme(bool dark)
    {
        _isDark = dark;

        var entry = ResolveTheme(_currentTheme);
        CurrentThemeAccent = null;
        ApplyPalette(entry.File, dark);

        // 主题切换后重新派生主题色，使选中态跟随明暗主题。
        ApplyAccentDerived(_accentColor);

        // 最后套用扩展的资源覆盖：扩展要盖过主题，就必须排在主题之后。
        Services.ExpandService.ApplyOverrides();
    }

    /// <summary>
    /// 读取主题 XAML，把其中的 Light.* / Dark.* 画刷按当前明暗模式复制到应用资源。
    /// 键去掉 "Light." / "Dark." 前缀，与界面里 {DynamicResource ForegroundBrush} 等保持一致。
    /// <paramref name="file"/> 为 null 时表示内置默认主题，从程序集资源读取。
    /// </summary>
    private static void ApplyPalette(string? file, bool dark)
    {
        var rd = file is null ? LoadEmbeddedDefaultTheme() : LoadThemeFile(file);
        if (rd is null)
            return;

        var prefix = dark ? "Dark." : "Light.";
        var resources = Application.Current.Resources;

        CurrentThemeAccent = NormalizeAccent(rd["__theme_accent"] as string);

        foreach (var keyObj in rd.Keys)
        {
            if (keyObj is not string key || !key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            resources[key[prefix.Length..]] = rd[keyObj];
        }
    }

    /// <summary>读取磁盘上的主题文件；文件缺失或解析失败返回 null。</summary>
    private static ResourceDictionary? LoadThemeFile(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return (ResourceDictionary)XamlReader.Load(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取内嵌的默认主题（程序集资源）。它不依赖 Themes 目录，因此外部 Themes\Default.xaml
    /// 被删除时界面依然能套用完整配色。
    /// </summary>
    private static ResourceDictionary? LoadEmbeddedDefaultTheme()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri(DefaultThemeResource, UriKind.Relative));
            if (info is null) return null;

            using var stream = info.Stream;
            return (ResourceDictionary)XamlReader.Load(stream);
        }
        catch
        {
            return null;
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

    /// <summary>把主题里推荐强调色规范化为 #RRGGBB；无效或为空返回 null。</summary>
    private static string? NormalizeAccent(string? value)
    {
        var color = ParseColor(value);
        return color is null ? null : $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
    }

    /// <summary>把 <paramref name="color"/> 向 <paramref name="target"/> 按比例 t (0~1) 混合。</summary>
    private static Color Blend(Color color, Color target, double t)
    {
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        return Color.FromRgb(Mix(color.R, target.R), Mix(color.G, target.G), Mix(color.B, target.B));
    }
}
