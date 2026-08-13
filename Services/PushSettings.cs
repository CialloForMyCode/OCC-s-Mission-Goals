using System.Collections.Generic;
using System.Text.Json;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 推送 / 仓库设置的持久化存储，写入 config.ini 的 [Push] 节。
/// 由「设置」页面读写，「更新日志」页面读取。
/// </summary>
public static class PushSettings
{
    private const string Section = "Push";

    private static readonly JsonSerializerOptions JsonOptions = new();

    // ======================== 仓库列表 ========================

    /// <summary>读取仓库列表；无配置或损坏时返回默认列表。</summary>
    public static List<RepositoryInfo> LoadRepositories()
    {
        var raw = ConfigManager.Get(Section, "Repositories", "");
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultRepositories();

        try
        {
            var list = JsonSerializer.Deserialize<List<RepositoryInfo>>(raw, JsonOptions);
            return list is { Count: > 0 } ? list : DefaultRepositories();
        }
        catch
        {
            return DefaultRepositories();
        }
    }

    /// <summary>保存仓库列表（覆盖原有内容）。</summary>
    public static void SaveRepositories(IEnumerable<RepositoryInfo> repositories)
    {
        ConfigManager.Set(Section, "Repositories",
            JsonSerializer.Serialize(new List<RepositoryInfo>(repositories), JsonOptions));
    }

    // ======================== 提交生成选项 ========================

    /// <summary>提交信息是否包含作者。</summary>
    public static bool IncludeAuthor
    {
        get => ConfigManager.Get(Section, "IncludeAuthor", "1") == "1";
        set => ConfigManager.Set(Section, "IncludeAuthor", value ? "1" : "0");
    }

    /// <summary>提交信息是否按日期分组。</summary>
    public static bool GroupByDate
    {
        get => ConfigManager.Get(Section, "GroupByDate", "1") == "1";
        set => ConfigManager.Set(Section, "GroupByDate", value ? "1" : "0");
    }

    /// <summary>版本号前缀（默认 v）。</summary>
    public static string VersionPrefix
    {
        get
        {
            var v = ConfigManager.Get(Section, "VersionPrefix", "v");
            return string.IsNullOrEmpty(v) ? "v" : v;
        }
        set => ConfigManager.Set(Section, "VersionPrefix",
            string.IsNullOrEmpty(value) ? "v" : value);
    }

    // ======================== 默认值 ========================

    private static List<RepositoryInfo> DefaultRepositories() => new()
    {
        new() { Name = "OCC's Mission & Goals", Url = "https://github.com/OCCO/OCC-Mission-Goals" },
        new() { Name = "Harvest Planner", Url = "https://github.com/OCCO/HarvPlan" }
    };
}
