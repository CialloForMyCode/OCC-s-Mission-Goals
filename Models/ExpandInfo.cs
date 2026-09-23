using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

/// <summary>
/// 扩展清单，对应 Expand\&lt;扩展目录&gt;\expand.json。
///
/// 扩展是「能改变程序呈现的插件」：可以覆盖资源键（颜色、圆角、边框粗细……），
/// 也可以提供布局片段替换主内容区的排布。与语言包（Languages）、主题（Themes）不同，
/// 扩展由本地目录中的 expand.json + 若干 XAML 组成，放进 Expand 目录即视为安装。
/// </summary>
public class ExpandInfo
{
    /// <summary>扩展唯一标识（稳定键，用于启用 / 禁用与去重）。</summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>显示名。</summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("Author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>是否启用。被禁用的扩展只列出、不装载。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 资源文件（相对扩展目录的路径，例如 ui.xaml）。
    /// 其内容是一个 ResourceDictionary，里面的键会覆盖 Application.Resources 中的同名键，
    /// 从而改变板块颜色、圆角、边框粗细等外观。
    /// </summary>
    [JsonPropertyName("Resources")]
    public string Resources { get; set; } = string.Empty;

    /// <summary>
    /// 布局文件（相对扩展目录的路径，例如 layout.xaml）。留空表示不提供布局。
    /// </summary>
    [JsonPropertyName("Layout")]
    public string Layout { get; set; } = string.Empty;

    // ==================== 运行期字段（不写入清单） ====================

    /// <summary>扩展所在目录（扫描时填入）。</summary>
    [JsonIgnore]
    public string Directory { get; set; } = string.Empty;

    /// <summary>装载过程中的问题（清单格式错误、资源文件读不到等）；为 null 表示正常。</summary>
    [JsonIgnore]
    public string? LoadError { get; set; }

    /// <summary>该扩展本次写入的资源键数量（用于提示与排查）。</summary>
    [JsonIgnore]
    public int AppliedKeyCount { get; set; }
}
