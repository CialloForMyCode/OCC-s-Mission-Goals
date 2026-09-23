using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OCCMissionGoals.Controls;

/// <summary>
/// 单行文本的"跑马灯"：文本宽度超过容器可用宽度时，把这块文本和一份等样的副本并排放好，
/// 整块以恒定速度一直向左滚 —— 正好滚过一块的宽度时，副本落回起点，画面与开始时一模一样，
/// 于是循环接得上：既没有停顿、也没有空隙和跳变，像店门口的招牌那样一遍遍地连着播。
/// 不溢出时什么都不做；未滚动时交回原生排版 —— 超出的部分按宿主原本的
/// <see cref="TextBlock.TextTrimming"/>（通常是省略号）收尾，不出现"半个字被硬切"的观感，
/// 只有滚动期间才解除截断。并排用的副本与原文等高，因此宿主高度在静止和滚动时完全一致。
/// 用法：挂在外层 ContentControl 上（内容为 Markdown 行内渲染出的 TextBlock），
/// 用 <see cref="IsRunningProperty"/> 控制"什么时候滚"（例：悬停预览打开时才滚）。
/// </summary>
public static class Marquee
{
    /// <summary>默认滚动速度（像素 / 秒）。</summary>
    public const double DefaultSpeed = 40;

    // 布局（悬停变宽动画、文本变化）稳定后再评估，避免动画期间反复重启滚动
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    private const double Epsilon = 0.5;

    #region 附加属性

    /// <summary>是否接管该元素的文本溢出显示：滚动期间去掉省略号改为可滚动，不滚动时保持原样。</summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(Marquee),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>是否正在滚动：由外部控制（如"悬停预览已打开"）。为 false 时立即停下并复位到开头。</summary>
    public static readonly DependencyProperty IsRunningProperty =
        DependencyProperty.RegisterAttached(
            "IsRunning",
            typeof(bool),
            typeof(Marquee),
            new PropertyMetadata(false, OnIsRunningChanged));

    public static bool GetIsRunning(DependencyObject element) => (bool)element.GetValue(IsRunningProperty);

    public static void SetIsRunning(DependencyObject element, bool value) => element.SetValue(IsRunningProperty, value);

    /// <summary>滚动速度（像素 / 秒），默认 <see cref="DefaultSpeed"/>。</summary>
    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.RegisterAttached(
            "Speed",
            typeof(double),
            typeof(Marquee),
            new PropertyMetadata(DefaultSpeed));

    public static double GetSpeed(DependencyObject element) => (double)element.GetValue(SpeedProperty);

    public static void SetSpeed(DependencyObject element, double value) => element.SetValue(SpeedProperty, value);

    #endregion

    private sealed class State
    {
        public TextBlock? Text;
        public TranslateTransform? Offset;
        public bool Running;
        public bool Scrolling;   // 已接管：并排容器搭好了、动画在跑

        // 无缝循环用的结构：宿主内容换成 Frame（Canvas），里面是并排的原文 + 等样副本（Track），平移 Track
        public object? OriginalContent;
        public Canvas? Frame;
        public StackPanel? Track;

        // 自己改动宿主 Content 时不要把 ContentDirty 打开，否则滚动会被自己的重排反复拉回起点
        public bool Suppress;

        // 触发来源：只有内容 / 可滚条件变了才重新接管；
        // 单纯的重排（多半是搭上、拆掉并排容器自己引起的）直接跳过，免得把滚动的动画反复拉回起点
        public bool ContentDirty = true;
        public bool SizeDirty = true;
        public double LastAvailable = double.NaN;

        // 接管前的原生排版设置：不滚动时按原样还原（保留省略号截断的观感）
        public bool OriginalsSaved;
        public TextTrimming OriginalTrimming;
        public HorizontalAlignment OriginalAlignment;
        public bool OriginalClipToBounds;

        public EventHandler? ContentChanged;
        public PropertyDescriptor? ContentDescriptor;
        public SizeChangedEventHandler? SizeChanged;
        public DispatcherTimer? Timer;
    }

    private static readonly ConditionalWeakTable<DependencyObject, State> States = new();

    private static State GetState(DependencyObject element)
    {
        if (!States.TryGetValue(element, out var state))
        {
            state = new State();
            States.Add(element, state);
        }

        return state;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement host) return;

        var state = GetState(d);

        if (e.NewValue is not true)
        {
            Teardown(host, state);
            return;
        }

