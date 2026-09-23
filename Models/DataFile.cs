using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

public class UserConfig
{
    [JsonPropertyName("SaveLocation")]
    public List<string> SaveLocation { get; set; } = new();
}

/// <summary>
/// JSON 数据文件的根模型，映射 EntryFiles/data.json 的整体结构。
/// 条目统一存放在 Entries 中，完成与否由 <see cref="GoalEntry.Status"/> 区分。
/// </summary>
public class DataFile
{
    [JsonPropertyName("User")]
    public UserConfig User { get; set; } = new();

    /// <summary>全部条目：未完成与已完成同列。</summary>
    [JsonPropertyName("Entries")]
    public List<GoalEntry> Entries { get; set; } = new();

    /// <summary>未完成条目的只读视图（按 Status 过滤，不参与序列化）。</summary>
    [JsonIgnore]
    public IEnumerable<GoalEntry> Unfinished =>
        Entries.Where(e => e.Status == EntryStatus.Unfinished);

    /// <summary>已完成条目的只读视图（按 Status 过滤，不参与序列化）。</summary>
    [JsonIgnore]
    public IEnumerable<GoalEntry> Finished =>
        Entries.Where(e => e.Status == EntryStatus.Finished);
}
