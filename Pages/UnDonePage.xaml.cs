using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Pages
{
    public partial class UnDonePage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<UnDoneItemVM> _items = new();
        private ObservableCollection<UnDoneVersionGroupVM> _groups = new();
        private SortMode _currentSort = SortMode.SeverityAsc;
        private string _searchFilter = string.Empty;
        private SearchMode _searchMode = SearchMode.Text;

        /// <summary>悬停 2 秒展开概览区：页面只维护一个计时器，跟随当前悬停的条目。</summary>
        private readonly System.Windows.Threading.DispatcherTimer _hoverTimer = new()
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        private object? _hoverTarget;

        private const string StarFilled =
            "M15.022 7.25497L12.203 10.003L12.869 13.883C12.917 14.165 12.844 14.438 12.664 14.654C12.479 14.872 12.205 15.001 11.929 15.001C11.775 15.001 11.626 14.963 11.485 14.89L8.00101 13.057L4.51701 14.889C4.13401 15.093 3.62401 14.991 3.34001 14.657C3.15801 14.439 3.08501 14.165 3.13201 13.884L3.79801 10.004L0.979007 7.25597C0.714007 6.99797 0.624007 6.63297 0.737007 6.27997C0.853007 5.92497 1.14001 5.68197 1.50701 5.62797L5.40301 5.06197L7.14501 1.53197C7.47301 0.865971 8.52801 0.865971 8.85601 1.53197L10.598 5.06197L14.494 5.62797C14.862 5.68197 15.149 5.92397 15.264 6.27597C15.378 6.63197 15.286 6.99697 15.022 7.25497Z";
        private const string StarOutline =
            "M11.928 15C11.774 15 11.625 14.962 11.484 14.889L8 13.056L4.516 14.888C4.132 15.092 3.623 14.99 3.339 14.656C3.157 14.438 3.084 14.164 3.131 13.883L3.797 10.003L0.978 7.25499C0.713 6.99699 0.623 6.63199 0.736 6.27899C0.852 5.92399 1.139 5.68099 1.506 5.62699L5.402 5.06099L7.144 1.53099C7.472 0.864994 8.527 0.864994 8.855 1.53099L10.597 5.06099L14.493 5.62699C14.861 5.68099 15.148 5.92299 15.263 6.27499C15.377 6.63099 15.286 6.99599 15.022 7.25399L12.203 10.002L12.869 13.882C12.917 14.164 12.844 14.437 12.664 14.653C12.479 14.871 12.204 15 11.928 15ZM7.959 1.97399L6.066 5.97499L1.65 6.61599L4.871 9.65299L4.117 14.05L8 11.925L11.892 13.972L11.129 9.65299L14.324 6.53799L9.934 5.97499L7.959 1.97399Z";

        public UnDonePage()
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
                if (entry.Status != EntryStatus.Unfinished) continue;
                var item = new UnDoneItemVM(entry, version);
                item.SubTaskToggled += PersistSubTaskToggle;
                _items.Add(item);
            }
        }

        private void OnSortModeChanged(SortMode mode) => ApplySort(mode);

        private IEnumerable<UnDoneItemVM> GetSortedItems()
        {
            var query = _items.AsEnumerable();

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(_searchFilter))
                query = query.Where(i => SearchMatcher.Matches(i.Entry, _searchFilter, _searchMode, useCompletedDate: false));

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

        private void ScrollToItem(UnDoneItemVM item)
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

        private FrameworkElement? LocateItemContainer(UnDoneItemVM item)
        {
            foreach (var group in _groups)
            {
                if (!group.Items.Contains(item)) continue;
                var gc = UnDoneList.ItemContainerGenerator.ContainerFromItem(group) as FrameworkElement;
                if (gc == null) continue;
                var groupItems = FindVisualChildByName<ItemsControl>(gc, "GroupItems");
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

        private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var found = FindVisualChildByName<T>(child, name);
                if (found != null) return found;
            }
            return null;
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
                var vm = new UnDoneVersionGroupVM
                {
                    VersionName = group.Key,
                    IsExpanded = oldExpandStates.TryGetValue(group.Key, out var expanded) ? expanded : true,
                };
                foreach (var item in group)
                    vm.Items.Add(item);
                _groups.Add(vm);
            }

            UnDoneList.ItemsSource = _groups;
            EmptyPlaceholder.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void VersionHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border header || header.Tag is not UnDoneVersionGroupVM group)
                return;

            // 只翻状态：折叠高度过渡和箭头文字都跟着绑定走
            group.IsExpanded = !group.IsExpanded;
        }

        /// <summary>折叠/展开版本分组（供右键菜单使用）。</summary>
        public void ToggleGroup(UnDoneVersionGroupVM group)
        {
            group.IsExpanded = !group.IsExpanded;
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not UnDoneItemVM vm) return;

            var star = FindStarPath(btn);
            vm.IsFavorited = !vm.IsFavorited;
            if (star != null) ApplyStarState(star, vm.IsFavorited);
            PersistFavorite(vm);
        }

        /// <summary>切换收藏状态（供右键菜单使用，重建列表后星标由模板刷新）。</summary>
        public void ToggleFavorite(UnDoneItemVM vm)
        {
            vm.IsFavorited = !vm.IsFavorited;
            PersistFavorite(vm);
        }

        private void PersistFavorite(UnDoneItemVM vm)
        {
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) => target.IsFavorited = vm.IsFavorited);
            ApplySort(_currentSort);
        }

        /// <summary>子任务区块被勾选：写回内容区与完成度（概览里的进度条读的就是它）。</summary>
        private void PersistSubTaskToggle(UnDoneItemVM item)
        {
            // 勾选是详情内的连续操作：抑制内部保存引发的整页刷新，否则详情会被收起。
            Services.DataService.IsInternalSave = true;
            try
            {
                Services.DataService.SaveToEntryVersion(
                    Services.ProjectService.CurrentProjectDir!, item.Entry,
                    (data, target) =>
                    {
                        target.Contents = item.Entry.Contents;
                        target.Progress = item.Entry.Progress;
                    });
            }
            finally
            {
                Services.DataService.IsInternalSave = false;
            }
        }

        private void StarPath_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Path star) return;

            var vm = (star.DataContext as UnDoneItemVM)
                  ?? ((star.Parent as FrameworkElement)?.DataContext as UnDoneItemVM);
            if (vm == null) return;

            ApplyStarState(star, vm.IsFavorited);
        }

        private static Path? FindStarPath(DependencyObject start)
        {
            DependencyObject? current = start;
            while (current != null)
            {
                if (current is FrameworkElement fe)
                {
                    var found = fe.FindName("StarPath") as Path;
                    if (found != null) return found;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void ApplyStarState(Path star, bool favorited)
        {
            star.Data = favorited
                ? Geometry.Parse(StarFilled)
                : Geometry.Parse(StarOutline);
            star.Fill = favorited
                ? new SolidColorBrush(Color.FromRgb(0xF0, 0xC0, 0x40))
                : (Brush)Application.Current.Resources["ForegroundBrush"];
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UnDoneItemVM vm)
                RunAnimation(btn, Colors.Red, () => DeleteItem(vm));
        }

        /// <summary>删除条目（供右键菜单使用，无折叠动画）。</summary>
        public void DeleteItem(UnDoneItemVM vm)
        {
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) => data.Entries.Remove(target));
            LoadFromData();
            RebuildGroups();
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UnDoneItemVM vm)
                RunAnimation(btn, Color.FromRgb(0x4C, 0xAF, 0x50), () => CompleteItem(vm));
        }

        /// <summary>完成条目（供右键菜单使用）。</summary>
        public void CompleteItem(UnDoneItemVM vm) => RemoveItem(vm);

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UnDoneItemVM vm)
                EditItem(vm);
        }

        /// <summary>打开条目编辑窗口（供右键菜单使用）。</summary>
        public void EditItem(UnDoneItemVM vm)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.ShowEditEntryDialog(vm.Entry);
        }

        private void RemoveItem(UnDoneItemVM vm)
        {
            vm.Entry.CompletedAt = DateTime.Now;
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) =>
                {
                    target.Status = EntryStatus.Finished;
                    target.CompletedAt = DateTime.Now;
                });
            LoadFromData();
            RebuildGroups();

            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.SetTipText(Services.TipService.GetCompleteTip(vm.Entry));
                mw.AddToDoneList(vm.Entry);
            }
        }

        private void RunAnimation(Button btn, Color glowColor, Action onComplete)
        {
            var border = FindCardBorder(btn);
            if (border == null)
            {
                onComplete();
                return;
            }

            border.IsHitTestVisible = false;

            var glow = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = 0,
                ShadowDepth = 0,
                Opacity = 0,
                RenderingBias = RenderingBias.Quality
            };
            border.Effect = glow;

            var sb = new Storyboard();
            var capturedHeight = border.ActualHeight;

            var blurAnim = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(blurAnim, border);
            Storyboard.SetTargetProperty(blurAnim, new PropertyPath("(UIElement.Effect).(DropShadowEffect.BlurRadius)"));
            sb.Children.Add(blurAnim);

            var glowOpacityAnim = new DoubleAnimation(0, 0.7, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(glowOpacityAnim, border);
            Storyboard.SetTargetProperty(glowOpacityAnim, new PropertyPath("(UIElement.Effect).(DropShadowEffect.Opacity)"));
            sb.Children.Add(glowOpacityAnim);

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
            if (toggle.DataContext is not UnDoneItemVM vm) return;
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
            if ((sender as FrameworkElement)?.Tag is not UnDoneItemVM vm) return;

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
            if (_hoverTarget is UnDoneItemVM vm) vm.IsHoverPreview = false;
            _hoverTarget = null;
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();

            // 计时期间若已展开详情，则不再进入悬停预览（概览已持久显示）
            if (_hoverTarget is UnDoneItemVM vm && !vm.IsDetailExpanded) vm.IsHoverPreview = true;
        }

        private void CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not UnDoneItemVM vm) return;
            CopyItemInfo(vm);
        }

        /// <summary>复制条目信息（供右键菜单使用）。</summary>
        public void CopyItemInfo(UnDoneItemVM vm)
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
    }

    public class UnDoneVersionGroupVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string VersionName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(VersionName) ? LocalizationManager.T("未指定版本") : VersionName;
        public string DisplayCount => Items.Count.ToString();
        public ObservableCollection<UnDoneItemVM> Items { get; set; } = new();

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
    }

    public class UnDoneItemVM : INotifyPropertyChanged
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
        public string Brief => Entry.Brief;
        public IEnumerable<TypeTag> TypeTags =>
            Entry.Type.Select(t => new TypeTag(t, Services.ProjectService.GetTypeColor(t)));
        public bool HasType => Entry.Type.Count > 0;
        public List<FileRef> RelatedFiles => Entry.RelatedFiles;
        public bool HasRelatedFiles => Entry.RelatedFiles.Count > 0;

        /// <summary>
        /// 内容区（区块化正文：文本 / 表格 / 分割线 / 代码块 / 文件引用 / 多级列表 / 子任务）。
        /// 每个区块包一层 VM —— 子任务区块在详情里可点击勾选完成。
        /// </summary>
        public ObservableCollection<ContentBlockVM> Contents { get; } = new();
        public bool HasContents => Contents.Count > 0;

        /// <summary>子任务区块被勾选时触发（页面据此写回数据文件）。</summary>
        public event Action<UnDoneItemVM>? SubTaskToggled;

        /// <summary>完成度（0-100）：内容区多级列表条目 + 子任务区块的勾选比例。</summary>
        public int Progress => Entry.Progress;
        public string ProgressText => Entry.Progress.ToString() + "%";

        /// <summary>子任务勾选后刷新完成度（概览里的进度条读的就是这两个属性）。</summary>
        public void NotifyProgressChanged()
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(ProgressText));
        }

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

        /// <summary>条目所属版本（来自它所在的版本文件）。</summary>
        public string Version { get; }

        public bool IsFavorited
        {
            get => Entry.IsFavorited;
            set => Entry.IsFavorited = value;
        }

        public UnDoneItemVM(GoalEntry entry, string version)
        {
            Entry = entry;
            Version = version;

            foreach (var block in entry.Contents)
                Contents.Add(new ContentBlockVM(block, OnSubTaskToggled));
        }

        /// <summary>子任务勾选：重算完成度 → 刷新进度条 → 通知页面写盘。</summary>
        private void OnSubTaskToggled(ContentBlockVM block)
        {
            Entry.Progress = Services.ContentBlocks.ComputeProgress(Entry.Contents);
            NotifyProgressChanged();
            SubTaskToggled?.Invoke(this);
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 内容区区块的视图模型。子任务区块的勾选状态双向绑定回条目数据，
    /// 并回调所属条目刷新完成度。
    /// </summary>
    public class ContentBlockVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Action<ContentBlockVM>? _onToggled;

        public ContentBlock Model { get; }

        public ContentBlockVM(ContentBlock model, Action<ContentBlockVM>? onToggled = null)
        {
            Model = model;
            _onToggled = onToggled;
        }

        /// <summary>是否是子任务区块：详情里换成可点击的勾选框。</summary>
        public bool IsSubTask => Model.Kind == ContentBlockKind.SubTask;

        public string Text => Model.Text;

        public bool Done
        {
            get => Model.Done;
            set
            {
                if (Model.Done == value) return;
                Model.Done = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Done)));
                _onToggled?.Invoke(this);
            }
        }
    }
}
