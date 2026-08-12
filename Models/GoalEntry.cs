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
    DeadlineAsc,
    DeadlineDesc,
    VersionAsc,
    VersionDesc,
    FavoritesOnly
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

    /// <summary>隐藏编号：PPPEEEEEE（PPP=项目编号，EEEEEE=条目编号）。</summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Severity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GoalSeverity Severity { get; set; } = GoalSeverity.General;

    [JsonPropertyName("Brief")]
    public string Brief { get; set; } = string.Empty;

    [JsonPropertyName("Detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("Deadline")]
    [JsonConverter(typeof(DateArrayConverter))]
    public DateTime Deadline { get; set; } = DateTime.Today.AddDays(7);

    [JsonPropertyName("CompletedAt")]
    [JsonConverter(typeof(DateArrayConverter))]
    public DateTime CompletedAt { get; set; } = DateTime.Today;

    [JsonPropertyName("ChangeDemand")]
    public int ChangeDemand { get; set; }

    [JsonPropertyName("IsFavorited")]
    public bool IsFavorited { get; set; }

    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("Type")]
    public List<string> Type { get; set; } = new();

    [JsonPropertyName("RelatedFiles")]
    [JsonConverter(typeof(FileRefDictionaryConverter))]
    public List<FileRef> RelatedFiles { get; set; } = new();
}

/// <summary>
/// JSON 中日期为 [年, 月, 日] 数组格式的转换器。
/// </summary>
public class DateArrayConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected array [year, month, day], got {reader.TokenType}");

        reader.Read();
        int year = reader.GetInt32();
        reader.Read();
        int month = reader.GetInt32();
        reader.Read();
        int day = reader.GetInt32();
        reader.Read(); // EndArray

        return new DateTime(year, month, day);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Year);
        writer.WriteNumberValue(value.Month);
        writer.WriteNumberValue(value.Day);
        writer.WriteEndArray();
    }
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
