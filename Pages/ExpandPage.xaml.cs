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
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Pages;

public partial class ExpandPage : Page
{
    private List<PluginInfo> _allPlugins => PluginCatalog.All;
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
        ApplyFilter();
    }

    private void LoadPlaceholderData()
    {
        _allPlugins.Clear();

        // TODO: 从真实数据源加载插件列表（填充 PluginCatalog.All）
        // _allPlugins.AddRange(...);

        _currentCategory = "全部";
        CategoryList.ItemsSource = new List<CategoryItem>();
    }

    // ==================== 过滤 & 搜索 ====================

    private void ApplyFilter()
    {
        var hasAny = _allPlugins.Count > 0;

        CategorySection.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        PluginList.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;

        if (!hasAny) return;

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

        PluginList.ItemsSource = filtered.ToList();
    }

    // ==================== 事件处理 ====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearch = SearchBox.Text;
        ApplyFilter();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CategoryItem item)
        {
            _currentCategory = item.Name;

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
                    LocalizationManager.T(
                        $"正在安装「{plugin.Name}」...\n\n（占位：将来对接真实下载源后，此处将触发下载并安装流程。）",
                        $"Installing \"{plugin.Name}\"...\n\n(Placeholder: once a real download source is wired up, this will trigger the download and install flow.)",
                        $"Установка «{plugin.Name}»...\n\n(Заглушка: после подключения реального источника загрузки здесь запустится загрузка и установка.)"),
                    LocalizationManager.T("安装扩展", "Install Extension", "Установить расширение"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                plugin.IsInstalled = true;
                ApplyFilter();
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }
}

// ==================== 数据模型 ====================

/// <summary>分类筛选项。</summary>
public class CategoryItem
{
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}
