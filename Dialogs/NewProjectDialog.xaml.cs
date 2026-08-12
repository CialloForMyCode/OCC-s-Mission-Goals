using System;
using System.Windows;
using System.Windows.Controls;

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

    public NewProjectDialog()
    {
        InitializeComponent();
    }

    /// <summary>重置所有输入字段。</summary>
    public void Reset()
    {
        ProjectNameTextBox.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        VersionTextBox.Text = "0.1.0-alpha.0";
        DialogTitle.Text = "新建项目";
    }

    /// <summary>加载已有项目配置用于编辑。</summary>
    public void LoadConfig(string name, string description, string currentVersion)
    {
        ProjectNameTextBox.Text = name;
        DescriptionTextBox.Text = description;
        VersionTextBox.Text = currentVersion;
        DialogTitle.Text = "设置项目";
    }

    /// <summary>
    /// 验证输入。返回 (是否有效, 错误消息)。
    /// </summary>
    public (bool IsValid, string Message) Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
            return (false, "项目名称不能为空。");

        if (ProjectName.Length > 100)
            return (false, "项目名称不能超过 100 个字符。");

        if (Description.Length > 500)
            return (false, "项目描述不能超过 500 个字符。");

        return (true, string.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (Confirmed == null)
        {
            MessageBox.Show("Confirmed 事件未绑定，请通过菜单「项目 → 新建项目」打开此弹窗。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Confirmed.Invoke(this, EventArgs.Empty);
    }
}
