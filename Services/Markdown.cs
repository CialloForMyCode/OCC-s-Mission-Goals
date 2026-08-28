using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OCCMissionGoals.Services;

/// <summary>
/// 轻量级 Markdown 渲染器：把条目「详情」里的 Markdown 文本转换为 WPF 可视元素。
/// 支持标题、无序/有序列表、引用、代码块、分隔线，以及行内的加粗 / 斜体 /
/// 删除线 / 行内代码 / 链接。输出元素的前景 / 背景通过 DynamicResource 引用主题画刷，
/// 因此会随明暗主题切换自动变色。
/// </summary>
public static class Markdown
{
    // 转义占位符（使用私用区字符，避免与正文冲突）。
    private const string EscStar = "\uE000";
    private const string EscUnderscore = "\uE001";
    private const string EscBacktick = "\uE002";
    private const string EscLBracket = "\uE003";
    private const string EscRBracket = "\uE004";
    private const string EscHash = "\uE005";
    private const string EscBackslash = "\uE006";

    private static readonly Regex ReHeading = new(@"^(#{1,6})\s+(.*)$");
    private static readonly Regex ReHr = new(@"^\s*(-{3,}|\*{3,}|_{3,})\s*$");
    private static readonly Regex ReUnorderedList = new(@"^\s*[-*+]\s+");
    private static readonly Regex ReOrderedList = new(@"^\s*\d+\.\s+");

    private static readonly (Regex Re, Func<Match, Inline> Build)[] InlineRules =
    {
        (new Regex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Singleline),
            m => Wrap(new Bold(), ParseInlines(m.Groups[1].Value))),

        (new Regex(@"\*\*(.+?)\*\*", RegexOptions.Singleline),
            m => Wrap(new Bold(), ParseInlines(m.Groups[1].Value))),

