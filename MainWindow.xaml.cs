using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using OCCMissionGoals.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OCCMissionGoals
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isMaximized;
        private bool _justMinimized;
        private SortMode _currentSortMode = LoadSortMode();

        // ── 动态页面注册 ──
        private readonly List<PageRegistration> _pageRegs = new();
        private readonly Dictionary<string, Page> _pageCache = new();

        private ToolPages.SwitchPage? _switchPage;
        private ToolPages.SortPage? _sortPage;
        private ToolPages.ControlButtonPage? _controlButtonPage;
        private ToolPages.MenuPage? _menuPage;
        private int _lastTabIndex = 0;
        private bool _isPageAnimating;
        private int _pendingTabIndex = -1;
        private bool _toolPageShowingControl;
        private bool _toolPageAnimating;
        private int _toolPagePending; // +1=need up, -1=need down, 0=none
        private bool _ctrlToolShowingMenu;
        private bool _ctrlToolAnimating;
        private int _ctrlToolPending;
        private System.Windows.Threading.DispatcherTimer? _tipTimer;
        private System.IO.FileSystemWatcher? _fileWatcher;
        private System.Windows.Threading.DispatcherTimer? _fileWatchDebounce;

        public event Action<SortMode>? SortModeChanged;

        public SortMode CurrentSortMode => _currentSortMode;

        /// <summary>从 config.ini 读取上次保存的排序方式，非法值回退为严重程度升序。</summary>
        private static SortMode LoadSortMode()
        {
            var tag = ConfigManager.Get("Sort", "SortMode", "0");
            return int.TryParse(tag, out var value) && Enum.IsDefined(typeof(SortMode), value)
                ? (SortMode)value
                : SortMode.SeverityAsc;
        }

        public MainWindow()
        {
            InitializeComponent();

            // 尝试恢复上次打开的项目，否则尝试兼容旧路径
            if (!Services.ProjectService.TryRestoreLastProject())
            {
                // 兼容旧版 EntryFiles/data.json
                var legacyPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Environment.ProcessPath)!, "EntryFiles", "data.json");
                if (System.IO.File.Exists(legacyPath))
                {
                    Services.DataService.SetFilePath(legacyPath);
                    Services.DataService.Load();
                }
            }

            _sortPage = new ToolPages.SortPage();
            ToolPage.Navigate(_sortPage);
            _switchPage = new ToolPages.SwitchPage();
            CtrlToolPage.Navigate(_switchPage);
            _controlButtonPage = new ToolPages.ControlButtonPage();
            _menuPage = new ToolPages.MenuPage();

            // ── 注册页面（顺序决定 tab 索引） ──
            RegisterPage(new PageRegistration
            {
                Key = "log", TabLabel = "更新日志", TabLabelEn = "Update Log",
                PageFactory = () => new Pages.LogPage(),
                OnInit = p => ((Pages.LogPage)p).RefreshStats(),
                OnRefresh = p => ((Pages.LogPage)p).RefreshStats()
            });
            RegisterPage(new PageRegistration
            {
                Key = "undone", TabLabel = "未完成的条目", TabLabelEn = "Unfinished",
                PageFactory = () => new Pages.UnDonePage(),
                OnInit = p => ((Pages.UnDonePage)p).LoadFromData(),
                OnRefresh = p =>
                {
                    var up = (Pages.UnDonePage)p;
                    up.LoadFromData();
                    up.ApplySort(CurrentSortMode);
                }
            });
            RegisterPage(new PageRegistration
            {
                Key = "done", TabLabel = "完成的条目", TabLabelEn = "Finished",
                PageFactory = () => new Pages.DonePage(),
                OnInit = p => ((Pages.DonePage)p).LoadFromData(),
                OnRefresh = p =>
                {
                    var dp = (Pages.DonePage)p;
                    dp.LoadFromData();
                    dp.ApplySort(CurrentSortMode);
                }
            });
            RegisterPage(new PageRegistration
            {
                Key = "expand", TabLabel = "扩展", TabLabelEn = "Extensions",
                PageFactory = () => new Pages.ExpandPage(),
                OnBeforeNavigate = p => ((Pages.ExpandPage)p).Refresh(),
                OnRefresh = p => ((Pages.ExpandPage)p).Refresh()
            });
            RegisterPage(new PageRegistration
            {
                Key = "settings", TabLabel = "设置", TabLabelEn = "Settings",
                PageFactory = () => new Pages.SettingsPage(),
                OnBeforeNavigate = p => ((Pages.SettingsPage)p).Refresh(),
                OnRefresh = p => ((Pages.SettingsPage)p).Refresh(),
                IsOverlayTab = true
            });
            RegisterPage(new PageRegistration
            {
                Key = "help", TabLabel = "帮助", TabLabelEn = "Help",
                PageFactory = () => new Pages.HelpPage(),
                IsOverlayTab = true
            });

            // 向 SwitchPage 注入隐藏页签按钮并订阅事件
            _switchPage.AddOverlayTab("settings", LocalizationManager.T("设置", "Settings"));
            _switchPage.AddOverlayTab("help", LocalizationManager.T("帮助", "Help"));
            _switchPage.TabSelected += OnTabSelected;

            // 默认打开第一个页面
            SwitchTab("log");
            SourceInitialized += OnSourceInitialized;
            StateChanged += OnStateChanged;
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewMouseDown += OnWindowPreviewMouseDown;
            PreviewKeyUp += OnPreviewKeyUp;
            Deactivated += OnWindowDeactivated;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            StartFileWatcher();
        }

        private void StartFileWatcher()
        {
            var dataPath = Services.DataService.GetFilePath();
            if (string.IsNullOrEmpty(dataPath)) return;

            var dir = System.IO.Path.GetDirectoryName(dataPath)!;
            var file = System.IO.Path.GetFileName(dataPath);

            _fileWatchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _fileWatchDebounce.Tick += (_, _) =>
            {
                _fileWatchDebounce?.Stop();
                Dispatcher.Invoke(() =>
                {
                    Services.DataService.Load();
                    RefreshAllViews();
                });
            };

            _fileWatcher = new System.IO.FileSystemWatcher(dir, file)
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _fileWatcher.Changed += (_, _) =>
            {
                if (Services.DataService.IsInternalSave) return;
                _fileWatchDebounce?.Stop();
                _fileWatchDebounce?.Start();
            };
        }

        private void RestartFileWatcher()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
            _fileWatchDebounce?.Stop();
            _fileWatchDebounce = null;
            StartFileWatcher();
        }

        private void PlayRestoreFromMinimized()
        {
            MainBorder.RenderTransform = Transform.Identity;
            MainBorder.Opacity = 1;
        }

        public struct ColorPalette
        {
            public const string LightIconData =
                "M532.5 150.4c-16.5-36.8 15.1-76.2 54-69.3C794.1 117.7 951.9 299 951.9 517.2 951.9 761.7 753.6 960 509 960c-218.2 0-399.4-157.7-436.1-365.4-6.9-38.9 32.5-70.5 69.3-54s77.6 25.7 120.8 25.7c163.1 0 295.2-132.2 295.2-295.2 0-43.1-9.2-83.9-25.7-120.7z m118.3 52.7c3.9 22.1 5.9 44.8 5.9 68 0 217.4-176.2 393.6-393.6 393.6-23.2 0-45.9-2-68-5.9 54 119.5 174.3 202.7 314 202.7 190.2 0 344.4-154.2 344.4-344.4-0.1-139.6-83.2-259.9-202.7-314zM192.4 306.4l3.1 6.9c13.4 27.8 34.6 51.1 60.9 67.1l4.9 2.9a3.3 3.3 0 0 1 0 5.8l-4.9 2.9c-26.3 16.1-47.5 39.4-60.9 67.1l-3.1 6.9c-1.2 2.8-5.2 2.8-6.4 0l-3.1-6.9C169.5 431.3 148.3 408 122 392l-4.9-2.9a3.3 3.3 0 0 1 0-5.8l4.9-2.9c26.3-16.1 47.5-39.4 60.9-67.1l3.1-6.9c1.2-2.8 5.1-2.8 6.4 0zM331.7 67.3c2-4.4 8.2-4.4 10.2 0l5 10.9c21.4 44.2 55 81.3 96.9 106.9l7.8 4.6c3.6 2 3.6 7.2 0 9.3l-7.8 4.6c-41.9 25.6-75.6 62.7-96.9 106.9-1.7 3.6-3.4 7.3-5 10.9-2 4.4-8.2 4.4-10.2 0l-5-10.9c-21.4-44.2-55-81.3-96.9-106.9L222 199c-3.6-2-3.6-7.2 0-9.3l7.8-4.6c41.9-25.6 75.6-62.7 96.9-106.9l5-10.9z";

            public const string DarkIconData =
                "M512 825.6c23 0 41.9 17.3 44.5 39.6l0.3 5.2v44.8c0 24.7-20.1 44.8-44.8 44.8-23 0-41.9-17.3-44.5-39.6l-0.3-5.2v-44.8c0-24.7 20.1-44.8 44.8-44.8z m-221.7-91.9c17.5 17.5 17.5 45.9 0 63.4l-31.7 31.7c-17.5 17.5-45.9 17.5-63.4 0s-17.5-45.9 0-63.4l31.7-31.7c17.5-17.4 45.9-17.4 63.4 0z m506.8 0l31.7 31.7c17.5 17.5 17.5 45.9 0 63.4s-45.9 17.5-63.4 0l-31.7-31.7c-17.5-17.5-17.5-45.9 0-63.4 17.5-17.4 45.9-17.4 63.4 0zM539.7 64c45.2 0 83.3 33.6 88.9 78.5l17 136.2C726.4 325 780.8 412.2 780.8 512c0 148.5-120.3 268.8-268.8 268.8S243.2 660.5 243.2 512c0-99.8 54.4-186.9 135.2-233.3l17-136.2C401 97.6 439.2 64 484.3 64h55.4zM512 332.8c-99 0-179.2 80.2-179.2 179.2 0 99 80.2 179.2 179.2 179.2 99 0 179.2-80.2 179.2-179.2 0-99-80.2-179.2-179.2-179.2zM153.6 467.2c24.7 0 44.8 20.1 44.8 44.8s-20.1 44.8-44.8 44.8h-44.8C84.1 556.8 64 536.7 64 512s20.1-44.8 44.8-44.8h44.8z m761.6 0c24.7 0 44.8 20.1 44.8 44.8s-20.1 44.8-44.8 44.8h-44.8c-24.7 0-44.8-20.1-44.8-44.8s20.1-44.8 44.8-44.8h44.8z m-86.4-272c17.5 17.5 17.5 45.9 0 63.4l-31.7 31.7c-17.5 17.5-45.9 17.5-63.4 0s-17.5-45.9 0-63.4l31.7-31.7c17.5-17.5 45.9-17.5 63.4 0z m-570.2 0l31.7 31.7c17.5 17.5 17.5 45.9 0 63.4s-45.9 17.5-63.4 0l-31.7-31.7c-17.5-17.5-17.5-45.9 0-63.4s45.9-17.5 63.4 0z m281.1-41.6h-55.3l-11.2 89.6h77.7l-11.2-89.6z";
        }

        public void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private Action? _pendingTopAction;

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchToActivePage();
            UpdateSearchBoard();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            // 回车执行首条结果（若有）
            if (_pendingTopAction != null)
            {
                var act = _pendingTopAction;
                _pendingTopAction = null;
                CloseSearchBoard();
                act();
            }
            e.Handled = true;
        }

        private void SearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 选中（聚焦）搜索框就显示结果板。延迟到本次鼠标/键盘事件处理结束后再打开，
            // 避免在鼠标按下期间打开面板导致同一次点击被判为「外部点击」而立即关闭。
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (SearchTextBox == null) return;
                if (!SearchTextBox.IsKeyboardFocused && !IsFocusInSearchBoard())
                    return;
                UpdateSearchBoard();
            }));
        }

        private bool IsFocusInSearchBoard()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            return focused != null && IsDescendantOf(focused, SearchBoard);
        }

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 焦点移到结果面板内部时不关闭（让点击项正常触发跳转）
            if (e.NewFocus is DependencyObject d && IsDescendantOf(d, SearchBoard))
                return;
            CloseSearchBoard();
        }

        /// <summary>把当前输入（支持 "Text:xxx" 等前缀）解析后应用到当前激活的列表页面。</summary>
        private void ApplySearchToActivePage()
        {
            if (SearchTextBox == null) return;

            if (_lastTabIndex >= 0 && _lastTabIndex < _pageRegs.Count)
                ApplySearch(_pageRegs[_lastTabIndex].Key, SearchTextBox.Text);
        }

        private void ApplySearch(string key, string rawInput)
        {
            var (mode, keyword) = SearchMatcher.Parse(rawInput);

            // 设置 / 插件 / 已安装插件是全局搜索，不作用于条目列表（结果在搜索板中展示）
            if (mode == SearchMode.Setting || mode == SearchMode.Plugins || mode == SearchMode.Expand)
                return;

            if (key == "undone")
                (_pageCache.GetValueOrDefault("undone") as Pages.UnDonePage)?.ApplyFilter(keyword, mode);
            else if (key == "done")
                (_pageCache.GetValueOrDefault("done") as Pages.DonePage)?.ApplyFilter(keyword, mode);
        }

        // ======================== 搜索结果板 ========================

        private sealed record SettingItem(string Key, string Title, string[] Keywords, Action Action);
        private sealed record SearchResultItem(
            string Title, string KindLabel, Brush KindBrush,
            string Subtitle, string ActionLabel, Action Action);

        private List<SettingItem> GetSettingItems() => new()
        {
            new("project", LocalizationManager.T("设置项目", "Project Settings"), new[] { "设置项目", "项目设置", "项目", "设置", "配置", "project", "config", "setting" }, OpenProjectSettings),
            new("theme", LocalizationManager.T("主题样式", "Theme"), new[] { "主题样式", "主题", "样式", "深浅", "亮色", "暗色", "theme", "dark", "light" }, ToggleTheme),
            new("push", LocalizationManager.T("推送设置", "Push Settings"), new[] { "推送设置", "推送", "上传", "仓库", "github", "备份", "push", "upload" }, OpenPushSettings),
        };

        private List<SettingItem> SettingSearchMatches(string keyword)
        {
            var q = (keyword ?? string.Empty).Trim().ToLowerInvariant();
            var items = GetSettingItems();
            if (q.Length == 0) return items;

            static bool Eq(string s, string q) => s.ToLowerInvariant() == q;
            bool Sub(string s) => s.ToLowerInvariant().Contains(q) || q.Contains(s.ToLowerInvariant());

            var exact = items.Where(i => Eq(i.Title, q) || i.Keywords.Any(k => Eq(k, q))).ToList();
            if (exact.Count > 0) return exact;

            return items.Where(i => Sub(i.Title) || i.Keywords.Any(Sub)).ToList();
        }

        private List<SearchResultItem> BuildSearchResults(string rawInput)
        {
            var results = new List<SearchResultItem>();

            // 空输入：只显示「按模式搜索」引导，不列条目
            if (string.IsNullOrWhiteSpace(rawInput))
                return results;

            var (mode, keyword) = SearchMatcher.Parse(rawInput);

            if (mode == SearchMode.Setting)
            {
                foreach (var s in SettingSearchMatches(keyword))
                    results.Add(BuildSettingResult(s));
                return results;
            }

            if (mode == SearchMode.Plugins || mode == SearchMode.Expand)
            {
                AppendPluginResults(results, keyword, mode == SearchMode.Expand);
                return results;
            }

            AppendEntryResults(results, keyword, mode);
            return results;
        }

        private void AppendEntryResults(List<SearchResultItem> results, string keyword, SearchMode mode)
        {
            // 没有实际关键词时不列出条目（例如刚点了「Text:」前缀还没输入内容）
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var data = Services.DataService.ReadAllVersions(Services.ProjectService.CurrentProjectDir!);
            foreach (var entry in data.Unfinished)
                if (SearchMatcher.Matches(entry, keyword, mode, useCompletedDate: false))
                    results.Add(BuildEntryResult(entry, finished: false));
            foreach (var entry in data.Finished)
                if (SearchMatcher.Matches(entry, keyword, mode, useCompletedDate: true))
                    results.Add(BuildEntryResult(entry, finished: true));
        }

        private void AppendPluginResults(List<SearchResultItem> results, string keyword, bool installedOnly)
        {
            var source = installedOnly ? Services.PluginCatalog.Installed : Services.PluginCatalog.All;
            var q = (keyword ?? string.Empty).Trim().ToLowerInvariant();

            IEnumerable<PluginInfo> matches = source;
            if (q.Length > 0)
            {
                matches = source.Where(p =>
                    p.Name.ToLowerInvariant().Contains(q) ||
                    p.Description.ToLowerInvariant().Contains(q) ||
                    p.Author.ToLowerInvariant().Contains(q));
            }

            foreach (var plugin in matches)
                results.Add(BuildPluginResult(plugin));
        }

        private SearchResultItem BuildPluginResult(PluginInfo plugin)
        {
            var installed = plugin.IsInstalled;
            var kindLabel = installed ? LocalizationManager.T("已安装", "Installed") : LocalizationManager.T("未安装", "Not installed");
            var kindBrush = new SolidColorBrush(installed
                ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : Color.FromRgb(0x8D, 0x8D, 0x8D));

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(plugin.Author)) parts.Add(plugin.Author);
            if (!string.IsNullOrWhiteSpace(plugin.Version)) parts.Add("v" + plugin.Version);
            if (!string.IsNullOrWhiteSpace(plugin.Description)) parts.Add(plugin.Description);
            var subtitle = parts.Count > 0 ? string.Join(" · ", parts) : plugin.Category;

            return new SearchResultItem(
                plugin.Name, kindLabel, kindBrush, subtitle, LocalizationManager.T("跳转", "Go"),
                () => { SwitchTab("expand"); CloseSearchBoard(); });
        }

        private SearchResultItem BuildSettingResult(SettingItem s)
        {
            var (actionLabel, subtitle) = s.Key switch
            {
                "theme" => (ThemeManager.IsDark ? LocalizationManager.T("切换为亮色", "Switch to light") : LocalizationManager.T("切换为深色", "Switch to dark"), LocalizationManager.T("切换深色 / 浅色主题", "Toggle dark / light theme")),
                "project" => (LocalizationManager.T("打开", "Open"), LocalizationManager.T("打开项目设置对话框", "Open project settings dialog")),
                "push" => (LocalizationManager.T("跳转", "Go"), LocalizationManager.T("打开推送（上传 / 备份）设置", "Open push (upload / backup) settings")),
                _ => (LocalizationManager.T("打开", "Open"), s.Title),
            };
            return new SearchResultItem(
                s.Title, LocalizationManager.T("设置", "Settings"),
                new SolidColorBrush(Color.FromRgb(0x3D, 0x9D, 0xE8)),
                subtitle, actionLabel,
                () => { s.Action(); CloseSearchBoard(); });
        }

        private SearchResultItem BuildEntryResult(GoalEntry entry, bool finished)
        {
            var kindLabel = finished ? LocalizationManager.T("已完成", "Finished") : LocalizationManager.T("未完成", "Unfinished");
            var kindBrush = new SolidColorBrush(finished
                ? Color.FromRgb(0x4C, 0xAF, 0x50)
                : Color.FromRgb(0x8D, 0x8D, 0x8D));
            var subtitle = $"{SeverityHelper.GetText(entry.Severity)} · {entry.Brief}";
            return new SearchResultItem(
                entry.Title, kindLabel, kindBrush, subtitle, LocalizationManager.T("跳转", "Go"),
                () => { JumpToEntry(entry, finished); CloseSearchBoard(); });
        }

        private void UpdateSearchBoard()
        {
            var raw = SearchTextBox?.Text ?? string.Empty;
            var results = BuildSearchResults(raw);

            var (mode, keyword) = SearchMatcher.Parse(raw);
            // 无实际关键词（空输入，或刚点了「Text:/Tag:…」前缀）→ 显示模式引导，不列条目；
            // Setting / Plugins / Expand 模式例外：空关键词也列出全部结果（设置快捷项 / 插件）。
            var isGlobalList = mode is SearchMode.Setting or SearchMode.Plugins or SearchMode.Expand;
            var showHint = string.IsNullOrWhiteSpace(keyword) && !isGlobalList;

            var hasResults = results.Count > 0;
            SearchBoardList.ItemsSource = hasResults ? results : null;
            SearchBoardList.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;

            SearchModeHint.Visibility = showHint ? Visibility.Visible : Visibility.Collapsed;
            SearchBoardEmpty.Visibility = (!showHint && !hasResults) ? Visibility.Visible : Visibility.Collapsed;

            _pendingTopAction = hasResults ? results[0].Action : null;
            OpenSearchBoard();
        }

        private void OpenSearchBoard()
        {
            // 相对主内容区定位：搜索框正下方，宽度与搜索框一致，随窗口一起移动
            SearchBoard.Width = SearchHost.ActualWidth;
            var pos = SearchHost.TranslatePoint(new Point(0, SearchHost.ActualHeight + 4), MainContentGrid);
            Canvas.SetLeft(SearchBoard, pos.X);
            Canvas.SetTop(SearchBoard, pos.Y);
            SearchBoard.Visibility = Visibility.Visible;
        }

        private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 点击搜索板 / 搜索框 / 标题栏（拖动窗口）之外的区域时收起面板
            if (SearchBoard.Visibility != Visibility.Visible) return;
            var src = e.OriginalSource as DependencyObject;
            if (src != null && (IsDescendantOf(src, SearchBoard) ||
                                IsDescendantOf(src, SearchHost) ||
                                IsDescendantOf(src, TitleBar)))
                return;
            CloseSearchBoard();
        }

        private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
        {
            while (node != null)
            {
                if (ReferenceEquals(node, ancestor)) return true;
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        private void SearchBoardItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SearchResultItem item)
            {
                item.Action();
            }
        }

        private void SearchModeChip_Click(object sender, RoutedEventArgs e)
        {
            // 点击「按模式搜索」引导项 → 在搜索框填入前缀，光标定位到末尾，聚焦继续输入
            if (sender is Button btn && btn.Tag is string prefix)
            {
                SearchTextBox.Text = prefix;
                SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
                SearchTextBox.Focus();
            }
        }

        private void CloseSearchBoard()
        {
            SearchBoard.Visibility = Visibility.Collapsed;
        }

        private void JumpToEntry(GoalEntry entry, bool finished)
        {
            var key = finished ? "done" : "undone";
            SwitchTab(key);
            var page = _pageCache.GetValueOrDefault(key);
            if (finished)
                (page as Pages.DonePage)?.SelectEntry(entry);
            else
                (page as Pages.UnDonePage)?.SelectEntry(entry);
        }

        private void OpenPushSettings()
        {
            // 推送/GitHub 设置入口在「更新日志」页面的侧边栏
            SwitchTab("log");
        }

        /// <summary>切换明暗主题并持久化。</summary>
        public void ToggleTheme() => SetTheme(!ThemeManager.IsDark);

        /// <summary>设置明暗主题（深色/浅色）并持久化、同步控制按钮图标。</summary>
        public void SetTheme(bool dark)
        {
            ThemeManager.ApplyTheme(dark);
            _controlButtonPage?.SyncThemeIcon();
            ConfigManager.Set("General", "theme", dark ? "dark" : "light");
        }

        /// <summary>设置主题色（#RRGGBB）并持久化。</summary>
        public void SetAccentColor(string hex)
        {
            ThemeManager.ApplyAccentColor(hex);
            ConfigManager.Set("General", "accent", ThemeManager.AccentColorHex);
        }

        /// <summary>语言切换后重建页签标签并刷新所有已加载视图。</summary>
        public void ReloadLanguage()
        {
            foreach (var reg in _pageRegs)
                _switchPage?.UpdateTabLabel(reg.Key, LocalizationManager.T(reg.TabLabel, reg.TabLabelEn));

            StatusText.Text = LocalizationManager.T("状态机无更新", "No status update");
            RefreshAllViews();
        }

        private void OnLanguageChanged(string lang) => ReloadLanguage();

        /// <summary>打开项目设置弹窗（等价于菜单里的「设置项目」）。</summary>
        public void OpenProjectSettings()
        {
            if (Services.ProjectService.CurrentProject == null)
            {
                SetTipText(LocalizationManager.T("没有打开的项目。", "No project is open."));
                return;
            }

            var p = Services.ProjectService.CurrentProject;
            NewProjectDialog.LoadConfig(p.Name, p.Description, p.CurrentVersion);
            NewProjectDialog.Confirmed += OnProjectSettingsConfirmed;
            NewProjectDialog.Cancelled += OnProjectSettingsDismissed;

            MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            DialogOverlay.Visibility = Visibility.Visible;
            NewProjectDialog.Visibility = Visibility.Visible;
        }

        private void OnProjectSettingsConfirmed(object? sender, EventArgs e)
        {
            var (isValid, message) = NewProjectDialog.Validate();
            if (!isValid)
            {
                SetTipText(message);
                return;
            }
            if (Services.ProjectService.CurrentProject == null) return;

            Services.ProjectService.CurrentProject.Name = NewProjectDialog.ProjectName;
            Services.ProjectService.CurrentProject.Description = NewProjectDialog.Description;
            Services.ProjectService.UpdateProjectConfig(Services.ProjectService.CurrentProject);
            RefreshAllViews();
            DismissProjectSettings();
            SetTipText(LocalizationManager.T("项目设置已保存。", "Project settings saved."));
        }

        private void OnProjectSettingsDismissed(object? sender, EventArgs e)
        {
            DismissProjectSettings();
        }

        private void DismissProjectSettings()
        {
            NewProjectDialog.Confirmed -= OnProjectSettingsConfirmed;
            NewProjectDialog.Cancelled -= OnProjectSettingsDismissed;
            NewProjectDialog.Visibility = Visibility.Collapsed;
            DialogOverlay.Visibility = Visibility.Collapsed;
            MainContentGrid.Effect = null;
        }

        public void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl) return;
            if (!e.IsRepeat)
            {
                if (!_toolPageShowingControl)
                {
                    _toolPageShowingControl = true;
                    AnimateToolPage(true);
                }
                if (!_ctrlToolShowingMenu)
                {
                    _ctrlToolShowingMenu = true;
                    AnimateCtrlToolPage(true);
                }
            }
            e.Handled = true;
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl) return;
            if (_toolPageShowingControl)
            {
                _toolPageShowingControl = false;
                AnimateToolPage(false);
            }
            if (_ctrlToolShowingMenu)
            {
                _ctrlToolShowingMenu = false;
                AnimateCtrlToolPage(false);
            }
            e.Handled = true;
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            if (_toolPageShowingControl)
            {
                _toolPageShowingControl = false;
                _toolPageAnimating = false;
                _toolPagePending = 0;
                ToolPage.RenderTransform = Transform.Identity;
                ToolPage.Navigate(_sortPage);
            }
            if (_ctrlToolShowingMenu)
            {
                _ctrlToolShowingMenu = false;
                _ctrlToolAnimating = false;
                _ctrlToolPending = 0;
                CtrlToolPage.RenderTransform = Transform.Identity;
                CtrlToolPage.Navigate(_switchPage);
            }
        }

        /// <param name="toControl">true = 向上切到 ControlButtonPage, false = 向下切回 SortPage</param>
        private void AnimateToolPage(bool toControl)
        {
            if (_toolPageAnimating)
            {
                // 排队：标志需要往哪个方向切
                _toolPagePending = toControl ? 1 : -1;
                return;
            }

            var frame = ToolPage;
            var height = frame.ActualHeight;

            if (height <= 0)
            {
                frame.Navigate(toControl
                    ? _controlButtonPage
                    : _sortPage);
                return;
            }

            // 已经是目标页，跳过
            var isControl = frame.Content is ToolPages.ControlButtonPage;
            if (toControl == isControl) return;

            _toolPageAnimating = true;
            _toolPagePending = 0;

            var tt = new TranslateTransform();
            frame.RenderTransform = tt;

            var outTarget = toControl ? -height * 0.5 : height * 0.5;
            var slideOut = new DoubleAnimation(0, outTarget, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (_, _) =>
            {
                frame.Navigate(toControl
                    ? _controlButtonPage
                    : _sortPage);

                var inStart = toControl ? height * 0.5 : -height * 0.5;
                tt.Y = inStart;

                var slideIn = new DoubleAnimation(inStart, 0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                slideIn.Completed += (_, _2) =>
                {
                    frame.RenderTransform = Transform.Identity;
                    _toolPageAnimating = false;

                    // 处理动画期间积攒的请求
                    if (_toolPagePending != 0)
                    {
                        var goUp = _toolPagePending > 0;
                        _toolPageShowingControl = goUp;
                        _toolPagePending = 0;
                        AnimateToolPage(goUp);
                        return;
                    }

                    // 自纠：确保页面与状态一致（防止重复按下或竞争）
                    var nowControl = frame.Content is ToolPages.ControlButtonPage;
                    if (_toolPageShowingControl && !nowControl)
                        AnimateToolPage(true);
                    else if (!_toolPageShowingControl && nowControl)
                        AnimateToolPage(false);
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slideIn);
            };
            tt.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        /// <param name="toMenu">true = 向上切到 MenuPage, false = 向下切回 SwitchPage</param>
        private void AnimateCtrlToolPage(bool toMenu)
        {
            if (_ctrlToolAnimating)
            {
                _ctrlToolPending = toMenu ? 1 : -1;
                return;
            }

            var frame = CtrlToolPage;
            var height = frame.ActualHeight;

            if (height <= 0)
            {
                frame.Navigate(toMenu
                    ? _menuPage
                    : _switchPage);
                return;
            }

            // 已经是目标页，跳过
            var isMenu = frame.Content is ToolPages.MenuPage;
            if (toMenu == isMenu) return;

            _ctrlToolAnimating = true;
            _ctrlToolPending = 0;

            var tt = new TranslateTransform();
            frame.RenderTransform = tt;

            var outTarget = toMenu ? -height * 0.5 : height * 0.5;
            var slideOut = new DoubleAnimation(0, outTarget, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (_, _) =>
            {
                frame.Navigate(toMenu
                    ? _menuPage
                    : _switchPage);

                var inStart = toMenu ? height * 0.5 : -height * 0.5;
                tt.Y = inStart;

                var slideIn = new DoubleAnimation(inStart, 0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                slideIn.Completed += (_, _2) =>
                {
                    frame.RenderTransform = Transform.Identity;
                    _ctrlToolAnimating = false;

                    if (_ctrlToolPending != 0)
                    {
                        var goUp = _ctrlToolPending > 0;
                        _ctrlToolShowingMenu = goUp;
                        _ctrlToolPending = 0;
                        AnimateCtrlToolPage(goUp);
                        return;
                    }

                    var nowMenu = frame.Content is ToolPages.MenuPage;
                    if (_ctrlToolShowingMenu && !nowMenu)
                        AnimateCtrlToolPage(true);
                    else if (!_ctrlToolShowingMenu && nowMenu)
                        AnimateCtrlToolPage(false);
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slideIn);
            };
            tt.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }


        public void OnSortModeChanged(SortMode mode)
        {
            if (_currentSortMode == mode) return;
            _currentSortMode = mode;
            SortModeChanged?.Invoke(mode);
        }

        public void AddToDoneList(GoalEntry entry)
        {
            var donePage = _pageCache.GetValueOrDefault("done") as Pages.DonePage;
            if (donePage == null)
            {
                donePage = new Pages.DonePage();
                _pageCache["done"] = donePage;
            }
            else
            {
                donePage.AddItem(entry);
            }
            (_pageCache.GetValueOrDefault("log") as Pages.LogPage)?.RefreshStats();
        }

        public void SetTipText(string tip)
        {
            if (StatusText.RenderTransform is not TranslateTransform tt)
            {
                StatusText.RenderTransform = new TranslateTransform(0, 0);
                tt = (TranslateTransform)StatusText.RenderTransform;
            }

            _tipTimer?.Stop();

            var moveUp = new DoubleAnimation(0, -12, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                StatusText.Text = tip;
                StatusText.Opacity = 0;
                tt.Y = 12;

                var moveDown = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeIn.Completed += (_, _) =>
                {
                    _tipTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    _tipTimer.Tick += (_, _) =>
                    {
                        _tipTimer.Stop();
                        DismissTip();
                    };
                    _tipTimer.Start();
                };

                tt.BeginAnimation(TranslateTransform.YProperty, moveDown);
                StatusText.BeginAnimation(OpacityProperty, fadeIn);
            };

            tt.BeginAnimation(TranslateTransform.YProperty, moveUp);
            StatusText.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void DismissTip()
        {
            if (StatusText.RenderTransform is not TranslateTransform tt)
            {
                StatusText.RenderTransform = new TranslateTransform(0, 0);
                tt = (TranslateTransform)StatusText.RenderTransform;
            }

            var moveUp = new DoubleAnimation(0, -12, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) => StatusText.Text = LocalizationManager.T("状态机无更新", "No status update");

            tt.BeginAnimation(TranslateTransform.YProperty, moveUp);
            StatusText.BeginAnimation(OpacityProperty, fadeOut);
        }

        // ======================== 状态栏按钮 ========================

        private void BellBtn_Click(object sender, RoutedEventArgs e)
        {
            // 点击铃铛显示当前提示
            if (StatusText.Text != LocalizationManager.T("状态机无更新", "No status update"))
                SetTipText(StatusText.Text);
            else
                SetTipText(LocalizationManager.T("暂无新消息。", "No new messages."));
        }

        private void NotificationBtn_Click(object sender, RoutedEventArgs e)
        {
            // 点击铃铛图标：显示当前打开的项目和数据文件信息
            var proj = Services.ProjectService.CurrentProject;
            if (proj == null)
            {
                SetTipText(LocalizationManager.T("未打开任何项目。", "No project opened."));
                return;
            }
            var dataPath = Services.DataService.GetFilePath();
            var dataName = dataPath != null
                ? System.IO.Path.GetFileName(dataPath)
                : "?";
            var merged = Services.DataService.ReadAllVersions(Services.ProjectService.CurrentProjectDir!);
            var unfinished = merged.Unfinished.Count;
            var finished = merged.Finished.Count;
            SetTipText(LocalizationManager.T($"项目：{proj.Name}  |  数据文件：{dataName}  |  未完成 {unfinished}，已完成 {finished}", $"Project: {proj.Name}  |  Data file: {dataName}  |  Unfinished {unfinished}, Finished {finished}"));
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionComboBox.SelectedItem is not ComboBoxItem item) return;
            var tag = item.Tag?.ToString();
            if (tag == "__placeholder__" || string.IsNullOrEmpty(tag)) return;

            try
            {
                Services.ProjectService.SwitchVersion(tag + ".json");
                RefreshAllViews();
                SetTipText(LocalizationManager.T($"已切换到版本 {tag}。", $"Switched to version {tag}."));
            }
            catch (Exception ex)
            {
                SetTipText(LocalizationManager.T($"切换失败：{ex.Message}", $"Switch failed: {ex.Message}"));
            }

            // 重置选中项
            VersionComboBox.SelectedIndex = 0;
        }

        /// <summary>刷新状态栏 ComboBox 为可用的版本列表。</summary>
        public void RefreshVersionCombo()
        {
            VersionComboBox.Items.Clear();
            VersionComboBox.Items.Add(new ComboBoxItem { Content = LocalizationManager.T("调整目标的文件", "Adjust target file"), Tag = "__placeholder__" });

            var dir = Services.ProjectService.CurrentProjectDir;
            if (dir == null) return;

            var files = Services.ProjectService.GetVersionFiles(dir);
            var currentVersion = Services.ProjectService.CurrentProject?.CurrentVersion ?? "";
            foreach (var f in files)
            {
                var clean = f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? f[..^5] : f;
                var display = clean == currentVersion ? $"⭐ {clean}" : clean;
                VersionComboBox.Items.Add(new ComboBoxItem { Content = display, Tag = clean });
            }
        }

        public void RefreshUnDoneList()
        {
            (_pageCache.GetValueOrDefault("undone") as Pages.UnDonePage)?.LoadFromData();
            (_pageCache.GetValueOrDefault("undone") as Pages.UnDonePage)?.ApplySort(CurrentSortMode);
            (_pageCache.GetValueOrDefault("log") as Pages.LogPage)?.RefreshStats();
        }

        /// <summary>刷新所有已加载的视图（切换项目/版本后调用）。</summary>
        public void RefreshAllViews()
        {
            // 遍历所有注册页面，调用 OnRefresh
            foreach (var reg in _pageRegs)
            {
                if (_pageCache.TryGetValue(reg.Key, out var page))
                    reg.OnRefresh?.Invoke(page);
            }
            RefreshVersionCombo();
            RestartFileWatcher();
        }

        /// <summary>展开所有条目详情。</summary>
        public void ExpandAllPageDetails()
        {
            (_pageCache.GetValueOrDefault("undone") as Pages.UnDonePage)?.ExpandAllDetails();
            (_pageCache.GetValueOrDefault("done") as Pages.DonePage)?.ExpandAllDetails();
            SetTipText(LocalizationManager.T("已展开所有条目详情。", "Expanded all entry details."));
        }

        /// <summary>收起所有条目详情。</summary>
        public void CollapseAllPageDetails()
        {
            (_pageCache.GetValueOrDefault("undone") as Pages.UnDonePage)?.CollapseAllDetails();
            (_pageCache.GetValueOrDefault("done") as Pages.DonePage)?.CollapseAllDetails();
            SetTipText(LocalizationManager.T("已收起所有条目详情。", "Collapsed all entry details."));
        }

        /// <summary>打开帮助页面。</summary>
        public void ShowHelpPage()
        {
            SwitchTab("help");
        }

        // ── 弹窗管理 ──

        /// <summary>打开新建条目弹窗。</summary>
        public void ShowNewEntryDialog()
        {
            NewEntryDialog.Reset();
            NewEntryDialog.Confirmed += OnDialogConfirmed;
            NewEntryDialog.Cancelled += OnDialogCancelled;
            ShowDialogOverlay();
        }

        /// <summary>打开编辑条目弹窗。</summary>
        public void ShowEditEntryDialog(GoalEntry entry)
        {
            NewEntryDialog.LoadEntry(entry);
            NewEntryDialog.Confirmed += OnDialogConfirmed;
            NewEntryDialog.Cancelled += OnDialogCancelled;
            ShowDialogOverlay();
        }

        private void ShowDialogOverlay()
        {
            MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            DialogOverlay.Visibility = Visibility.Visible;
            NewEntryDialog.Visibility = Visibility.Visible;
        }

        private void DismissDialogOverlay()
        {
            NewEntryDialog.Confirmed -= OnDialogConfirmed;
            NewEntryDialog.Cancelled -= OnDialogCancelled;
            NewEntryDialog.Visibility = Visibility.Collapsed;
            DialogOverlay.Visibility = Visibility.Collapsed;
            MainContentGrid.Effect = null;
        }

        private void OnDialogConfirmed(object? sender, EventArgs e)
        {
            var (isValid, message) = NewEntryDialog.Validate();
            if (!isValid)
            {
                SetTipText(message);
                return;
            }

            if (NewEntryDialog.EditingEntry is GoalEntry existing)
            {
                // 编辑模式：更新已有条目（保存到条目的原始版本文件）
                var title = NewEntryDialog.EntryTitle;
                Services.DataService.SaveToEntryVersion(
                    Services.ProjectService.CurrentProjectDir!, existing,
                    (data, target) =>
                    {
                        target.Title = title;
                        target.Severity = (GoalSeverity)NewEntryDialog.Severity;
                        target.Brief = NewEntryDialog.Brief;
                        target.Detail = NewEntryDialog.Detail;
                        target.Deadline = NewEntryDialog.Deadline;
                        target.Version = NewEntryDialog.Version;
                        target.Type = NewEntryDialog.Type;
                        target.RelatedFiles = new(NewEntryDialog.Files);
                    });
                RefreshAllViews();
                DismissDialogOverlay();
                SetTipText(LocalizationManager.T($"已更新条目「{title}」。", $"Updated entry \"{title}\"."));
            }
            else
            {
                // 新建模式
                var entry = new GoalEntry
                {
                    Title = NewEntryDialog.EntryTitle,
                    Severity = (GoalSeverity)NewEntryDialog.Severity,
                    Brief = NewEntryDialog.Brief,
                    Detail = NewEntryDialog.Detail,
                    Deadline = NewEntryDialog.Deadline,
                    ChangeDemand = 0,
                    IsFavorited = false,
                    Version = string.IsNullOrWhiteSpace(NewEntryDialog.Version)
                        ? Services.ProjectService.CurrentProject?.CurrentVersion ?? string.Empty
                        : NewEntryDialog.Version,
                    Type = NewEntryDialog.Type,
                    RelatedFiles = new(NewEntryDialog.Files)
                };
                Services.ProjectService.AssignEntryId(entry);
                Services.DataService.Current.Unfinished.Add(entry);
                Services.DataService.Save();
                RefreshUnDoneList();
                DismissDialogOverlay();
                SetTipText(LocalizationManager.T($"已添加条目「{entry.Title}」。", $"Added entry \"{entry.Title}\"."));
            }
        }

        private void OnDialogCancelled(object? sender, EventArgs e)
        {
            DismissDialogOverlay();
        }

        /// <summary>公开的 Tab 切换入口，供 SwitchPage 等调用（按字符串 key）。</summary>
        public void SwitchTab(string key)
        {
            var reg = _pageRegs.FirstOrDefault(r => r.Key == key);
            if (reg == null) return;

            var idx = _pageRegs.IndexOf(reg);
            var page = GetOrCreatePage(reg);
            if (page == null) return;

            // 每次都触发 OnBeforeNavigate
            reg.OnBeforeNavigate?.Invoke(page);

            // 切换 Tab 时把当前搜索重新应用到目标页
            ApplySearch(reg.Key, SearchTextBox.Text);

            NavigateByKey(idx, reg, page);
        }

        /// <summary>公开的 Tab 切换入口（按索引，兼容旧调用）。</summary>
        public void SwitchTab(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < _pageRegs.Count)
                SwitchTab(_pageRegs[tabIndex].Key);
        }

        private Page? GetOrCreatePage(PageRegistration reg)
        {
            if (_pageCache.TryGetValue(reg.Key, out var cached))
                return cached;

            var page = reg.PageFactory();
            _pageCache[reg.Key] = page;
            reg.OnInit?.Invoke(page);
            return page;
        }

        private void NavigateByKey(int tabIndex, PageRegistration reg, Page page)
        {
            if (reg.IsOverlayTab)
                _switchPage?.ShowOverlayTab(reg.Key);
            else
                _switchPage?.HideOverlayTabs();

            ExecuteNavigation(tabIndex, f => f.Navigate(page));
        }

        private void OnTabSelected(string key)
        {
            SwitchTab(key);
        }

        private void RegisterPage(PageRegistration reg)
        {
            _pageRegs.Add(reg);
            if (!reg.IsOverlayTab)
                _switchPage?.AddTabButton(reg.Key, reg.TabLabel, _pageRegs.Count - 1);
        }

        private void NavigateToPage(int tabIndex, Action<Frame> getPage)
        {
            if (_isPageAnimating)
            {
                _pendingTabIndex = tabIndex;
                return;
            }
            if (tabIndex == _lastTabIndex) return;

            ExecuteNavigation(tabIndex, getPage);
        }

        private void ExecuteNavigation(int tabIndex, Action<Frame> getPage)
        {
            _isPageAnimating = true;
            _pendingTabIndex = -1;

            var goingRight = tabIndex > _lastTabIndex;
            _lastTabIndex = tabIndex;

            var frame = ListPage;
            var width = frame.ActualWidth;
            var tt = new TranslateTransform();
            frame.RenderTransform = tt;

            // Phase 1: slide out
            var outOffset = goingRight ? -width : width;
            var slideOut = new DoubleAnimation(0, outOffset * 0.3, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (_, _) =>
            {
                getPage(frame);

                var inStart = goingRight ? width * 0.3 : -width * 0.3;
                tt.X = inStart;

                var slideIn = new DoubleAnimation(inStart, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                slideIn.Completed += (_, _2) =>
                {
                    frame.RenderTransform = Transform.Identity;
                    _isPageAnimating = false;

                    // 处理动画期间积攒的切换请求
                    if (_pendingTabIndex >= 0 && _pendingTabIndex != _lastTabIndex)
                    {
                        var pending = _pendingTabIndex;
                        _pendingTabIndex = -1;
                        if (pending >= 0 && pending < _pageRegs.Count)
                            SwitchTab(_pageRegs[pending].Key);
                    }
                    else
                    {
                        _pendingTabIndex = -1;
                    }
                };
                tt.BeginAnimation(TranslateTransform.XProperty, slideIn);
            };
            tt.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        #region 拖拽与最大化
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                MaximizeWindow_Click(sender, e);
            else if (!_isMaximized)
                DragMove();
        }

        public void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_isMaximized)
                Restore();
            else
                Maximize();
        }

        private void Maximize()
        {
            ApplyMaximizedStyle();
            ResizeMode = ResizeMode.CanMinimize;
            _isMaximized = true;
            WindowState = WindowState.Maximized;
        }

        private void Restore()
        {
            ApplyNormalStyle();
            ResizeMode = ResizeMode.CanResize;
            _isMaximized = false;
            WindowState = WindowState.Normal;
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                _isMaximized = true;
                ApplyMaximizedStyle();
                ResizeMode = ResizeMode.CanMinimize;
            }
            else if (WindowState == WindowState.Normal)
            {
                _isMaximized = false;
                ApplyNormalStyle();
                ResizeMode = ResizeMode.CanResize;

                if (_justMinimized)
                {
                    _justMinimized = false;
                    PlayRestoreFromMinimized();
                }
            }
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle);
            source!.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var monitor = NativeMethods.MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
                var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                NativeMethods.GetMonitorInfo(monitor, ref info);

                // 将最大化限制在工作区内，不遮挡任务栏
                mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }
        #endregion

        #region 窗口样式
        private void ApplyMaximizedStyle()
        {
            MaximizeIcon.Text = "\uE923"; // 还原图标
            MainBorder.Margin = new Thickness(0);
            MainBorder.CornerRadius = new CornerRadius(0);
            MainBorder.BorderThickness = new Thickness(0);
        }

        private void ApplyNormalStyle()
        {
            MaximizeIcon.Text = "\uE922"; // 最大化图标
            MainBorder.Margin = new Thickness(5);
            MainBorder.CornerRadius = new CornerRadius(10);
            MainBorder.BorderThickness = new Thickness(2);
        }
        #endregion
    }

    #region 原生互操作
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    #endregion
}
