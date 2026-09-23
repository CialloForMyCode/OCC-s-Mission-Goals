namespace OCCMissionGoals.Models;

/// <summary>插件/扩展信息。</summary>
public class PluginInfo
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
}
