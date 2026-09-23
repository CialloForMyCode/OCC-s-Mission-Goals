using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

/// <summary>
/// 项目元数据，保存在每个项目文件夹下的 project.json。
/// </summary>
public class ProjectConfig
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("CurrentVersion")]
    public string CurrentVersion { get; set; } = "0.1.0-alpha.0";

    [JsonPropertyName("Type")]
    public List<string> TypeOptions { get; set; } = new();

    /// <summary>与 Type 数组按索引对齐的可选颜色（hex 或颜色名，空串表示无颜色）。</summary>
    [JsonPropertyName("TypeColor")]
    public List<string> TypeColors { get; set; } = new();

    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("ProjectNumber")]
    public int ProjectNumber { get; set; }

    [JsonPropertyName("NextEntryId")]
    public int NextEntryId { get; set; } = 1;

    /// <summary>
    /// 计入统计的版本（版本文件名，不含 .json）：列表分组与数据统计只包含这些版本。
    /// 目前容纳全部版本；为空时同样视为「全部版本」，避免旧项目行为变化。
    /// </summary>
    [JsonPropertyName("StatsVersions")]
    public List<string> StatsVersions { get; set; } = new();

}
