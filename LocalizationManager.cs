using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace OCCMissionGoals;

/// <summary>
/// 界面语言管理。当前支持中文（zh，默认）、英文（en）与俄语（ru）。
/// 语言切换会立即通知所有通过 <see cref="LocExtension"/> 建立的绑定，
/// 并通过静态事件 <see cref="LanguageChanged"/> 通知代码侧刷新动态生成的界面。
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    /// <summary>语言切换后触发（参数为 "zh"、"en" 或 "ru"）。</summary>
    public static event Action<string>? LanguageChanged;

    private string _language = "zh";

    private LocalizationManager() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前语言，"zh"、"en" 或 "ru"。</summary>
    public string Language => _language;

    public bool IsEnglish => _language == "en";

    public bool IsRussian => _language == "ru";

    /// <summary>读取 config.ini 中的语言设置（App 启动时调用一次）。</summary>
    public static void LoadFromConfig()
    {
        var lang = ConfigManager.Get("General", "language", "zh");
        Instance._language = Normalize(lang);
    }

    /// <summary>切换语言并持久化。相同语言时不做任何事。</summary>
    public void SetLanguage(string lang)
    {
        lang = Normalize(lang);
        if (_language == lang) return;

        _language = lang;
        ConfigManager.Set("General", "language", lang);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(_language);
    }

    /// <summary>按当前语言取文本：中文传 zh，英文传 en，俄语传 ru（可省略）。</summary>
    public static string T(string zh, string en, string ru = "") =>
        Instance._language switch
        {
            "en" => en,
            "ru" => string.IsNullOrEmpty(ru)
                ? (string.IsNullOrEmpty(en) ? zh : en)
                : ru,
            _ => zh,
        };

    private static string Normalize(string lang) => lang?.Trim().ToLowerInvariant() switch
    {
        "en" => "en",
        "ru" => "ru",
        _ => "zh",
    };
}

/// <summary>一条可翻译文本（中文 / 英文 / 俄语）。</summary>
public sealed record LocEntry(string Zh, string En, string Ru);

/// <summary>根据当前语言在 <see cref="LocEntry"/> 中取对应文本。</summary>
public sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var lang = value as string ?? "zh";
        if (parameter is LocEntry entry)
        {
            return lang switch
            {
                "en" => string.IsNullOrEmpty(entry.En) ? entry.Zh : entry.En,
                "ru" => string.IsNullOrEmpty(entry.Ru)
                    ? (string.IsNullOrEmpty(entry.En) ? entry.Zh : entry.En)
                    : entry.Ru,
                _ => entry.Zh,
            };
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// XAML 标记扩展：{loc:Loc 中文, En=English, Ru=Русский} 或 {loc:Loc Zh=中文, En=English, Ru=Русский}。
/// 语言切换时自动更新目标属性，无需手动刷新。
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Zh { get; set; } = "";
    public string En { get; set; } = "";
    public string Ru { get; set; } = "";

    public LocExtension() { }

    public LocExtension(string zh) => Zh = zh;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(LocalizationManager.Language))
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocConverter.Instance,
            ConverterParameter = new LocEntry(Zh, En, Ru)
        };

        try
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not null)
                return binding.ProvideValue(serviceProvider);
        }
        catch
        {
            // 目标不是 DependencyProperty 或设计时环境：回退为静态文本。
        }

        return Zh;
    }
}
