using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

        // ===== 已完成条目折线图的画布与几何参数 =====
        /// <summary>折线图覆盖的天数（含今天）。</summary>
        private const int TrendDays = 30;
        /// <summary>折线图的画布高度；宽度跟随卡片，所以横坐标要按实际宽度算。</summary>
        private const double TrendCanvasHeight = 300.0;
        /// <summary>绘图区四边留白：左侧留严重程度刻度，底部留日期刻度。</summary>
        private const double TrendPlotLeft = 66.0;
        private const double TrendPlotRight = 20.0;
        private const double TrendPlotTop = 16.0;
        private const double TrendPlotBottom = 34.0;
        /// <summary>折线图数据点的半径：同一天的条目均分后间距有限，点不宜过大。</summary>
        private const double TrendDotRadius = 4.0;
        /// <summary>
        /// 同一严重程度的数据点中心距小于此值时共用一个点（此时圆点已经挨上了），该点内改用滚轮切换。
        /// 严重程度不同的点即使挨在一起也各画各的。合并只影响「有几个点」：点的位置和大小都不变，
        /// 被并掉的那几条与它们之间的连线也不再画。
        /// </summary>
        private const double TrendClusterDistance = TrendDotRadius * 2 + 2.0;

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
        private int _trendPointCount;
        /// <summary>折线图当前绘制的数据点：窗口内的全部已完成条目，按完成时间升序。</summary>
        private List<Models.GoalEntry> _trendEntries = new();
        /// <summary>折线图横轴窗口的起点，即「最近 30 天」的第一天 00:00。</summary>
        private DateTime _trendStart;

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

        /// <summary>已完成条目数，用于卡片标题右侧的「共 N 条」计数。</summary>
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

        /// <summary>
        /// 折线图里的数据点数：最近 30 天完成的条目数。为 0 时收起图表、只留空态文案。
        /// </summary>
        public int TrendPointCount
        {
            get => _trendPointCount;
            set
            {
                if (_trendPointCount == value) return;
                _trendPointCount = value;
                OnPropertyChanged();
            }
        }

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
            FinishedEntryCount = finished.Count;   // 折线图卡片标题右侧的「共 N 条」
            TotalCount = data.Entries.Count;
            ProgressRatio = TotalCount > 0 ? Math.Min(1.0, (double)CompletedCount / TotalCount) : 0;
            ProgressPercent = $"{ProgressRatio:P0}";

            BuildSeverityDonut(unfinished);
            BuildContributionGraph(finished);
            BuildSeverityContributionGraph(finished);
            BuildSeverityTrend(finished);
        }

        // ======================== 已完成条目：最近 30 天的严重程度折线图 ========================

        /// <summary>
        /// 画「最近 30 天完成条目的严重程度」折线：横轴是最近 30 天，纵轴是五个严重等级
        /// （更新在底、致命在顶）。每个已完成条目是一个数据点，按完成时间先后连成折线；
        /// 同一天的条目会在当天的横向区间里均匀铺开，免得几个点叠在同一处。
        /// 点的填充色就是它自己的严重程度色，悬停能看到日期、等级与标题。
        /// 数据与「贡献记录」月表同源（当前项目的全部版本）。
        /// </summary>
        private void BuildSeverityTrend(List<Models.GoalEntry> finished)
        {
            _trendStart = DateTime.Today.AddDays(-(TrendDays - 1));   // 窗口第一天 00:00
            var end = _trendStart.AddDays(TrendDays);                // 窗口终点 = 今天 24:00

            _trendEntries = finished
                .Where(e => e.CompletedAt >= _trendStart && e.CompletedAt < end)
                .OrderBy(e => e.CompletedAt)
                .ToList();

            TrendPointCount = _trendEntries.Count;
            DrawSeverityTrend();
        }

        /// <summary>卡片被拉伸或压缩时按新宽度重画，坐标才不会走形。</summary>
        private void SeverityTrendCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
            => DrawSeverityTrend();

        private void DrawSeverityTrend()
        {
            CloseTrendTips();
            SeverityTrendCanvas.Children.Clear();

            // 首次布局尚未完成时 ActualWidth 还是 0，先按设计宽度画一版，等 SizeChanged 再纠正
            double canvasWidth = SeverityTrendCanvas.ActualWidth > 0 ? SeverityTrendCanvas.ActualWidth : 1080.0;
            double plotWidth = canvasWidth - TrendPlotLeft - TrendPlotRight;
            double plotHeight = TrendCanvasHeight - TrendPlotTop - TrendPlotBottom;
            double dayWidth = plotWidth / TrendDays;

            // 按天归位：同一天的条目先按完成时间排好，再各自分到当天的横向区间里
            var slots = new Dictionary<Models.GoalEntry, (int Day, int Index, int Count)>();
            foreach (var group in _trendEntries.GroupBy(e => (e.CompletedAt.Date - _trendStart).Days))
            {
                var sameDay = group.OrderBy(e => e.CompletedAt).ToList();
                for (int i = 0; i < sameDay.Count; i++)
                    slots[sameDay[i]] = (group.Key, i, sameDay.Count);
            }

            // 条目 → 横坐标：当天区间里的第 (i + 0.5) / n 处，两侧各留半格
            double X(Models.GoalEntry entry)
            {
                var slot = slots[entry];
                return TrendPlotLeft + (slot.Day + (slot.Index + 0.5) / slot.Count) * dayWidth;
            }

            // 等级 → 纵坐标：更新（最低）在底、致命（最高）在顶
            double Y(int level) => TrendPlotTop + plotHeight - (level - 1) * (plotHeight / 4.0);

            DrawTrendAxes(_trendStart, plotWidth, plotHeight, Y);

            if (_trendEntries.Count == 0) return;

            // 相邻太近的同严重程度条目共用一个点，折线也只连这些点，被并掉的那一段不画
            var clusters = ClusterTrendEntries(_trendEntries, X);
            var dots = clusters.Select(c => c[0]).ToList();

            // 折线只用一根浅白细线把点连起来，不跟各点的严重程度配色抢眼
            // 用半像素宽度：抗锯齿后就是一根发丝细线，比 1 像素更轻
            var polyline = new Polyline
            {
                StrokeThickness = 0.5,
                StrokeLineJoin = PenLineJoin.Round,
                Points = new PointCollection(dots.Select(e => new Point(X(e), Y(LevelOf(e.Severity)))))
            };
            polyline.SetResourceReference(Shape.StrokeProperty, "ForegroundBrush");
            SeverityTrendCanvas.Children.Add(polyline);

            // 每个点代表一条或多条（同严重程度、又挨在一起的）条目：悬停看明细，滚轮切换，点击跳转
            foreach (var cluster in clusters)
                SeverityTrendCanvas.Children.Add(CreateTrendCluster(cluster, X, Y));
        }

        // ======================== 已完成条目折线图：数据点的合并与点击跳转 ========================

        /// <summary>
        /// 把数据点按严重程度和横坐标归簇：和簇里第一个点严重程度相同、且挨得够近（中心距小于
        /// TrendClusterDistance）才并进同一簇。距离跟簇首比而不是跟前一个点比，免得点一串串地
        /// 被链式吸收——把离簇首已经很远的点也并进来。
        /// 条目已按完成时间升序，横坐标也基本有序，顺序扫一遍就够。
        /// </summary>
        private static List<List<Models.GoalEntry>> ClusterTrendEntries(
            List<Models.GoalEntry> entries, Func<Models.GoalEntry, double> x)
        {
            var clusters = new List<List<Models.GoalEntry>>();
            double firstX = 0.0;
            foreach (var entry in entries)
            {
                double px = x(entry);
                if (clusters.Count == 0
                    || LevelOf(entry.Severity) != LevelOf(clusters[^1][0].Severity)
                    || px - firstX >= TrendClusterDistance)
                {
                    clusters.Add(new List<Models.GoalEntry>());
                    firstX = px;
                }
                clusters[^1].Add(entry);
            }
            return clusters;
        }

        /// <summary>
        /// 画一个数据点。簇里可能只有一条（普通点），也可能有几条同严重程度、又挨在一起的（合并点）：
        /// 悬停弹出提示，滚轮在簇内切换条目——提示内容跟着换成当前那一条，
        /// 左键点一下就切到「完成的条目」页并选中它。
        /// 点本身不因合并而改变位置和大小：固定画在簇里第一条（也是最早完成的那条）的位置上，
        /// 也就是折线在该处的拐点上。
        /// </summary>
        private Ellipse CreateTrendCluster(
            List<Models.GoalEntry> entries, Func<Models.GoalEntry, double> x, Func<int, double> y)
        {
            int index = 0;
            var head = entries[0];
            double centerX = x(head);
            double centerY = y(LevelOf(head.Severity));

            var dot = new Ellipse
            {
                Width = TrendDotRadius * 2,
                Height = TrendDotRadius * 2,
                // 簇内严重程度一致，所以颜色固定，滚轮切换时不必跟着变
                Fill = Models.SeverityHelper.GetBrush(head.Severity),
                Cursor = Cursors.Hand,
                StrokeThickness = 1.5
            };
            // 描边取卡片背景色：点挨在一起时也能彼此分开
            dot.SetResourceReference(Shape.StrokeProperty, "CardBackgroundBrush");
            Canvas.SetLeft(dot, centerX - TrendDotRadius);
            Canvas.SetTop(dot, centerY - TrendDotRadius);

            // StaysOpen：滚轮切条目时提示不能自己收起来，改由鼠标进出与点击来控制开关
            var tip = new ToolTip
            {
                PlacementTarget = dot,
                Placement = PlacementMode.Top,
                StaysOpen = true
            };
            dot.ToolTip = tip;

            void Refresh() => tip.Content = TrendTipText(entries, index);

            dot.MouseEnter += (_, _) => tip.IsOpen = true;
            dot.MouseLeave += (_, _) => tip.IsOpen = false;
            dot.PreviewMouseWheel += (_, args) =>
            {
                if (entries.Count < 2) return;
                // 往上滚看前一条、往下滚看后一条，到头绕回去
                index = (index + (args.Delta > 0 ? entries.Count - 1 : 1)) % entries.Count;
                Refresh();
                args.Handled = true;      // 拦在这里，别让外层 ScrollViewer 跟着一起滚
            };
            dot.MouseLeftButtonUp += (_, args) =>
            {
                tip.IsOpen = false;
                (Window.GetWindow(this) as MainWindow)?.JumpToFinishedEntry(entries[index]);
                args.Handled = true;
            };

            Refresh();
            return dot;
        }

        /// <summary>数据点的悬停提示：完成时间 · 严重程度 · 标题；合并点再补一行切换提示。</summary>
        private static string TrendTipText(List<Models.GoalEntry> entries, int index)
        {
            var entry = entries[index];
            var text = $"{entry.CompletedAt:yyyy-MM-dd HH:mm} · {Models.SeverityHelper.GetText(entry.Severity)} · {entry.Title}";
            var jump = LocalizationManager.T("点击跳转到该条目");
            return entries.Count > 1
                ? $"{text}\n{LocalizationManager.T("滚轮切换 {0} / {1}", index + 1, entries.Count)} · {jump}"
                : $"{text}\n{jump}";
        }

        /// <summary>重绘前先关掉开着的悬停提示：它们是 StaysOpen 的，不会随点被移除而自己消失。</summary>
        private void CloseTrendTips()
        {
            foreach (var child in SeverityTrendCanvas.Children.OfType<FrameworkElement>())
                if (child.ToolTip is ToolTip tip) tip.IsOpen = false;
        }

        /// <summary>严重程度换算成纵轴档位：1 = 更新（最低）… 5 = 致命（最高）。</summary>
        private static int LevelOf(Models.GoalSeverity severity) => 5 - (int)severity;

        /// <summary>纵轴档位换算回严重程度，用来取刻度的文字。</summary>
        private static Models.GoalSeverity SeverityOf(int level) => (Models.GoalSeverity)(5 - level);

        /// <summary>
        /// 画折线图的底子：五条等级横线与左侧等级刻度、每 5 天一条竖线与底部日期刻度。
        /// </summary>
        private void DrawTrendAxes(DateTime start, double plotWidth, double plotHeight, Func<int, double> y)
        {
            double axisY = TrendPlotTop + plotHeight;

            for (int level = 5; level >= 1; level--)
            {
                double lineY = y(level);

                var grid = new Line
                {
                    X1 = TrendPlotLeft,
                    X2 = TrendPlotLeft + plotWidth,
                    Y1 = lineY,
                    Y2 = lineY,
                    StrokeThickness = 1,
                    Opacity = 0.12
                };
                grid.SetResourceReference(Shape.StrokeProperty, "ForegroundBrush");
                SeverityTrendCanvas.Children.Add(grid);

                var label = new TextBlock
                {
                    Text = Models.SeverityHelper.GetText(SeverityOf(level)),
                    FontSize = 12,
                    Width = TrendPlotLeft - 12,
                    TextAlignment = TextAlignment.Right,
                    Opacity = 0.5
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, lineY - 9);
                SeverityTrendCanvas.Children.Add(label);
            }

            // 底部时间轴
            var axis = new Line
            {
                X1 = TrendPlotLeft,
                X2 = TrendPlotLeft + plotWidth,
                Y1 = axisY,
                Y2 = axisY,
                StrokeThickness = 1,
                Opacity = 0.25
            };
            axis.SetResourceReference(Shape.StrokeProperty, "ForegroundBrush");
            SeverityTrendCanvas.Children.Add(axis);

            // 每 5 天一个刻度，末尾补上今天；今天的标签右对齐，不会探出绘图区
            var offsets = Enumerable.Range(0, TrendDays / 5)
                .Select(i => i * 5)
                .ToList();
            offsets.Add(TrendDays - 1);

            foreach (int offset in offsets)
            {
                var date = start.AddDays(offset);
                double x = TrendPlotLeft + (double)offset / TrendDays * plotWidth;
                bool isLast = offset == TrendDays - 1;

                var tick = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = TrendPlotTop,
                    Y2 = axisY,
                    StrokeThickness = 1,
                    Opacity = 0.08
                };
                tick.SetResourceReference(Shape.StrokeProperty, "ForegroundBrush");
                SeverityTrendCanvas.Children.Add(tick);

                var label = new TextBlock
                {
                    Text = date.ToString("M/d"),
                    FontSize = 12,
                    Width = 60,
                    TextAlignment = isLast ? TextAlignment.Right : TextAlignment.Center,
                    Opacity = 0.5
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
                Canvas.SetLeft(label, isLast ? x - 60 : x - 30);
                Canvas.SetTop(label, axisY + 8);
                SeverityTrendCanvas.Children.Add(label);
            }
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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ======================== 数据模型 ========================

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
