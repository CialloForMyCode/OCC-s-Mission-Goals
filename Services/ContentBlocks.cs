using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 内容区（条目详情正文）的渲染与派生数据。
///
/// 块树的统一语义：<c>ContentBlock.Items</c> 是该块的「子块」；
/// <see cref="ContentBlockKind.List"/> 是一个容器 —— 它给直属子块加上列表标记（编号 / ·）
/// 与完成勾选框，子块里再嵌一个 List 就是多级列表。
/// </summary>
public static class ContentBlocks
{
    /// <summary>把一组区块渲染成竖向面板（顶层内容区）。</summary>
    public static FrameworkElement RenderBlocks(IEnumerable<ContentBlock>? blocks)
    {
        var host = new StackPanel();
        AppendBlocks(host, blocks, 0, false, false);
        return host;
    }

    /// <summary>把单个区块（含其子块）渲染成竖向面板。</summary>
    public static FrameworkElement RenderBlock(ContentBlock block)
    {
        var host = new StackPanel();
        AppendBlocks(host, new[] { block }, 0, false, false);
        return host;
    }

    // ==================== 渲染 ====================

    /// <param name="inList">当前层是否位于某个列表容器内（是则带编号/项目符号标记）。</param>
    /// <param name="ordered">该层是否使用编号。</param>
    private static void AppendBlocks(
        Panel host,
        IEnumerable<ContentBlock>? blocks,
        int depth,
        bool inList,
        bool ordered)
    {
        if (blocks == null) return;

        var index = 1;
        foreach (var block in blocks)
        {
            if (block == null) continue;

            // 列表容器自身不占行：把它的子块作为「条目」渲染在这一层。
            if (block.Kind == ContentBlockKind.List)
            {
                AppendBlocks(host, block.Items, depth, true, block.Ordered);
                continue;
            }

            var row = new Grid { Margin = new Thickness(depth * 14, 1, 0, 1) };

            // 文件引用行：记下引用信息，供「相关文件」表点击后定位并高亮这一行。
            if (block.Kind == ContentBlockKind.FileRef && block.File != null)
            {
                row.Tag = block.File;
                row.Background = Brushes.Transparent;
            }

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (inList)
            {
                var markerHost = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 1, 6, 0)
                };
                markerHost.Children.Add(BuildMarker(ordered ? index + "." : "·"));
                Grid.SetColumn(markerHost, 0);
                row.Children.Add(markerHost);
                index++;
            }

            var content = RenderOne(block);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            host.Children.Add(row);