        state.SizeChanged ??= (_, _) =>
        {
            state.SizeDirty = true;
            Schedule(host, state);
        };
        host.SizeChanged += state.SizeChanged;

        // 内容由 converter 生成，每次绑定求值都是新的 TextBlock，需要盯着 Content 变化
        if (host is ContentControl)
        {
            state.ContentChanged ??= (_, _) =>
            {
                if (state.Suppress) return;   // 并排容器是自己换上去的，不算内容变化
                state.ContentDirty = true;
                Schedule(host, state);
            };
            state.ContentDescriptor ??= DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
            state.ContentDescriptor?.AddValueChanged(host, state.ContentChanged);
        }

        state.ContentDirty = true;
        Schedule(host, state);
    }

    private static void OnIsRunningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement host) return;

        var state = GetState(d);
        state.Running = e.NewValue is true;
        state.ContentDirty = true;

        if (state.Running)
        {
            Schedule(host, state);
            return;
        }

        // 立刻停下并复位，同时拆掉并排容器、交回原生排版（省略号收尾），不必等防抖计时
        Stop(state);
        state.Scrolling = false;
        if (state.Text is not null && state.OriginalsSaved) GoIdle(host, state, state.Text);
    }

    private static void Schedule(FrameworkElement host, State state)
    {
        if (state.Timer is null)
        {
            state.Timer = new DispatcherTimer(DispatcherPriority.Background, host.Dispatcher) { Interval = Debounce };
            state.Timer.Tick += (_, _) =>
            {
                state.Timer!.Stop();
                Evaluate(host);
            };
        }

        state.Timer.Stop();
        state.Timer.Start();
    }

    private static void Evaluate(FrameworkElement host)
    {
        if (!GetIsEnabled(host)) return;

        var state = GetState(host);

        // 消费这一轮的重排来源：内容 / 可滚条件变了才算"输入变了"
        var contentDirty = state.ContentDirty;
        state.ContentDirty = false;
        state.SizeDirty = false;

        // 已接管时宿主的 Content 是 Frame（不是 TextBlock 了），直接沿用记下来的那一块
        var text = state.Frame is not null && state.Text is not null ? state.Text : FindText(host);
        if (text is null)
        {
            Stop(state);
            state.Scrolling = false;
            return;
        }

        if (!ReferenceEquals(state.Text, text))
        {
            // 宿主换了一块新的 TextBlock（多半是内容重绑）：先把旧的并排容器拆掉还回去
            DetachCopy(host, state);
            state.Text = text;
            state.Offset ??= new TranslateTransform();
            state.OriginalsSaved = false;
            state.Scrolling = false;
            state.LastAvailable = double.NaN;
            contentDirty = true;
        }

        SaveOriginals(host, text, state);

        var available = host.ActualWidth;
        if (available <= 1)
        {
            // 还没量出宽度，等下一次布局
            state.ContentDirty = true;
            return;
        }

        // 正在滚、可用宽度也没变（多半是并排容器自己引起的重排）：
        // 什么都别动，尤其别把动画重新开始，否则滚动会被反复拉回起点
        if (!contentDirty && state.Scrolling && Math.Abs(available - state.LastAvailable) < Epsilon)
            return;

        // 量一次"一份"文本的完整宽度
        text.Width = double.NaN;
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = text.DesiredSize.Width;
        if (double.IsInfinity(textWidth) || double.IsNaN(textWidth)) return;

        state.LastAvailable = available;

        if (!state.Running || textWidth - available <= Epsilon)
        {
            // 不滚动 / 放得下：拆掉并排容器，交回原生排版，超出部分由宿主原本的 TextTrimming（省略号）收尾
            Stop(state);
            state.Scrolling = false;
            GoIdle(host, state, text);
            return;
        }

        Start(host, state, text, textWidth);
        state.Scrolling = true;
    }

    /// <summary>记下接管前的原生排版设置，供不滚动时还原。</summary>
    private static void SaveOriginals(FrameworkElement host, TextBlock text, State state)
    {
        if (state.OriginalsSaved) return;

        state.OriginalTrimming = text.TextTrimming;
        state.OriginalAlignment = text.HorizontalAlignment;
        state.OriginalClipToBounds = host.ClipToBounds;
        state.OriginalsSaved = true;
    }

    /// <summary>交回原生排版：拆掉并排容器，宽度自适应容器（配合 TextTrimming 显示省略号），不接管裁剪。</summary>
    private static void GoIdle(FrameworkElement host, State state, TextBlock text)
    {
        DetachCopy(host, state);

        if (!double.IsNaN(text.Width)) text.Width = double.NaN;
        if (text.HorizontalAlignment != state.OriginalAlignment) text.HorizontalAlignment = state.OriginalAlignment;
        if (text.TextTrimming != state.OriginalTrimming) text.TextTrimming = state.OriginalTrimming;
        if (host.ClipToBounds != state.OriginalClipToBounds) host.ClipToBounds = state.OriginalClipToBounds;
    }

    /// <summary>
    /// 起滚：把原文本和一份等样的副本并排放在一个横向容器里，然后整块以恒定速度一直向左滚。
    /// 位移满一份的宽度时，副本正好落回起点，画面与开始时一模一样。
    /// </summary>
    private static void Start(FrameworkElement host, State state, TextBlock text, double textWidth)
    {
        if (state.Offset is null) return;

        // 滚动期间解除省略号截断，让整行文字都排出来；超出的部分由宿主的 ClipToBounds 裁掉
        if (text.TextTrimming != TextTrimming.None) text.TextTrimming = TextTrimming.None;
        if (text.HorizontalAlignment != HorizontalAlignment.Left) text.HorizontalAlignment = HorizontalAlignment.Left;
        if (!host.ClipToBounds) host.ClipToBounds = true;

        // 高度按不滚动时的实际文本高度钉死，免得滚动期间宿主高度发生变化
        var staticHeight = text.ActualHeight;
        if (double.IsNaN(staticHeight) || staticHeight <= 0) staticHeight = text.DesiredSize.Height;

        if (!EnsureCopy(host, state, text, textWidth, staticHeight))
        {
            // 搭不起并排容器就退回原生排版，宁可不动也不要露出被硬切的一半文字
            Stop(state);
            GoIdle(host, state, text);
            return;
        }

        if (double.IsNaN(text.Width) || Math.Abs(text.Width - textWidth) > Epsilon) text.Width = textWidth;

        var speed = GetSpeed(host);
        if (speed <= 1) speed = DefaultSpeed;

        var loop = TimeSpan.FromSeconds(textWidth / speed);   // 滚过一份的宽度：画面回到起点
        var frames = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(-textWidth, KeyTime.FromTimeSpan(loop)));

        state.Offset.BeginAnimation(TranslateTransform.XProperty, frames);
    }

    /// <summary>
    /// 搭出并排结构：新建一块等样 TextBlock 装同样的内容，与原块横向并排，
    /// 外层容器宽度正好是两份，平移容器即可循环。两份内容一模一样，接缝处是同一个字，不会有空隙。
    /// </summary>
    private static bool EnsureCopy(FrameworkElement host, State state, TextBlock text, double textWidth, double staticHeight)
    {
        if (state.Frame is not null) return true;
        if (host is not ContentControl content) return false;

        var copy = new TextBlock
        {
            TextWrapping = text.TextWrapping,
            TextTrimming = TextTrimming.None,
            TextAlignment = text.TextAlignment,
            TextDecorations = text.TextDecorations,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontStretch = text.FontStretch,
            FontWeight = text.FontWeight,
            FontStyle = text.FontStyle,
            Foreground = text.Foreground,
            LineHeight = text.LineHeight,
            LineStackingStrategy = text.LineStackingStrategy,
            Padding = text.Padding,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = text.VerticalAlignment,
            Width = textWidth,
        };

        // 图片之类的行内元素没法这样复制：宁可不滚，也不要接缝处断掉
        if (!CopyInlines(text.Inlines, copy.Inlines)) return false;

        var track = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = textWidth * 2,
            RenderTransform = state.Offset,
        };

        // 用 Canvas 承载并排容器：Canvas 给子元素无限约束，容器按自己的宽度排开，
        // 不会因为"放不下"被 WPF 自动裁到一屏宽 —— 那种裁剪会跟着平移一起走，
        // 滚过一屏之后画面就被裁没了。这里平移的是内容，裁剪区固定在宿主位置，正好裁掉溢出部分。
        var frame = new Canvas
        {
            Height = staticHeight,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
        };

        var previous = content.Content;
        state.Suppress = true;
        try
        {
            // 原块先从宿主上摘下来，才能和副本并排（用 SetCurrentValue 保住宿主原本的绑定）
            content.SetCurrentValue(ContentControl.ContentProperty, null);
            track.Children.Add(text);
            track.Children.Add(copy);
            frame.Children.Add(track);
            content.SetCurrentValue(ContentControl.ContentProperty, frame);
        }
        catch
        {
            // 容器搭不起来（例如原块还被别处占着）：原样还回去，别连累原生排版
            frame.Children.Clear();
            track.Children.Clear();
            content.SetCurrentValue(ContentControl.ContentProperty, previous);
            return false;
        }
        finally
        {
            state.Suppress = false;
        }

        state.OriginalContent = previous;
        state.Frame = frame;
        state.Track = track;
        return true;
    }

    /// <summary>拆掉并排结构：把原块交回宿主显示，副本丢弃。</summary>
    private static void DetachCopy(FrameworkElement host, State state)
    {
        if (state.Frame is null) return;

        state.Suppress = true;
        try
        {
            var frame = state.Frame;
            frame.Children.Clear();       // 先摘掉并排容器
            state.Track?.Children.Clear(); // 再把原块从并排容器里摘出来，之后才能还给宿主

            if (host is ContentControl content && ReferenceEquals(content.Content, frame))
                content.SetCurrentValue(ContentControl.ContentProperty, state.OriginalContent);
        }
        finally
        {
            state.Suppress = false;
        }

        state.Frame = null;
        state.Track = null;
        state.OriginalContent = null;
    }

    // 逐条复制行内内容：副本必须是"重新排版的同样文字"，接缝处才不会断开。
    private static bool CopyInlines(InlineCollection source, InlineCollection target)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case Run run:
                {
                    var copy = new Run(run.Text);
                    CopyInlineFormat(run, copy);
                    target.Add(copy);
                    break;
                }
                case Hyperlink link:
                {
                    var copy = new Hyperlink { NavigateUri = link.NavigateUri, ToolTip = link.ToolTip };
                    CopyInlineFormat(link, copy);
                    if (!CopyInlines(link.Inlines, copy.Inlines)) return false;
                    target.Add(copy);
                    break;
                }
                case Span span:
                {
                    Span copy = span switch
                    {
                        Bold _ => new Bold(),
                        Italic _ => new Italic(),
                        Underline _ => new Underline(),
                        _ => new Span(),
                    };
                    CopyInlineFormat(span, copy);
                    if (!CopyInlines(span.Inlines, copy.Inlines)) return false;
                    target.Add(copy);
                    break;
                }
                default:
                    // 图片之类的行内元素没法这么复制：宁可不滚，也不要接缝处断掉
                    return false;
            }
        }

        return true;
    }

    private static void CopyInlineFormat(Inline source, Inline target)
    {
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.FontStretch = source.FontStretch;
        target.FontWeight = source.FontWeight;
        target.FontStyle = source.FontStyle;
        target.Foreground = source.Foreground;
        target.TextDecorations = source.TextDecorations;
        target.BaselineAlignment = source.BaselineAlignment;
    }

    private static void Stop(State state)
    {
        if (state.Offset is null) return;

        state.Offset.BeginAnimation(TranslateTransform.XProperty, null);
        state.Offset.X = 0;
    }

    private static void Teardown(FrameworkElement host, State state)
    {
        if (state.SizeChanged is not null) host.SizeChanged -= state.SizeChanged;
        if (state.ContentDescriptor is not null && state.ContentChanged is not null)
            state.ContentDescriptor.RemoveValueChanged(host, state.ContentChanged);

        state.Timer?.Stop();
        state.Timer = null;
        Stop(state);

        // 先把并排容器拆掉、原块交回宿主，再还原原生排版设置
        DetachCopy(host, state);

        if (state.Text is not null)
        {
            state.Text.Width = double.NaN;

            if (state.OriginalsSaved)
            {
                state.Text.TextTrimming = state.OriginalTrimming;
                state.Text.HorizontalAlignment = state.OriginalAlignment;
            }
        }

        state.Text = null;
        state.Offset = null;
        state.LastAvailable = double.NaN;
        state.ContentDirty = true;
        state.SizeDirty = true;
        state.Scrolling = false;

        // 只有确实接管过才还原，避免从未评估到就误改宿主原本的裁剪设置
        if (state.OriginalsSaved && host.ClipToBounds != state.OriginalClipToBounds)
            host.ClipToBounds = state.OriginalClipToBounds;

        state.OriginalsSaved = false;
    }

    private static TextBlock? FindText(DependencyObject root)
    {
        if (root is ContentControl { Content: TextBlock direct }) return direct;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text) return text;

            var found = FindText(child);
            if (found is not null) return found;
        }

        return null;
    }
}
