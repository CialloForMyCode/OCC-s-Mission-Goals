namespace OCCMissionGoals.Models;

/// <summary>
/// 推送目标仓库信息（用于「更新日志」提交生成与「设置」页面的仓库管理）。
/// </summary>
public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
