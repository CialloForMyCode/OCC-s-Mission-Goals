using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Dialogs;

public partial class NewEntryDialog : UserControl
{
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    private readonly ObservableCollection<TypeTag> _types = new();
    private string? _pendingColorHex;
    private GoalEntry? _editingEntry;

    public bool IsEditing => _editingEntry != null;
    public GoalEntry? EditingEntry => _editingEntry;

    public int Severity =>
        SeverityComboBox.SelectedItem is ComboBoxItem item && item.Tag != null
            ? Convert.ToInt32(item.Tag)
            : 0;

    public string EntryTitle => TitleTextBox.Text.Trim();
    public string Brief => BriefTextBox.Text.Trim();

    /// <summary>内容区块（内容区编辑结果）。</summary>
    public List<ContentBlock> Contents => ContentEditor.Build();

    public List<string> Type => _types.Select(t => t.Text).ToList();

    public NewEntryDialog()
    {
        InitializeComponent();
        TypeListControl.ItemsSource = _types;
        BuildColorSwatches();

        // 内容区一改，下面的「相关文件」立即重新汇总。
        ContentEditor.Changed += (_, _) => RefreshAutoFiles();
    }

    public void Reset()
    {
        _editingEntry = null;
        DialogTitle.Text = LocalizationManager.T("新建条目");
        SeverityComboBox.SelectedIndex = 2;
        TitleTextBox.Text = string.Empty;
        BriefTextBox.Text = string.Empty;
        PopulateTypes();
        TypeComboBox.Text = string.Empty;
        _types.Clear();
        ContentEditor.Load(null);
        RefreshAutoFiles();
        ResetColorSelection();
    }

    public void LoadEntry(GoalEntry entry)
    {
        _editingEntry = entry;
        DialogTitle.Text = LocalizationManager.T("编辑条目");

        SeverityComboBox.SelectedIndex = (int)entry.Severity;
        TitleTextBox.Text = entry.Title;
        BriefTextBox.Text = entry.Brief;
        PopulateTypes();
        TypeComboBox.Text = string.Empty;

        _types.Clear();
        foreach (var t in entry.Type)
            _types.Add(new TypeTag(t, Services.ProjectService.GetTypeColor(t)));

        ContentEditor.Load(BuildInitialContents(entry));

        RefreshAutoFiles();
    }

    /// <summary>
    /// 准备内容区：以条目的内容区为副本（取消编辑不会改到原条目）；
    /// 旧数据还没有内容区时，把原来的关联文件迁移成文件引用块，避免历史信息丢失。
    /// </summary>
    private static List<ContentBlock> BuildInitialContents(GoalEntry entry)
    {
        if (entry.Contents.Count > 0)
            return Services.ContentBlocks.CloneBlocks(entry.Contents);

        var migrated = new List<ContentBlock>();
        foreach (var f in entry.RelatedFiles)
        {
            migrated.Add(new ContentBlock
            {
                Kind = ContentBlockKind.FileRef,
                File = new FileRef
                {
                    Path = f.Path,
                    Line = f.Line,
                    Column = f.Column,
                    Function = f.Function
                }
            });
        }

        return migrated;
    }

    /// <summary>把内容区里的文件引用汇总到「相关文件」列表（列表里显示完整路径）。</summary>
    private void RefreshAutoFiles()
        => FileListControl.ItemsSource = Services.ContentBlocks.CollectFileRefs(ContentEditor.Build());

    private void PopulateTypes()
    {
        TypeComboBox.Items.Clear();
        var cfg = Services.ProjectService.CurrentProject;

        if (cfg?.TypeOptions != null)
        {
            Services.ProjectService.EnsureTypeColorsAligned();
            for (var i = 0; i < cfg.TypeOptions.Count; i++)
            {
                var typeName = cfg.TypeOptions[i];
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                var colorHex = i < cfg.TypeColors.Count ? cfg.TypeColors[i] : string.Empty;
                var item = new ComboBoxItem
                {
                    Content = BuildTypeContent(typeName, colorHex),
                    Tag = typeName,
                    Style = (Style)FindResource("DeletableDialogComboBoxItem")
                };
                item.Loaded += (s, _) =>
                {
                    var cbi = (ComboBoxItem)s;
                    if (cbi.Template.FindName("DeleteButton", cbi) is Button btn)
                    {
                        btn.PreviewMouseLeftButtonDown += (_, e) =>
                        {
                            e.Handled = true;
                            DeleteTypeOption(typeName);
                        };
                    }
                };
                TypeComboBox.Items.Add(item);
            }
        }
    }

    private void AddType_Click(object sender, RoutedEventArgs e)
    {
        TryAddType(TypeComboBox.Text);
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            TryAddType(tag);
    }

