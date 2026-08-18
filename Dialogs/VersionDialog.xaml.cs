using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Dialogs;

public partial class VersionDialog : UserControl
{
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public string VersionName => VersionNameBox.Text.Trim();
    public string? SelectedVersion => VersionListBox.SelectedItem as string;
    public string? SelectedDeleteVersion => DeleteVersionListBox.SelectedItem as string;

    /// <summary>当前是否为「删除版本」模式。</summary>
    public bool IsDeleteMode => _isDeleteMode;

    private bool _isNewMode = true;
    private bool _isDeleteMode;

    public VersionDialog()
    {
        InitializeComponent();
        NewModeBtn.IsChecked = true; // 默认新建模式
    }

    public void LoadVersions(string projectDir)
    {
        var files = ProjectService.GetVersionFiles(projectDir);
        var cleanNames = files
            .Select(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? f[..^5]
                : f)
            .ToList();
        VersionListBox.ItemsSource = cleanNames;
        DeleteVersionListBox.ItemsSource = cleanNames;
        if (cleanNames.Count > 0)
        {
            VersionListBox.SelectedIndex = 0;
            DeleteVersionListBox.SelectedIndex = 0;
            // 预填下一个版本号
            var current = ProjectService.CurrentProject?.CurrentVersion ?? "0.1.0-alpha.0";
            VersionNameBox.Text = NextVersion(current);
        }
    }

    public void Reset()
    {
        NewModeBtn.IsChecked = true;
        VersionNameBox.Text = "0.2.0-alpha.0";
        NewHint.Text = "";
        OpenHint.Text = "";
        DeleteHint.Text = "";
    }

    private void NewModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || DeletePanel == null || ConfirmBtn == null) return;
        _isNewMode = true;
        _isDeleteMode = false;
        NewPanel.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
        DeletePanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("创建");
    }

    private void OpenModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || DeletePanel == null || ConfirmBtn == null) return;
        _isNewMode = false;
        _isDeleteMode = false;
        NewPanel.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Visible;
        DeletePanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("打开");
    }

    private void DeleteModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || DeletePanel == null || ConfirmBtn == null) return;
        _isNewMode = false;
        _isDeleteMode = true;
        NewPanel.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Collapsed;
        DeletePanel.Visibility = Visibility.Visible;
        ConfirmBtn.Content = LocalizationManager.T("删除");
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isDeleteMode)
        {
            if (string.IsNullOrEmpty(SelectedDeleteVersion))
            {
                DeleteHint.Text = LocalizationManager.T("请选择一个版本。");
                return;
            }
            if (SelectedDeleteVersion == (ProjectService.CurrentProject?.CurrentVersion ?? ""))
            {
                DeleteHint.Text = LocalizationManager.T("不能删除当前版本。");
                return;
            }
        }
        else if (_isNewMode)
        {
            if (string.IsNullOrWhiteSpace(VersionName))
            {
                NewHint.Text = LocalizationManager.T("请输入版本号。");
                return;
            }
            if (!IsValidVersionName(VersionName))
            {
                NewHint.Text = LocalizationManager.T("版本号包含无效字符。");
                return;
            }
            // 检查是否已存在
            var files = ProjectService.GetVersionFiles(
                ProjectService.CurrentProjectDir ?? "");
            var targetFile = VersionName + ".json";
            if (files.Contains(targetFile))
            {
                NewHint.Text = LocalizationManager.T("版本 {0} 已存在。", VersionName);
                return;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(SelectedVersion))
            {
                OpenHint.Text = LocalizationManager.T("请选择一个版本。");
                return;
            }
        }
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsValidVersionName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return !name.Any(c => invalid.Contains(c));
    }

    private static string NextVersion(string current)
    {
        // 尝试递增版本号的最后一段数字
        var parts = current.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[^1], out int num))
        {
            parts[^1] = (num + 1).ToString();
            return string.Join(".", parts);
        }

        // fallback：简单拼接
        var last = current.Split('-').LastOrDefault() ?? "0";
        if (int.TryParse(last, out int n))
            return current[..(current.Length - last.Length)] + (n + 1);
        return current + ".1";
    }
}
