using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Dialogs;

public partial class NewEntryDialog : UserControl
{
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    private readonly ObservableCollection<FileRef> _files = new();
    private readonly ObservableCollection<string> _types = new();
    private GoalEntry? _editingEntry;

    public bool IsEditing => _editingEntry != null;
    public GoalEntry? EditingEntry => _editingEntry;

    public int Severity =>
        SeverityComboBox.SelectedItem is ComboBoxItem item && item.Tag != null
            ? Convert.ToInt32(item.Tag)
            : 0;

    public string EntryTitle => TitleTextBox.Text.Trim();
    public string Brief => BriefTextBox.Text.Trim();
    public string Detail => DetailTextBox.Text.Trim();
    public DateTime Deadline =>
        DeadlinePicker.SelectedDate ?? DateTime.Today.AddDays(7);

    public string Version =>
        VersionComboBox.SelectedItem is ComboBoxItem item
            ? (item.Tag?.ToString() ?? string.Empty)
            : string.Empty;

    public ObservableCollection<FileRef> Files => _files;
    public List<string> Type => _types.ToList();

    public NewEntryDialog()
    {
        InitializeComponent();
        FileListControl.ItemsSource = _files;
        TypeListControl.ItemsSource = _types;
    }

    public void Reset()
    {
        _editingEntry = null;
        DialogTitle.Text = "新建条目";
        SeverityComboBox.SelectedIndex = 2;
        TitleTextBox.Text = string.Empty;
        BriefTextBox.Text = string.Empty;
        DetailTextBox.Text = string.Empty;
        DeadlinePicker.SelectedDate = DateTime.Today.AddDays(7);
        PopulateVersions();
        SelectVersion(Services.ProjectService.CurrentProject?.CurrentVersion);
        PopulateTypes();
        TypeComboBox.Text = string.Empty;
        _types.Clear();
        _files.Clear();
    }

    public void LoadEntry(GoalEntry entry)
    {
        _editingEntry = entry;
        DialogTitle.Text = "编辑条目";

        SeverityComboBox.SelectedIndex = (int)entry.Severity;
        TitleTextBox.Text = entry.Title;
        BriefTextBox.Text = entry.Brief;
        DetailTextBox.Text = entry.Detail;
        DeadlinePicker.SelectedDate = entry.Deadline == default
            ? DateTime.Today.AddDays(7)
            : entry.Deadline;
        PopulateVersions();
        SelectVersion(entry.Version);
        PopulateTypes();
        TypeComboBox.Text = string.Empty;

        _types.Clear();
        foreach (var t in entry.Type)
            _types.Add(t);

        _files.Clear();
        foreach (var f in entry.RelatedFiles)
            _files.Add(new FileRef
            {
                Path = f.Path,
                Line = f.Line,
                Column = f.Column,
                Function = f.Function
            });
    }

    private void PopulateVersions()
    {
        VersionComboBox.Items.Clear();
        var dir = Services.ProjectService.CurrentProjectDir;
        if (dir == null) return;

        var files = Services.ProjectService.GetVersionFiles(dir);
        foreach (var file in files)
        {
            var version = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? file[..^5]
                : file;
            VersionComboBox.Items.Add(new ComboBoxItem
            {
                Content = version,
                Tag = version,
                Style = (Style)FindResource("DialogComboBoxItem")
            });
        }
    }

    private void SelectVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            if (VersionComboBox.Items.Count > 0)
                VersionComboBox.SelectedIndex = 0;
            return;
        }

        foreach (ComboBoxItem item in VersionComboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), version, StringComparison.OrdinalIgnoreCase))
            {
                VersionComboBox.SelectedItem = item;
                return;
            }
        }

        if (VersionComboBox.Items.Count > 0)
            VersionComboBox.SelectedIndex = 0;
    }

    private void PopulateTypes()
    {
        TypeComboBox.Items.Clear();
        var cfg = Services.ProjectService.CurrentProject;

        if (cfg?.TypeOptions != null)
        {
            foreach (var t in cfg.TypeOptions)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var typeName = t;
                var item = new ComboBoxItem
                {
                    Content = typeName,
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
        if (_types.Contains(text, StringComparer.OrdinalIgnoreCase)) return;

        _types.Add(text);
        TypeComboBox.Text = string.Empty;

        // 存到 project.json
        var cfg = Services.ProjectService.CurrentProject;
        if (cfg != null && !cfg.TypeOptions.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            cfg.TypeOptions.Add(text);
            Services.ProjectService.UpdateProjectConfig(cfg);
            PopulateTypes(); // 重新填充并自动启用下拉框
        }
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string type)
            _types.Remove(type);
    }

    /// <summary>从 project.json 中删除类别选项并刷新下拉框。</summary>
    private void DeleteTypeOption(string typeName)
    {
        var cfg = Services.ProjectService.CurrentProject;
        if (cfg == null) return;

        cfg.TypeOptions.Remove(typeName);
        Services.ProjectService.UpdateProjectConfig(cfg);

        // 如果该类别已被添加到当前条目的 _types 列表，也一并移除
        _types.Remove(typeName);

        // 重新填充并保持下拉框展开
        var wasOpen = TypeComboBox.IsDropDownOpen;
        PopulateTypes();
        if (wasOpen)
            TypeComboBox.IsDropDownOpen = true;
    }

    public (bool IsValid, string Message) Validate()
    {
        if (string.IsNullOrWhiteSpace(EntryTitle))
            return (false, "标题不能为空。");

        if (EntryTitle.Length > 200)
            return (false, "标题不能超过 200 个字符。");

        if (Brief.Length > 500)
            return (false, "简介不能超过 500 个字符。");

        return (true, string.Empty);
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择要关联的文件",
            Multiselect = true,
            Filter = "所有文件 (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var path in dlg.FileNames)
            {
                _files.Add(new FileRef
                {
                    Path = path,
                    Line = 0,
                    Column = 0,
                    Function = string.Empty
                });
            }
        }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FileRef file)
            _files.Remove(file);
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
