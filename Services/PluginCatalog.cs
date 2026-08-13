using System.Collections.Generic;
using System.Linq;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 插件目录（当前为占位实现，暂无真实数据源）。
/// 扩展中心（ExpandPage）与搜索框共用同一份列表，保证 Plugins: / Expand: 搜索与页面一致。
/// </summary>
public static class PluginCatalog
{
    /// <summary>全部插件（含未安装与已安装）。</summary>
    public static List<PluginInfo> All { get; } = new();

    /// <summary>已安装的插件。</summary>
    public static IEnumerable<PluginInfo> Installed => All.Where(p => p.IsInstalled);
}
