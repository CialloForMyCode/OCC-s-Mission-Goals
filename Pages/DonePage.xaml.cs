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

        public DonePage()
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
            foreach (var entry in data.Finished)
                _items.Add(new DoneItemVM(entry));
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
            return a.Title == b.Title && a.Version == b.Version;
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
                return data != null && data.Unfinished.Count == 0 && data.Finished.Count > 0;
            }
            catch { return false; }
        }

        private void VersionHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border header || header.Tag is not DoneVersionGroupVM group)
                return;

            group.IsExpanded = !group.IsExpanded;

            // 向上遍历找到 DataTemplate 根部的 StackPanel，再找 GroupItems
            StackPanel? rootPanel = null;
            var p = header.Parent;
            while (p != null)
            {
                if (p is StackPanel sp) { rootPanel = sp; break; }
                p = VisualTreeHelper.GetParent(p);
            }
            if (rootPanel == null) return;

            foreach (var child in rootPanel.Children)
            {
                if (child is ItemsControl ic && ic.Name == "GroupItems")
                    ic.Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }

            // header 就是包含 GroupArrow 的内部 Border
            UpdateGroupArrow(header, group.IsExpanded);
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

        private static void UpdateGroupArrow(Border header, bool expanded)
        {
            // header.Child 现在是 Grid，需要递归查找 GroupArrow
            var arrow = FindNameInTree<TextBlock>(header, "GroupArrow");
            if (arrow != null)
                arrow.Text = expanded ? "▼" : "▶";
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
                RunAnimation(btn, null, () =>
                {
                    Services.DataService.SaveToEntryVersion(
                        Services.ProjectService.CurrentProjectDir!, vm.Entry,
                        (data, target) =>
                        {
                            data.Finished.Remove(target);
                            target.CompletedAt = default;
                            data.Unfinished.Insert(0, target);
                        });
                    LoadFromData();
                    RebuildGroups();

                    if (Window.GetWindow(this) is MainWindow mw)
                    {
                        mw.SetTipText(Services.TipService.GetUndoCompleteTip(vm.Entry));
                        mw.RefreshUnDoneList();
                    }
                });
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DoneItemVM vm)
            {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.ShowEditEntryDialog(vm.Entry);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DoneItemVM vm)
                RunAnimation(btn, Colors.Red, () =>
                {
                    Services.DataService.SaveToEntryVersion(
                        Services.ProjectService.CurrentProjectDir!, vm.Entry,
                        (data, target) => data.Finished.Remove(target));
                    LoadFromData();
                    RebuildGroups();
                });
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button btn || btn.Tag is not DoneVersionGroupVM group) return;
                if (group.Items.Count == 0) return;

                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var binDir = Path.Combine(exeDir, "bin");
                Directory.CreateDirectory(binDir);

                var verName = string.IsNullOrEmpty(group.VersionName) ? LocalizationManager.T("未指定版本", "No version") : group.VersionName;
                var safeName = verName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var fileName = $"archive_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                var filePath = Path.Combine(binDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine(LocalizationManager.T($"# 归档 — {verName}", $"# Archive — {verName}"));
                sb.AppendLine();
                sb.AppendLine(LocalizationManager.T($"> 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", $"> Exported at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
                sb.AppendLine(LocalizationManager.T($"> 条目数量: {group.Items.Count}", $"> Entry count: {group.Items.Count}"));
                sb.AppendLine();

                foreach (var item in group.Items)
                {
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine($"## {EscapeMd(item.Title)}");
                    sb.AppendLine();
                    sb.AppendLine(LocalizationManager.T("| 字段 | 内容 |", "| Field | Content |"));
                    sb.AppendLine($"|------|------|");
                    sb.AppendLine(LocalizationManager.T($"| 严重程度 | {SeverityHelper.GetText(item.Entry.Severity)} |", $"| Severity | {SeverityHelper.GetText(item.Entry.Severity)} |"));
                    sb.AppendLine(LocalizationManager.T($"| 详细信息 | {EscapeMd(item.Detail)} |", $"| Details | {EscapeMd(item.Detail)} |"));
                    sb.AppendLine(LocalizationManager.T($"| 截止日期 | {item.Entry.Deadline:yyyy-MM-dd} |", $"| Deadline | {item.Entry.Deadline:yyyy-MM-dd} |"));
                    sb.AppendLine(LocalizationManager.T($"| 完成时间 | {item.Entry.CompletedAt:yyyy-MM-dd} |", $"| Completed | {item.Entry.CompletedAt:yyyy-MM-dd} |"));

                    if (item.Entry.RelatedFiles.Count > 0)
                    {
                        var files = string.Join(LocalizationManager.T("、", ", "), item.Entry.RelatedFiles.Select(f =>
                        {
                            var name = Path.GetFileName(f.Path);
                            return $"{name}[{f.Line}:{f.Column}]";
                        }));
                        sb.AppendLine(LocalizationManager.T($"| 相关文件 | {EscapeMd(files)} |", $"| Related Files | {EscapeMd(files)} |"));
                    }
                    else
                    {
                        sb.AppendLine(LocalizationManager.T("| 相关文件 | (无) |", "| Related Files | (none) |"));
                    }

                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                // 安全校验：确认版本内全部条目均已完成
                if (!CanArchiveVersion(group.VersionName))
                {
                    MessageBox.Show(LocalizationManager.T("该版本中仍有未完成的条目，无法归档。", "This version still has unfinished entries and cannot be archived."), LocalizationManager.T("无法归档", "Cannot Archive"),
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
                    mw.SetTipText(LocalizationManager.T($"已归档 {group.Items.Count} 条到 bin/{fileName}。", $"Archived {group.Items.Count} entries to bin/{fileName}."));
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.T($"归档失败: {ex.Message}", $"Archive failed: {ex.Message}"), LocalizationManager.T("错误", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void AddItem(GoalEntry entry)
        {
            var vm = new DoneItemVM(entry);
            _items.Add(vm);
            RebuildGroups();
        }
    }

    public class DoneVersionGroupVM
    {
        public string VersionName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(VersionName) ? LocalizationManager.T("未指定版本", "No version") : VersionName;
        public string DisplayCount => Items.Count.ToString();
        public ObservableCollection<DoneItemVM> Items { get; set; } = new();
        public bool IsExpanded { get; set; } = true;
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
            }
        }

        public bool IsExcerptVisible => !_isDetailExpanded;
        public string DetailToggleText => _isDetailExpanded ? LocalizationManager.T("收起", "Collapse") : LocalizationManager.T("详情", "Details");

        public string Title => Entry.Title;
        public string SeverityText => SeverityHelper.GetText(Entry.Severity);
        public Brush SeverityBrush => SeverityHelper.GetBrush(Entry.Severity);
        public DateTime CompletedAt => Entry.CompletedAt;
        public string Brief => Entry.Brief;
        public string Detail => Entry.Detail;
        public string Version => Entry.Version;
        public IEnumerable<TypeTag> TypeTags =>
            Entry.Type.Select(t => new TypeTag(t, Services.ProjectService.GetTypeColor(t)));
        public bool HasType => Entry.Type.Count > 0;
        public List<FileRef> RelatedFiles => Entry.RelatedFiles;
        public bool HasRelatedFiles => Entry.RelatedFiles.Count > 0;

        public DoneItemVM(GoalEntry entry) => Entry = entry;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
