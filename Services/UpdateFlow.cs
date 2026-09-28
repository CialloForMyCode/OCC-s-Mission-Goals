using System;
using System.Threading.Tasks;
using System.Windows;

namespace OCCMissionGoals.Services;

/// <summary>一次「下载并安装更新」流程的结果。</summary>
public enum UpdateOutcome
{
    /// <summary>没有可用更新，或检查阶段就失败了。</summary>
    Nothing,

    /// <summary>用户选择暂不更新。</summary>
    Declined,

    /// <summary>下载或启动安装程序失败，原因已写进状态文字。</summary>
    Failed,

    /// <summary>安装程序已启动：调用方应立刻退出应用，好让安装器覆盖主程序。</summary>
    InstallerStarted
}

/// <summary>
/// 「检查更新 → 询问用户 → 下载安装包 → 启动安装器」的共用流程。
/// 主窗口的「检查更新」命令、启动时的静默检查、设置页的「下载并安装」都走这里，
/// 免得各处各写一遍、行为还不一致。
/// </summary>
public static class UpdateFlow
{
    /// <summary>
    /// 检查更新；有更新时询问用户，确认后下载并启动安装程序。
    /// 失败与「已是最新」都会把消息写进状态文字，由调用方展示。
    /// </summary>
    public static async Task<UpdateOutcome> CheckAndInstallAsync(Window owner, Action<string> setStatus)
    {
        setStatus(LocalizationManager.T("正在检查更新…"));
        var result = await UpdateService.CheckAsync();

        if (!result.Succeeded || !result.HasUpdate)
        {
            setStatus(result.Message);
            return UpdateOutcome.Nothing;
        }

        return await AskAndInstallAsync(owner, result, setStatus);
    }

    /// <summary>
    /// 已经拿到检查结果时使用：先问用户，同意后再装。
    /// 启动时的静默检查走这条路径，没有更新就什么都不做、不打扰用户。
    /// </summary>
    public static async Task<UpdateOutcome> AskAndInstallAsync(
        Window owner, UpdateCheckResult result, Action<string> setStatus)
    {
        var yes = MessageBox.Show(
            owner,
            LocalizationManager.T("发现新版本 {0}，是否下载并安装？", result.LatestVersion),
            LocalizationManager.T("更新"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (yes != MessageBoxResult.Yes) return UpdateOutcome.Declined;

        return await InstallAsync(result, setStatus);
    }

    /// <summary>下载安装包并启动安装程序（调用前应已得到用户同意）。</summary>
    public static async Task<UpdateOutcome> InstallAsync(UpdateCheckResult result, Action<string> setStatus)
    {
        if (string.IsNullOrEmpty(result.InstallerDownloadUrl))
        {
            // 没有可下载的安装包时退回到发布页，让用户自己取。
            setStatus(LocalizationManager.T("下载更新失败。"));
            if (!string.IsNullOrEmpty(result.HtmlUrl)) UpdateService.OpenUrl(result.HtmlUrl);
            return UpdateOutcome.Failed;
        }

        setStatus(LocalizationManager.T("正在下载更新…"));
        var progress = new Progress<string>(setStatus);

        string? path;
        try
        {
            path = await UpdateService.DownloadInstallerAsync(
                result.InstallerDownloadUrl, UpdateService.InstallerFileName, progress);
        }
        catch (Exception)
        {
            setStatus(LocalizationManager.T("下载更新失败。"));
            return UpdateOutcome.Failed;
        }

        if (string.IsNullOrEmpty(path))
        {
            // 下载失败 / 更新包不完整的具体原因，DownloadInstallerAsync 已经报给 setStatus 了。
            return UpdateOutcome.Failed;
        }

        setStatus(LocalizationManager.T("正在启动安装程序…"));
        if (!UpdateService.LaunchInstaller(path))
        {
            setStatus(LocalizationManager.T("启动安装程序失败：{0}", path));
            return UpdateOutcome.Failed;
        }

        return UpdateOutcome.InstallerStarted;
    }

    /// <summary>
    /// 更新装完后重启应用：安装器要覆盖正在运行的 OCCMissionGoals.exe，必须先退出。
    /// </summary>
    public static void ShutdownForUpdate()
    {
        try
        {
            Application.Current?.Shutdown();
        }
        catch
        {
            // 退不掉也不该把「更新」这件事本身变成失败。
        }
    }
}
