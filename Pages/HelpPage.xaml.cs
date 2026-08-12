using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OCCMissionGoals.Pages;

public partial class HelpPage : Page
{
    private readonly Dictionary<string, FrameworkElement> _sectionMap = new();
    private readonly Dictionary<string, Button> _navMap = new();
    private string _currentTag = "Overview";
    private bool _isNavigating;

    public HelpPage()
    {
        InitializeComponent();

        _sectionMap = new Dictionary<string, FrameworkElement>
        {
            ["Overview"]     = Section_Overview,
            ["Dashboard"]    = Section_Dashboard,
            ["UnDone"]       = Section_UnDone,
            ["Done"]         = Section_Done,
            ["Expand"]       = Section_Expand,
            ["Severity"]     = Section_Severity,
            ["ChangeDemand"] = Section_ChangeDemand,
            ["Version"]      = Section_Version,
            ["Type"]         = Section_Type,
            ["RelatedFiles"] = Section_RelatedFiles,
            ["Shortcuts"]    = Section_Shortcuts,
            ["Project"]      = Section_Project,
            ["CLI"]          = Section_CLI,
        };

        _navMap = new Dictionary<string, Button>
        {
            ["Overview"]     = Nav_Overview,
            ["Dashboard"]    = Nav_Dashboard,
            ["UnDone"]       = Nav_UnDone,
            ["Done"]         = Nav_Done,
            ["Expand"]       = Nav_Expand,
            ["Severity"]     = Nav_Severity,
            ["ChangeDemand"] = Nav_ChangeDemand,
            ["Version"]      = Nav_Version,
            ["Type"]         = Nav_Type,
            ["RelatedFiles"] = Nav_RelatedFiles,
            ["Shortcuts"]    = Nav_Shortcuts,
            ["Project"]      = Nav_Project,
            ["CLI"]          = Nav_CLI,
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HighlightNav(_currentTag);
    }

    // ==================== 导航点击 → 滚动 ====================

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

    // ==================== 辅助 ====================

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

    // ==================== 滚动动画 ====================

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
}
