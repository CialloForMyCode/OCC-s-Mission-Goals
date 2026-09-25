using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OCCMissionGoals.Models;

/// <summary>插件/扩展信息。</summary>
public class PluginInfo : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>图标路径（文件路径、pack URI 或 HTTP URL）。</summary>
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>稳定分类键（用于筛选）。</summary>
    public string Category { get; set; } = "";
    /// <summary>分类的本地化显示名。</summary>
    public string CategoryName { get; set; } = "";
    public int Downloads { get; set; }
    public bool IsInstalled { get; set; }
    /// <summary>操作按钮文案（安装 / 卸载 / 启用 / 禁用），由各来源按自身语义填充。</summary>
    public string ActionLabel { get; set; } = "";
    /// <summary>已安装徽标文案（已安装 / 已启用）。</summary>
    public string InstalledLabel { get; set; } = "";
    /// <summary>下载地址（语言包等远程资源的 raw URL）。</summary>
    public string DownloadUrl { get; set; } = "";
    /// <summary>本地文件名（例如 de.xaml），安装 / 卸载时使用。</summary>
    public string FileName { get; set; } = "";

    /// <summary>仓库中该文件的 blob SHA（取自 GitHub contents API），用于判断本地是否为最新。</summary>
    public string RemoteSha { get; set; } = "";

    private bool _hasUpdate;

    /// <summary>本地文件与仓库中的最新内容不一致时，卡片上出现「更新」按钮。</summary>
    public bool HasUpdate
    {
        get => _hasUpdate;
        set => SetField(ref _hasUpdate, value);
    }

    private bool _isInstalling;

    /// <summary>是否正在安装（下载）中。为 true 时卡片上用进度条替换操作按钮。</summary>
    public bool IsInstalling
    {
        get => _isInstalling;
        set => SetField(ref _isInstalling, value);
    }

    private double _installProgress;

    /// <summary>安装进度（0–100），与 <see cref="IsInstalling"/> 一同驱动卡片上的进度条。</summary>
    public double InstallProgress
    {
        get => _installProgress;
        set => SetField(ref _installProgress, value);
    }

    private string _installProgressText = "";

    /// <summary>安装进度文案（如「42%」；总大小未知时为「下载中…」）。</summary>
    public string InstallProgressText
    {
        get => _installProgressText;
        set => SetField(ref _installProgressText, value);
    }

    /// <summary>安装状态变化时通知界面（其余属性在列表重建时一次性赋值，无需通知）。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
