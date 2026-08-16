using System.IO;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 推送 / 仓库设置的持久化存储，写入当前项目文件夹下的 project.json 的 Push 节点。
/// 由「设置」页面读写，「更新日志」页面读取。未打开项目时返回默认值且不写入。
/// </summary>
public static class PushSettings
{
    private static PushConfig? Current => ProjectService.CurrentProject?.Push;

    // ======================== 仓库列表 ========================

    /// <summary>读取仓库列表；无项目或未配置时返回空列表。</summary>
    public static List<RepositoryInfo> LoadRepositories()
    {
        var cfg = Current;
        if (cfg?.Repositories == null) return new List<RepositoryInfo>();

        // 旧数据可能没有分支字段，统一补为默认值，保证界面显示与推送一致。
        foreach (var repo in cfg.Repositories)
        {
            if (string.IsNullOrWhiteSpace(repo.Branch))
                repo.Branch = "main";
        }

        return new List<RepositoryInfo>(cfg.Repositories);
    }

    /// <summary>保存仓库列表（覆盖原有内容）。未打开项目时忽略。</summary>
    public static void SaveRepositories(IEnumerable<RepositoryInfo> repositories)
    {
        var cfg = Current;
        if (cfg == null) return;

        cfg.Repositories = new List<RepositoryInfo>(repositories);
        ProjectService.SaveCurrentProject();
    }

    // ======================== 提交生成选项 ========================

    /// <summary>提交信息是否包含作者。</summary>
    public static bool IncludeAuthor
    {
        get => Current?.IncludeAuthor ?? true;
        set
        {
            if (Current is not { } cfg) return;
            cfg.IncludeAuthor = value;
            ProjectService.SaveCurrentProject();
        }
    }

    /// <summary>提交信息是否按日期分组。</summary>
    public static bool GroupByDate
    {
        get => Current?.GroupByDate ?? true;
        set
        {
            if (Current is not { } cfg) return;
            cfg.GroupByDate = value;
            ProjectService.SaveCurrentProject();
        }
    }

    /// <summary>程序目录下的 bin 文件夹（存放待推送/归档的文件）。</summary>
    public static string BinDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");

    /// <summary>列出 bin 文件夹内的文件名（仅文件名，按名称排序）。</summary>
    public static List<string> ListBinFiles()
    {
        var list = new List<string>();
        if (!Directory.Exists(BinDirectory)) return list;

        foreach (var file in Directory.GetFiles(BinDirectory))
            list.Add(Path.GetFileName(file));

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>要推送的 bin 文件（文件名，空表示未选择）。</summary>
    public static string RemotePath
    {
        get
        {
            var v = Current?.RemotePath;
            return string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();
        }
        set
        {
            if (Current is not { } cfg) return;
            cfg.RemotePath = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            ProjectService.SaveCurrentProject();
        }
    }
}
