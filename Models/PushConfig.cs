using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

/// <summary>
/// 项目推送 / 仓库发布相关设置，作为 ProjectConfig.Push 节点保存到 project.json。
/// </summary>
public class PushConfig
{
    /// <summary>推送目标仓库列表（名称、URL）。</summary>
    [JsonPropertyName("Repositories")]
    public List<RepositoryInfo> Repositories { get; set; } = new();

    /// <summary>提交信息是否包含作者。</summary>
    [JsonPropertyName("IncludeAuthor")]
    public bool IncludeAuthor { get; set; } = true;

    /// <summary>提交信息是否按日期分组。</summary>
    [JsonPropertyName("GroupByDate")]
    public bool GroupByDate { get; set; } = true;

    /// <summary>要推送的 bin 文件名（空表示未选择）。</summary>
    [JsonPropertyName("RemotePath")]
    public string RemotePath { get; set; } = string.Empty;
}
