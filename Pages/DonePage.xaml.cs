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

namespace OCCMissionGoals.Pages
{
    public partial class DonePage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<DoneItemVM> _items = new();
        private ObservableCollection<DoneVersionGroupVM> _groups = new();
        private SortMode _currentSort = SortMode.SeverityAsc;
        private string _searchFilter = string.Empty;
        private SearchMode _searchMode = SearchMode.Text;

        /// <summary>悬停 2 秒展开概览区：页面只维护一个计时器，跟随当前悬停的条目。</summary>
        private readonly System.Windows.Threading.DispatcherTimer _hoverTimer = new()
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        private object? _hoverTarget;

        public DonePage()
        {
            InitializeComponent();
            _hoverTimer.Tick += HoverTimer_Tick;
            LoadFromData();
            ApplySort(SortMode.SeverityAsc);

            Loaded += (_, _) =>
            {
                if (Window.GetWindow(this) is MainWindow mw)
                {
                    mw.SortModeChanged += OnSortModeChanged;
                    ApplySort(mw.CurrentSortMode);
                }
            };

            Unloaded += (_, _) =>
            {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.SortModeChanged -= OnSortModeChanged;
            };
        }

        public void LoadFromData()
        {
            _items.Clear();
            // 版本名来自条目所在的版本文件 —— 条目已不再自带版本字段。
            foreach (var (entry, version) in Services.DataService.ReadStatsVersions(Services.ProjectService.CurrentProjectDir!))
            {
                if (entry.Status != EntryStatus.Finished) continue;
                _items.Add(new DoneItemVM(entry, version));
            }
        }

        private void OnSortModeChanged(SortMode mode) => ApplySort(mode);

        private IEnumerable<DoneItemVM> GetSortedItems()
        {
            var query = _items.AsEnumerable();

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(_searchFilter))
                query = query.Where(i => SearchMatcher.Matches(i.Entry, _searchFilter, _searchMode, useCompletedDate: true));

            if (_currentSort == SortMode.FavoritesOnly)
                query = query.Where(i => i.Entry.IsFavorited);

