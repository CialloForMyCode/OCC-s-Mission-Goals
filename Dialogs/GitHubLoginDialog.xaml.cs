using System;
using System.Windows;
using System.Windows.Controls;
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Dialogs;

public partial class GitHubLoginDialog : UserControl
{
    /// <summary>点击登录时触发。</summary>
    public event EventHandler? Confirmed;

    /// <summary>点击取消时触发。</summary>
    public event EventHandler? Cancelled;

    /// <summary>输入的 Personal Access Token。</summary>
    public string Token => TokenBox.Password.Trim();

    public GitHubLoginDialog()
    {
        InitializeComponent();
    }

    /// <summary>重置对话框状态。</summary>
    public void Reset()
    {
        TokenBox.Password = string.Empty;
        HideError();
    }

    /// <summary>显示错误信息。</summary>
    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>隐藏错误信息。</summary>
    public void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    /// <summary>登录过程中锁定按钮，防止重复提交。</summary>
    public void SetBusy(bool busy)
    {
        ConfirmBtn.IsEnabled = !busy;
        CancelBtn.IsEnabled = !busy;
        TokenBox.IsEnabled = !busy;
    }

    private void OpenTokenPage_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenUrl("https://github.com/settings/tokens");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ShowError(LocalizationManager.T("请输入 Personal Access Token。", "Please enter a Personal Access Token.", "Введите Personal Access Token."));
            return;
        }

        HideError();
        Confirmed?.Invoke(this, EventArgs.Empty);
    }
}
