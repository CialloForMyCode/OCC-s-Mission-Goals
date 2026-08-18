using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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
    /// <summary>语言包在扩展中心里的稳定分类键（用于筛选，显示名见 <see cref="PluginInfo.CategoryName"/>）。</summary>
    private const string LanguagePackCategory = "language-pack";

    /// <summary>语言代码 → 中文语言名（用于生成「中文语言包 / 英文语言包」这类简介）。</summary>
    private static readonly Dictionary<string, string> _languageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh"] = "中文",
        ["en"] = "英文",
        ["ja"] = "日文",
        ["ko"] = "韩文",
        ["ru"] = "俄文",
    };

    private List<LanguagePack> _packs = new();
    private string _currentCategory = "all";
    private string _currentSearch = "";
    private bool _loading;
    private bool _hasLoaded;

    private List<PluginInfo> _allPlugins => PluginCatalog.All;

    public ExpandPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    /// <summary>
    /// 导航到本页或全局刷新时调用。已加载时仅重建（重本地化 + 重算安装状态），
    /// 不重复请求网络；尚未加载时触发一次加载。
    /// </summary>
    public void Refresh()
    {
        if (_hasLoaded)
        {
            RebuildCatalog();
        }
        else if (!_loading)
        {
            _ = LoadAsync();
        }
    }

    // ==================== 数据加载 ====================

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;

        CategorySection.Visibility = Visibility.Collapsed;
        PluginList.Visibility = Visibility.Collapsed;
        SetEmptyHint(LocalizationManager.T("正在加载扩展…"));

        List<LanguagePack>? packs = null;
        string? error = null;
        try
        {
            packs = await LanguagePackService.FetchAvailableAsync();
        }
        catch (Exception ex)
        {
            error = LocalizationManager.T("加载扩展失败：{0}", ex.Message) + "\n" +
                    LocalizationManager.T("点击「刷新」重试。");
        }
        finally
        {
            _loading = false;
        }

        if (error != null)
        {
            SetEmptyHint(error);
            return;
        }

        _packs = packs ?? new List<LanguagePack>();
        _hasLoaded = true;
        RebuildCatalog();
    }

    /// <summary>用已拉取的语言包重建目录（含本地化文案与安装状态）并重新应用筛选。</summary>
    private void RebuildCatalog()
    {
        PluginCatalog.All.Clear();
        foreach (var pack in _packs)
            PluginCatalog.All.Add(BuildPlugin(pack));

        BuildCategories();
        ApplyFilter();
    }

    private static PluginInfo BuildPlugin(LanguagePack pack) => new()
    {
        Id = "lang:" + pack.Code,
        Name = pack.Name,
        Icon = "",
        Description = LanguagePackDescription(pack),
        Author = LanguagePackService.RepoOwner,
        Version = "",
        Category = LanguagePackCategory,
        CategoryName = LocalizationManager.T("语言包"),
        Downloads = 0,
        IsInstalled = LanguagePackService.IsInstalled(pack.Code),
        DownloadUrl = pack.DownloadUrl,
        FileName = pack.FileName,
    };

    /// <summary>生成语言包简介，例如「中文语言包」「英文语言包」。</summary>
    private static string LanguagePackDescription(LanguagePack pack)
    {
        var name = _languageNames.TryGetValue(pack.Code, out var known)
            ? known
            : string.IsNullOrWhiteSpace(pack.Name) ? pack.Code : pack.Name;
        return name + LocalizationManager.T("语言包");
    }

    private void BuildCategories()
    {
        var items = new List<CategoryItem>
        {
            new() { Key = "all", Name = LocalizationManager.T("全部"), IsSelected = _currentCategory == "all" },
            new() { Key = "installed", Name = LocalizationManager.T("已安装"), IsSelected = _currentCategory == "installed" },
        };

        foreach (var cat in _allPlugins.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
        {
            var display = _allPlugins.First(p => p.Category == cat).CategoryName;
            if (string.IsNullOrWhiteSpace(display)) display = cat;
            items.Add(new() { Key = cat, Name = display, IsSelected = _currentCategory == cat });
        }

        CategoryList.ItemsSource = items;
    }

    // ==================== 过滤 & 搜索 ====================

    private void ApplyFilter()
    {
        var hasAny = _allPlugins.Count > 0;

        if (!hasAny)
        {
            CategorySection.Visibility = Visibility.Collapsed;
            PluginList.Visibility = Visibility.Collapsed;
            SetEmptyHint(LocalizationManager.T("没有扩展"));
            return;
        }

        SetEmptyHint(null);
        CategorySection.Visibility = Visibility.Visible;
        PluginList.Visibility = Visibility.Visible;

        var filtered = _allPlugins.AsEnumerable();

        // 分类筛选（使用稳定键）
        if (_currentCategory == "installed")
            filtered = filtered.Where(p => p.IsInstalled);
        else if (_currentCategory != "all")
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

    private void SetEmptyHint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            EmptyHint.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyHintText.Text = text;
        EmptyHint.Visibility = Visibility.Visible;
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
            _currentCategory = item.Key;

            if (CategoryList.ItemsSource is IEnumerable<CategoryItem> categories)
            {
                foreach (var c in categories)
                    c.IsSelected = c.Key == _currentCategory;
            }

            ApplyFilter();
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PluginInfo plugin) return;

        if (plugin.IsInstalled)
        {
            await ToggleInstall(plugin);
            return;
        }

        btn.IsEnabled = false;
        btn.Content = LocalizationManager.T("下载中…");
        try
        {
            await ToggleInstall(plugin);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.ClearValue(ContentControl.ContentProperty);
        }
    }

    /// <summary>安装 / 卸载插件（供右键菜单使用，不依赖具体按钮）。</summary>
    public async Task ToggleInstall(PluginInfo plugin)
    {
        var pack = ToPack(plugin);

        if (plugin.IsInstalled)
        {
            // 卸载不再弹确认框，直接在状态栏给出结果提示。
            var error = LanguagePackService.Uninstall(pack);
            if (error != null)
            {
                ShowTip(error);
                return;
            }

            LocalizationManager.Instance.Reload();
            ShowTip(LocalizationManager.T("已卸载语言包「{0}」。", plugin.Name));
            return;
        }

        try
        {
            var error = await LanguagePackService.InstallAsync(pack);
            if (error != null)
            {
                ShowTip(error);
                return;
            }

            // 重新扫描语言目录，让新语言立即出现在设置页并可切换。
            LocalizationManager.Instance.Reload();
            ShowTip(LocalizationManager.T("语言包「{0}」安装成功，可在「设置 → 语言」中切换。", plugin.Name));
        }
        catch (Exception ex)
        {
            ShowTip(LocalizationManager.T("安装失败：{0}", ex.Message));
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    // ==================== 内部辅助 ====================

    /// <summary>从目录项还原语言包（用于安装 / 卸载时定位远程文件）。</summary>
    private static LanguagePack ToPack(PluginInfo plugin)
    {
        var code = plugin.Id.StartsWith("lang:", StringComparison.Ordinal)
            ? plugin.Id["lang:".Length..]
            : plugin.FileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                ? plugin.FileName[..^5]
                : plugin.Id;
        return new LanguagePack(code, plugin.Name, plugin.FileName, plugin.DownloadUrl);
    }

    /// <summary>在状态栏显示一条非阻塞提示（替代安装 / 卸载弹窗）。</summary>
    private void ShowTip(string message) =>
        (Window.GetWindow(this) as MainWindow)?.SetTipText(message);
}

// ==================== 数据模型 ====================

/// <summary>分类筛选项（Key 为稳定键，Name 为本地化显示名）。</summary>
public class CategoryItem
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}
