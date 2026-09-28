using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

/// <summary>
/// 扩展清单，对应 Expand\&lt;扩展目录&gt;\expand.json。
///
/// 扩展是「扩展中心里可启用 / 禁用的插件」：清单描述它的身份与开关状态。
/// 界面外观（颜色、圆角、边框粗细、间距、字号）由主题决定，见 Themes 目录与
/// <see cref="Services.ThemeManager"/>，扩展不再覆盖资源键。
/// 与语言包（Languages）、主题（Themes）不同，扩展放进 Expand 目录即视为安装。
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

    /// <summary>是否启用。被禁用的扩展只列出、不启用。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    // ==================== 运行期字段（不写入清单） ====================

    /// <summary>扩展所在目录（扫描时填入）。</summary>
    [JsonIgnore]
    public string Directory { get; set; } = string.Empty;

    /// <summary>读取过程中的问题（清单格式错误等）；为 null 表示正常。</summary>
    [JsonIgnore]
    public string? LoadError { get; set; }
}
