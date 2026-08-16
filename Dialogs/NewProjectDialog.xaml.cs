using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Dialogs;

public partial class NewProjectDialog : UserControl
{
    /// <summary>点击确认时触发。</summary>
    public event EventHandler? Confirmed;

    /// <summary>点击取消或关闭时触发。</summary>
    public event EventHandler? Cancelled;

    /// <summary>项目名称。</summary>
    public string ProjectName => ProjectNameTextBox.Text.Trim();

    /// <summary>项目描述。</summary>
    public string Description => DescriptionTextBox.Text.Trim();

    /// <summary>初始版本（如 "0.1.0-alpha.0"），数据文件固定为 data.json。</summary>
    public string InitialVersion => VersionTextBox.Text.Trim();

    private bool _isOpenMode;

    /// <summary>是否处于「打开项目」模式。</summary>
    public bool IsOpenMode => _isOpenMode;

    /// <summary>当前选中的项目文件夹路径（打开模式）。</summary>
    public string? SelectedProjectDir => (ProjectComboBox.SelectedItem as ProjectChoice)?.Directory;

    /// <summary>当前选中的项目名称（打开模式）。</summary>
    public string? SelectedProjectName => (ProjectComboBox.SelectedItem as ProjectChoice)?.Name;

    public NewProjectDialog()
    {
        InitializeComponent();
    }

    /// <summary>重置为「新建项目」模式并清空字段。</summary>
    public void Reset()
    {
        ModeSwitcher.Visibility = Visibility.Visible;
        _isOpenMode = false;
        NewModeBtn.IsChecked = true;
        NewPanel.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("创建", "Create", "Создать");
        ProjectNameTextBox.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        VersionTextBox.Text = "0.1.0-alpha.0";
        OpenHint.Text = string.Empty;
        DialogTitle.Text = LocalizationManager.T("新建项目", "New Project", "Новый проект");
    }

    /// <summary>切换到「打开项目」模式，并自动读取可选择的项目。</summary>
    public void PrepareOpen()
    {
        ModeSwitcher.Visibility = Visibility.Visible;
        _isOpenMode = true;
        OpenModeBtn.IsChecked = true;
        NewPanel.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Visible;
        ConfirmBtn.Content = LocalizationManager.T("打开", "Open", "Открыть");
        OpenHint.Text = string.Empty;
        DialogTitle.Text = LocalizationManager.T("打开项目", "Open Project", "Открыть проект");
        LoadProjects();
    }

    /// <summary>加载已有项目配置用于编辑（设置项目）。</summary>
    public void LoadConfig(string name, string description, string currentVersion)
    {
        // 设置项目复用「新建/编辑」表单，隐藏模式切换
        ModeSwitcher.Visibility = Visibility.Collapsed;
        _isOpenMode = false;
        NewModeBtn.IsChecked = true;
        NewPanel.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("保存", "Save", "Сохранить");
        ProjectNameTextBox.Text = name;
        DescriptionTextBox.Text = description;
        VersionTextBox.Text = currentVersion;
        DialogTitle.Text = LocalizationManager.T("设置项目", "Project Settings", "Настройки проекта");
    }

    /// <summary>
    /// 读取 Projects/ 下的所有子文件夹，找出含 project.json 的项目，填充下拉框。
    /// </summary>
    private void LoadProjects()
    {
        var choices = new List<ProjectChoice>();
        foreach (var dir in ProjectService.GetProjectDirectories())
        {
            var folderName = Path.GetFileName(dir);
            var name = folderName;
            try
            {
                var json = File.ReadAllText(Path.Combine(dir, "project.json"));
                var cfg = JsonSerializer.Deserialize<ProjectConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.Name))
                    name = cfg.Name;
            }
            catch
            {
                // project.json 损坏时回退到文件夹名
            }
            choices.Add(new ProjectChoice { Directory = dir, Folder = folderName, Name = name });
        }

        ProjectComboBox.ItemsSource = choices;
        if (choices.Count > 0)
            ProjectComboBox.SelectedIndex = 0;
    }

    /// <summary>
    /// 验证输入。返回 (是否有效, 错误消息)。
    /// </summary>
    public (bool IsValid, string Message) Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
            return (false, LocalizationManager.T("项目名称不能为空。", "Project name cannot be empty.", "Название проекта не может быть пустым."));

        if (ProjectName.Length > 100)
            return (false, LocalizationManager.T("项目名称不能超过 100 个字符。", "Project name cannot exceed 100 characters.", "Название проекта не может превышать 100 символов."));

        if (Description.Length > 500)
            return (false, LocalizationManager.T("项目描述不能超过 500 个字符。", "Project description cannot exceed 500 characters.", "Описание проекта не может превышать 500 символов."));

        return (true, string.Empty);
    }

    private void NewModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || ConfirmBtn == null) return;
        _isOpenMode = false;
        NewPanel.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("创建", "Create", "Создать");
    }

    private void OpenModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || ConfirmBtn == null) return;
        _isOpenMode = true;
        NewPanel.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Visible;
        ConfirmBtn.Content = LocalizationManager.T("打开", "Open", "Открыть");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (Confirmed == null)
        {
            MessageBox.Show(LocalizationManager.T("Confirmed 事件未绑定，请通过菜单「项目」打开此弹窗。", "Confirmed event is not bound. Open this dialog via the Project menu.", "Событие Confirmed не привязано. Откройте это окно через меню «Проект»."),
                LocalizationManager.T("提示", "Notice", "Уведомление"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 打开模式：需先选择项目
        if (IsOpenMode && string.IsNullOrEmpty(SelectedProjectDir))
        {
            OpenHint.Text = LocalizationManager.T("请选择一个项目。", "Please select a project.", "Выберите проект.");
            return;
        }

        Confirmed.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>下拉框中可选择的项目项。</summary>
public class ProjectChoice
{
    public string Directory { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>下拉框显示文本（名称与文件夹不同时附带文件夹名）。</summary>
    public string Display => string.Equals(Name, Folder, StringComparison.OrdinalIgnoreCase)
        ? Name
        : $"{Name} ({Folder})";

    /// <summary>ComboBox 未应用模板时的回退显示（避免出现类型名）。</summary>
    public override string ToString() => Display;
}
