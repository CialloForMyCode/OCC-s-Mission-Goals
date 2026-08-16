using System.Diagnostics;
using Microsoft.Win32;

namespace OCCMissionGoals.Services;

/// <summary>
/// 开机自启动：通过当前用户的「启动」注册表项（HKCU\...\Run）实现，
/// 无需管理员权限。仅在 Windows 上可用。
/// </summary>
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>注册表值名，作为稳定标识，避免应用名中的撇号造成问题。</summary>
    private const string ValueName = "OCCMissionGoals";

    /// <summary>当前是否已开启开机自启动。</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>开启或关闭开机自启动。开启时把当前可执行文件路径写入注册表。</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);

        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var exe = GetExecutablePath();
        if (string.IsNullOrEmpty(exe))
            throw new InvalidOperationException("无法确定当前可执行文件路径。");

        // 用引号包裹，避免路径含空格时被 Windows 拆分。
        key.SetValue(ValueName, $"\"{exe}\"");
    }

    /// <summary>获取当前进程的可执行文件完整路径。</summary>
    private static string? GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
            return path;

        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
