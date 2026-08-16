using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Pages;

public partial class SettingsPage : Page
{
    private bool _loading;
    private readonly Dictionary<string, FrameworkElement> _sectionMap = new();
    private readonly Dictionary<string, Button> _navMap = new();
    private string _currentTag = "Appearance";
    private bool _isNavigating;
    private UpdateCheckResult? _latestUpdate;

    private readonly List<GitHubRepo> _repoOptions = new();
    private readonly List<string> _branchOptions = new();
    private RepositoryInfo? _currentRepo;
    private bool _syncingRepo;

    public SettingsPage()
    {
        InitializeComponent();

        _sectionMap = new Dictionary<string, FrameworkElement>
        {
            ["Appearance"] = Section_Appearance,
            ["Project"]    = Section_Project,
            ["Push"]       = Section_Push,
            ["System"]     = Section_System,
        };

        _navMap = new Dictionary<string, Button>
        {
            ["Appearance"] = Nav_Appearance,
            ["Project"]    = Nav_Project,
            ["Push"]       = Nav_Push,
            ["System"]     = Nav_System,
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HighlightNav(_currentTag);
    }

    // ==================== 侧边导航 → 滚动 ====================

    private async void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        if (tag is null || !_sectionMap.TryGetValue(tag, out var target)) return;
        if (!target.IsLoaded) return;

        HighlightNav(tag);

        var to = GetSectionTop(target);

        _isNavigating = true;
        await AnimateScrollAsync(ContentScroll.VerticalOffset, to, TimeSpan.FromMilliseconds(300));
        _isNavigating = false;
    }

    // ==================== 滚动 → 导航高亮 (scroll-spy) ====================

