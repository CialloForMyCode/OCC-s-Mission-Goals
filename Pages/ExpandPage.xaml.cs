using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Pages;

public partial class ExpandPage : Page
{
    private readonly List<PluginInfo> _allPlugins = new();
    private string _currentCategory = "全部";
    private string _currentSearch = "";

    public ExpandPage()
    {
        InitializeComponent();
        LoadPlaceholderData();
        ApplyFilter();
    }

    /// <summary>外部调用刷新（用于项目切换等场景）。</summary>
    public void Refresh()
    {
        // 将来对接真实数据源时，在这里重新拉取
        ApplyFilter();
    }

    // ==================== 占位数据 ====================

    private void LoadPlaceholderData()
    {
        // TODO: 替换为真实数据源（GitHub Release / 服务器 API / 本地文件夹）
        _allPlugins.Clear();
        _allPlugins.AddRange(new[]
        {
            new PluginInfo
            {
                Id = "occ.theme-dark-pro",
                Name = "暗夜 Pro 主题",
                Icon = "🌙",
                Description = "深色 OLED 优化主题，护眼且省电，支持自定义色彩方案。",
                Author = "OCC Team",
                Version = "1.2.0",
                Category = "主题",
                Downloads = 3420,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.exporter-pdf",
                Name = "PDF 导出器",
                Icon = "📄",
                Description = "将项目报告和条目列表导出为精美的 PDF 文档，支持自定义模板。",
                Author = "OCC Team",
                Version = "2.1.3",
                Category = "工具",
                Downloads = 1890,
                IsInstalled = true
            },
            new PluginInfo
            {
                Id = "occ.sync-webdav",
                Name = "WebDAV 同步",
                Icon = "☁️",
                Description = "通过 WebDAV 协议将项目数据同步到自建云盘或 NAS。",
                Author = "Community",
                Version = "0.9.1",
                Category = "集成",
                Downloads = 756,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.chart-gantt",
                Name = "甘特图视图",
                Icon = "📊",
                Description = "以甘特图形式展示任务时间线和依赖关系，支持拖拽调整。",
                Author = "Community",
                Version = "1.0.0",
                Category = "视图",
                Downloads = 2103,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.notify-email",
                Name = "邮件通知",
                Icon = "📧",
                Description = "在条目到期前通过邮件发送提醒，支持 SMTP 自定义配置。",
                Author = "OCC Team",
                Version = "1.0.2",
                Category = "集成",
                Downloads = 1204,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.lang-zh-tw",
                Name = "繁體中文語言包",
                Icon = "🌐",
                Description = "將介面翻譯為繁體中文（臺灣），包含所有頁面和對話框。",
                Author = "Community",
                Version = "1.1.0",
                Category = "语言",
                Downloads = 945,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.ai-assistant",
                Name = "AI 智能助手",
                Icon = "🤖",
                Description = "基于大语言模型的智能任务拆解和优先级建议，支持自然语言输入。",
                Author = "OCC Team",
                Version = "0.5.0-beta",
                Category = "工具",
                Downloads = 4507,
                IsInstalled = false
            },
            new PluginInfo
            {
                Id = "occ.backup-auto",
                Name = "自动备份",
                Icon = "💾",
                Description = "定时自动备份项目数据到指定目录，支持增量备份和版本保留策略。",
                Author = "OCC Team",
                Version = "2.0.0",
                Category = "工具",
                Downloads = 2876,
                IsInstalled = true
            }
        });

        // 分类列表
        var categories = new[] { "全部", "已安装" }
            .Concat(_allPlugins.Select(p => p.Category).Distinct().OrderBy(c => c))
            .Select(c => new CategoryItem { Name = c, IsSelected = c == _currentCategory })
            .ToList();

        CategoryList.ItemsSource = categories;
    }

    // ==================== 过滤 & 搜索 ====================

    private void ApplyFilter()
    {
        var filtered = _allPlugins.AsEnumerable();

        // 分类筛选
        if (_currentCategory == "已安装")
            filtered = filtered.Where(p => p.IsInstalled);
        else if (_currentCategory != "全部")
            filtered = filtered.Where(p => p.Category == _currentCategory);

        // 搜索筛选
        if (!string.IsNullOrWhiteSpace(_currentSearch))
        {
            var kw = _currentSearch.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                p.Author.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        PluginList.ItemsSource = list;
        EmptyHint.Visibility = list.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ==================== 事件处理 ====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearch = SearchBox.Text;
        ApplyFilter();

        // 隐藏/显示 placeholder 文字
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CategoryItem item)
        {
            _currentCategory = item.Name;

            // 更新选中状态
            if (CategoryList.ItemsSource is List<CategoryItem> categories)
            {
                foreach (var c in categories)
                    c.IsSelected = c.Name == _currentCategory;
                CategoryList.ItemsSource = null;
                CategoryList.ItemsSource = categories;
            }

            ApplyFilter();
        }
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginInfo plugin)
        {
            if (plugin.IsInstalled)
            {
                // TODO: 卸载逻辑
                plugin.IsInstalled = false;
                ApplyFilter();
            }
            else
            {
                // TODO: 下载安装逻辑
                MessageBox.Show(
                    $"正在安装「{plugin.Name}」...\n\n（占位：将来对接真实下载源后，此处将触发下载并安装流程。）",
                    "安装扩展",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                plugin.IsInstalled = true;
                ApplyFilter();
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 从远程源重新拉取插件列表
        ApplyFilter();
    }
}

// ==================== 数据模型 ====================

/// <summary>插件/扩展信息。</summary>
public class PluginInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🧩";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    public string Category { get; set; } = "";
    public int Downloads { get; set; }
    public bool IsInstalled { get; set; }
}

/// <summary>分类筛选项。</summary>
public class CategoryItem
{
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}
