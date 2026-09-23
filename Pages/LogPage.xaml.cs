using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace OCCMissionGoals.Pages
{
    public partial class LogPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>贡献格子的总周数（含当前周）。</summary>
        private const int Weeks = 53;
        /// <summary>每个格子的步距，等于 636 / 53。</summary>
        private const double CellStep = 12.0;
        /// <summary>贡献月表最深色的上限：单日 10 条及以上都按最深着色，不会更深。</summary>
        private const double DailyContributionFullScale = 10.0;
        /// <summary>
        /// 修复严重程度月表最深色的上限：单日 5.0 分（一天 10 条全致命 = 10 × 0.5）及以上都按最深着色。
        /// 这两个值只是「最深色」的封顶线，实际分档还要结合区间内最大值，见 BuildMonthTable。
        /// </summary>
        private const double DailySeverityFullScale = 5.0;

        // ===== 严重程度环形图的画布与几何参数 =====
        /// <summary>画布宽：中轴两侧各留出「引线 + 标签列」。</summary>
        private const double ChartCanvasWidth = 380.0;
        /// <summary>画布高：够放下圆环与上下最外侧的标签。</summary>
        private const double ChartCanvasHeight = 240.0;
        /// <summary>圆环外半径。</summary>
        private const double RingOuterRadius = 76.0;
        /// <summary>圆环宽度；内半径 = 外半径 - 环宽，也就是环形图中间留白的半径。</summary>
        private const double RingWidth = 30.0;
        /// <summary>左右两列标签的宽度。</summary>
        private const double LabelWidth = 92.0;
        /// <summary>标签行高：把锚点换算成 Canvas.Top，同时作为同侧标签的最小间距。</summary>
        private const double LabelRowHeight = 17.0;
        /// <summary>引线第一段（沿扇区中线往外）的长度；这一段短，最后一截水平线才明显。</summary>
        private const double LeaderLength = 14.0;
        /// <summary>引线水平段与标签列之间的间隙。</summary>
        private const double LeaderGap = 4.0;
        /// <summary>标签列到画布边缘的留白。</summary>
        private const double LabelMargin = 8.0;

        private int _completedCount;
        private int _unfinishedCount;
        private int _totalCount;
        private double _progressRatio;
        private string _progressPercent = string.Empty;

        private string _projectName = string.Empty;
        private string _currentVersionDisplay = string.Empty;
        private int _finishedEntryCount;

        /// <summary>环形图画布尺寸，供 XAML 绑定：图表容器与每个扇环共用同一套坐标系。</summary>
        public double ChartWidth => ChartCanvasWidth;
        public double ChartHeight => ChartCanvasHeight;

        public int CompletedCount
        {
            get => _completedCount;
            set { _completedCount = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 未完成条目数，也就是环形图的数据规模：为 0 时才显示「没有动态」空态，
        /// 不能用 CompletedCount 判断——环形图画的本来就不是已完成条目。
        /// </summary>
        public int UnfinishedCount
        {
            get => _unfinishedCount;
            set
            {
                if (_unfinishedCount == value) return;
                _unfinishedCount = value;
                OnPropertyChanged();
            }
        }
        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); }
        }
        public double ProgressRatio
        {
            get => _progressRatio;
            set { _progressRatio = value; OnPropertyChanged(); }
        }
        public string ProgressPercent
        {
            get => _progressPercent;
            set { _progressPercent = value; OnPropertyChanged(); }
        }

        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }
        public string CurrentVersionDisplay
        {
            get => _currentVersionDisplay;
            set { _currentVersionDisplay = value; OnPropertyChanged(); }
        }

        /// <summary>已完成条目数，用于列表标题右侧计数与空态显示。</summary>
        public int FinishedEntryCount
        {
            get => _finishedEntryCount;
            set
            {
                if (_finishedEntryCount == value) return;
                _finishedEntryCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinishedCountText));
            }
        }

        /// <summary>「共 N 条」计数文案。</summary>
        public string FinishedCountText => LocalizationManager.T("共 {0} 条", FinishedEntryCount);

        // ===== 图表数据 =====
        public ObservableCollection<SeveritySlice> SeveritySlices { get; } = new();
        public ObservableCollection<ContributionDay> ContributionDays { get; } = new();
        public ObservableCollection<ChartMarker> MonthMarkers { get; } = new();
        public ObservableCollection<ChartMarker> WeekdayMarkers { get; } = new();

        /// <summary>月度修复严重程度表的格子：与贡献月表同构，深浅由当日修复权重之和决定。</summary>
        public ObservableCollection<ContributionDay> SeverityDays { get; } = new();
        public ObservableCollection<ChartMarker> SeverityMonthMarkers { get; } = new();
        public ObservableCollection<ChartMarker> SeverityWeekdayMarkers { get; } = new();

        /// <summary>两张月表（贡献数 / 修复严重程度）各自的绑定宿主，建表逻辑共用。</summary>
        private MonthTableBinding _contributionTable = null!;
        private MonthTableBinding _severityTable = null!;

        /// <summary>「已完成条目」列表的数据源，按完成时间倒序。</summary>
        public ObservableCollection<FinishedEntryItem> FinishedEntries { get; } = new();

        public LogPage()
        {
            InitializeComponent();
            DataContext = this;
            _contributionTable = new MonthTableBinding
            {
                Cells = ContributionDays,
                Grid = ContributionGrid,
                MonthMarkers = MonthMarkers,
                MonthHost = MonthLabels,
                WeekdayMarkers = WeekdayMarkers,
                WeekdayHost = WeekdayLabels
            };
            _severityTable = new MonthTableBinding
            {
                Cells = SeverityDays,
                Grid = SeverityGrid,
                MonthMarkers = SeverityMonthMarkers,
                MonthHost = SeverityMonthLabels,
                WeekdayMarkers = SeverityWeekdayMarkers,
                WeekdayHost = SeverityWeekdayLabels
            };
            Loaded += (_, _) => RefreshStats();
        }

        public void RefreshStats()
        {
            RefreshProjectInfo();
            LoadData();
        }

        private void RefreshProjectInfo()
        {
            var proj = Services.ProjectService.CurrentProject;
            ProjectName = proj?.Name ?? LocalizationManager.T("未打开项目");
            var ver = proj?.CurrentVersion ?? "";
            // 兼容旧格式（带 .json 后缀）
            var cleanVer = ver.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? ver[..^5]
                : ver;
            CurrentVersionDisplay = cleanVer;
        }

        private void LoadData()
        {
            var projectDir = Services.ProjectService.CurrentProjectDir;
            if (string.IsNullOrEmpty(projectDir)) return;

            var data = Services.DataService.ReadAllVersions(projectDir);
            var finished = data.Finished
                .Where(e => e.CompletedAt != default)
                .ToList();

            // 环形图看的是「还没做完」的条目：按严重程度反映剩余工作量的分布
            var unfinished = data.Unfinished.ToList();
            UnfinishedCount = unfinished.Count;

            CompletedCount = finished.Count;
            TotalCount = data.Entries.Count;
            ProgressRatio = TotalCount > 0 ? Math.Min(1.0, (double)CompletedCount / TotalCount) : 0;
            ProgressPercent = $"{ProgressRatio:P0}";

            BuildSeverityDonut(unfinished);
            BuildContributionGraph(finished);
            BuildSeverityContributionGraph(finished);
            BuildFinishedEntryList(finished);
        }

        // ======================== 已完成条目列表 ========================

        /// <summary>
        /// 填充「已完成条目」列表：每行是严重程度 + 标题 + 相对完成时间，最近完成的排在最前。
        /// 数据与「贡献记录」同源（当前项目的全部版本）。
        /// </summary>
        private void BuildFinishedEntryList(List<Models.GoalEntry> finished)
        {
            FinishedEntries.Clear();

            // 同一次刷新内共用一个「现在」，避免逐行计算时出现秒级偏差
            var now = DateTime.Now;
            foreach (var e in finished.OrderByDescending(e => e.CompletedAt))
            {
                FinishedEntries.Add(new FinishedEntryItem
                {
                    Entry = e,
                    Title = e.Title,
                    SeverityText = Models.SeverityHelper.GetText(e.Severity),
                    SeverityBrush = Models.SeverityHelper.GetBrush(e.Severity),
                    TimeAgo = Models.RelativeTime.Format(e.CompletedAt, now)
                });
            }

            FinishedEntryCount = FinishedEntries.Count;
        }

        // ======================== 严重程度环形图 ========================

        private void BuildSeverityDonut(List<Models.GoalEntry> unfinished)
        {
            SeveritySlices.Clear();

            var groups = unfinished
                .GroupBy(e => e.Severity)
                .OrderBy(g => (int)g.Key)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToList();

            int total = groups.Sum(g => g.Count);
            if (total <= 0)
            {
                SeverityPie.ItemsSource = SeveritySlices;
                return;
            }

            double cx = ChartCanvasWidth / 2.0;
            double cy = ChartCanvasHeight / 2.0;
            // 相邻扇环各向外多扫 0.3°，用来盖住 WPF 在相邻几何拼缝处留下的抗锯齿细线
            const double seamOverlapDeg = 0.6;

            // 先把扇环与标签锚点都算出来，再统一排布，避免同侧标签互相压住
            var slices = new List<SeveritySlice>(groups.Count);
            var anchors = new List<double>(groups.Count);
            var sides = new List<bool>(groups.Count);
            var midAngles = new List<double>(groups.Count);

            double startAngle = 0.0;
            foreach (var g in groups)
            {
                double sweep = (double)g.Count / total * 360.0;
                // 只有一个分类时从 3 点方向引出，否则引线要绕到圆环正下方
                double midAngle = groups.Count == 1 ? 90.0 : startAngle + sweep / 2.0;
                string label = Models.SeverityHelper.GetText(g.Severity);
                string percent = $"{(double)g.Count / total:P0}";

                slices.Add(new SeveritySlice
                {
                    Label = label,
                    Count = g.Count,
                    PercentText = percent,
                    LabelText = $"{label} {percent}",
                    TooltipText = LocalizationManager.T("{0} · {1} 个条目", label, g.Count),
                    Brush = Models.SeverityHelper.GetBrush(g.Severity),
                    Geometry = BuildRingSliceGeometry(cx, cy, startAngle - seamOverlapDeg / 2.0, sweep + seamOverlapDeg)
                });

                // 锚点沿扇区中线投到环外，位置正好对引线的折点：正上方的分类落到画布顶部，正右方的落到中线上
                anchors.Add(cy - Math.Cos(Rad(midAngle)) * (RingOuterRadius + LeaderLength));
                sides.Add(Math.Sin(Rad(midAngle)) >= 0.0);
                midAngles.Add(midAngle);

                startAngle += sweep;
            }

            LayoutLabels(slices, anchors, sides, midAngles, cy);

            foreach (var slice in slices)
                SeveritySlices.Add(slice);
            SeverityPie.ItemsSource = SeveritySlices;
        }

        /// <summary>
        /// 摆放圆环外侧的标签与引线：同侧标签自上而下依次排开（位置不够就往下推），
        /// 引线从扇环外缘沿中线引出，折一次后水平指向标签列。
        /// </summary>
        private static void LayoutLabels(List<SeveritySlice> slices, List<double> anchors,
                                         List<bool> sides, List<double> midAngles, double centerY)
        {
            var labelY = new double[slices.Count];

            foreach (bool right in new[] { true, false })
            {
                double previous = double.NegativeInfinity;
                foreach (int i in Enumerable.Range(0, slices.Count)
                                            .Where(i => sides[i] == right)
                                            .OrderBy(i => anchors[i]))
                {
                    double y = Math.Max(anchors[i], previous + LabelRowHeight + 1.0);
                    y = Math.Min(y, ChartCanvasHeight - LabelRowHeight / 2.0 - 2.0);
                    labelY[i] = Math.Max(y, LabelRowHeight / 2.0 + 2.0);
                    previous = labelY[i];
                }
            }

            double cx = ChartCanvasWidth / 2.0;
            double rightColumnX = ChartCanvasWidth - LabelWidth - LabelMargin;
            double leftColumnX = LabelMargin;

            for (int i = 0; i < slices.Count; i++)
            {
                bool right = sides[i];
                double columnX = right ? rightColumnX : leftColumnX;
                // 引线末端的水平落点：右列取列左缘，左列取列右缘
                double edgeX = right ? columnX - LeaderGap : columnX + LabelWidth + LeaderGap;

                Point start = PolarPoint(cx, centerY, RingOuterRadius + 2.0, midAngles[i]);

                // 折点只沿中线往外走 LeaderLength，所以斜段始终是短短一截；
                // 折点高度改成标签所在行，末段才是一条水平线（推挤过标签时斜段会略微变形，但仍很短）
                double bendX = PolarPoint(cx, centerY, RingOuterRadius + LeaderLength, midAngles[i]).X;
                bendX = right ? Math.Min(bendX, edgeX) : Math.Max(bendX, edgeX);

                var leader = new PointCollection
                {
                    start,
                    new Point(bendX, labelY[i]),
                    new Point(edgeX, labelY[i])
                };
                leader.Freeze();

                SeveritySlice slice = slices[i];
                slice.LeaderPoints = leader;
                slice.LabelLeft = columnX;
                slice.LabelWidth = LabelWidth;
                slice.LabelTop = labelY[i] - LabelRowHeight / 2.0;
                slice.LabelAlignment = right ? TextAlignment.Left : TextAlignment.Right;
            }
        }

        /// <summary>
        /// 构造一个扇环（环形图分块）几何体：外弧顺时针、内弧逆时针绕回起点，中间自然留白。
        /// 角度以 12 点方向为 0，顺时针递增。
        /// </summary>
        private static Geometry BuildRingSliceGeometry(double cx, double cy, double startAngle, double sweepAngle)
        {
            double innerRadius = RingOuterRadius - RingWidth;

            // 整圆（只有一个分类）：用 EvenOdd 的内圆把外圆中间挖空
            if (sweepAngle >= 359.999)
            {
                var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
                ring.Children.Add(new EllipseGeometry(new Point(cx, cy), RingOuterRadius, RingOuterRadius));
                ring.Children.Add(new EllipseGeometry(new Point(cx, cy), innerRadius, innerRadius));
                ring.Freeze();
                return ring;
            }

            double endAngle = startAngle + sweepAngle;
            bool largeArc = sweepAngle > 180.0;

            var figure = new PathFigure
            {
                StartPoint = PolarPoint(cx, cy, innerRadius, startAngle),
                IsClosed = true,
                IsFilled = true
            };
            figure.Segments.Add(new LineSegment(PolarPoint(cx, cy, RingOuterRadius, startAngle), false));
            figure.Segments.Add(new ArcSegment(
                PolarPoint(cx, cy, RingOuterRadius, endAngle),
                new Size(RingOuterRadius, RingOuterRadius),
                0.0,
                largeArc,
                SweepDirection.Clockwise,
                false));
            figure.Segments.Add(new LineSegment(PolarPoint(cx, cy, innerRadius, endAngle), false));
            figure.Segments.Add(new ArcSegment(
                PolarPoint(cx, cy, innerRadius, startAngle),
                new Size(innerRadius, innerRadius),
                0.0,
                largeArc,
                SweepDirection.Counterclockwise,
                false));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        /// <summary>极坐标取点：角度以 12 点方向为 0，顺时针递增。</summary>
        private static Point PolarPoint(double cx, double cy, double radius, double angleDeg)
        {
            double rad = Rad(angleDeg) - Math.PI / 2.0;
            return new Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
        }

        private static double Rad(double deg) => deg * Math.PI / 180.0;

        // ======================== GitHub 同款贡献月表 ========================

        private void BuildContributionGraph(List<Models.GoalEntry> finished)
            => BuildMonthTable(finished, _ => 1.0, "{0} · {1} 个贡献", "0",
                DailyContributionFullScale, _contributionTable);

        /// <summary>
        /// 构建「月度修复严重程度」月表：格子的值 = 当日修复问题的严重程度权重之和
        /// （更新 0.1 / 补丁 0.2 / 一般 0.3 / 严重 0.4 / 致命 0.5），权重越高格子越深。
        /// </summary>
        private void BuildSeverityContributionGraph(List<Models.GoalEntry> finished)
            => BuildMonthTable(finished, e => Models.SeverityHelper.GetRepairWeight(e.Severity),
                "{0} · 修复分 {1}", "0.0", DailySeverityFullScale, _severityTable);

        /// <summary>
        /// 建一张 GitHub 同款月表：53 周 × 7 天，某天的值由 <paramref name="valueOfDay"/> 汇总得到。
        /// 颜色深浅按该值占「区间内最大值」的比例分档，但分母不超过 <paramref name="fullScale"/>：
        /// 数值小的时候靠区间最大值拉开对比，达到/超过上限则封顶为最深色、不会更深。
        /// 两张月表共用同一套分档与刻度，外观才一致。
        /// </summary>
        private void BuildMonthTable(
            List<Models.GoalEntry> finished,
            Func<Models.GoalEntry, double> valueOfDay,
            string tooltipFormat,
            string valueFormat,
            double fullScale,
            MonthTableBinding table)
        {
            var today = DateTime.Today;

            // 起点 = 52 周前的周日，终点 = 本周周六
            int dayOfWeek = (int)today.DayOfWeek;          // Sunday = 0
            var thisWeekStart = today.AddDays(-dayOfWeek);
            var start = thisWeekStart.AddDays(-7 * (Weeks - 1));

            // 只统计落在表内的日期：区间外的高值会把整张表压成浅色
            var byDate = finished
                .Where(e => e.CompletedAt.Date >= start && e.CompletedAt.Date <= today)
                .GroupBy(e => e.CompletedAt.Date)
                .ToDictionary(g => g.Key, g => g.Sum(valueOfDay));

            // 分档分母 = min(区间内最大值, 最深色上限)
            double maxValue = byDate.Count > 0 ? byDate.Values.Max() : 0.0;
            double scale = Math.Min(maxValue, fullScale);

            // UniformGrid 按行优先填充，故索引 = 星期 * 周数 + 周
            var cells = new List<ContributionDay>(Weeks * 7);
            for (int day = 0; day < 7; day++)
            {
                for (int week = 0; week < Weeks; week++)
                {
                    var date = start.AddDays(week * 7 + day);
                    bool future = date > today;
                    double value = byDate.TryGetValue(date, out var v) ? v : 0.0;

                    cells.Add(new ContributionDay
                    {
                        Date = date,
                        Score = value,
                        Opacity = future ? 0.0 : LevelOpacity(value, scale),
                        EmptyOpacity = (!future && value <= 0.0) ? 1.0 : 0.0,
                        Tooltip = LocalizationManager.T(tooltipFormat, date.ToString("yyyy-MM-dd"), value.ToString(valueFormat))
                    });
                }
            }

            table.Cells.Clear();
            foreach (var day in cells)
                table.Cells.Add(day);
            table.Grid.ItemsSource = table.Cells;

            BuildMonthMarkers(start, table.MonthMarkers, table.MonthHost);
            BuildWeekdayMarkers(table.WeekdayMarkers, table.WeekdayHost);
        }

        /// <summary>把当日数值按分档分母映射到 0.28 / 0.48 / 0.72 / 1.0 四档透明度；无值为 0（只显示空档底衬）。</summary>
        private static double LevelOpacity(double value, double scale)
        {
            if (value <= 0.0 || scale <= 0.0) return 0.0;
            double ratio = value / scale;
            if (ratio <= 0.25) return 0.28;
            if (ratio <= 0.50) return 0.48;
            if (ratio <= 0.75) return 0.72;
            return 1.0;
        }

        private static void BuildMonthMarkers(DateTime start, ObservableCollection<ChartMarker> markers, ItemsControl host)
        {
            markers.Clear();

            int lastColumn = -99;
            var seenMonths = new HashSet<int>();
            for (int week = 0; week < Weeks; week++)
            {
                var firstDayOfWeek = start.AddDays(week * 7);
                if (!seenMonths.Add(firstDayOfWeek.Month)) continue;
                if (week - lastColumn < 3) continue;   // 太挤就跳过，避免标签重叠

                markers.Add(new ChartMarker
                {
                    Name = LocalizationManager.T(firstDayOfWeek.Month + "月"),
                    Margin = new Thickness(week * CellStep, 0, 0, 0)
                });
                lastColumn = week;
            }

            host.ItemsSource = markers;
        }

        private static void BuildWeekdayMarkers(ObservableCollection<ChartMarker> markers, ItemsControl host)
        {
            markers.Clear();
            markers.Add(new ChartMarker
            {
                Name = LocalizationManager.T("周一"),
                Margin = new Thickness(0, 1 * CellStep, 0, 0)
            });
            markers.Add(new ChartMarker
            {
                Name = LocalizationManager.T("周三"),
                Margin = new Thickness(0, 3 * CellStep, 0, 0)
            });
            markers.Add(new ChartMarker
            {
                Name = LocalizationManager.T("周五"),
                Margin = new Thickness(0, 5 * CellStep, 0, 0)
            });

            host.ItemsSource = markers;
        }

        // ======================== 已完成条目：点击跳转 ========================

        /// <summary>点击某一行 → 切到「完成的条目」页并滚动高亮该条目。</summary>
        private void FinishedEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: Models.GoalEntry entry } &&
                Window.GetWindow(this) is MainWindow win)
            {
                win.JumpToFinishedEntry(entry);
            }
        }

        /// <summary>
        /// 列表滚到两端后把滚轮交还给外层页面，避免内层 ScrollViewer 把整页滚动吃掉。
        /// </summary>
        private void FinishedList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer inner) return;

            bool atTop = inner.VerticalOffset <= 0.0;
            bool atBottom = inner.VerticalOffset >= inner.ScrollableHeight - 0.5;
            if ((e.Delta > 0 && atTop) || (e.Delta < 0 && atBottom))
            {
                e.Handled = true;
                var outer = FindAncestorScrollViewer(inner);
                outer?.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta / 3.0);
            }
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject start)
        {
            for (var node = VisualTreeHelper.GetParent(start); node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is ScrollViewer sv) return sv;
            }
            return null;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ======================== 数据模型 ========================

    /// <summary>「已完成条目」列表里的一行：严重程度 + 标题 + 相对完成时间。</summary>
    public class FinishedEntryItem
    {
        /// <summary>对应条目，点击整行时按它跳转（Tag 传回 code-behind）。</summary>
        public Models.GoalEntry Entry { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string SeverityText { get; set; } = string.Empty;
        public Brush SeverityBrush { get; set; } = Brushes.Gray;

        /// <summary>相对完成时间文案，例如「3分钟前」「2小时前」「1个星期前」。</summary>
        public string TimeAgo { get; set; } = string.Empty;
    }

    /// <summary>环形图分块的数据模型：扇环几何 + 外侧标签 + 引线。</summary>
    public class SeveritySlice
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public string PercentText { get; set; } = string.Empty;
        public Brush Brush { get; set; } = Brushes.Gray;
        public Geometry Geometry { get; set; } = Geometry.Empty;

        /// <summary>标签文字，格式为「分类名称 百分比」。</summary>
        public string LabelText { get; set; } = string.Empty;

        /// <summary>标签的悬停提示（分类、数量）。</summary>
        public string TooltipText { get; set; } = string.Empty;

        /// <summary>引线折线：扇环外缘 → 折点 → 标签列。</summary>
        public PointCollection LeaderPoints { get; set; } = new();

        /// <summary>标签左上角坐标与宽度，由 code-behind 在共用坐标系里算好。</summary>
        public double LabelLeft { get; set; }
        public double LabelTop { get; set; }
        public double LabelWidth { get; set; }

        /// <summary>标签对齐方式：右侧标签左对齐、左侧标签右对齐，都以引线那一端为基准。</summary>
        public TextAlignment LabelAlignment { get; set; }
    }

    /// <summary>月表中单个格子的数据模型（贡献月表与修复严重程度月表共用）。</summary>
    public class ContributionDay
    {
        public DateTime Date { get; set; }

        /// <summary>当日数值：贡献月表是条目数，修复严重程度月表是权重之和。</summary>
        public double Score { get; set; }

        public double Opacity { get; set; }

        /// <summary>空档底衬的不透明度：无贡献且非未来日期时为 1，其余为 0。</summary>
        public double EmptyOpacity { get; set; }

        public string Tooltip { get; set; } = string.Empty;
    }

    /// <summary>图表刻度标签（月份 / 星期）的数据模型，Margin 决定它在坐标系里的落点。</summary>
    public class ChartMarker
    {
        public string Name { get; set; } = string.Empty;
        public Thickness Margin { get; set; }
    }

    /// <summary>
    /// 一张月表要用到的绑定宿主：格子集合与格子网格，月份 / 星期刻度各自的集合与标签宿主。
    /// 两张月表（贡献数、修复严重程度）结构相同，建表逻辑靠它复用。
    /// </summary>
    public class MonthTableBinding
    {
        public ObservableCollection<ContributionDay> Cells { get; init; } = new();
        public ItemsControl Grid { get; init; } = null!;
        public ObservableCollection<ChartMarker> MonthMarkers { get; init; } = new();
        public ItemsControl MonthHost { get; init; } = null!;
        public ObservableCollection<ChartMarker> WeekdayMarkers { get; init; } = new();
        public ItemsControl WeekdayHost { get; init; } = null!;
    }

    /// <summary>严重程度分布的横条数据模型（由 SettingsPage 使用）。</summary>
    public class SeverityStat : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _label = string.Empty;
        private int _count;
        private double _ratio;
        private Brush _colorBrush = Brushes.Gray;
        public Models.GoalSeverity Severity { get; set; }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }
        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }
        public double Ratio
        {
            get => _ratio;
            set { _ratio = value; OnPropertyChanged(); }
        }
        public Brush ColorBrush
        {
            get => _colorBrush;
            set { _colorBrush = value; OnPropertyChanged(); }
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not double ratio || values[1] is not double total)
                return 0.0;
            return Math.Max(0, Math.Min(1.0, ratio) * total);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