            if (block.Items.Count > 0)
                AppendBlocks(host, block.Items, depth + 1, false, false);
        }
    }

    private static TextBlock BuildMarker(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7,
            MinWidth = 10,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        return tb;
    }

    private static FrameworkElement RenderOne(ContentBlock block) => block.Kind switch
    {
        ContentBlockKind.Divider => Markdown.BuildHorizontalRule(),
        ContentBlockKind.Table => RenderTable(block),
        ContentBlockKind.Code => RenderCode(block),
        ContentBlockKind.FileRef => RenderFileRef(block),
        ContentBlockKind.SubTask => RenderSubTask(block),
        _ => Markdown.Render(block.Text),
    };

    /// <summary>
    /// 子任务区块：只读的勾选框 + 文本。未完成页的详情里换成可点击的勾选框
    /// （勾选状态写回数据文件），这里保证完成页等只读场景也能看到勾选状态。
    /// </summary>
    private static FrameworkElement RenderSubTask(ContentBlock block)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var box = new CheckBox
        {
            IsChecked = block.Done,
            IsHitTestVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 6, 0),
        };
        box.SetResourceReference(FrameworkElement.StyleProperty, "SettingsCheckBox");
        Grid.SetColumn(box, 0);
        row.Children.Add(box);

        var text = Markdown.Render(block.Text);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        return row;
    }

    private static FrameworkElement RenderTable(ContentBlock block)
    {
        var data = block.Table;
        if (data == null || (data.Headers.Count == 0 && data.Rows.Count == 0))
            return Markdown.Render(string.Empty);

        var columns = Math.Max(
            data.Headers.Count,
            data.Rows.Count == 0 ? 0 : data.Rows.Max(r => r?.Count ?? 0));
        if (columns == 0) return Markdown.Render(string.Empty);

        var headers = Pad(data.Headers, columns);
        var rows = data.Rows.Select(r => Pad(r, columns)).ToList();
        var aligns = Enumerable.Repeat(TextAlignment.Left, columns).ToList();
        return Markdown.BuildTable(headers, aligns, rows);
    }

    private static List<string> Pad(List<string>? source, int columns)
    {
        var list = new List<string>(columns);
        for (var i = 0; i < columns; i++)
            list.Add(source != null && i < source.Count ? source[i] ?? string.Empty : string.Empty);
        return list;
    }

    private static FrameworkElement RenderCode(ContentBlock block)
    {
        var data = block.Code ?? new CodeBlockData();
        var lines = SplitLines(data.Code);
        var element = Markdown.BuildCodeBlock(data.Language, lines, data.ShowLineNumbers);
        element.Margin = new Thickness(0, 2, 0, 2);
        return element;
    }

    private static FrameworkElement RenderFileRef(ContentBlock block)
    {
        var file = block.File ?? new FileRef();
        var hasPath = !string.IsNullOrWhiteSpace(file.Path);
        var name = hasPath ? Path.GetFileName(file.Path) : LocalizationManager.T("（未指定文件）");

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 2),
            ToolTip = hasPath ? file.Path : null,
        };

        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M13 2H5C3.9 2 3 2.9 3 4V20C3 21.1 3.9 22 5 22H15C16.1 22 17 21.1 17 20V6L13 2ZM13 4.5L14.5 6H13V4.5ZM5 20V4H12V7H15V20H5Z"),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            Opacity = 0.7,
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ForegroundBrush");
        panel.Children.Add(icon);

        panel.Children.Add(new TextBlock
        {
            Text = name,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.WithForeground());

        var meta = BuildFileMeta(file);
        if (meta.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = meta,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            }.WithForeground());
        }

        return panel;
    }

    /// <summary>文件项的副信息：「函数 行:列」，全空时返回空串。</summary>
    private static string BuildFileMeta(FileRef file)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(file.Function)) parts.Add(file.Function);
        if (file.Line > 0) parts.Add(file.Column > 0 ? $"{file.Line}:{file.Column}" : file.Line.ToString());
        return parts.Count == 0 ? string.Empty : $"{string.Join(" ", parts)}";
    }

    private static TextBlock WithForeground(this TextBlock tb)
    {
        tb.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        return tb;
    }

    private static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
    }

    // ==================== 派生数据 ====================

    // ==================== 深拷贝 ====================

    /// <summary>深拷贝一组区块：编辑对话框在副本上工作，取消编辑不会改动原条目。</summary>
    public static List<ContentBlock> CloneBlocks(IEnumerable<ContentBlock>? blocks)
    {
        var result = new List<ContentBlock>();
        if (blocks == null) return result;

        foreach (var block in blocks)
        {
            if (block != null) result.Add(CloneBlock(block));
        }

        return result;
    }

    public static ContentBlock CloneBlock(ContentBlock block)
    {
        var copy = new ContentBlock
        {
            Kind = block.Kind,
            Text = block.Text,
            Ordered = block.Ordered,
            Done = block.Done,
            Table = block.Table == null ? null : new TableBlockData
            {
                Headers = new List<string>(block.Table.Headers),
                Rows = block.Table.Rows.Select(row => new List<string>(row)).ToList(),
            },
            Code = block.Code == null ? null : new CodeBlockData
            {
                Language = block.Code.Language,
                Code = block.Code.Code,
                ShowLineNumbers = block.Code.ShowLineNumbers,
            },
            File = block.File == null ? null : new FileRef
            {
                Path = block.File.Path,
                Line = block.File.Line,
                Column = block.File.Column,
                Function = block.File.Function,
            },
        };

        copy.Items = CloneBlocks(block.Items);
        return copy;
    }

    /// <summary>深度遍历内容区里的全部区块（含各级子块）。</summary>
    public static IEnumerable<ContentBlock> Flatten(IEnumerable<ContentBlock>? blocks)
    {
        if (blocks == null) yield break;
        foreach (var block in blocks)
        {
            if (block == null) continue;
            yield return block;
            foreach (var child in Flatten(block.Items))
                yield return child;
        }
    }

    /// <summary>
    /// 收集内容区里所有文件引用项，按「路径 / 行 / 列 / 函数」去重后返回。
    /// 条目保存时用它自动填充 RelatedFiles（取代以往手动选择文件）。
    /// </summary>
    public static List<FileRef> CollectFileRefs(IEnumerable<ContentBlock>? blocks)
    {
        var result = new List<FileRef>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in Flatten(blocks))
        {
            if (block.Kind != ContentBlockKind.FileRef || block.File == null) continue;
            var file = block.File;
            if (string.IsNullOrWhiteSpace(file.Path)) continue;

            var key = $"{file.Path}|{file.Line}|{file.Column}|{file.Function}";
            if (!seen.Add(key)) continue;

            result.Add(new FileRef
            {
                Path = file.Path,
                Line = file.Line,
                Column = file.Column,
                Function = file.Function,
            });
        }

        return result;
    }

    /// <summary>
    /// 完成度（0-100）：内容区里所有「子任务」区块中已勾选的比例；没有子任务时为 0。
    ///
    /// 列表容器（编号列表 / 列表）只负责排版，它的条目不算进度项 —— 条目的勾选状态
    /// 不参与统计；列表里放的「子任务」仍按子任务计入（也不会被重复计数）。
    /// </summary>
    public static int ComputeProgress(IEnumerable<ContentBlock>? blocks)
    {
        var total = 0;
        var done = 0;

        foreach (var block in Flatten(blocks))
        {
            // 子任务区块：一条即一项，勾选状态记在它自己的 Done 上。
            if (block.Kind != ContentBlockKind.SubTask) continue;

            total++;
            if (block.Done) done++;
        }

        return total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);
    }

    /// <summary>
    /// 按当前规则重算条目的完成度并写回。加载数据时调用，
    /// 这样统计口径的变化对已经存下来的旧数据即时生效。
    /// </summary>
    public static void RefreshProgress(GoalEntry? entry)
    {
        if (entry == null) return;
        entry.Progress = ComputeProgress(entry.Contents);
    }

    /// <summary>按当前规则重算一组条目的完成度（见 <see cref="RefreshProgress(GoalEntry)"/>）。</summary>
    public static void RefreshProgress(DataFile? data)
    {
        if (data == null) return;
        foreach (var entry in data.Entries)
            RefreshProgress(entry);
    }

    /// <summary>把内容区导出为纯文本（「复制信息」与命令行使用）。</summary>
    public static string ToPlainText(IEnumerable<ContentBlock>? blocks)
    {
        var sb = new StringBuilder();
        AppendPlainText(sb, blocks, 0, false, false);
        return sb.ToString().TrimEnd();
    }

    private static void AppendPlainText(
        StringBuilder sb,
        IEnumerable<ContentBlock>? blocks,
        int depth,
        bool inList,
        bool ordered)
    {
        if (blocks == null) return;

        var indent = new string(' ', depth * 2);
        var index = 1;

        foreach (var block in blocks)
        {
            if (block == null) continue;

            if (block.Kind == ContentBlockKind.List)
            {
                AppendPlainText(sb, block.Items, depth, true, block.Ordered);
                continue;
            }

            var prefix = inList ? (ordered ? $"{index++}. " : "· ") : string.Empty;
            var check = inList ? (block.Done ? "[x] " : "[ ] ") : string.Empty;

            switch (block.Kind)
            {
                case ContentBlockKind.Divider:
                    sb.AppendLine($"{indent}{prefix}{check}———");
                    break;
                case ContentBlockKind.Table:
                    foreach (var line in TableToText(block))
                        sb.AppendLine($"{indent}{prefix}{check}{line}");
                    break;
                case ContentBlockKind.Code:
                    sb.AppendLine($"{indent}{prefix}{check}[{block.Code?.Language}]");
                    foreach (var line in SplitLines(block.Code?.Code))
                        sb.AppendLine($"{indent}    {line}");
                    break;
                case ContentBlockKind.FileRef:
                    var file = block.File ?? new FileRef();
                    var meta = BuildFileMeta(file);
                    sb.AppendLine($"{indent}{prefix}{check}{(string.IsNullOrWhiteSpace(file.Path) ? LocalizationManager.T("（未指定文件）") : file.Path)}" +
                                  (meta.Length > 0 ? $"（{meta}）" : string.Empty));
                    break;
                case ContentBlockKind.SubTask:
                    // 子任务区块自带勾选状态，用 [x] / [ ] 标记。
                    foreach (var line in SplitLines(block.Text))
                        sb.AppendLine($"{indent}{prefix}{(block.Done ? "[x] " : "[ ] ")}{line}");
                    break;
                default:
                    foreach (var line in SplitLines(block.Text))
                        sb.AppendLine($"{indent}{prefix}{check}{line}");
                    break;
            }

            if (block.Items.Count > 0)
                AppendPlainText(sb, block.Items, depth + 1, false, false);
        }
    }

    private static IEnumerable<string> TableToText(ContentBlock block)
    {
        var data = block.Table;
        if (data == null) yield break;

        if (data.Headers.Count > 0)
            yield return string.Join(" | ", data.Headers);

        foreach (var row in data.Rows)
            yield return string.Join(" | ", row ?? new List<string>());
    }
}

/// <summary>把内容区块直接渲染成可视元素，供详情区的 ItemsControl 使用。</summary>
public sealed class ContentBlockConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ContentBlock block ? ContentBlocks.RenderBlock(block) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
