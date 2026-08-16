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

        private const string StarFilled =
            "M15.022 7.25497L12.203 10.003L12.869 13.883C12.917 14.165 12.844 14.438 12.664 14.654C12.479 14.872 12.205 15.001 11.929 15.001C11.775 15.001 11.626 14.963 11.485 14.89L8.00101 13.057L4.51701 14.889C4.13401 15.093 3.62401 14.991 3.34001 14.657C3.15801 14.439 3.08501 14.165 3.13201 13.884L3.79801 10.004L0.979007 7.25597C0.714007 6.99797 0.624007 6.63297 0.737007 6.27997C0.853007 5.92497 1.14001 5.68197 1.50701 5.62797L5.40301 5.06197L7.14501 1.53197C7.47301 0.865971 8.52801 0.865971 8.85601 1.53197L10.598 5.06197L14.494 5.62797C14.862 5.68197 15.149 5.92397 15.264 6.27597C15.378 6.63197 15.286 6.99697 15.022 7.25497Z";
        private const string StarOutline =
            "M11.928 15C11.774 15 11.625 14.962 11.484 14.889L8 13.056L4.516 14.888C4.132 15.092 3.623 14.99 3.339 14.656C3.157 14.438 3.084 14.164 3.131 13.883L3.797 10.003L0.978 7.25499C0.713 6.99699 0.623 6.63199 0.736 6.27899C0.852 5.92399 1.139 5.68099 1.506 5.62699L5.402 5.06099L7.144 1.53099C7.472 0.864994 8.527 0.864994 8.855 1.53099L10.597 5.06099L14.493 5.62699C14.861 5.68099 15.148 5.92299 15.263 6.27499C15.377 6.63099 15.286 6.99599 15.022 7.25399L12.203 10.002L12.869 13.882C12.917 14.164 12.844 14.437 12.664 14.653C12.479 14.871 12.204 15 11.928 15ZM7.959 1.97399L6.066 5.97499L1.65 6.61599L4.871 9.65299L4.117 14.05L8 11.925L11.892 13.972L11.129 9.65299L14.324 6.53799L9.934 5.97499L7.959 1.97399Z";

        public UnDonePage()
        {
            InitializeComponent();
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
            var data = Services.DataService.ReadAllVersions(Services.ProjectService.CurrentProjectDir!);
            foreach (var entry in data.Unfinished)
                _items.Add(new UnDoneItemVM(entry));
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
                SortMode.DeadlineAsc  => query.OrderBy(i => i.Entry.Deadline),
                SortMode.DeadlineDesc => query.OrderByDescending(i => i.Entry.Deadline),
                SortMode.VersionAsc   => query.OrderBy(i => i.Entry.Version),
                SortMode.VersionDesc  => query.OrderByDescending(i => i.Entry.Version),
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
            return a.Title == b.Title && a.Version == b.Version;
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
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
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

            group.IsExpanded = !group.IsExpanded;

            if (header.Parent is not StackPanel parentPanel) return;

            foreach (var child in parentPanel.Children)
            {
                if (child is ItemsControl ic && ic.Name == "GroupItems")
                    ic.Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdateGroupArrow(header, group.IsExpanded);
        }

        private static void UpdateGroupArrow(Border header, bool expanded)
        {
            if (header.Child is not StackPanel sp) return;
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb && tb.Name == "GroupArrow")
                {
                    tb.Text = expanded ? "▼" : "▶";
                    return;
                }
            }
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not UnDoneItemVM vm) return;

            Path? star = FindStarPath(btn);
            if (star == null) return;

            vm.IsFavorited = !vm.IsFavorited;
            ApplyStarState(star, vm.IsFavorited);
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) => target.IsFavorited = vm.IsFavorited);
            ApplySort(_currentSort);
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
                RunAnimation(btn, Colors.Red, () =>
                {
                    Services.DataService.SaveToEntryVersion(
                        Services.ProjectService.CurrentProjectDir!, vm.Entry,
                        (data, target) => data.Unfinished.Remove(target));
                    LoadFromData();
                    RebuildGroups();
                });
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UnDoneItemVM vm)
                RunAnimation(btn, Color.FromRgb(0x4C, 0xAF, 0x50), () => RemoveItem(vm));
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UnDoneItemVM vm)
            {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.ShowEditEntryDialog(vm.Entry);
            }
        }

        private void RemoveItem(UnDoneItemVM vm)
        {
            vm.Entry.CompletedAt = DateTime.Now;
            Services.DataService.SaveToEntryVersion(
                Services.ProjectService.CurrentProjectDir!, vm.Entry,
                (data, target) =>
                {
                    data.Unfinished.Remove(target);
                    target.CompletedAt = DateTime.Now;
                    data.Finished.Add(target);
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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class UnDoneVersionGroupVM
    {
        public string VersionName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(VersionName) ? LocalizationManager.T("未指定版本") : VersionName;
        public string DisplayCount => Items.Count.ToString();
        public ObservableCollection<UnDoneItemVM> Items { get; set; } = new();
        public bool IsExpanded { get; set; } = true;
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
            }
        }

        public bool IsExcerptVisible => !_isDetailExpanded;
        public string DetailToggleText => _isDetailExpanded ? LocalizationManager.T("收起") : LocalizationManager.T("详情");

        public string Title => Entry.Title;
        public string SeverityText => SeverityHelper.GetText(Entry.Severity);
        public Brush SeverityBrush => SeverityHelper.GetBrush(Entry.Severity);
        public DateTime Deadline => Entry.Deadline;
        public string Brief => Entry.Brief;
        public string Detail => Entry.Detail;
        public string Version => Entry.Version;
        public IEnumerable<TypeTag> TypeTags =>
            Entry.Type.Select(t => new TypeTag(t, Services.ProjectService.GetTypeColor(t)));
        public bool HasType => Entry.Type.Count > 0;
        public List<FileRef> RelatedFiles => Entry.RelatedFiles;
        public bool HasRelatedFiles => Entry.RelatedFiles.Count > 0;
        public bool IsFavorited
        {
            get => Entry.IsFavorited;
            set => Entry.IsFavorited = value;
        }

        public UnDoneItemVM(GoalEntry entry) => Entry = entry;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
