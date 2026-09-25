using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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

    /// <summary>主题在扩展中心里的稳定分类键。</summary>
    private const string ThemePackCategory = "theme";

    /// <summary>本地扩展（Expand 目录）在扩展中心里的稳定分类键。</summary>
    private const string ExpandCategory = "extension";

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
    private List<ThemePack> _themes = new();

    /// <summary>本次目录中出现的本地扩展，按清单 Id 索引（按钮点击时回查）。</summary>
    private readonly Dictionary<string, ExpandInfo> _expandMap = new();
    private string _currentCategory = "all";
    private bool _loading;
    private bool _hasLoaded;
    private bool _checkingUpdates;

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
        // 重新扫描 Expand 目录：用户可能刚把扩展放进去 / 删掉，刷新页面时同步。
        ExpandService.Reload();

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
        List<ThemePack>? themes = null;
        string? error = null;
        try
        {
            packs = await LanguagePackService.FetchAvailableAsync();
            themes = await ThemePackService.FetchAvailableAsync();
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
            // 网络失败也要把本地扩展列出来 —— 它们不依赖网络。
            RebuildCatalog();
            if (PluginCatalog.All.Count == 0)
                SetEmptyHint(error);
            return;
        }

        _packs = packs ?? new List<LanguagePack>();
        _themes = themes ?? new List<ThemePack>();
        _hasLoaded = true;
        RebuildCatalog();
    }

    /// <summary>用已拉取的语言包与主题重建目录（含本地化文案与安装状态）并重新应用筛选。</summary>
    private void RebuildCatalog()
    {
        PluginCatalog.All.Clear();
        _expandMap.Clear();

        // 本地扩展（Expand 目录）与语言包、主题并列；对扩展来说「安装状态」= 是否启用。
        foreach (var ext in ExpandService.All)
        {
            _expandMap[ext.Id] = ext;
            PluginCatalog.All.Add(BuildExpandPlugin(ext));
        }

        foreach (var pack in _packs)
            PluginCatalog.All.Add(BuildPlugin(pack));
        foreach (var theme in _themes)
            PluginCatalog.All.Add(BuildThemePlugin(theme));

        BuildCategories();
        ApplyFilter();

        // 列表渲染后异步核对已安装项是否落后于仓库里的内容（有新版时卡片上出现「更新」按钮）。
        _ = CheckUpdatesAsync();
    }

    private static PluginInfo BuildPlugin(LanguagePack pack) => new()
    {
        Id = "lang:" + pack.Code,
        Name = pack.Name,
        Icon = "",
        Description = string.IsNullOrWhiteSpace(pack.Description)
            ? LanguagePackDescription(pack)
            : pack.Description,
        Author = string.IsNullOrWhiteSpace(pack.Author)
            ? LocalizationManager.T("匿名开发者")
            : pack.Author,
        Version = "",
        Category = LanguagePackCategory,
        CategoryName = LocalizationManager.T("语言包"),
        Downloads = 0,
        IsInstalled = LanguagePackService.IsInstalled(pack.Code),
        ActionLabel = LocalizationManager.T(LanguagePackService.IsInstalled(pack.Code) ? "卸载" : "安装"),
        InstalledLabel = LocalizationManager.T("已安装"),
        DownloadUrl = pack.DownloadUrl,
        FileName = pack.FileName,
        RemoteSha = pack.Sha,
    };

    private static PluginInfo BuildThemePlugin(ThemePack theme) => new()
    {
        Id = "theme:" + theme.Name,
        Name = theme.Name,
        Icon = "",
        Description = string.IsNullOrWhiteSpace(theme.Description)
            ? LocalizationManager.T("主题")
            : theme.Description,
        Author = string.IsNullOrWhiteSpace(theme.Author)
            ? LocalizationManager.T("匿名开发者")
            : theme.Author,
        Version = "",
        Category = ThemePackCategory,
        CategoryName = LocalizationManager.T("主题"),
        Downloads = 0,
        IsInstalled = ThemePackService.IsInstalled(theme.FileName),
        ActionLabel = LocalizationManager.T(ThemePackService.IsInstalled(theme.FileName) ? "卸载" : "安装"),
        InstalledLabel = LocalizationManager.T("已安装"),
        DownloadUrl = theme.DownloadUrl,
        FileName = theme.FileName,
        RemoteSha = theme.Sha,
    };

    /// <summary>把本地扩展（Expand 目录）转换成扩展中心的目录项。
    /// 对扩展而言「安装状态」= 是否启用；清单或资源有问题时把原因显示在简介里。</summary>
    private static PluginInfo BuildExpandPlugin(ExpandInfo ext) => new()
    {
        Id = ext.Id,
        Name = ext.Name,
        Icon = "",
        Description = string.IsNullOrWhiteSpace(ext.LoadError) ? ext.Description : ext.LoadError,
        Author = string.IsNullOrWhiteSpace(ext.Author)
            ? LocalizationManager.T("匿名开发者")
            : ext.Author,
        Version = ext.Version,
        Category = ExpandCategory,
        CategoryName = LocalizationManager.T("扩展"),
        Downloads = 0,
        IsInstalled = ext.Enabled,
        ActionLabel = LocalizationManager.T(ext.Enabled ? "禁用" : "启用"),
        InstalledLabel = LocalizationManager.T("已启用"),
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
        // 是否显示安装进度条由 ToggleInstall 判断：只有需要下载的安装才显示。
        if (sender is Button { Tag: PluginInfo plugin })
            await ToggleInstall(plugin);
    }

    /// <summary>安装 / 卸载插件（供右键菜单使用，不依赖具体按钮）。</summary>
    public async Task ToggleInstall(PluginInfo plugin)
    {
        // 正在安装（下载）时忽略重复触发：按钮已折叠，右键菜单仍可能点到。
        if (plugin.IsInstalling) return;

        if (plugin.Category == ExpandCategory)
        {
            ToggleExpand(plugin);
            return;
        }

        if (plugin.Category == ThemePackCategory)
        {
            await ToggleThemeInstall(plugin);
            return;
        }

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
            RebuildCatalog();
            ShowTip(LocalizationManager.T("已卸载语言包「{0}」。", plugin.Name));
            return;
        }

        var progress = BeginInstall(plugin);
        try
        {
            var error = await LanguagePackService.InstallAsync(pack, progress);
            if (error != null)
            {
                ShowTip(error);
                return;
            }

            // 重新扫描语言目录，让新语言立即出现在设置页并可切换。
            LocalizationManager.Instance.Reload();
            RebuildCatalog();
            ShowTip(LocalizationManager.T("语言包「{0}」安装成功，可在「设置 → 语言」中切换。", plugin.Name));
        }
        catch (Exception ex)
        {
            ShowTip(LocalizationManager.T("安装失败：{0}", ex.Message));
        }
        finally
        {
            EndInstall(plugin);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    /// <summary>启用 / 禁用本地扩展：改写它的 expand.json 并立即重新应用资源覆盖。</summary>
    private void ToggleExpand(PluginInfo plugin)
    {
        if (!_expandMap.TryGetValue(plugin.Id, out var ext))
        {
            ShowTip(LocalizationManager.T("找不到扩展目录。"));
            return;
        }

        var error = ExpandService.SetEnabled(ext, !ext.Enabled);
        if (error != null)
        {
            ShowTip(error);
            return;
        }

        RebuildCatalog();
        ShowTip(LocalizationManager.T(
            ext.Enabled ? "已启用扩展「{0}」。" : "已禁用扩展「{0}」。", ext.Name));
    }

    /// <summary>安装 / 卸载主题：写入或删除本地 Themes 目录，并让主题下拉立即生效。</summary>
    private async Task ToggleThemeInstall(PluginInfo plugin)
    {
        var theme = ToThemePack(plugin);

        if (plugin.IsInstalled)
        {
            var error = ThemePackService.Uninstall(theme);
            if (error != null)
            {
                ShowTip(error);
                return;
            }

            ThemeManager.Reload();
            RebuildCatalog();
            ShowTip(LocalizationManager.T("已卸载主题「{0}」。", plugin.Name));
            return;
        }

        var progress = BeginInstall(plugin);
        try
        {
            var error = await ThemePackService.InstallAsync(theme, progress);
            if (error != null)
            {
                ShowTip(error);
                return;
            }

            ThemeManager.Reload();
            RebuildCatalog();
            ShowTip(LocalizationManager.T("主题「{0}」安装成功，可在「设置 → 主题样式」中切换。", plugin.Name));
        }
        catch (Exception ex)
        {
            ShowTip(LocalizationManager.T("安装失败：{0}", ex.Message));
        }
        finally
        {
            EndInstall(plugin);
        }
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
        return new LanguagePack(code, plugin.Name, plugin.Author, plugin.Description, plugin.FileName, plugin.DownloadUrl);
    }

    /// <summary>从目录项还原主题包（用于安装 / 卸载时定位远程文件）。</summary>
    private static ThemePack ToThemePack(PluginInfo plugin) =>
        new(plugin.Name, plugin.Author, plugin.Description, plugin.FileName, plugin.DownloadUrl);

    // ==================== 安装进度 ====================

    /// <summary>
    /// 把目录项切到「安装中」，并返回把服务端上报的进度写入该目录项的上报器
    /// （0–100；-1 表示响应未给出总大小，此时只显示「下载中…」）。
    /// 报告在创建它的线程（UI 线程）上回调，属性变更直接驱动卡片上的进度条。
    /// </summary>
    private static IProgress<double> BeginInstall(PluginInfo plugin)
    {
        plugin.InstallProgress = 0;
        plugin.InstallProgressText = LocalizationManager.T("下载中…");
        plugin.IsInstalling = true;

        return new Progress<double>(percent =>
        {
            plugin.InstallProgress = percent < 0 ? 0 : percent;
            plugin.InstallProgressText = percent < 0
                ? LocalizationManager.T("下载中…")
                : percent.ToString("F0") + "%";
        });
    }

    /// <summary>结束安装状态：收起进度条，恢复卡片上的操作按钮。</summary>
    private static void EndInstall(PluginInfo plugin)
    {
        plugin.IsInstalling = false;
        plugin.InstallProgress = 0;
        plugin.InstallProgressText = "";
    }

    // ==================== 更新检查 ====================

    /// <summary>
    /// 核对已安装的语言包 / 主题是否落后于仓库里的同名文件：
    /// contents API 给出的 blob SHA 与本地文件内容的 blob SHA 相同即视为最新。
    /// 只读本地文件、不额外下载；计算放后台线程，结果回到 UI 线程赋值。
    /// </summary>
    private async Task CheckUpdatesAsync()
    {
        if (_checkingUpdates) return;

        // 只检查拿到远程 SHA 的已安装项；本地扩展（Expand 目录）不联网。
        var all = PluginCatalog.All.ToList();
        var targets = all
            .Where(p => p.IsInstalled && !string.IsNullOrWhiteSpace(p.RemoteSha))
            .ToList();
        if (targets.Count == 0)
        {
            // 没有可检查的项时也要保证未安装的卡片不残留「更新」按钮。
            foreach (var plugin in all)
                plugin.HasUpdate = false;
            return;
        }

        _checkingUpdates = true;
        try
        {
            var outdated = await Task.Run(() => targets.Where(IsOutdated).ToHashSet());

            // 逐个显式赋值：只有「已安装 + 与仓库不一致」才留下「更新」按钮，其余一律复位。
            foreach (var plugin in all)
                plugin.HasUpdate = outdated.Contains(plugin);
        }
        catch
        {
            // 读文件失败（目录被删、文件被占用等）不打扰用户：保持没有「更新」按钮的状态。
            foreach (var plugin in all)
                plugin.HasUpdate = false;
        }
        finally
        {
            _checkingUpdates = false;
        }
    }

    /// <summary>本地文件是否与仓库内容不一致；本地文件缺失时不提示更新。</summary>
    private static bool IsOutdated(PluginInfo plugin)
    {
        var directory = plugin.Category == ThemePackCategory
            ? ThemePackService.LocalThemesDirectory
            : LanguagePackService.LocalLanguagesDirectory;
        var path = Path.Combine(directory, plugin.FileName);

        if (!File.Exists(path)) return false;

        return !string.Equals(BlobSha(path), plugin.RemoteSha, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按 git 的算法算文件的 blob SHA（SHA-1 over "blob {字节数}\0" + 内容），
    /// 与 GitHub contents API 返回的 sha 可直接比较。
    /// </summary>
    private static string BlobSha(string path)
    {
        var content = File.ReadAllBytes(path);
        var header = Encoding.UTF8.GetBytes($"blob {content.Length}\0");

        using var sha1 = SHA1.Create();
        sha1.TransformBlock(header, 0, header.Length, null, 0);
        sha1.TransformFinalBlock(content, 0, content.Length);
        return Convert.ToHexString(sha1.Hash!).ToLowerInvariant();
    }

    // ==================== 更新（升级到仓库最新版） ====================

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PluginInfo plugin })
            await UpdatePluginAsync(plugin);
    }

    /// <summary>
    /// 把已安装的语言包 / 主题升级为仓库里的最新内容：直接下载覆盖（不经过卸载），
    /// 卡片上的进度条与安装共用。
    /// </summary>
    public async Task UpdatePluginAsync(PluginInfo plugin)
    {
        if (plugin.IsInstalling || !plugin.IsInstalled || !plugin.HasUpdate) return;
        if (plugin.Category == ExpandCategory) return;

        var progress = BeginInstall(plugin);
        try
        {
            if (plugin.Category == ThemePackCategory)
            {
                var error = await ThemePackService.InstallAsync(ToThemePack(plugin), progress);
                if (error != null)
                {
                    ShowTip(error);
                    return;
                }

                ThemeManager.Reload();
            }
            else
            {
                var error = await LanguagePackService.InstallAsync(ToPack(plugin), progress);
                if (error != null)
                {
                    ShowTip(error);
                    return;
                }

                // 重新扫描语言目录，让更新后的文案立即生效。
                LocalizationManager.Instance.Reload();
            }

            RebuildCatalog();
            ShowTip(LocalizationManager.T("「{0}」已更新到最新版本。", plugin.Name));
        }
        catch (Exception ex)
        {
            ShowTip(LocalizationManager.T("更新失败：{0}", ex.Message));
        }
        finally
        {
            EndInstall(plugin);
        }
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
