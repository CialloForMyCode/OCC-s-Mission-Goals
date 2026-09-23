using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace OCCMissionGoals.Services
{
    /// <summary>
    /// 「展开 / 收起」的平滑过渡：把面板高度从 0 撑到自然高度、或反过来收到 0，并附轻微淡入淡出，
    /// 下方内容跟着布局整体上移 / 下移，看起来像牌面滑动。
    /// 用法：面板的可见状态交给本类，不要再绑 Visibility：
    /// <c>&lt;StackPanel svc:ExpandAnimation.Expanded="{Binding IsDetailExpanded}" ClipToBounds="True"&gt;</c>
    /// 元素需要裁掉溢出内容，过渡中才不会把子元素画到面板外。
    /// </summary>
    public static class ExpandAnimation
    {
        /// <summary>展开耗时（毫秒）：CubicEase EaseOut，与项目其它动画节奏一致。</summary>
        public const int ExpandDurationMs = 220;

        /// <summary>收起耗时（毫秒）：比展开略快，CubicEase EaseIn。</summary>
        public const int CollapseDurationMs = 170;

        /// <summary>
        /// 目标可见状态，绑定到 ViewModel 上的开关（true 展开 / false 收起）。
        /// 默认值取 true：元素天然是可见的展开态，绑到 false 时才会触发回调收起。
        /// </summary>
        public static readonly DependencyProperty ExpandedProperty =
            DependencyProperty.RegisterAttached(
                "Expanded",
                typeof(bool),
                typeof(ExpandAnimation),
                new PropertyMetadata(true, OnExpandedChanged));

        public static void SetExpanded(DependencyObject element, bool value)
            => element.SetValue(ExpandedProperty, value);

        public static bool GetExpanded(DependencyObject element)
            => (bool)element.GetValue(ExpandedProperty);

        private static void OnExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement panel) return;

            bool expanded = e.NewValue is true;

            // 容器刚生成、还没进视觉树（数据模板首帧求值）：直接落到目标状态。
            // 这样列表重建（排序、搜索、切换项目）时不会满屏播放展开动画。
            if (!panel.IsLoaded)
            {
                ApplyInstant(panel, expanded);
                return;
            }

            if (expanded) PlayExpand(panel);
            else PlayCollapse(panel);
        }

        /// <summary>不做过渡，直接落到目标状态。</summary>
        private static void ApplyInstant(FrameworkElement panel, bool expanded)
        {
            ClearAnimations(panel);
            panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void PlayExpand(FrameworkElement panel)
        {
            ClearAnimations(panel);
            panel.Visibility = Visibility.Visible;

            double target = MeasureNaturalHeight(panel);
            if (target <= 0.0)
            {
                // 量不到内容高度（离屏等）：直接显示，免得动画把面板锁在 0 高度。
                return;
            }

            var height = new DoubleAnimation(0.0, target, TimeSpan.FromMilliseconds(ExpandDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            height.Completed += (_, _) =>
            {
                // 收尾后把高度交还布局：内容再长也不会被固定在动画时的尺寸。
                panel.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                panel.MaxHeight = double.PositiveInfinity;
            };

            panel.BeginAnimation(FrameworkElement.MaxHeightProperty, height);
            panel.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(ExpandDurationMs * 3 / 4))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private static void PlayCollapse(FrameworkElement panel)
        {
            double from = panel.ActualHeight;
            if (from <= 0.0)
            {
                ApplyInstant(panel, false);
                return;
            }

            var height = new DoubleAnimation(from, 0.0, TimeSpan.FromMilliseconds(CollapseDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            height.Completed += (_, _) =>
            {
                panel.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                panel.MaxHeight = double.PositiveInfinity;
                panel.BeginAnimation(UIElement.OpacityProperty, null);

                // 收起途中若又被展开，交给展开动画收尾，别把面板藏掉。
                if (GetExpanded(panel))
                {
                    panel.Opacity = 1.0;
                    return;
                }

                panel.Visibility = Visibility.Collapsed;
            };

            panel.BeginAnimation(FrameworkElement.MaxHeightProperty, height);
            panel.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(CollapseDurationMs * 3 / 4))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                });
        }

        /// <summary>清掉历史动画，把高度与透明度还原成自然状态。</summary>
        private static void ClearAnimations(FrameworkElement panel)
        {
            panel.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
            panel.BeginAnimation(UIElement.OpacityProperty, null);
            panel.MaxHeight = double.PositiveInfinity;
            panel.Opacity = 1.0;
        }

        /// <summary>
        /// 量出面板在自然高度下的期望大小。只测量这一棵子树，不刷新整页布局，
        /// 「展开全部条目」时几十上百个面板一起过渡也不会明显变慢。
        /// </summary>
        private static double MeasureNaturalHeight(FrameworkElement panel)
        {
            double width = panel.ActualWidth;
            if (width <= 0.0 && panel.Parent is FrameworkElement parent)
                width = parent.ActualWidth;

            panel.Measure(new Size(width > 0.0 ? width : double.PositiveInfinity, double.PositiveInfinity));
            return panel.DesiredSize.Height;
        }
    }
}
