using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OCCMissionGoals.ToolPages;

public partial class SwitchPage : Page
{
    private readonly List<RadioButton> _tabButtons = new();
    private RadioButton? _helpButton;
    private bool _suppressEvents;
    private int _helpColumnIndex = -1;
    private int _lastCheckedIndex = 0;

    public SwitchPage()
    {
        InitializeComponent();
    }

    // ==================== 公开 API ====================

    /// <summary>添加一个普通页签按钮（由 MainWindow.RegisterPage 调用）。</summary>
    public RadioButton AddTabButton(string label, int index)
    {
        // 插入新列
        var colDef = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        TabGrid.ColumnDefinitions.Insert(index, colDef);

        var btn = new RadioButton
        {
            Content = label,
            Height = 27,
            Margin = new Thickness(5, 5, 0, 5),
            Style = (Style)FindResource("ConsoleTabRadioButton"),
            GroupName = "TabGroup"
        };
        btn.SetValue(Grid.ColumnProperty, index);
        btn.Checked += TabButton_Checked;

        // 将新按钮插入正确位置，并调整后续元素列号
        TabGrid.Children.Insert(index, btn);
        _tabButtons.Insert(index, btn);

        // 重新编号后续普通按钮和帮助按钮的列
        for (int i = index + 1; i < _tabButtons.Count; i++)
            _tabButtons[i].SetValue(Grid.ColumnProperty, i);
        if (_helpButton != null)
            _helpButton.SetValue(Grid.ColumnProperty, _tabButtons.Count);

        // 首次添加时默认选中
        if (_tabButtons.Count == 1)
        {
            _suppressEvents = true;
            btn.IsChecked = true;
            _suppressEvents = false;
        }

        return btn;
    }

    /// <summary>添加帮助按钮（始终在最后）。</summary>
    public RadioButton AddHelpButton(string label)
    {
        _helpColumnIndex = _tabButtons.Count;

        // 帮助按钮用 Auto 宽度列
        TabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var btn = new RadioButton
        {
            Content = label,
            Height = 27,
            Margin = new Thickness(5, 5, 0, 5),
            Style = (Style)FindResource("ConsoleTabRadioButton"),
            GroupName = "TabGroup",
            Visibility = Visibility.Collapsed
        };
        btn.SetValue(Grid.ColumnProperty, _helpColumnIndex);
        btn.Checked += HelpTabBtn_Checked;
        btn.Unchecked += HelpTabBtn_Unchecked;

        TabGrid.Children.Add(btn);
        _helpButton = btn;
        return btn;
    }

    /// <summary>显示帮助按钮并选中。</summary>
    public void ShowHelpButton()
    {
        if (_helpButton == null) return;
        _suppressEvents = true;
        _helpButton.Visibility = Visibility.Visible;
        _helpButton.IsChecked = true;
        _suppressEvents = false;
    }

    /// <summary>隐藏帮助按钮。</summary>
    public void HideHelpButton()
    {
        if (_helpButton == null) return;
        _suppressEvents = true;
        _helpButton.IsChecked = false;
        _helpButton.Visibility = Visibility.Collapsed;
        _suppressEvents = false;

        // 恢复选中上一个普通页签
        if (_lastCheckedIndex >= 0 && _lastCheckedIndex < _tabButtons.Count)
        {
            _tabButtons[_lastCheckedIndex].IsChecked = true;
        }
    }

    /// <summary>程序化选中某个普通页签（不触发事件）。</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabButtons.Count) return;
        _suppressEvents = true;
        _tabButtons[index].IsChecked = true;
        _suppressEvents = false;
        _lastCheckedIndex = index;
    }

    // ==================== 事件 ====================

    public event System.Action<int>? TabSelected;

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (sender is not RadioButton btn) return;
        var idx = _tabButtons.IndexOf(btn);
        if (idx < 0) return;
        _lastCheckedIndex = idx;
        TabSelected?.Invoke(idx);
    }

    private void HelpTabBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        // 触发帮助导航由 MainWindow 通过注册表处理
        TabSelected?.Invoke(-1); // -1 = 帮助页签
    }

    private void HelpTabBtn_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (_helpButton != null)
            _helpButton.Visibility = Visibility.Collapsed;
    }
}