    private void TypeComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (TypeComboBox.Items.Count == 0)
            TypeComboBox.IsDropDownOpen = false;
    }

    private void TypeComboBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            TryAddType(TypeComboBox.Text);
    }

    private void TryAddType(string raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // 去重
        if (_types.Any(t => string.Equals(t.Text, text, StringComparison.OrdinalIgnoreCase))) return;

        // 显式选择的颜色优先，否则回退到该类别已保存的颜色
        var tagColor = _pendingColorHex ?? Services.ProjectService.GetTypeColor(text);
        _types.Add(new TypeTag(text, tagColor));
        TypeComboBox.Text = string.Empty;

        // 存到 project.json（类别 + 对齐的颜色）
        var cfg = Services.ProjectService.CurrentProject;
        if (cfg == null) return;

        var idx = cfg.TypeOptions.FindIndex(
            t => string.Equals(t, text, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            cfg.TypeOptions.Add(text);
            Services.ProjectService.EnsureTypeColorsAligned();
            cfg.TypeColors[cfg.TypeOptions.Count - 1] = _pendingColorHex ?? string.Empty;
        }
        else if (_pendingColorHex != null)
        {
            Services.ProjectService.EnsureTypeColorsAligned();
            cfg.TypeColors[idx] = _pendingColorHex;
        }
        else
        {
            return; // 已存在且未显式选色：无需更新配置
        }

        Services.ProjectService.UpdateProjectConfig(cfg);
        PopulateTypes(); // 重新填充并自动启用下拉框
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TypeTag tag)
            _types.Remove(tag);
    }

    /// <summary>从 project.json 中删除类别选项（类别 + 对齐颜色）并刷新下拉框。</summary>
    private void DeleteTypeOption(string typeName)
    {
        var cfg = Services.ProjectService.CurrentProject;
        if (cfg == null) return;

        var idx = cfg.TypeOptions.FindIndex(
            t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            cfg.TypeOptions.RemoveAt(idx);
            if (idx < cfg.TypeColors.Count) cfg.TypeColors.RemoveAt(idx);
        }
        Services.ProjectService.UpdateProjectConfig(cfg);

        // 如果该类别已被添加到当前条目的 _types 列表，也一并移除
        var existing = _types.FirstOrDefault(
            t => string.Equals(t.Text, typeName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) _types.Remove(existing);

        // 重新填充并保持下拉框展开
        var wasOpen = TypeComboBox.IsDropDownOpen;
        PopulateTypes();
        if (wasOpen)
            TypeComboBox.IsDropDownOpen = true;
    }

    private static readonly string[] PresetTagColors =
    {
        "#E83D3D", "#E88D3D", "#E8D43D", "#4CAF50",
        "#3D9DE8", "#9C27B0", "#E91E63", "#00BCD4",
        "#795548", "#607D8B", "#FF9800", "#8D8D8D"
    };

    private void BuildColorSwatches()
    {
        ColorSwatchPanel.Children.Clear();
        ColorSwatchPanel.Children.Add(MakeSwatch(string.Empty, isDefault: true));
        foreach (var hex in PresetTagColors)
            ColorSwatchPanel.Children.Add(MakeSwatch(hex, isDefault: false));
    }

    private void ResetColorSelection()
    {
        foreach (var child in ColorSwatchPanel.Children)
        {
            if (child is RadioButton rb && string.IsNullOrEmpty(rb.Tag as string))
            {
                rb.IsChecked = true;
                break;
            }
        }
        _pendingColorHex = null;
    }

    private RadioButton MakeSwatch(string hex, bool isDefault)
    {
        var rb = new RadioButton
        {
            GroupName = "TypeColor",
            Tag = hex,
            IsChecked = isDefault,
            Style = (Style)FindResource("ColorSwatchRadio"),
            ToolTip = string.IsNullOrEmpty(hex) ? LocalizationManager.T("无颜色") : hex
        };

        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            Background = string.IsNullOrEmpty(hex) ? Brushes.Transparent : (ColorUtil.ParseBrush(hex) ?? Brushes.Transparent),
            BorderBrush = (Brush)Application.Current.FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (string.IsNullOrEmpty(hex))
        {
            swatch.Child = new TextBlock
            {
                Text = "✕",
                FontSize = 9,
                Foreground = (Brush)Application.Current.FindResource("ForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        rb.Content = swatch;
        rb.Checked += (_, _) => _pendingColorHex = hex;
        return rb;
    }

    private static UIElement BuildTypeContent(string typeName, string? colorHex)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var swatch = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(3),
            Background = ColorUtil.ParseBrush(colorHex) ?? Brushes.Transparent,
            BorderBrush = (Brush)Application.Current.FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock
        {
            Text = typeName,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    public (bool IsValid, string Message) Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryTitle))
            return (false, LocalizationManager.T("标题不能为空。"));

        if (EntryTitle.Length > 200)
            return (false, LocalizationManager.T("标题不能超过 200 个字符。"));

        if (Brief.Length > 110)
            return (false, LocalizationManager.T("简介不能超过 110 个字符。"));

        return (true, string.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
    }
}
