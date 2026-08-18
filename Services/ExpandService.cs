using System.IO;

namespace OCCMissionGoals.Services;

/// <summary>
/// 扩展服务：管理扩展插件（区别于语言包）的本地存放目录。
/// 语言包安装在 exe 同目录的 Languages 文件夹，扩展插件安装在 exe 同目录的 Expand 文件夹。
/// </summary>
public static class ExpandService
{
    private const string ExpandDir = "Expand";

    /// <summary>本地扩展插件目录（exe 同目录下的 Expand）。</summary>
    public static string LocalExpandDirectory =>
        Path.Combine(AppContext.BaseDirectory, ExpandDir);

    /// <summary>确保本地扩展插件目录存在（下载 / 安装扩展插件前调用）。</summary>
    public static void EnsureDirectory() =>
        Directory.CreateDirectory(LocalExpandDirectory);
}