        (new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Singleline),
            m => Wrap(new Italic(), ParseInlines(m.Groups[1].Value))),

        (new Regex(@"~~(.+?)~~", RegexOptions.Singleline),
            m => Strike(ParseInlines(m.Groups[1].Value))),

        (new Regex(@"(?<!~)~([^~\n]+?)~(?!~)", RegexOptions.Singleline),
            m => Strike(ParseInlines(m.Groups[1].Value))),

        (new Regex(@"`([^`\n]+)`", RegexOptions.Singleline),
            m => InlineCode(Unescape(m.Groups[1].Value))),

        (new Regex(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Singleline),
            m => BuildLink(Unescape(m.Groups[1].Value), Unescape(m.Groups[2].Value))),
    };

    /// <summary>把 Markdown 文本渲染为一个可放入面板的元素（空文本返回空面板）。</summary>
    public static FrameworkElement Render(string? markdown)
    {
        var root = new StackPanel();
        if (string.IsNullOrEmpty(markdown)) return root;

        var lines = Escape(markdown)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // 围栏代码块
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                var language = line.TrimStart().Substring(3).Trim();
                var codeLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(Unescape(lines[i]));
                    i++;
                }
                i++; // 跳过结束围栏
                root.Children.Add(BuildCodeBlock(language, codeLines));
                continue;
            }

            // 空行
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // 表格（当前行含 | 且下一行为分隔行）
            if (IsTableStart(lines, i))
            {
                var header = SplitTableRow(line);
                var aligns = ParseTableAligns(lines[i + 1]);
                i += 2;
                var tableRows = new List<List<string>>();
                while (i < lines.Length && IsTableRow(lines[i]) && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    tableRows.Add(SplitTableRow(lines[i]));
                    i++;
                }
                root.Children.Add(BuildTable(header, aligns, tableRows));
                continue;
            }

            // 分隔线
            if (ReHr.IsMatch(line))
            {
                root.Children.Add(BuildHorizontalRule());
                i++;
                continue;
            }

            // 标题
            var heading = ReHeading.Match(line);
            if (heading.Success)
            {
                root.Children.Add(BuildHeading(heading.Groups[2].Value, heading.Groups[1].Value.Length));
                i++;
                continue;
            }

            // 引用
            if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    var q = lines[i].TrimStart();
                    quoteLines.Add(q.Length > 1 ? q.Substring(1).TrimStart() : string.Empty);
                    i++;
                }
                root.Children.Add(BuildBlockquote(quoteLines));
                continue;
            }

            // 无序列表
            if (ReUnorderedList.IsMatch(line))
            {
                var items = new List<string>();
                while (i < lines.Length && ReUnorderedList.IsMatch(lines[i]))
                {
                    items.Add(ReUnorderedList.Replace(lines[i], string.Empty, 1));
                    i++;
                }
                foreach (var it in items)
                    root.Children.Add(BuildListItem("•", it));
                continue;
            }

            // 有序列表
            if (ReOrderedList.IsMatch(line))
            {
                var items = new List<string>();
                var n = 1;
                while (i < lines.Length && ReOrderedList.IsMatch(lines[i]))
                {
                    items.Add(ReOrderedList.Replace(lines[i], string.Empty, 1));
                    i++;
                }
                n = 1;
                foreach (var it in items)
                    root.Children.Add(BuildListItem($"{n++}.", it));
                continue;
            }

            // 普通段落：收集连续非空且非块起始的行
            var para = new List<string> { line };
            i++;
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !IsBlockStart(lines[i]) && !IsTableStart(lines, i))
            {
                para.Add(lines[i]);
                i++;
            }
            root.Children.Add(BuildParagraph(para));
        }

        return root;
    }

    /// <summary>
    /// 只渲染行内 Markdown（加粗 / 斜体 / 删除线 / 行内代码 / 链接），用于标题、简介等单行文本。
    /// 返回的 TextBlock 不设置前景 / 字号 / 字重，继承宿主 ContentControl 的样式。
    /// </summary>
    public static TextBlock RenderInline(string? markdown, bool trim = false)
    {
        var tb = new TextBlock();
        if (trim) tb.TextTrimming = TextTrimming.CharacterEllipsis;

        if (!string.IsNullOrEmpty(markdown))
        {
            var text = Escape(markdown);
            foreach (var inline in ParseInlines(text))
                tb.Inlines.Add(inline);
        }

        return tb;
    }

    /// <summary>简介在条目卡片中最多显示的字符数，超出部分以 "..." 结尾。</summary>
    public const int BriefExcerptMaxLength = 60;

    /// <summary>把简介文本截断到固定显示长度；超出时在末尾追加 "..."（避免破坏代理对）。</summary>
    public static string TruncateExcerpt(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= BriefExcerptMaxLength)
            return text ?? string.Empty;

        var end = BriefExcerptMaxLength;
        if (char.IsHighSurrogate(text[end - 1]))
            end--;
        return text.Substring(0, end) + "...";
    }

    private static bool IsBlockStart(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("```", StringComparison.Ordinal)
            || t.StartsWith("#", StringComparison.Ordinal)
            || t.StartsWith(">", StringComparison.Ordinal)
            || ReUnorderedList.IsMatch(line)
            || ReOrderedList.IsMatch(line)
            || ReHr.IsMatch(line);
    }

    private static TextBlock NewTextBlock()
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        return tb;
    }

    private static TextBlock BuildParagraph(List<string> lines)
    {
        var tb = NewTextBlock();
        for (var k = 0; k < lines.Count; k++)
        {
            if (k > 0) tb.Inlines.Add(new LineBreak());
            foreach (var inline in ParseInlines(lines[k]))
                tb.Inlines.Add(inline);
        }
        return tb;
    }

    private static TextBlock BuildHeading(string text, int level)
    {
        var tb = NewTextBlock();
        tb.FontWeight = FontWeights.SemiBold;
        tb.FontSize = level switch { 1 => 18, 2 => 16, 3 => 14, _ => 13 };
        tb.Margin = new Thickness(0, 6, 0, 2);
        foreach (var inline in ParseInlines(text))
            tb.Inlines.Add(inline);
        return tb;
    }

    private static FrameworkElement BuildCodeBlock(string language, List<string> codeLines)
    {
        var codeText = string.Join("\n", codeLines);

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(0),
        };
        border.SetResourceReference(Border.BackgroundProperty, "CodeBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "CodeBorderBrush");

        var panel = new StackPanel();

        // 头部：左侧语言标签，右侧复制按钮。
        panel.Children.Add(BuildCodeHeader(language, codeText));

        var sep = new Border { Height = 1 };
        sep.SetResourceReference(Border.BackgroundProperty, "CodeBorderBrush");
        panel.Children.Add(sep);

        var code = new TextBlock
        {
            Text = codeText,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(10, 6, 10, 8),
        };
        code.SetResourceReference(TextBlock.ForegroundProperty, "TerminalForegroundBrush");
        panel.Children.Add(code);

        border.Child = panel;
        return border;
    }

    private static FrameworkElement BuildCodeHeader(string language, string codeText)
    {
        var grid = new Grid { Margin = new Thickness(10, 3, 6, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language) ? "CODE" : language.Trim().ToUpperInvariant(),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.55,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var copyButton = BuildCopyButton(codeText);
        Grid.SetColumn(copyButton, 1);
        grid.Children.Add(copyButton);

        return grid;
    }

    private static Button BuildCopyButton(string codeText)
    {
        var icon = new Path
        {
            Data = Geometry.Parse("M16 1H4C2.9 1 2 1.9 2 3V17H4V3H16V1ZM19 5H8C6.9 5 6 5.9 6 7V21C6 22.1 6.9 23 8 23H19C20.1 23 21 22.1 21 21V7C21 5.9 20.1 5 19 5ZM19 21H8V7H19V21Z"),
            Width = 11,
            Height = 11,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 4, 0),
        };
        icon.SetResourceReference(Shape.FillProperty, "ForegroundBrush");

        var text = new TextBlock
        {
            Text = LocalizationManager.T("复制"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");

        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(icon);
        content.Children.Add(text);

        var btn = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(6, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Application.Current?.TryFindResource("CardActionBtn") is Style style)
        {
            btn.Style = style;
        }
        else
        {
            // 找不到应用样式时的简单回退：手动悬停效果。
            btn.MouseEnter += (_, _) => btn.SetResourceReference(Control.BackgroundProperty, "HoverBackgroundBrush");
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
        }
        btn.Click += (_, _) => CopyCode(text, codeText);

        return btn;
    }

    private static void CopyCode(TextBlock label, string codeText)
    {
        try
        {
            Clipboard.SetText(codeText);
        }
        catch
        {
            // 剪贴板被占用等异常：静默忽略，仍给出反馈。
        }

        label.Text = LocalizationManager.T("已复制");

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (_, _) =>
        {
            label.Text = LocalizationManager.T("复制");
            timer.Stop();
        };
        timer.Start();
    }

    // ---- 表格 ----

    private static bool IsTableRow(string line) => line.Contains('|');

    private static bool IsTableSeparator(string line)
    {
        if (!line.Contains('|')) return false;
        var cells = SplitTableRow(line);
        return cells.Count > 0 && cells.All(c => c.Length > 0 && c.All(ch => ch == '-' || ch == ':' || ch == ' '));
    }

    private static bool IsTableStart(string[] lines, int index)
        => index + 1 < lines.Length
           && lines[index].Contains('|')
           && IsTableSeparator(lines[index + 1]);

    private static List<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|", StringComparison.Ordinal)) trimmed = trimmed[1..];
        if (trimmed.EndsWith("|", StringComparison.Ordinal)) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(s => s.Trim()).ToList();
    }

    private static List<TextAlignment> ParseTableAligns(string line)
    {
        return SplitTableRow(line)
            .Select(c =>
            {
                var t = c.Trim();
                var left = t.StartsWith(":", StringComparison.Ordinal);
                var right = t.EndsWith(":", StringComparison.Ordinal);
                if (left && right) return TextAlignment.Center;
                if (right) return TextAlignment.Right;
                return TextAlignment.Left;
            })
            .ToList();
    }

    private static FrameworkElement BuildTable(List<string> header, List<TextAlignment> aligns, List<List<string>> rows)
    {
        var colCount = Math.Max(header.Count, aligns.Count);
        if (rows.Count > 0)
            colCount = Math.Max(colCount, rows.Max(r => r.Count));
        if (colCount <= 0) colCount = 1;

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(0),
        };
        border.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

        var grid = new Grid();
        for (var c = 0; c < colCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var totalRows = 1 + rows.Count;
        for (var r = 0; r < totalRows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var c = 0; c < colCount; c++)
        {
            var cell = BuildTableCell(header.Count > c ? header[c] : string.Empty, aligns, c,
                isHeader: true, isLastRow: rows.Count == 0, isLastColumn: c == colCount - 1);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < colCount; c++)
            {
                var cell = BuildTableCell(row.Count > c ? row[c] : string.Empty, aligns, c,
                    isHeader: false, isLastRow: r == rows.Count - 1, isLastColumn: c == colCount - 1);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        border.Child = grid;
        return border;
    }

    private static Border BuildTableCell(string text, List<TextAlignment> aligns, int col, bool isHeader, bool isLastRow, bool isLastColumn)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0),
            TextAlignment = col < aligns.Count ? aligns[col] : TextAlignment.Left,
        };
        if (isHeader) tb.FontWeight = FontWeights.SemiBold;
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        foreach (var inline in ParseInlines(text))
            tb.Inlines.Add(inline);

        var cell = new Border
        {
            Child = tb,
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(0, 0, isLastColumn ? 0 : 1, isLastRow ? 0 : 1),
        };
        cell.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        if (isHeader)
            cell.SetResourceReference(Border.BackgroundProperty, "CodeBackgroundBrush");

        return cell;
    }

    private static FrameworkElement BuildHorizontalRule()
    {
        var border = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 6, 0, 6),
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBorderBrush");
        return border;
    }

    private static FrameworkElement BuildBlockquote(List<string> lines)
    {
        var tb = NewTextBlock();
        tb.Margin = new Thickness(0);
        for (var k = 0; k < lines.Count; k++)
        {
            if (k > 0) tb.Inlines.Add(new LineBreak());
            foreach (var inline in ParseInlines(lines[k]))
                tb.Inlines.Add(inline);
        }

        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 2, 0, 2),
            Margin = new Thickness(0, 2, 0, 4),
            Child = tb,
        };
        border.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
        return border;
    }

    private static FrameworkElement BuildListItem(string marker, string content)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var markerTb = new TextBlock
        {
            Text = marker,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 18,
            TextAlignment = TextAlignment.Right,
        };
        markerTb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");

        var contentTb = NewTextBlock();
        contentTb.Margin = new Thickness(0);
        foreach (var inline in ParseInlines(content))
            contentTb.Inlines.Add(inline);

        Grid.SetColumn(markerTb, 0);
        Grid.SetColumn(contentTb, 1);
        grid.Children.Add(markerTb);
        grid.Children.Add(contentTb);
        return grid;
    }

    // ---- 行内解析 ----

    private static List<Inline> ParseInlines(string text)
    {
        var result = new List<Inline>();
        if (string.IsNullOrEmpty(text)) return result;

        var pos = 0;
        while (pos < text.Length)
        {
            var next = FindNextInline(text, pos);
            if (next == null)
            {
                result.Add(new Run(Unescape(text.Substring(pos))));
                break;
            }

            if (next.Value.Start > pos)
                result.Add(new Run(Unescape(text.Substring(pos, next.Value.Start - pos))));

            result.Add(next.Value.Inline);
            pos = next.Value.End;
        }

        return result;
    }

    private static (int Start, int End, Inline Inline)? FindNextInline(string text, int from)
    {
        (int Start, int End, Inline Inline)? best = null;

        foreach (var (re, build) in InlineRules)
        {
            var m = re.Match(text, from);
            if (!m.Success) continue;

            if (best == null || m.Index < best.Value.Start)
                best = (m.Index, m.Index + m.Length, build(m));
        }

        return best;
    }

    private static Inline Wrap(Span span, List<Inline> inlines)
    {
        foreach (var inline in inlines)
            span.Inlines.Add(inline);
        return span;
    }

    private static Inline Strike(List<Inline> inlines)
    {
        var span = new Span { TextDecorations = TextDecorations.Strikethrough };
        foreach (var inline in inlines)
            span.Inlines.Add(inline);
        return span;
    }

    private static Inline InlineCode(string code)
    {
        // 行内代码：带边框的圆角「胶囊」，前景跟随主题正文色（ForegroundBrush），
        // 背景用代码底色，边框用代码边框色；Center 对齐使胶囊与周围文字精确垂直居中。
        var text = new TextBlock(new Run(code))
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");

        var chip = new Border
        {
            Child = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(1, 0, 1, 0),
            Padding = new Thickness(2,2,2,1),
        };
        chip.SetResourceReference(Border.BackgroundProperty, "CodeBackgroundBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "CodeBorderBrush");

        return new InlineUIContainer(chip) { BaselineAlignment = BaselineAlignment.Center };
    }

    private static Inline BuildLink(string text, string url)
    {
        var link = new Hyperlink(new Run(text)) { TextDecorations = TextDecorations.Underline };

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "mailto"))
        {
            link.NavigateUri = uri;
            link.RequestNavigate += (_, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
                }
                catch
                {
                    // 忽略无法打开链接的异常。
                }
                e.Handled = true;
            };
        }

        link.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
        return link;
    }

    // ---- 转义处理 ----

    private static string Escape(string input) =>
        input.Replace(@"\\", EscBackslash)
             .Replace(@"\*", EscStar)
             .Replace(@"\_", EscUnderscore)
             .Replace(@"\`", EscBacktick)
             .Replace(@"\[", EscLBracket)
             .Replace(@"\]", EscRBracket)
             .Replace(@"\#", EscHash);

    private static string Unescape(string input) =>
        input.Replace(EscBackslash, "\\")
             .Replace(EscStar, "*")
             .Replace(EscUnderscore, "_")
             .Replace(EscBacktick, "`")
             .Replace(EscLBracket, "[")
             .Replace(EscRBracket, "]")
             .Replace(EscHash, "#");
}

/// <summary>把字符串绑定值转换为 Markdown 渲染后的可视元素。</summary>
public sealed class MarkdownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Markdown.Render(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把字符串绑定值转换为行内 Markdown（用于标题 / 简介等单行文本）。</summary>
public sealed class MarkdownInlineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        var p = parameter as string;
        var trim = string.Equals(p, "trim", StringComparison.OrdinalIgnoreCase);

        // "excerpt"：用于简介，固定显示长度 + 省略号（超出部分用 "..." 代替）。
        if (string.Equals(p, "excerpt", StringComparison.OrdinalIgnoreCase))
        {
            trim = true;
            text = Markdown.TruncateExcerpt(text);
        }

        return Markdown.RenderInline(text, trim);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
