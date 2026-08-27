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

    public SettingsPage()
    {
        InitializeComponent();

        _sectionMap = new Dictionary<string, FrameworkElement>
        {
            ["Appearance"] = Section_Appearance,
            ["Project"]    = Section_Project,
            ["Stats"]      = Section_Stats,
            ["System"]     = Section_System,
        };

        _navMap = new Dictionary<string, Button>
        {
            ["Appearance"] = Nav_Appearance,
            ["Project"]    = Nav_Project,
            ["Stats"]      = Nav_Stats,
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

            // 主题样式（配色方案，每个方案都含深/浅两套配色）
            BuildThemeStyleCombo();
            SelectCurrentThemeStyle();

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

            // 数据统计
            BuildStats();

            // 系统 / 更新
            AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
            AutoCheckStartupCheck.IsChecked = UpdateService.AutoCheckOnStartup;
            CurrentVersionText.Text = UpdateService.CurrentVersion;
        }
        finally
        {
            _loading = false;
        }
    }

    private MainWindow? MainWindow => Window.GetWindow(this) as MainWindow;

    // ==================== 外观主题 ====================

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not RadioButton rb || rb.IsChecked != true) return;

        MainWindow?.SetTheme(rb == DarkThemeRadio);
    }

    private void ThemeStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ThemeStyleCombo.SelectedItem is not ComboBoxItem item) return;

        var name = item.Tag as string ?? ThemeManager.DefaultThemeName;
        if (name == ThemeManager.CurrentThemeName) return;

        MainWindow?.SetThemeStyle(name);
    }

    /// <summary>根据 ThemeManager.ThemeNames 重建主题样式下拉。</summary>
    private void BuildThemeStyleCombo()
    {
        var style = TryFindResource("DialogComboBoxItem") as Style;
        ThemeStyleCombo.Items.Clear();
        foreach (var name in ThemeManager.ThemeNames)
        {
            ThemeStyleCombo.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.T(name),
                Tag = name,
                Style = style
            });
        }
    }

    private void SelectCurrentThemeStyle()
    {
        var current = ThemeManager.CurrentThemeName;
        for (var i = 0; i < ThemeStyleCombo.Items.Count; i++)
        {
            if (ThemeStyleCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
            {
                ThemeStyleCombo.SelectedIndex = i;
                return;
            }
        }
        ThemeStyleCombo.SelectedIndex = 0;
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

    // ==================== 数据统计 ====================

    /// <summary>供外部跳转：滚动到数据统计区块。</summary>
    public void NavigateToStats() => NavigateTo("Stats");

    private void BuildStats()
    {
        var projectDir = ProjectService.CurrentProjectDir;
        if (string.IsNullOrEmpty(projectDir))
        {
            ClearStats();
            return;
        }

        var data = DataService.ReadAllVersions(projectDir);
        var all = data.Unfinished.Concat(data.Finished).ToList();
        var finished = data.Finished.Count;
        var unfinished = data.Unfinished.Count;
        var total = finished + unfinished;
        var favorited = all.Count(e => e.IsFavorited);
        var rate = total > 0 ? Math.Min(1.0, (double)finished / total) : 0;

        BuildStatsCards(total, unfinished, finished, rate, favorited);
        BuildSeverityDist(all);
        BuildTypeDist(all);
        BuildVersionDist(all);
    }

    private void ClearStats()
    {
        StatsCardsPanel.Children.Clear();
        SeverityDistPanel.ItemsSource = null;
        TypeDistPanel.ItemsSource = null;
        VersionDistPanel.ItemsSource = null;
    }

    private void BuildStatsCards(int total, int unfinished, int finished, double rate, int favorited)
    {
        StatsCardsPanel.Children.Clear();
        StatsCardsPanel.Children.Add(MakeStatCard(LocalizationManager.T("总条目"), total.ToString(), null));
        StatsCardsPanel.Children.Add(MakeStatCard(LocalizationManager.T("未完成"), unfinished.ToString(), null));
        StatsCardsPanel.Children.Add(MakeStatCard(LocalizationManager.T("已完成"), finished.ToString(), null));
        StatsCardsPanel.Children.Add(MakeStatCard(LocalizationManager.T("完成率"), $"{rate:P0}", null));
        StatsCardsPanel.Children.Add(MakeStatCard(LocalizationManager.T("收藏"), favorited.ToString(), null));
    }

    private Border MakeStatCard(string label, string value, Brush? accent)
    {
        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = accent ?? (Brush)FindResource("ForegroundBrush"),
        };
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.6,
            Foreground = (Brush)FindResource("ForegroundBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        };
        var panel = new StackPanel();
        panel.Children.Add(valueBlock);
        panel.Children.Add(labelBlock);

        return new Border
        {
            MinWidth = 92,
            Margin = new Thickness(0, 0, 16, 8),
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource("CardBackgroundBrush"),
            BorderBrush = (Brush)FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
    }

    private void BuildSeverityDist(List<Models.GoalEntry> all)
    {
        var groups = all
            .GroupBy(e => e.Severity)
            .OrderBy(g => g.Key)
            .ToList();
        var max = groups.Count > 0 ? groups.Max(g => g.Count()) : 1;
        SeverityDistPanel.ItemsSource = groups
            .Select(g => new SeverityStat
            {
                Severity = g.Key,
                Label = Models.SeverityHelper.GetText(g.Key),
                Count = g.Count(),
                Ratio = (double)g.Count() / max,
                ColorBrush = Models.SeverityHelper.GetBrush(g.Key),
            })
            .ToList();
    }

    private void BuildTypeDist(List<Models.GoalEntry> all)
    {
        var accent = (Brush)FindResource("PrimaryBrush");
        var groups = all
            .SelectMany(e => e.Type)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ToList();
        var max = groups.Count > 0 ? groups.Max(g => g.Count()) : 1;
        TypeDistPanel.ItemsSource = groups
            .Select(g => new SeverityStat
            {
                Label = g.Key,
                Count = g.Count(),
                Ratio = (double)g.Count() / max,
                ColorBrush = accent,
            })
            .ToList();
    }

    private void BuildVersionDist(List<Models.GoalEntry> all)
    {
        var accent = (Brush)FindResource("PrimaryBrush");
        var groups = all
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Version) ? LocalizationManager.T("未标记") : e.Version)
            .OrderByDescending(g => g.Count())
            .ToList();
        var max = groups.Count > 0 ? groups.Max(g => g.Count()) : 1;
        VersionDistPanel.ItemsSource = groups
            .Select(g => new SeverityStat
            {
                Label = g.Key,
                Count = g.Count(),
                Ratio = (double)g.Count() / max,
                ColorBrush = accent,
            })
            .ToList();
    }

    /// <summary>
    /// 供外部跳转：滚动到设置页指定区块（Appearance / Project / Stats / System）。
    /// </summary>
    public void NavigateTo(string tag, string? anchor = null)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            var target = _sectionMap.TryGetValue(tag, out var section) ? section : null;

            if (target is null || !target.IsLoaded) return;

            _isNavigating = true;
            ContentScroll.ScrollToVerticalOffset(Math.Max(0, GetSectionTop(target) - 12));
            HighlightNav(tag);
            _isNavigating = false;
        }));
    }

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
