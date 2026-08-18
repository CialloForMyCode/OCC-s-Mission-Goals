using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace OCCMissionGoals;

/// <summary>
/// 界面语言管理。翻译文本来自 <c>Languages/*.xaml</c>（每个语言一个 ResourceDictionary），
/// 键为中文原文；新增语言只需在 Languages 目录放入一个新的 XAML 文件即可。
/// 语言切换会立即通知所有通过 <see cref="LocExtension"/> 建立的绑定，
/// 并通过静态事件 <see cref="LanguageChanged"/> 通知代码侧刷新动态生成的界面。
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    /// <summary>默认（回退）语言代码。键本身即该语言文本。</summary>
    public const string DefaultLanguage = "zh";

    /// <summary>语言切换后触发（参数为语言代码，如 "zh"、"en"、"ru"）。</summary>
    public static event Action<string>? LanguageChanged;

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.Ordinal);
    private readonly List<(string Code, string Name)> _languages = new();
    private string _language = DefaultLanguage;

    private LocalizationManager()
    {
        LoadLanguages();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前语言代码。</summary>
    public string Language => _language;

    public bool IsEnglish => _language == "en";

    public bool IsRussian => _language == "ru";

    /// <summary>可用语言列表（Code, 显示名）。默认语言排在首位。</summary>
    public IReadOnlyList<(string Code, string Name)> AvailableLanguages => _languages;

    /// <summary>
    /// 从 Languages 目录扫描并加载所有 *.xaml 语言资源字典。
    /// 每个文件是一个 <see cref="ResourceDictionary"/>，键为中文原文，
    /// 值为该语言译文；<c>__lang_code</c> / <c>__lang_name</c> 为语言元数据。
    /// </summary>
    private void LoadLanguages()
    {
        _tables.Clear();
        _languages.Clear();

        var dir = Path.Combine(AppContext.BaseDirectory, "Languages");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.xaml"))
            {
                try
                {
                    ResourceDictionary rd;
                    using (var stream = File.OpenRead(file))
                        rd = (ResourceDictionary)XamlReader.Load(stream);

                    var code = Path.GetFileNameWithoutExtension(file);
                    var name = code;
                    var table = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var keyObj in rd.Keys)
                    {
                        if (keyObj is not string key || rd[keyObj] is not string value)
                            continue;

                        if (key == "__lang_code")
                        {
                            if (!string.IsNullOrWhiteSpace(value)) code = value;
                            continue;
                        }
                        if (key == "__lang_name")
                        {
                            if (!string.IsNullOrWhiteSpace(value)) name = value;
                            continue;
                        }

                        table[key] = value;
                    }

                    _tables[code] = table;
                    if (!_languages.Any(l => l.Code == code))
                        _languages.Add((code, name));
                }
                catch
                {
                    // 忽略损坏的语言文件，不影响启动。
                }
            }
        }

        // 默认语言永远可用（其文本即键本身）。
        if (!_tables.ContainsKey(DefaultLanguage))
            _tables[DefaultLanguage] = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!_languages.Any(l => l.Code == DefaultLanguage))
            _languages.Insert(0, (DefaultLanguage, "中文"));

        _languages.Sort((a, b) =>
        {
            if (a.Code == DefaultLanguage) return -1;
            if (b.Code == DefaultLanguage) return 1;
            return string.CompareOrdinal(a.Code, b.Code);
        });
    }

    /// <summary>读取 config.ini 中的语言设置（App 启动时调用一次）。</summary>
    public static void LoadFromConfig()
    {
        var lang = ConfigManager.Get("General", "language", DefaultLanguage);
        Instance._language = Instance.Normalize(lang);
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

    /// <summary>
    /// 重新扫描 Languages 目录并重建语言表（安装 / 卸载语言包后调用）。
    /// 若当前语言已不存在则回退到默认语言，并通知界面刷新。
    /// </summary>
    public void Reload()
    {
        var previous = _language;
        LoadLanguages();
        _language = Normalize(previous);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableLanguages)));
        LanguageChanged?.Invoke(_language);
    }

    /// <summary>按当前语言取文本。key 为中文原文，找不到翻译时回退为 key。</summary>
    public static string T(string key) => Instance.Lookup(key);

    /// <summary>取文本并格式化，例如 T("已更新条目「{0}」。", title)。</summary>
    public static string T(string key, params object[] args) =>
        args is { Length: > 0 }
            ? string.Format(CultureInfo.CurrentCulture, Instance.Lookup(key), args)
            : Instance.Lookup(key);

    private string Lookup(string key)
    {
        if (_tables.TryGetValue(_language, out var table) &&
            table.TryGetValue(key, out var value) &&
            !string.IsNullOrEmpty(value))
            return value;

        if (_language != DefaultLanguage &&
            _tables.TryGetValue(DefaultLanguage, out var fallback) &&
            fallback.TryGetValue(key, out var fb) &&
            !string.IsNullOrEmpty(fb))
            return fb;

        return key;
    }

    private string Normalize(string lang)
    {
        var code = lang?.Trim().ToLowerInvariant() ?? string.Empty;
        return _tables.ContainsKey(code) ? code : DefaultLanguage;
    }
}

/// <summary>根据当前语言在 key（中文原文）上取对应译文。</summary>
public sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // value 为语言代码，仅用于触发绑定重算；实际查找走 LocalizationManager。
        return parameter is string key ? LocalizationManager.T(key) : parameter?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// XAML 标记扩展：{loc:Loc 取消} 或 {loc:Loc &quot;Text: 文字&quot;}。
/// 参数为中文原文（即翻译键），语言切换时自动更新目标属性。
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension() { }

    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(LocalizationManager.Language))
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocConverter.Instance,
            ConverterParameter = Key
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

        return Key;
    }
}