            return _currentSort switch
            {
                SortMode.FavoritesOnly => query.OrderBy(i => i.Entry.Severity),
                SortMode.SeverityAsc  => query.OrderBy(i => i.Entry.Severity),
                SortMode.SeverityDesc => query.OrderByDescending(i => i.Entry.Severity),
                SortMode.VersionAsc   => query.OrderBy(i => i.Version),
                SortMode.VersionDesc  => query.OrderByDescending(i => i.Version),
                SortMode.TypeAsc      => query.OrderBy(i => (i.Entry.Type.FirstOrDefault() ?? string.Empty).ToLowerInvariant()),
                _ => query.OrderBy(i => i.Entry.Severity),
            };
        }

        public void ApplySort(SortMode mode)
        {
            _currentSort = mode;
            RebuildGroups();
        }

        /// <summary>应用搜索过滤。</summary>
        public void ApplyFilter(string filter, SearchMode mode = SearchMode.Text)
        {
            _searchFilter = (filter ?? string.Empty).Trim();
            _searchMode = mode;
            RebuildGroups();
        }

        /// <summary>展开所有条目的详细信息。</summary>
        public void ExpandAllDetails()
        {
            foreach (var group in _groups)
            foreach (var item in group.Items)
                item.IsDetailExpanded = true;
        }

        /// <summary>收起所有条目的详细信息。</summary>
        public void CollapseAllDetails()
        {
            foreach (var group in _groups)
            foreach (var item in group.Items)
                item.IsDetailExpanded = false;
        }

        /// <summary>跳转并高亮指定条目（供搜索板跳转使用）。</summary>
        public void SelectEntry(GoalEntry entry)
        {
            var group = _groups.FirstOrDefault(g => g.Items.Any(i => SameEntry(i.Entry, entry)));
            if (group == null) return;
            var item = group.Items.First(i => SameEntry(i.Entry, entry));
            group.IsExpanded = true;

            ScrollToItem(item);
        }

        private void ScrollToItem(DoneItemVM item)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            var ticks = 0;
            timer.Tick += (_, _) =>
            {
                ticks++;
                var container = LocateItemContainer(item);
                if (container != null)
                {
                    timer.Stop();
                    container.BringIntoView();
                    FlashEntryCard(container);
                    return;
                }
                if (ticks >= 20) timer.Stop();
            };
            timer.Start();
        }

        private FrameworkElement? LocateItemContainer(DoneItemVM item)
        {
            foreach (var group in _groups)
            {
                if (!group.Items.Contains(item)) continue;
                var gc = DoneList.ItemContainerGenerator.ContainerFromItem(group) as FrameworkElement;
                if (gc == null) continue;
                var groupItems = FindNameInTree<ItemsControl>(gc, "GroupItems");
                if (groupItems == null) continue;
                groupItems.UpdateLayout();
                return groupItems.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            }
            return null;
        }

        private static bool SameEntry(GoalEntry a, GoalEntry b)
        {
            if (!string.IsNullOrEmpty(a.Id) && !string.IsNullOrEmpty(b.Id))
                return a.Id == b.Id;
            return a.Title == b.Title;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private static void FlashEntryCard(FrameworkElement container)
        {
            var card = container as Border ?? FindVisualChild<Border>(container);
            if (card == null) return;
            var original = card.BorderBrush;
            // 提示色跟随主题色，让搜索跳转的高亮与当前配色协调。
            card.BorderBrush = Application.Current.Resources["PrimaryBrush"] as SolidColorBrush
                               ?? new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) => { timer.Stop(); card.BorderBrush = original; };
            timer.Start();
        }

        private void RebuildGroups()
        {
            var sorted = GetSortedItems().ToList();

            var oldExpandStates = new Dictionary<string, bool>();
            foreach (var g in _groups)
                oldExpandStates[g.VersionName] = g.IsExpanded;

            var grouped = sorted
                .GroupBy(i => string.IsNullOrEmpty(i.Version) ? string.Empty : i.Version)
                .OrderBy(g => g.Key);

            _groups.Clear();
            foreach (var group in grouped)
            {
                var vm = new DoneVersionGroupVM
                {
                    VersionName = group.Key,
                    IsExpanded = oldExpandStates.TryGetValue(group.Key, out var expanded) ? expanded : true,
                    CanArchive = CanArchiveVersion(group.Key),
                };
                foreach (var item in group)
                    vm.Items.Add(item);
                _groups.Add(vm);
            }

            DoneList.ItemsSource = _groups;
            EmptyPlaceholder.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool CanArchiveVersion(string versionName)
        {
            // 未指定版本不可归档
            if (string.IsNullOrEmpty(versionName))
                return false;

            var projectDir = Services.ProjectService.CurrentProjectDir;
            if (string.IsNullOrEmpty(projectDir)) return false;

            var versionsDir = Services.ProjectService.GetVersionsDir(projectDir);
            var versionFile = Path.Combine(versionsDir, versionName + ".json");
            if (!File.Exists(versionFile)) return false;

            try
            {
                var json = File.ReadAllText(versionFile);
                var data = System.Text.Json.JsonSerializer.Deserialize<DataFile>(json);
                // 只有全部条目均已完成时才可归档
                return data != null && data.Entries.Count > 0
                    && data.Entries.All(e => e.Status == EntryStatus.Finished);
            }
            catch { return false; }
        }

        private void VersionHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border header || header.Tag is not DoneVersionGroupVM group)
                return;

            // 只翻状态：折叠高度过渡和箭头文字都跟着绑定走
            group.IsExpanded = !group.IsExpanded;
        }

        private static T? FindParentOfType<T>(DependencyObject child) where T : DependencyObject
        {
            var p = VisualTreeHelper.GetParent(child);
            while (p != null)
            {
                if (p is T t) return t;
                p = VisualTreeHelper.GetParent(p);
            }
            return default;
        }

        /// <summary>折叠/展开版本分组（供右键菜单使用）。</summary>
        public void ToggleGroup(DoneVersionGroupVM group)
        {
            group.IsExpanded = !group.IsExpanded;
        }

        private static T? FindNameInTree<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var found = FindNameInTree<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void UndoComplete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DoneItemVM vm)
                RunAnimation(btn, null, () => UndoItem(vm));
        }

        /// <summary>撤销完成（供右键菜单使用，无折叠动画）。</summary>
        public void UndoItem(DoneItemVM vm)
        {
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) =>
                {
                    target.Status = EntryStatus.Unfinished;
                    target.CompletedAt = default;
                });
            LoadFromData();
            RebuildGroups();

            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.SetTipText(Services.TipService.GetUndoCompleteTip(vm.Entry));
                mw.RefreshUnDoneList();
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DoneItemVM vm)
                EditItem(vm);
        }

        /// <summary>打开条目编辑窗口（供右键菜单使用）。</summary>
        public void EditItem(DoneItemVM vm)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.ShowEditEntryDialog(vm.Entry);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DoneItemVM vm)
                RunAnimation(btn, Colors.Red, () => DeleteItem(vm));
        }

        /// <summary>删除条目（供右键菜单使用，无折叠动画）。</summary>
        public void DeleteItem(DoneItemVM vm)
        {
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) => data.Entries.Remove(target));
            LoadFromData();
            RebuildGroups();
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DoneVersionGroupVM group) return;
            ArchiveGroup(group);
        }

        /// <summary>归档版本（供右键菜单使用）。</summary>
        public void ArchiveGroup(DoneVersionGroupVM group)
        {
            try
            {
                if (group.Items.Count == 0) return;

                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var binDir = Path.Combine(exeDir, "bin");
                Directory.CreateDirectory(binDir);

                var verName = string.IsNullOrEmpty(group.VersionName) ? LocalizationManager.T("未指定版本") : group.VersionName;
                var safeName = verName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var fileName = $"archive_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                var filePath = Path.Combine(binDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine(LocalizationManager.T("# 归档 — {0}", verName));
                sb.AppendLine();
                sb.AppendLine(LocalizationManager.T("> 导出时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
                sb.AppendLine(LocalizationManager.T("> 条目数量: {0}", group.Items.Count));
                sb.AppendLine();

                // 每个错误/条目占一行，用一张 Markdown 表格呈现
                sb.AppendLine($"| {LocalizationManager.T("标题")} | {LocalizationManager.T("严重程度")} | {LocalizationManager.T("完成时间")} | {LocalizationManager.T("相关文件")} |");
                sb.AppendLine("|------|------|------|------|");

                foreach (var item in group.Items)
                {
                    var files = item.Entry.RelatedFiles.Count > 0
                        ? string.Join(LocalizationManager.T("、"), item.Entry.RelatedFiles.Select(f =>
                        {
                            var name = Path.GetFileName(f.Path);
                            return $"{name}[{f.Line}:{f.Column}]";
                        }))
                        : LocalizationManager.T("(无)");

                    sb.AppendLine(
                        $"| {EscapeMd(item.Title)} | {SeverityHelper.GetText(item.Entry.Severity)} | {item.Entry.CompletedAt:yyyy-MM-dd HH:mm} | {EscapeMd(files)} |");
                }

                sb.AppendLine();

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                // 安全校验：确认版本内全部条目均已完成
                if (!CanArchiveVersion(group.VersionName))
                {
                    MessageBox.Show(LocalizationManager.T("该版本中仍有未完成的条目，无法归档。"), LocalizationManager.T("无法归档"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 删除对应的版本 JSON 文件
                var projectDir = Services.ProjectService.CurrentProjectDir;
                if (!string.IsNullOrEmpty(projectDir) && !string.IsNullOrEmpty(group.VersionName))
                {
                    var versionsDir = Services.ProjectService.GetVersionsDir(projectDir);
                    var versionFile = Path.Combine(versionsDir, group.VersionName + ".json");
                    if (File.Exists(versionFile))
                        File.Delete(versionFile);
                }

                // 刷新页面
                LoadFromData();
                RebuildGroups();

                if (Window.GetWindow(this) is MainWindow mw)
                    mw.SetTipText(LocalizationManager.T("已归档 {0} 条到 bin/{1}。", group.Items.Count, fileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.T("归档失败: {0}", ex.Message), LocalizationManager.T("错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeMd(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("|", "\\|").Replace("\r", "").Replace("\n", "<br>");
        }

        private void RunAnimation(Button btn, Color? glowColor, Action onComplete)
        {
            var border = FindCardBorder(btn);
            if (border == null)
            {
                onComplete();
                return;
            }

            border.IsHitTestVisible = false;

            Effect effect;
            PropertyPath radiusPath;

            if (glowColor is Color color)
            {
                effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 0,
                    ShadowDepth = 0,
                    Opacity = 0,
                    RenderingBias = RenderingBias.Quality
                };
                radiusPath = new PropertyPath("(UIElement.Effect).(DropShadowEffect.BlurRadius)");
            }
            else
            {
                effect = new BlurEffect
                {
                    Radius = 0,
                    KernelType = KernelType.Gaussian,
                    RenderingBias = RenderingBias.Quality
                };
                radiusPath = new PropertyPath("(UIElement.Effect).(BlurEffect.Radius)");
            }

            border.Effect = effect;

            var sb = new Storyboard();
            var capturedHeight = border.ActualHeight;

            var blurAnim = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(blurAnim, border);
            Storyboard.SetTargetProperty(blurAnim, radiusPath);
            sb.Children.Add(blurAnim);

            if (glowColor != null)
            {
                var glowOpacityAnim = new DoubleAnimation(0, 0.7, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                Storyboard.SetTarget(glowOpacityAnim, border);
                Storyboard.SetTargetProperty(glowOpacityAnim, new PropertyPath("(UIElement.Effect).(DropShadowEffect.Opacity)"));
                sb.Children.Add(glowOpacityAnim);
            }

            var fade1 = new DoubleAnimation(1, 0.6, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(fade1, border);
            Storyboard.SetTargetProperty(fade1, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fade1);

            var heightAnim = new DoubleAnimation(capturedHeight, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(heightAnim, border);
            Storyboard.SetTargetProperty(heightAnim, new PropertyPath(FrameworkElement.HeightProperty));
            sb.Children.Add(heightAnim);

            var fade2 = new DoubleAnimation(0.6, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };
            Storyboard.SetTarget(fade2, border);
            Storyboard.SetTargetProperty(fade2, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fade2);

            sb.Completed += (_, _) =>
            {
                border.Height = double.NaN;
                border.Effect = null;
                border.IsHitTestVisible = true;
                onComplete();
            };

            sb.Begin();
        }

        private static Border? FindCardBorder(DependencyObject start)
        {
            while (start != null)
            {
                if (start is Border b && b.CornerRadius.TopLeft > 0)
                    return b;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }

        private void DetailToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TextBlock toggle) return;
            if (toggle.DataContext is not DoneItemVM vm) return;
            vm.IsDetailExpanded = !vm.IsDetailExpanded;
        }

        /// <summary>
        /// 「相关文件」表里点一行：滚到内容区里引用该文件的那一行并短暂高亮。
        /// 目标行由内容区渲染时打的 Tag 认定，作用范围限定在本条目的详情面板内。
        /// </summary>
        private void RelatedFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement row || row.DataContext is not FileRef file) return;

            // 行已自行处理，别再冒泡给卡片上的其它点击处理
            e.Handled = true;

            var panel = FindDetailPanel(row);
            if (panel != null && Services.FileRefJump.TryJump(panel, file)) return;

            if (Window.GetWindow(this) is MainWindow mw)
                mw.SetTipText(LocalizationManager.T("未在内容区找到该文件的引用。"));
        }

        /// <summary>从行往上找所在条目的详情面板（跳转不越出当前条目）。</summary>
        private static StackPanel? FindDetailPanel(DependencyObject start)
        {
            while (start != null)
            {
                if (start is StackPanel panel && panel.Name == "DetailPanel") return panel;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }

        // ==================== 悬停 2 秒：变宽 + 展开概览 ====================

        private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not DoneItemVM vm) return;

            // 已展开详情的条目不再接受聚焦：概览已持久显示，无需悬停效果
            if (vm.IsDetailExpanded)
            {
                _hoverTarget = null;
                _hoverTimer.Stop();
                return;
            }

            _hoverTarget = vm;
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }

        private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _hoverTimer.Stop();
            if (_hoverTarget is DoneItemVM vm) vm.IsHoverPreview = false;
            _hoverTarget = null;
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();

            // 计时期间若已展开详情，则不再进入悬停预览（概览已持久显示）
            if (_hoverTarget is DoneItemVM vm && !vm.IsDetailExpanded) vm.IsHoverPreview = true;
        }

        private void CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DoneItemVM vm) return;
            CopyItemInfo(vm);
        }

        /// <summary>复制条目信息（供右键菜单使用）。</summary>
        public void CopyItemInfo(DoneItemVM vm)
        {
            try
            {
                Clipboard.SetText(Services.EntryCopyFormatter.BuildText(vm.Entry));

                if (Window.GetWindow(this) is MainWindow mw)
                    mw.SetTipText(LocalizationManager.T("已复制条目信息。"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.T("操作失败：{0}", ex.Message), LocalizationManager.T("错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void AddItem(GoalEntry entry)
        {
            var vm = new DoneItemVM(entry,
                Services.ProjectService.CurrentProject?.CurrentVersion ?? string.Empty);
            _items.Add(vm);
            RebuildGroups();
        }
    }

    public class DoneVersionGroupVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string VersionName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(VersionName) ? LocalizationManager.T("未指定版本") : VersionName;
        public string DisplayCount => Items.Count.ToString();
        public ObservableCollection<DoneItemVM> Items { get; set; } = new();

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArrowText)));
            }
        }

        /// <summary>折叠箭头：展开 ▼ / 折叠 ▶。</summary>
        public string ArrowText => IsExpanded ? "▼" : "▶";

        public bool CanArchive { get; set; }
    }

    public class DoneItemVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public GoalEntry Entry { get; }

        private bool _isDetailExpanded;
        public bool IsDetailExpanded
        {
            get => _isDetailExpanded;
            set
            {
                _isDetailExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExcerptVisible));
                OnPropertyChanged(nameof(DetailToggleText));
                OnPropertyChanged(nameof(ShowOverview));
            }
        }

        public bool IsExcerptVisible => !_isDetailExpanded;
        public string DetailToggleText => _isDetailExpanded ? LocalizationManager.T("收起") : LocalizationManager.T("详情");

        public string Title => Entry.Title;
        public string SeverityText => SeverityHelper.GetText(Entry.Severity);
        public Brush SeverityBrush => SeverityHelper.GetBrush(Entry.Severity);
        public DateTime CompletedAt => Entry.CompletedAt;
        public string Brief => Entry.Brief;

        /// <summary>条目所属版本（来自它所在的版本文件）。</summary>
        public string Version { get; }

        public IEnumerable<TypeTag> TypeTags =>
            Entry.Type.Select(t => new TypeTag(t, Services.ProjectService.GetTypeColor(t)));
        public bool HasType => Entry.Type.Count > 0;
        public List<FileRef> RelatedFiles => Entry.RelatedFiles;
        public bool HasRelatedFiles => Entry.RelatedFiles.Count > 0;

        /// <summary>内容区（区块化正文：文本 / 表格 / 分割线 / 代码块 / 文件引用 / 多级列表）。</summary>
        public List<ContentBlock> Contents => Entry.Contents;
        public bool HasContents => Entry.Contents.Count > 0;

        /// <summary>完成度（0-100）：由内容区多级列表条目的勾选情况推导。</summary>
        public int Progress => Entry.Progress;
        public string ProgressText => Entry.Progress.ToString() + "%";

        /// <summary>概览区显示的类别串。</summary>
        public string TypeSummary => Entry.Type.Count == 0
            ? LocalizationManager.T("未分类")
            : string.Join(" · ", Entry.Type);

        private bool _isHoverPreview;

        /// <summary>鼠标在条目上停留 2 秒后置真：条目变宽并展开概览区域。</summary>
        public bool IsHoverPreview
        {
            get => _isHoverPreview;
            set
            {
                if (_isHoverPreview == value) return;
                _isHoverPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowOverview));
            }
        }

        /// <summary>概览区是否展开：鼠标悬停预览，或详情已展开时持久显示。</summary>
        public bool ShowOverview => _isHoverPreview || _isDetailExpanded;

        public DoneItemVM(GoalEntry entry, string version)
        {
            Entry = entry;
            Version = version;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
