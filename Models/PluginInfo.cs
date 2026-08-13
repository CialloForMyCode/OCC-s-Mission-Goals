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
    public string Category { get; set; } = "";
    public int Downloads { get; set; }
    public bool IsInstalled { get; set; }
}
