using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OCCMissionGoals.Controls;

/// <summary>
/// 进度条填充宽度的平滑过渡：Value 变化时把"目标比例"用 DoubleAnimation 过渡到 AnimatedRatio。
/// 动画只作用在附加属性上、不碰绑定的 Value，因此不会污染数据，也不影响其它读 Value 的绑定。
/// </summary>
public static class ProgressBarAnimation
{
    /// <summary>动画驱动的进度比例（0..1）。进度条模板按它 × 轨道实际宽度算出填充宽度。</summary>
    public static readonly DependencyProperty AnimatedRatioProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedRatio",
            typeof(double),
            typeof(ProgressBarAnimation),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static double GetAnimatedRatio(DependencyObject element) => (double)element.GetValue(AnimatedRatioProperty);

    public static void SetAnimatedRatio(DependencyObject element, double value) => element.SetValue(AnimatedRatioProperty, value);

    /// <summary>过渡时长，默认 420ms。</summary>
    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.RegisterAttached(
            "AnimationDuration",
            typeof(Duration),
            typeof(ProgressBarAnimation),
            new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(420))));

    public static Duration GetAnimationDuration(DependencyObject element) => (Duration)element.GetValue(AnimationDurationProperty);

    public static void SetAnimationDuration(DependencyObject element, Duration value) => element.SetValue(AnimationDurationProperty, value);

    /// <summary>置为 true 后，该进度条随 Value 变化平滑过渡（增大与减小都带动画）。</summary>
    public static readonly DependencyProperty IsAnimatedProperty =
        DependencyProperty.RegisterAttached(
            "IsAnimated",
            typeof(bool),
            typeof(ProgressBarAnimation),
            new PropertyMetadata(false, OnIsAnimatedChanged));

    public static bool GetIsAnimated(DependencyObject element) => (bool)element.GetValue(IsAnimatedProperty);

    public static void SetIsAnimated(DependencyObject element, bool value) => element.SetValue(IsAnimatedProperty, value);

    private static void OnIsAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProgressBar bar) return;

        bar.ValueChanged -= OnValueChanged;
        bar.BeginAnimation(AnimatedRatioProperty, null);

        // 挂接（或解除）时先按当前 Value 落位，不播放动画：
        // 挂接时 Value 常还没被绑定赋值，直接动画会多播一次"从 0 长出来"的入场效果。
        SetAnimatedRatio(bar, GetRatio(bar));

        if (e.NewValue is true) bar.ValueChanged += OnValueChanged;
    }

    private static void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not ProgressBar bar) return;

        var target = GetRatio(bar);
        if (Math.Abs(GetAnimatedRatio(bar) - target) < 0.0001) return;

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = GetAnimationDuration(bar),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        // 自然结束后摘掉时钟，让属性回落到本地值，下一次过渡才能从"当前显示值"接着走（连续变化不跳变）。
        animation.Completed += (_, _) =>
        {
            bar.BeginAnimation(AnimatedRatioProperty, null);
            SetAnimatedRatio(bar, target);
        };

        bar.BeginAnimation(AnimatedRatioProperty, animation);
    }

    private static double GetRatio(ProgressBar bar)
    {
        var range = bar.Maximum - bar.Minimum;
        if (range <= 0) return 0;

        return Math.Clamp((bar.Value - bar.Minimum) / range, 0, 1);
    }
}
