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

    private bool _isNewMode = true;

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
        if (cleanNames.Count > 0)
        {
            VersionListBox.SelectedIndex = 0;
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
    }

    private void NewModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || ConfirmBtn == null) return;
        _isNewMode = true;
        NewPanel.Visibility = Visibility.Visible;
        OpenPanel.Visibility = Visibility.Collapsed;
        ConfirmBtn.Content = LocalizationManager.T("创建", "Create");
    }

    private void OpenModeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (NewPanel == null || OpenPanel == null || ConfirmBtn == null) return;
        _isNewMode = false;
        NewPanel.Visibility = Visibility.Collapsed;
        OpenPanel.Visibility = Visibility.Visible;
        ConfirmBtn.Content = LocalizationManager.T("打开", "Open");
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isNewMode)
        {
            if (string.IsNullOrWhiteSpace(VersionName))
            {
                NewHint.Text = LocalizationManager.T("请输入版本号。", "Please enter a version number.");
                return;
            }
            if (!IsValidVersionName(VersionName))
            {
                NewHint.Text = LocalizationManager.T("版本号包含无效字符。", "Version number contains invalid characters.");
                return;
            }
            // 检查是否已存在
            var files = ProjectService.GetVersionFiles(
                ProjectService.CurrentProjectDir ?? "");
            var targetFile = VersionName + ".json";
            if (files.Contains(targetFile))
            {
                NewHint.Text = LocalizationManager.T($"版本 {VersionName} 已存在。", $"Version {VersionName} already exists.");
                return;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(SelectedVersion))
            {
                OpenHint.Text = LocalizationManager.T("请选择一个版本。", "Please select a version.");
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
