using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        // 开发者信息（GitHub 登录）
        private bool _isLoggedIn;
        private string _userName = string.Empty;
        private string _userLogin = string.Empty;
        private string _userBio = string.Empty;
        private string _userCompany = string.Empty;
        private string _userLocation = string.Empty;
        private ImageSource? _userAvatar;
        private Models.RepositoryInfo? _selectedRepository;
        private bool _includeAuthor = true;
        private bool _groupByDate = true;
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

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(); }
        }
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }
        public string UserLogin
        {
            get => _userLogin;
            set { _userLogin = value; OnPropertyChanged(); }
        }
        public string UserBio
        {
            get => _userBio;
            set { _userBio = value; OnPropertyChanged(); }
        }
        public string UserCompany
        {
            get => _userCompany;
            set { _userCompany = value; OnPropertyChanged(); }
        }
        public string UserLocation
        {
            get => _userLocation;
            set { _userLocation = value; OnPropertyChanged(); }
        }
        public ImageSource? UserAvatar
        {
            get => _userAvatar;
            set { _userAvatar = value; OnPropertyChanged(); }
        }
        public ObservableCollection<Models.RepositoryInfo> Repositories { get; } = new();
        public Models.RepositoryInfo? SelectedRepository
        {
            get => _selectedRepository;
            set { _selectedRepository = value; OnPropertyChanged(); }
        }
        public bool IncludeAuthor
        {
            get => _includeAuthor;
            set { _includeAuthor = value; OnPropertyChanged(); }
        }
        public bool GroupByDate
        {
            get => _groupByDate;
            set { _groupByDate = value; OnPropertyChanged(); }
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
            _ = RefreshGitHubUserAsync();
        }

        private void RefreshProjectInfo()
        {
            var proj = Services.ProjectService.CurrentProject;
            ProjectName = proj?.Name ?? LocalizationManager.T("未打开项目", "No project opened", "Проект не открыт");
            var ver = proj?.CurrentVersion ?? "";
            // 兼容旧格式（带 .json 后缀）
            var cleanVer = ver.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? ver[..^5]
                : ver;
            CurrentVersionDisplay = cleanVer;

            // 解析 [major.minor.patch]-[tag].[iter] 格式
            ParseVersionIntoFields(cleanVer);
        }

        private void ParseVersionIntoFields(string version)
        {
            VersionTagBox.Text = version;
            var dashIdx = version.IndexOf('-');
            string numPart, tag, tagIter;
            if (dashIdx >= 0)
            {
                numPart = version[..dashIdx];
                var tagPart = version[(dashIdx + 1)..];
                var dotIdx = tagPart.LastIndexOf('.');
                if (dotIdx >= 0)
                {
                    tag = tagPart[..dotIdx];
                    tagIter = tagPart[(dotIdx + 1)..];
                }
                else
                {
                    tag = tagPart;
                    tagIter = "0";
                }
            }
            else
            {
                numPart = version;
                tag = "alpha";
                tagIter = "0";
            }

            var nums = numPart.Split('.');
            VersionMajor.Text = nums.Length > 0 ? nums[0] : "0";
            VersionMinor.Text = nums.Length > 1 ? nums[1] : "0";
            VersionPatch.Text = nums.Length > 2 ? nums[2] : "0";
            VersionTag.Text = tag;
            VersionTagIter.Text = tagIter;
        }

        private string ComposeVersion()
        {
            var major = VersionMajor.Text.Trim();
            var minor = VersionMinor.Text.Trim();
            var patch = VersionPatch.Text.Trim();
            var tag = VersionTag.Text.Trim();
            var iter = VersionTagIter.Text.Trim();
            if (string.IsNullOrWhiteSpace(major)) major = "0";
            if (string.IsNullOrWhiteSpace(minor)) minor = "0";
            if (string.IsNullOrWhiteSpace(patch)) patch = "0";
            if (string.IsNullOrWhiteSpace(tag)) tag = "alpha";
            if (string.IsNullOrWhiteSpace(iter)) iter = "0";
            return $"{major}.{minor}.{patch}-{tag}.{iter}";
        }

        private void SaveVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var proj = Services.ProjectService.CurrentProject;
            if (proj == null) return;

            var newVersion = ComposeVersion();
            try
            {
                Services.ProjectService.UpdateVersion(newVersion);
                CurrentVersionDisplay = newVersion;
                VersionTagBox.Text = newVersion;
            }
            catch { }
        }

        private void IterateVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var proj = Services.ProjectService.CurrentProject;
            if (proj == null) return;

            var iter = VersionTagIter.Text.Trim();
            if (int.TryParse(iter, out var n))
                VersionTagIter.Text = (n + 1).ToString();
            else
                VersionTagIter.Text = "1";

            var newVersion = ComposeVersion();
            try
            {
                Services.ProjectService.UpdateVersion(newVersion);
                CurrentVersionDisplay = newVersion;
                VersionTagBox.Text = newVersion;
            }
            catch { }
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
                    TypeLabel = LocalizationManager.T("已完成", "Completed", "Завершено"),
                    TypeBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                })
                .ToList();

            RecentActivities.Clear();
            foreach (var a in recent)
                RecentActivities.Add(a);
            ActivityList.ItemsSource = RecentActivities;
            HasRecentActivity = recent.Count > 0;

            // ===== 推送仓库与发布设置 =====
            Repositories.Clear();
            foreach (var repo in Services.PushSettings.LoadRepositories())
                Repositories.Add(repo);
            SelectedRepository = Repositories.Count > 0 ? Repositories[0] : null;
            IncludeAuthor = Services.PushSettings.IncludeAuthor;
            GroupByDate = Services.PushSettings.GroupByDate;
        }

        // ======================== 开发者信息（GitHub 登录） ========================

        /// <summary>登录成功后立即用已拉取的用户信息刷新界面（无需再次请求）。</summary>
        public void ApplyGitHubUser(Services.GitHubUser user)
        {
            UserName = string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;
            UserLogin = user.Login;
            UserBio = user.Bio;
            UserCompany = user.Company;
            UserLocation = user.Location;
            IsLoggedIn = true;
            _ = LoadAvatarAsync(user.AvatarUrl);
        }

        /// <summary>异步拉取当前 GitHub 用户并刷新界面。</summary>
        private async Task RefreshGitHubUserAsync()
        {
            if (!Services.GitHubService.HasToken)
            {
                SetLoggedOut();
                return;
            }

            try
            {
                var user = await Services.GitHubService.FetchUserAsync(Services.GitHubService.Token);
                ApplyGitHubUser(user);
            }
            catch
            {
                // 令牌失效或网络异常时按未登录处理。
                SetLoggedOut();
            }
        }

        private async Task LoadAvatarAsync(string avatarUrl)
        {
            UserAvatar = null;
            if (string.IsNullOrWhiteSpace(avatarUrl)) return;

            var bytes = await Services.GitHubService.DownloadBytesAsync(avatarUrl);
            if (bytes is { Length: > 0 })
                UserAvatar = BytesToImage(bytes);
        }

        private void SetLoggedOut()
        {
            IsLoggedIn = false;
            UserName = string.Empty;
            UserLogin = string.Empty;
            UserBio = string.Empty;
            UserCompany = string.Empty;
            UserLocation = string.Empty;
            UserAvatar = null;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.ShowGitHubLoginDialog();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Services.GitHubService.Logout();
            SetLoggedOut();
            (Window.GetWindow(this) as MainWindow)?.SetTipText(
                LocalizationManager.T("已退出 GitHub 登录。", "Signed out of GitHub.", "Вы вышли из GitHub."));
        }

        // ======================== 底部推送配置入口 ========================

        private void GitHubSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var mw = Window.GetWindow(this) as MainWindow;
            if (!Services.GitHubService.HasToken)
            {
                mw?.ShowGitHubLoginDialog();
                return;
            }

            mw?.SetTipText(LocalizationManager.T(
                $"已登录 GitHub：{UserLogin}",
                $"Signed in to GitHub as {UserLogin}",
                $"Выполнен вход в GitHub как {UserLogin}"));
        }

        private void PushLocationButton_Click(object sender, RoutedEventArgs e)
            => (Window.GetWindow(this) as MainWindow)?.OpenPushSettingsPage("repos");

        private void PushFileSettingsButton_Click(object sender, RoutedEventArgs e)
            => (Window.GetWindow(this) as MainWindow)?.OpenPushSettingsPage("file");

        private void MorePushSettingsButton_Click(object sender, RoutedEventArgs e)
            => (Window.GetWindow(this) as MainWindow)?.OpenPushSettingsPage("options");

        private async void PushNowButton_Click(object sender, RoutedEventArgs e)
        {
            var mw = Window.GetWindow(this) as MainWindow;

            var proj = Services.ProjectService.CurrentProject;
            if (proj == null)
            {
                mw?.SetTipText(LocalizationManager.T(
                    "没有打开的项目，无法推送。",
                    "No project is open; nothing to push.",
                    "Нет открытого проекта, нечего отправлять."));
                return;
            }

            if (!Services.GitHubService.HasToken)
            {
                mw?.SetTipText(LocalizationManager.T(
                    "请先登录 GitHub。",
                    "Please sign in to GitHub first.",
                    "Сначала войдите в GitHub."));
                mw?.ShowGitHubLoginDialog();
                return;
            }

            if (SelectedRepository == null)
            {
                mw?.SetTipText(LocalizationManager.T(
                    "请先在设置中配置推送仓库。",
                    "Please configure a push repo in settings first.",
                    "Сначала настройте репозиторий для отправки в настройках."));
                mw?.OpenPushSettingsPage("repos");
                return;
            }

            var remotePath = Services.PushSettings.RemotePath;
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                mw?.SetTipText(LocalizationManager.T(
                    "请先在设置中选择要推送的文件。",
                    "Please select a file to push in settings first.",
                    "Сначала выберите файл для отправки в настройках."));
                mw?.OpenPushSettingsPage("file");
                return;
            }

            var binDir = Services.PushSettings.BinDirectory;
            var filePath = Path.Combine(binDir, remotePath);
            if (!File.Exists(filePath))
            {
                mw?.SetTipText(LocalizationManager.T(
                    "找不到要推送的文件。",
                    "The file to push was not found.",
                    "Файл для отправки не найден."));
                return;
            }

            var content = File.ReadAllText(filePath);
            var message = BuildCommitMessage();
            var branch = string.IsNullOrWhiteSpace(SelectedRepository.Branch)
                ? "main"
                : SelectedRepository.Branch;

            PushNowButton.IsEnabled = false;
            try
            {
                var error = await Services.GitHubService.PushFileAsync(
                    SelectedRepository.Url, branch, remotePath, content, message);

                if (error == null)
                {
                    mw?.SetTipText(LocalizationManager.T(
                        $"已推送到 {SelectedRepository.Name}（{branch}）。",
                        $"Pushed to {SelectedRepository.Name} ({branch}).",
                        $"Отправлено в {SelectedRepository.Name} ({branch})."));
                }
                else
                {
                    mw?.SetTipText(LocalizationManager.T(
                        $"推送失败：{error}",
                        $"Push failed: {error}",
                        $"Ошибка отправки: {error}"));
                }
            }
            finally
            {
                PushNowButton.IsEnabled = true;
            }
        }

        /// <summary>根据推送设置与当前数据自动生成提交信息。</summary>
        private string BuildCommitMessage()
        {
            var proj = Services.ProjectService.CurrentProject;
            var version = proj?.CurrentVersion ?? string.Empty;
            if (version.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                version = version[..^5];

            var data = Services.DataService.Current;
            var finished = data?.Finished?.Count ?? 0;
            var unfinished = data?.Unfinished?.Count ?? 0;

            var head = string.IsNullOrEmpty(version) ? "Update data" : version;
            var parts = new List<string>
            {
                $"{head}: update data ({finished} completed, {unfinished} unfinished)"
            };

            if (GroupByDate)
                parts.Add(DateTime.Now.ToString("yyyy-MM-dd"));
            if (IncludeAuthor && !string.IsNullOrWhiteSpace(UserLogin))
                parts.Add($"@{UserLogin}");

            return string.Join(" · ", parts);
        }

        private static ImageSource BytesToImage(byte[] bytes)
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
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
            ? LocalizationManager.T("已过期", "Overdue", "Просрочено")
            : LocalizationManager.T($"{_daysLeft}天", $"{_daysLeft}d", $"{_daysLeft} дн.");
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
                if (diff.TotalMinutes < 1) return LocalizationManager.T("刚刚", "just now", "только что");
                if (diff.TotalMinutes < 60) return LocalizationManager.T($"{(int)diff.TotalMinutes}分钟前", $"{(int)diff.TotalMinutes} min ago", $"{(int)diff.TotalMinutes} мин. назад");
                if (diff.TotalHours < 24) return LocalizationManager.T($"{(int)diff.TotalHours}小时前", $"{(int)diff.TotalHours} h ago", $"{(int)diff.TotalHours} ч. назад");
                if (diff.TotalDays < 30) return LocalizationManager.T($"{(int)diff.TotalDays}天前", $"{(int)diff.TotalDays} d ago", $"{(int)diff.TotalDays} дн. назад");
                if (diff.TotalDays < 365) return LocalizationManager.T($"{(int)(diff.TotalDays / 30)}个月前", $"{(int)(diff.TotalDays / 30)} mo ago", $"{(int)(diff.TotalDays / 30)} мес. назад");
                return LocalizationManager.T($"{(int)(diff.TotalDays / 365)}年前", $"{(int)(diff.TotalDays / 365)} y ago", $"{(int)(diff.TotalDays / 365)} г. назад");
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
            if (diff.TotalMinutes < 1) return LocalizationManager.T("刚刚", "just now", "только что");
            if (diff.TotalMinutes < 60) return LocalizationManager.T($"{(int)diff.TotalMinutes}分钟前", $"{(int)diff.TotalMinutes} min ago", $"{(int)diff.TotalMinutes} мин. назад");
            if (diff.TotalHours < 24) return LocalizationManager.T($"{(int)diff.TotalHours}小时前", $"{(int)diff.TotalHours} h ago", $"{(int)diff.TotalHours} ч. назад");
            if (diff.TotalDays < 30) return LocalizationManager.T($"{(int)diff.TotalDays}天前", $"{(int)diff.TotalDays} d ago", $"{(int)diff.TotalDays} дн. назад");
            if (diff.TotalDays < 365) return LocalizationManager.T($"{(int)(diff.TotalDays / 30)}个月前", $"{(int)(diff.TotalDays / 30)} mo ago", $"{(int)(diff.TotalDays / 30)} мес. назад");
            return LocalizationManager.T($"{(int)(diff.TotalDays / 365)}年前", $"{(int)(diff.TotalDays / 365)} y ago", $"{(int)(diff.TotalDays / 365)} г. назад");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
