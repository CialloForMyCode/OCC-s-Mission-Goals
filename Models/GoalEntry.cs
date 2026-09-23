using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCCMissionGoals.Models;

public enum GoalSeverity
{
    Fatal,      // 致命
    Severe,     // 严重
    General,    // 一般
    Patch,      // 补丁
    Update      // 更新
}

public enum SortMode
{
    SeverityAsc,
    SeverityDesc,
    VersionAsc,
    VersionDesc,
    FavoritesOnly,
    TypeAsc
}

/// <summary>搜索框的匹配模式。</summary>
public enum SearchMode
{
    Text,    // 文字（标题 / 简要）
    Tag,     // 类型标签
    Setting, // 设置（项目设置 / 主题样式 / 数据统计，全局搜索）
    Function, // 功能（新建条目 / 新建项目 / 打开项目等，全局搜索）
    File,    // 关联文件
    Date,    // 日期
    Plugins, // 插件（扩展中心全部插件，全局搜索）
    Expand   // 已安装插件（全局搜索）
}

public class FileRef
{
    public string Path { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Function { get; set; } = string.Empty;
}

public class GoalEntry
{
    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>条目唯一编号：PPPEEEEEE（PPP=项目编号，EEEEEE=条目编号）。
    /// 条目的定位、引用一律以 Id 为准，标题可以自由修改。</summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Severity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GoalSeverity Severity { get; set; } = GoalSeverity.General;

    /// <summary>完成状态。取代以往「放在 Unfinished 还是 Finished 列表」的隐含状态。</summary>
    [JsonPropertyName("Status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EntryStatus Status { get; set; } = EntryStatus.Unfinished;

    [JsonPropertyName("Brief")]
    public string Brief { get; set; } = string.Empty;


    /// <summary>完成时间（ISO 8601，含时分秒）；未完成时为 default。</summary>
    [JsonPropertyName("CompletedAt")]
    public DateTime CompletedAt { get; set; }

    /// <summary>创建时间（ISO 8601，含时分秒）。</summary>
    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最后一次修改时间（ISO 8601，含时分秒）。</summary>
    [JsonPropertyName("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("IsFavorited")]
    public bool IsFavorited { get; set; }


    [JsonPropertyName("Type")]
    public List<string> Type { get; set; } = new();

    [JsonPropertyName("RelatedFiles")]
    [JsonConverter(typeof(FileRefDictionaryConverter))]
    public List<FileRef> RelatedFiles { get; set; } = new();

    /// <summary>
    /// 内容区：条目正文，由若干可拖拽排序的区块组成（文本 / 表格 / 分割线 / 代码块 /
    /// 文件引用 / 多级列表 / 子任务）。保存条目时，<see cref="RelatedFiles"/> 与
    /// <see cref="Progress"/> 都由内容区自动汇总。
    /// </summary>
    [JsonPropertyName("Contents")]
    public List<ContentBlock> Contents { get; set; } = new();

    /// <summary>完成度（0-100）：由内容区多级列表条目与子任务区块的勾选情况推导，保存条目时刷新。</summary>
    [JsonPropertyName("Progress")]
    public int Progress { get; set; }
}

/// <summary>条目的完成状态。</summary>
public enum EntryStatus
{
    Unfinished, // 未完成
    Finished    // 已完成
}

/// <summary>
/// RelatedFiles 在 JSON 中为 { "path": [row, col, "func"] } 格式的转换器。
/// </summary>
public class FileRefDictionaryConverter : JsonConverter<List<FileRef>>
{
    public override List<FileRef> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<FileRef>();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected object for RelatedFiles, got {reader.TokenType}");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string path = reader.GetString()!;

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected array for file \"{path}\"");

            reader.Read();
            int row = reader.GetInt32();
            reader.Read();
            int col = reader.GetInt32();
            reader.Read();
            string func = reader.GetString()!;
            reader.Read(); // EndArray

            list.Add(new FileRef
            {
                Path = path,
                Line = row,
                Column = col,
                Function = func
            });
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<FileRef> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var f in value)
        {
            writer.WriteStartArray(f.Path);
            writer.WriteNumberValue(f.Line);
            writer.WriteNumberValue(f.Column);
            writer.WriteStringValue(f.Function);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
