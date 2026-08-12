using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

public class UserConfig
{
    [JsonPropertyName("SaveLocation")]
    public List<string> SaveLocation { get; set; } = new();

    [JsonPropertyName("UploadedGithub")]
    public bool UploadedGithub { get; set; }
}

/// <summary>
/// JSON 数据文件的根模型，映射 EntryFiles/data.json 的整体结构。
/// </summary>
public class DataFile
{
    [JsonPropertyName("User")]
    public UserConfig User { get; set; } = new();

    [JsonPropertyName("Unfinished")]
    public List<GoalEntry> Unfinished { get; set; } = new();

    [JsonPropertyName("Finished")]
    public List<GoalEntry> Finished { get; set; } = new();
}
