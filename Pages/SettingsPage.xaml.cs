using System.Collections.ObjectModel;
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

    private static readonly string[] AccentPresets =
    {
        "#4CAF50", "#8BC34A", "#009688", "#00BCD4",
        "#2196F3", "#3F51B5", "#9C27B0", "#E91E63",
        "#FF5722", "#FF9800", "#795548", "#607D8B"
    };

    public ObservableCollection<RepositoryInfo> Repositories { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        RepoList.ItemsSource = Repositories;

        _sectionMap = new Dictionary<string, FrameworkElement>
        {
            ["Appearance"] = Section_Appearance,
            ["Project"]    = Section_Project,
            ["Push"]       = Section_Push,
        };

        _navMap = new Dictionary<string, Button>
        {
            ["Appearance"] = Nav_Appearance,
            ["Project"]    = Nav_Project,
            ["Push"]       = Nav_Push,
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

            // 语言
            LanguageCombo.SelectedIndex = LocalizationManager.Instance.IsEnglish ? 1 : 0;

            // 主题色
            BuildAccentSwatches();
            AccentHexBox.Text = ThemeManager.AccentColorHex;

            // 项目信息
            var proj = ProjectService.CurrentProject;
            ProjectNameBox.Text = proj?.Name ?? string.Empty;
            ProjectDescBox.Text = proj?.Description ?? string.Empty;
            ProjectVersionBox.Text = proj?.CurrentVersion ?? string.Empty;

            // 推送 / 仓库
            Repositories.Clear();
            foreach (var repo in PushSettings.LoadRepositories())
                Repositories.Add(repo);
            IncludeAuthorCheck.IsChecked = PushSettings.IncludeAuthor;
            GroupByDateCheck.IsChecked = PushSettings.GroupByDate;
            VersionPrefixBox.Text = PushSettings.VersionPrefix;
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

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageCombo.SelectedItem is not ComboBoxItem item) return;

        var lang = item.Tag as string ?? "zh";
        if (lang == LocalizationManager.Instance.Language) return;

        LocalizationManager.Instance.SetLanguage(lang);
        MainWindow?.ReloadLanguage();
    }

    private void BuildAccentSwatches()
    {
        AccentSwatchPanel.Children.Clear();
        var current = ThemeManager.AccentColorHex;
        foreach (var hex in AccentPresets)
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
            MainWindow?.SetTipText(LocalizationManager.T("无效的颜色值，请输入 #RRGGBB 格式。", "Invalid color value. Please enter #RRGGBB format."));
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
            MainWindow?.SetTipText(LocalizationManager.T("没有打开的项目。", "No project is open."));
            return;
        }

        var name = ProjectNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MainWindow?.SetTipText(LocalizationManager.T("项目名称不能为空。", "Project name cannot be empty."));
            return;
        }

        proj.Name = name;
        proj.Description = ProjectDescBox.Text.Trim();
        ProjectService.UpdateProjectConfig(proj);

        MainWindow?.RefreshAllViews();
        MainWindow?.SetTipText(LocalizationManager.T("项目设置已保存。", "Project settings saved."));
    }

    // ==================== 推送 / 仓库 ====================

    private void AddRepo_Click(object sender, RoutedEventArgs e)
    {
        var name = RepoNameBox.Text.Trim();
        var url = RepoUrlBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(url))
        {
            MainWindow?.SetTipText(LocalizationManager.T("请填写仓库名称或 URL。", "Please enter a repo name or URL."));
            return;
        }

        Repositories.Add(new RepositoryInfo
        {
            Name = string.IsNullOrWhiteSpace(name) ? url : name,
            Url = url
        });

        PushSettings.SaveRepositories(Repositories);

        RepoNameBox.Text = string.Empty;
        RepoUrlBox.Text = string.Empty;
        MainWindow?.SetTipText(LocalizationManager.T("已添加仓库。", "Repo added."));
    }

    private void RemoveRepo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RepositoryInfo repo }) return;

        Repositories.Remove(repo);
        PushSettings.SaveRepositories(Repositories);
        MainWindow?.SetTipText(LocalizationManager.T("已删除仓库。", "Repo removed."));
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

    private void VersionPrefix_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        PushSettings.VersionPrefix = VersionPrefixBox.Text;
        if (VersionPrefixBox.Text != PushSettings.VersionPrefix)
            VersionPrefixBox.Text = PushSettings.VersionPrefix;
    }
}