    private void ContentScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isNavigating) return;

        var viewportTop = ContentScroll.VerticalOffset + 60;
        FrameworkElement? best = null;
        var bestDistance = double.MaxValue;

        foreach (var (tag, section) in _sectionMap)
        {
            if (!section.IsLoaded) continue;

            var top = GetSectionTop(section);
            if (top <= viewportTop && (viewportTop - top) < bestDistance)
            {
                best = section;
                bestDistance = viewportTop - top;
            }
        }

        if (best is null) return;

        foreach (var (tag, sec) in _sectionMap)
        {
            if (sec == best)
            {
                HighlightNav(tag);
                break;
            }
        }
    }

    private double GetSectionTop(FrameworkElement section)
    {
        var transform = section.TransformToVisual(ContentScroll);
        return ContentScroll.VerticalOffset + transform.Transform(new Point(0, 0)).Y;
    }

    private void HighlightNav(string activeTag)
    {
        if (_currentTag == activeTag) return;
        _currentTag = activeTag;

        foreach (var (tag, btn) in _navMap)
        {
            if (tag == activeTag)
            {
                btn.FontWeight = FontWeights.Bold;
                btn.Opacity = 1;
            }
            else
            {
                btn.FontWeight = FontWeights.Normal;
                btn.Opacity = 0.55;
            }
        }
    }

    private async Task AnimateScrollAsync(double from, double to, TimeSpan duration)
    {
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            var progress = Math.Min(sw.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);
            var eased = easing.Ease(progress);
            ContentScroll.ScrollToVerticalOffset(from + (to - from) * eased);

            if (progress >= 1.0) break;

            await Task.Delay(16);
        }
    }

    /// <summary>
    /// 导航到本页或全局刷新时调用，重新载入当前状态。
    /// </summary>
    public void Refresh()
    {
        _loading = true;
        try
        {
            // 外观主题
            var dark = ThemeManager.IsDark;
            LightThemeRadio.IsChecked = !dark;
            DarkThemeRadio.IsChecked = dark;

            // 语言（由 Languages/*.xml 动态生成）
            BuildLanguageCombo();
            SelectCurrentLanguage();

            // 主题色
            BuildAccentSwatches();
            AccentHexBox.Text = ThemeManager.AccentColorHex;

            // 项目信息
            var proj = ProjectService.CurrentProject;
            ProjectNameBox.Text = proj?.Name ?? string.Empty;
            ProjectDescBox.Text = proj?.Description ?? string.Empty;
            ProjectVersionBox.Text = proj?.CurrentVersion ?? string.Empty;

            // 推送 / 仓库
            var savedRepos = PushSettings.LoadRepositories();
            _currentRepo = savedRepos.Count > 0 ? savedRepos[0] : null;
            IncludeAuthorCheck.IsChecked = PushSettings.IncludeAuthor;
            GroupByDateCheck.IsChecked = PushSettings.GroupByDate;
            LoadBinFiles();

            // 系统 / 更新
            AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
            AutoCheckStartupCheck.IsChecked = UpdateService.AutoCheckOnStartup;
            CurrentVersionText.Text = UpdateService.CurrentVersion;
        }
        finally
        {
            _loading = false;
        }

        // 异步加载 GitHub 仓库与分支（避免阻塞 UI）。
        _ = LoadReposAsync();
    }

    private MainWindow? MainWindow => Window.GetWindow(this) as MainWindow;

    // ==================== 外观主题 ====================

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.IsChecked != true) return;

        MainWindow?.SetTheme(rb == DarkThemeRadio);
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageCombo.SelectedItem is not ComboBoxItem item) return;

        var lang = item.Tag as string ?? LocalizationManager.DefaultLanguage;
        if (lang == LocalizationManager.Instance.Language) return;

        LocalizationManager.Instance.SetLanguage(lang);
        MainWindow?.ReloadLanguage();
    }

    /// <summary>根据 LocalizationManager.AvailableLanguages 重建语言下拉。</summary>
    private void BuildLanguageCombo()
    {
        var style = TryFindResource("DialogComboBoxItem") as Style;
        LanguageCombo.Items.Clear();
        foreach (var (code, name) in LocalizationManager.Instance.AvailableLanguages)
        {
            LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code, Style = style });
        }
    }

    private void SelectCurrentLanguage()
    {
        var current = LocalizationManager.Instance.Language;
        for (var i = 0; i < LanguageCombo.Items.Count; i++)
        {
            if (LanguageCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
            {
                LanguageCombo.SelectedIndex = i;
                return;
            }
        }
        LanguageCombo.SelectedIndex = 0;
    }

    private void BuildAccentSwatches()
    {
        AccentSwatchPanel.Children.Clear();
        var current = ThemeManager.AccentColorHex;
        foreach (var hex in ThemeManager.AccentPresets)
        {
            AccentSwatchPanel.Children.Add(MakeAccentSwatch(hex,
                string.Equals(hex, current, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private RadioButton MakeAccentSwatch(string hex, bool isSelected)
    {
        var rb = new RadioButton
        {
            GroupName = "AccentColor",
            Tag = hex,
            IsChecked = isSelected,
            Style = (Style)FindResource("AccentSwatchRadio"),
            ToolTip = hex
        };

        var swatch = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = ColorUtil.ParseBrush(hex) ?? Brushes.Transparent,
            BorderBrush = (Brush)Application.Current.FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        rb.Content = swatch;
        rb.Checked += AccentSwatch_Checked;
        return rb;
    }

    private void AccentSwatch_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.IsChecked != true) return;
        if (rb.Tag is not string hex) return;

        ApplyAccent(hex);
    }

    private void ApplyAccent(string hex)
    {
        MainWindow?.SetAccentColor(hex);
        AccentHexBox.Text = ThemeManager.AccentColorHex;
    }

    private void ApplyAccentHex_Click(object sender, RoutedEventArgs e)
    {
        var hex = AccentHexBox.Text.Trim();
        if (ColorUtil.ParseBrush(hex) == null)
        {
            MainWindow?.SetTipText(LocalizationManager.T("无效的颜色值，请输入 #RRGGBB 格式。"));
            AccentHexBox.Text = ThemeManager.AccentColorHex;
            return;
        }

        ApplyAccent(hex);
        BuildAccentSwatches();
    }

    // ==================== 项目信息 ====================

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var proj = ProjectService.CurrentProject;
        if (proj == null)
        {
            MainWindow?.SetTipText(LocalizationManager.T("没有打开的项目。"));
            return;
        }

        var name = ProjectNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MainWindow?.SetTipText(LocalizationManager.T("项目名称不能为空。"));
            return;
        }

        proj.Name = name;
        proj.Description = ProjectDescBox.Text.Trim();
        ProjectService.UpdateProjectConfig(proj);

        MainWindow?.RefreshAllViews();
        MainWindow?.SetTipText(LocalizationManager.T("项目设置已保存。"));
    }

    // ==================== 推送 / 仓库 ====================

    private async void RefreshRepos_Click(object sender, RoutedEventArgs e)
    {
        await LoadReposAsync();
    }

    private async Task LoadReposAsync()
    {
        RepoSelector.ItemsSource = null;
        BranchSelector.ItemsSource = null;
        _repoOptions.Clear();
        _branchOptions.Clear();
        RepoSelector.SelectedItem = null;
        BranchSelector.Text = string.Empty;

        if (!GitHubService.HasToken)
        {
            RepoLoginHint.Visibility = Visibility.Visible;
            // 未登录时仅回显已保存的仓库，方便用户查看当前配置。
            if (_currentRepo != null)
            {
                _repoOptions.Add(new GitHubRepo
                {
                    Name = string.IsNullOrWhiteSpace(_currentRepo.Name) ? _currentRepo.Url : _currentRepo.Name,
                    Url = _currentRepo.Url,
                    DefaultBranch = _currentRepo.Branch,
                });
                RepoSelector.ItemsSource = _repoOptions;
                RepoSelector.SelectedIndex = 0;
                BranchSelector.Text = _currentRepo.Branch;
            }
            return;
        }

        RepoLoginHint.Visibility = Visibility.Collapsed;
        try
        {
            _repoOptions.AddRange(await GitHubService.FetchRepositoriesAsync());
        }
        catch (Exception ex)
        {
            MainWindow?.SetTipText(LocalizationManager.T("加载仓库列表失败：{0}", ex.Message));
            return;
        }

        RepoSelector.ItemsSource = _repoOptions;

        // 尝试匹配已保存的仓库；选中会触发 RepoSelector_SelectionChanged 加载分支。
        if (_currentRepo != null)
        {
            var selected = _repoOptions.FirstOrDefault(r =>
                string.Equals(r.Name, _currentRepo.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Url, _currentRepo.Url, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
                RepoSelector.SelectedItem = selected;
        }
    }

    private async void RepoSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _syncingRepo) return;
        if (RepoSelector.SelectedItem is not GitHubRepo repo) return;

        // 切换到同一仓库时保留已保存的分支；否则回退到仓库默认分支。
        var sameRepo = _currentRepo != null &&
            (string.Equals(_currentRepo.Name, repo.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(_currentRepo.Url, repo.Url, StringComparison.OrdinalIgnoreCase));
        var branch = sameRepo && !string.IsNullOrWhiteSpace(_currentRepo!.Branch)
            ? _currentRepo.Branch
            : string.IsNullOrWhiteSpace(repo.DefaultBranch) ? "main" : repo.DefaultBranch;

        _currentRepo = new RepositoryInfo { Name = repo.Name, Url = repo.Url, Branch = branch };
        SaveCurrentRepo();

        _syncingRepo = true;
        try
        {
            await LoadBranchesAsync(repo);
            BranchSelector.Text = branch;
        }
        finally
        {
            _syncingRepo = false;
        }
    }

    private async Task LoadBranchesAsync(GitHubRepo repo)
    {
        BranchSelector.ItemsSource = null;
        _branchOptions.Clear();
        try
        {
            _branchOptions.AddRange(await GitHubService.FetchBranchesAsync(repo.Url));
        }
        catch
        {
            // 分支获取失败时保留为空，仍允许用户手动输入分支名。
        }
        BranchSelector.ItemsSource = _branchOptions;
    }

    private void BranchSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _syncingRepo) return;
        ApplyBranchText();
    }

    private void BranchSelector_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || _syncingRepo) return;
        ApplyBranchText();
    }

    private void ApplyBranchText()
    {
        var branch = BranchSelector.Text?.Trim();
        if (string.IsNullOrWhiteSpace(branch) || _currentRepo == null) return;

        if (_currentRepo.Branch == branch) return;
        _currentRepo.Branch = branch;
        SaveCurrentRepo();
    }

    private void SaveCurrentRepo()
    {
        if (ProjectService.CurrentProject == null) return;
        PushSettings.SaveRepositories(_currentRepo == null
            ? new List<RepositoryInfo>()
            : new List<RepositoryInfo> { _currentRepo });
    }

    private void IncludeAuthor_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        PushSettings.IncludeAuthor = IncludeAuthorCheck.IsChecked == true;
    }

    private void GroupByDate_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        PushSettings.GroupByDate = GroupByDateCheck.IsChecked == true;
    }

    private void RefreshBinFiles_Click(object sender, RoutedEventArgs e)
    {
        LoadBinFiles();
    }

    private void LoadBinFiles()
    {
        var files = PushSettings.ListBinFiles();
        RemotePathCombo.ItemsSource = files;

        var current = PushSettings.RemotePath;
        _loading = true;
        try
        {
            RemotePathCombo.SelectedItem = files.Contains(current) ? current : null;
        }
        finally
        {
            _loading = false;
        }
    }

    private void RemotePath_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (RemotePathCombo.SelectedItem is not string file) return;
        PushSettings.RemotePath = file;
    }

    /// <summary>
    /// 供外部跳转：滚动到设置页指定区块（Appearance / Project / Push / System）。
    /// anchor 可定位推送区块的子区块（repos / file / options）。
    /// </summary>
    public void NavigateTo(string tag, string? anchor = null)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            FrameworkElement? target = anchor switch
            {
                "file" => RemotePathRow,
                "options" => CommitOptionsHeader,
                "repos" => ReposHeader,
                _ => _sectionMap.TryGetValue(tag, out var section) ? section : null,
            };

            if (target is null || !target.IsLoaded) return;

            _isNavigating = true;
            ContentScroll.ScrollToVerticalOffset(Math.Max(0, GetSectionTop(target) - 12));
            HighlightNav(tag);
            _isNavigating = false;
        }));
    }

    /// <summary>供「更新日志」页跳转：滚动到推送设置的指定子区块（repos / file / options）。</summary>
    public void NavigateToPush(string anchor = "repos") => NavigateTo("Push", anchor);

    // ==================== 系统 / 更新 ====================

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var enable = AutoStartCheck.IsChecked == true;
        try
        {
            AutoStartService.SetEnabled(enable);
            MainWindow?.SetTipText(enable
                ? LocalizationManager.T("已开启开机自启动。")
                : LocalizationManager.T("已关闭开机自启动。"));
        }
        catch (Exception ex)
        {
            _loading = true;
            AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
            _loading = false;
            MainWindow?.SetTipText(LocalizationManager.T("设置开机自启动失败：{0}", ex.Message));
        }
    }

    private void AutoCheckStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateService.AutoCheckOnStartup = AutoCheckStartupCheck.IsChecked == true;
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        DownloadUpdateBtn.Visibility = Visibility.Collapsed;
        OpenReleaseBtn.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = LocalizationManager.T("正在检查更新…");
        _latestUpdate = null;

        try
        {
            var result = await UpdateService.CheckAsync();
            _latestUpdate = result;

            if (!result.Succeeded)
            {
                UpdateStatusText.Text = result.Message;
            }
            else if (!result.HasUpdate)
            {
                UpdateStatusText.Text = result.Message;
            }
            else
            {
                UpdateStatusText.Text = LocalizationManager.T("发现新版本 {0}，点击「下载并安装」开始更新。", result.LatestVersion);

                if (!string.IsNullOrEmpty(result.InstallerDownloadUrl))
                    DownloadUpdateBtn.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(result.HtmlUrl))
                    OpenReleaseBtn.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        var update = _latestUpdate;
        if (update is null || string.IsNullOrEmpty(update.InstallerDownloadUrl)) return;

        CheckUpdateBtn.IsEnabled = false;
        DownloadUpdateBtn.IsEnabled = false;

        try
        {
            var progress = new Progress<string>(msg => UpdateStatusText.Text = msg);
            var path = await UpdateService.DownloadInstallerAsync(
                update.InstallerDownloadUrl, "OCC-Mission-Goals-setup.exe", progress);

            if (string.IsNullOrEmpty(path))
            {
                UpdateStatusText.Text = LocalizationManager.T("下载失败，请稍后重试或使用「打开下载页」。");
            }
            else
            {
                UpdateStatusText.Text = LocalizationManager.T("正在启动安装程序…");
                UpdateService.LaunchInstaller(path);
            }
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
            DownloadUpdateBtn.IsEnabled = true;
        }
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        var update = _latestUpdate;
        if (update is null || string.IsNullOrEmpty(update.HtmlUrl)) return;
        UpdateService.OpenUrl(update.HtmlUrl);
    }
}
