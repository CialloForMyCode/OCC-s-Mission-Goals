using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 「相关文件」表 → 内容区引用位置的应用内跳转。
///
/// 内容区渲染时给每个文件引用行打了 <see cref="FrameworkElement.Tag"/>（值为 <see cref="FileRef"/>），
/// 这里按「路径 + 行 + 列 + 函数」找到对应的那一行（找不到时退化为只比路径），
/// 滚入视野后短暂高亮 —— 用户点汇总表里的一行，就能看清这份引用落在正文的哪一处。
/// </summary>
public static class FileRefJump
{
    /// <summary>高亮保持亮起的时间，之后开始淡出。</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(450);

    /// <summary>高亮淡出时长。</summary>
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(700);

    /// <summary>高亮色候选资源键，按优先级取；主题里都没有时用中性灰。</summary>
    private static readonly string[] HighlightKeys =
    {
        "SelectedBackgroundBrush",
        "PressedBackgroundBrush",
        "HoverBackgroundBrush",
    };

    /// <summary>
    /// 在 <paramref name="scope"/>（条目的详情面板）里定位引用 <paramref name="target"/> 的那一行，
    /// 滚入视野并高亮；没找到返回 false。
    /// </summary>
    public static bool TryJump(DependencyObject? scope, FileRef? target)
    {
        if (scope == null || target == null) return false;

        var row = FindRow(scope, target, exact: true) ?? FindRow(scope, target, exact: false);
        if (row == null) return false;

        row.BringIntoView();
        CenterInViewport(row);
        Flash(row);
        return true;
    }

    /// <summary>
    /// 只靠 BringIntoView 目标行会贴着视口边缘（通常是最底部），不好辨认。
    /// 这里再把它挪到视口上方约 1/3 处；已经滚到尽头时自然停在边界。
    /// </summary>
    private static void CenterInViewport(FrameworkElement row)
    {
        var scroll = FindScrollViewer(row);
        if (scroll == null || scroll.ViewportHeight <= 0) return;

        // BringIntoView 刚改过偏移，先让布局跟上再量位置
        scroll.UpdateLayout();

        var top = row.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;
        var delta = top - scroll.ViewportHeight / 3;
        if (Math.Abs(delta) < 1) return;

        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + delta);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject start)
    {
        while (start != null)
        {
            if (start is ScrollViewer viewer) return viewer;
            start = VisualTreeHelper.GetParent(start);
        }

        return null;
    }

    /// <summary>深度优先找第一个匹配的文件引用行（正文里同一引用可能出现多次，取最靠前的一处）。</summary>
    private static FrameworkElement? FindRow(DependencyObject root, FileRef target, bool exact)
    {
        if (root is FrameworkElement element && element.Tag is FileRef file && Matches(file, target, exact))
            return element;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindRow(VisualTreeHelper.GetChild(root, i), target, exact);
            if (found != null) return found;
        }

        return null;
    }

    private static bool Matches(FileRef candidate, FileRef target, bool exact)
    {
        if (!string.Equals(candidate.Path, target.Path, StringComparison.OrdinalIgnoreCase)) return false;
        if (!exact) return true;

        return candidate.Line == target.Line
            && candidate.Column == target.Column
            && string.Equals(candidate.Function, target.Function, StringComparison.Ordinal);
    }

    /// <summary>亮起一会儿再淡出，结束后清掉背景，不在界面上留下痕迹。</summary>
    private static void Flash(FrameworkElement row)
    {
        if (row is not Panel panel) return;

        var brush = new SolidColorBrush(HighlightColor());
        panel.SetValue(Panel.BackgroundProperty, brush);

        var fade = new DoubleAnimation(1, 0, FadeTime)
        {
            BeginTime = HoldTime,
            FillBehavior = FillBehavior.HoldEnd,
        };
        fade.Completed += (_, _) => panel.ClearValue(Panel.BackgroundProperty);
        brush.BeginAnimation(Brush.OpacityProperty, fade);
    }

    private static Color HighlightColor()
    {
        foreach (var key in HighlightKeys)
        {
            if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
                return brush.Color;
        }

        return Color.FromArgb(0x66, 0x80, 0x80, 0x80);
    }
}
