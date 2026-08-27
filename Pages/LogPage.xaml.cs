using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace OCCMissionGoals.Pages
{
    public partial class LogPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _completedCount;
        private int _totalCount;
        private double _progressRatio;
        private string _progressPercent = string.Empty;
        private int _maxCount;
        private bool _hasUpcomingDeadlines;
        private bool _hasRecentActivity;

        private string _projectName = string.Empty;
        private string _currentVersionDisplay = string.Empty;

        public int CompletedCount
        {
            get => _completedCount;
            set { _completedCount = value; OnPropertyChanged(); }
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
        public bool HasActivity => CompletedCount > 0;
        public int MaxCount
        {
            get => _maxCount;
            set { _maxCount = value; OnPropertyChanged(); }
        }
        public ObservableCollection<string> TickLabels { get; } = new();

        public bool HasUpcomingDeadlines
        {
            get => _hasUpcomingDeadlines;
            set { _hasUpcomingDeadlines = value; OnPropertyChanged(); }
        }
        public bool HasRecentActivity
        {
            get => _hasRecentActivity;
            set { _hasRecentActivity = value; OnPropertyChanged(); }
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

        // 新增数据集合
        public ObservableCollection<SeverityStat> SeverityStats { get; } = new();
        public ObservableCollection<DeadlineItem> UpcomingDeadlines { get; } = new();
        public ObservableCollection<ActivityItem> RecentActivities { get; } = new();

        public LogPage()
        {
            InitializeComponent();
            DataContext = this;
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
            var today = DateTime.Today;

            // ===== 贡献记录 =====
            var groups = data.Finished
                .Where(e => e.CompletedAt != default)
                .GroupBy(e => e.CompletedAt.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DateContribution
                {
                    Label = DateLabelFormatter.Format(g.Key, today),
                    Count = g.Count(),
                    RawCount = g.Count()
                })
                .ToList();

            var maxCount = groups.Count > 0 ? groups.Max(d => d.RawCount) : 0;
            MaxCount = maxCount;

            TickLabels.Clear();
            if (maxCount > 0)
                for (int s = 0; s <= 4; s++)
                    TickLabels.Add((maxCount * s / 4).ToString());
            else
                for (int s = 0; s <= 4; s++) TickLabels.Add("0");
            foreach (var d in groups)
                d.Ratio = maxCount > 0 ? (double)d.RawCount / maxCount : 0;

            MonthlyBars.ItemsSource = groups;

            CompletedCount = data.Finished.Count;
            TotalCount = data.Unfinished.Count + data.Finished.Count;
            ProgressRatio = TotalCount > 0 ? Math.Min(1.0, (double)CompletedCount / TotalCount) : 0;
            ProgressPercent = $"{ProgressRatio:P0}";
            OnPropertyChanged(nameof(HasActivity));

            // ===== 严重程度分布 =====
            var allEntries = data.Unfinished.Concat(data.Finished).ToList();
            var severityGroups = allEntries
                .GroupBy(e => e.Severity)
                .OrderBy(g => g.Key)
                .ToList();

            int maxSev = severityGroups.Count > 0 ? severityGroups.Max(g => g.Count()) : 1;
            SeverityStats.Clear();
            foreach (var g in severityGroups)
            {
                SeverityStats.Add(new SeverityStat
                {
                    Severity = g.Key,
                    Label = Models.SeverityHelper.GetText(g.Key),
                    Count = g.Count(),
                    Ratio = (double)g.Count() / maxSev,
                    ColorBrush = Models.SeverityHelper.GetBrush(g.Key)
                });
            }
            SeverityBars.ItemsSource = SeverityStats;

            // ===== 即将到期 =====
            var upcoming = data.Unfinished
                .Where(e => e.Deadline != default && e.Deadline.Date <= today.AddDays(7))
                .OrderBy(e => e.Deadline)
                .Select(e => new DeadlineItem
                {
                    Title = e.Title,
                    Severity = e.Severity,
                    SeverityBrush = Models.SeverityHelper.GetBrush(e.Severity),
                    DaysLeft = (e.Deadline.Date - today).Days,
                    IsOverdue = e.Deadline.Date < today
                })
                .ToList();

            UpcomingDeadlines.Clear();
            foreach (var d in upcoming)
                UpcomingDeadlines.Add(d);
            DeadlineList.ItemsSource = UpcomingDeadlines;
            HasUpcomingDeadlines = upcoming.Count > 0;

            // ===== 最近活动 =====
            var recent = data.Finished
                .Where(e => e.CompletedAt != default)
                .OrderByDescending(e => e.CompletedAt)
                .Take(20)
                .Select(e => new ActivityItem
                {
                    Title = e.Title,
                    Severity = e.Severity,
                    SeverityBrush = Models.SeverityHelper.GetBrush(e.Severity),
                    CompletedAt = e.CompletedAt,
                    TypeLabel = LocalizationManager.T("已完成"),
                    TypeBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                })
                .ToList();

            RecentActivities.Clear();
            foreach (var a in recent)
                RecentActivities.Add(a);
            ActivityList.ItemsSource = RecentActivities;
            HasRecentActivity = recent.Count > 0;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ======================== 数据模型 ========================

    /// <summary>严重程度分布条目的数据模型。</summary>
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

    /// <summary>即将到期条目的数据模型。</summary>
    public class DeadlineItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _title = string.Empty;
        private Models.GoalSeverity _severity;
        private Brush _severityBrush = Brushes.Gray;
        private int _daysLeft;
        private bool _isOverdue;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public Models.GoalSeverity Severity
        {
            get => _severity;
            set { _severity = value; OnPropertyChanged(); }
        }
        public Brush SeverityBrush
        {
            get => _severityBrush;
            set { _severityBrush = value; OnPropertyChanged(); }
        }
        public int DaysLeft
        {
            get => _daysLeft;
            set { _daysLeft = value; OnPropertyChanged(); }
        }
        public bool IsOverdue
        {
            get => _isOverdue;
            set { _isOverdue = value; OnPropertyChanged(); }
        }
        public string DaysLeftText => IsOverdue
            ? LocalizationManager.T("已过期")
            : LocalizationManager.T("{0}天", _daysLeft);
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>最近活动条目的数据模型。</summary>
    public class ActivityItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _title = string.Empty;
        private Models.GoalSeverity _severity;
        private Brush _severityBrush = Brushes.Gray;
        private DateTime _completedAt;
        private string _typeLabel = string.Empty;
        private string _timeAgo = string.Empty;
        private Brush _typeBrush = Brushes.Gray;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public Models.GoalSeverity Severity
        {
            get => _severity;
            set { _severity = value; OnPropertyChanged(); }
        }
        public Brush SeverityBrush
        {
            get => _severityBrush;
            set { _severityBrush = value; OnPropertyChanged(); }
        }
        public DateTime CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeAgo)); }
        }
        public string TypeLabel
        {
            get => _typeLabel;
            set { _typeLabel = value; OnPropertyChanged(); }
        }
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - _completedAt;
                if (diff.TotalMinutes < 1) return LocalizationManager.T("刚刚");
                if (diff.TotalMinutes < 60) return LocalizationManager.T("{0}分钟前", (int)diff.TotalMinutes);
                if (diff.TotalHours < 24) return LocalizationManager.T("{0}小时前", (int)diff.TotalHours);
                if (diff.TotalDays < 30) return LocalizationManager.T("{0}天前", (int)diff.TotalDays);
                if (diff.TotalDays < 365) return LocalizationManager.T("{0}个月前", (int)(diff.TotalDays / 30));
                return LocalizationManager.T("{0}年前", (int)(diff.TotalDays / 365));
            }
        }
        public Brush TypeBrush
        {
            get => _typeBrush;
            set { _typeBrush = value; OnPropertyChanged(); }
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ======================== 已有模型 ========================

    public class DateContribution : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _label = string.Empty;
        private int _count;
        private double _ratio;
        public int RawCount { get; set; }

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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class DateLabelFormatter
    {
        public static string Format(DateTime date, DateTime today)
        {
            if (LocalizationManager.Instance.IsRussian)
            {
                if (date == today) return "Сегодня";
                if (date == today.AddDays(-1)) return "Вчера";
                if (date == today.AddDays(-2)) return "Позавчера";
                if (date.Year == today.Year && date.Month == today.Month) return $"Число {date.Day}";
                if (date.Year == today.Year) return date.ToString("d MMM");
                return date.ToString("d MMM yyyy");
            }

            if (LocalizationManager.Instance.IsEnglish)
            {
                if (date == today) return "Today";
                if (date == today.AddDays(-1)) return "Yesterday";
                if (date == today.AddDays(-2)) return "2 days ago";
                if (date.Year == today.Year && date.Month == today.Month) return $"Day {date.Day}";
                if (date.Year == today.Year) return date.ToString("MMM d");
                return date.ToString("MMM d, yyyy");
            }

            if (date == today) return "今日";
            if (date == today.AddDays(-1)) return "昨日";
            if (date == today.AddDays(-2)) return "前日";
            if (date.Year == today.Year && date.Month == today.Month) return $"本月{date.Day}日";
            if (date.Year == today.Year) return $"{date.Month}月{date.Day}日";
            return $"{date.Year}年{date.Month}月{date.Day}日";
        }
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

    /// <summary>将距离时间转为自然语言（备用转换器，当前由 ActivityItem.TimeAgo 属性直接计算）。</summary>
    public class TimeAgoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dt)
                return string.Empty;
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return LocalizationManager.T("刚刚");
            if (diff.TotalMinutes < 60) return LocalizationManager.T("{0}分钟前", (int)diff.TotalMinutes);
            if (diff.TotalHours < 24) return LocalizationManager.T("{0}小时前", (int)diff.TotalHours);
            if (diff.TotalDays < 30) return LocalizationManager.T("{0}天前", (int)diff.TotalDays);
            if (diff.TotalDays < 365) return LocalizationManager.T("{0}个月前", (int)(diff.TotalDays / 30));
            return LocalizationManager.T("{0}年前", (int)(diff.TotalDays / 365));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
