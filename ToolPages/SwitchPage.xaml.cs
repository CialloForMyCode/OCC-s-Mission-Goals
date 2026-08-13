using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OCCMissionGoals.ToolPages;

public partial class SwitchPage : Page
{
    private readonly List<RadioButton> _tabButtons = new();
    private readonly Dictionary<string, RadioButton> _overlayButtons = new();
    private readonly List<string> _overlayOrder = new();
    private bool _suppressEvents;
    private int _lastCheckedIndex = 0;

    public SwitchPage()
    {
        InitializeComponent();
    }

    // ==================== 公开 API ====================

    /// <summary>添加一个普通页签按钮（由 MainWindow.RegisterPage 调用）。</summary>
    public RadioButton AddTabButton(string key, string label, int index)
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
            GroupName = "TabGroup",
            Tag = key
        };
        btn.SetValue(Grid.ColumnProperty, index);
        btn.Checked += TabButton_Checked;

        // 将新按钮插入正确位置，并调整后续元素列号
        TabGrid.Children.Insert(index, btn);
        _tabButtons.Insert(index, btn);

        // 重新编号后续普通按钮和隐藏页签按钮的列
        for (int i = index + 1; i < _tabButtons.Count; i++)
            _tabButtons[i].SetValue(Grid.ColumnProperty, i);
        int col = _tabButtons.Count;
        foreach (var overlayKey in _overlayOrder)
            _overlayButtons[overlayKey].SetValue(Grid.ColumnProperty, col++);

        // 首次添加时默认选中
        if (_tabButtons.Count == 1)
        {
            _suppressEvents = true;
            btn.IsChecked = true;
            _suppressEvents = false;
        }

        return btn;
    }

    /// <summary>添加一个隐藏页签按钮（如帮助、设置），初始折叠，点击对应入口后才出现。</summary>
    public RadioButton AddOverlayTab(string key, string label)
    {
        // 隐藏页签按钮用 Auto 宽度列，紧随普通页签之后
        int col = _tabButtons.Count + _overlayOrder.Count;
        TabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var btn = new RadioButton
        {
            Content = label,
            Height = 27,
            Margin = new Thickness(5, 5, 0, 5),
            Style = (Style)FindResource("ConsoleTabRadioButton"),
            GroupName = "TabGroup",
            Tag = key,
            Visibility = Visibility.Collapsed
        };
        btn.SetValue(Grid.ColumnProperty, col);
        btn.Checked += OverlayTab_Checked;
        btn.Unchecked += OverlayTab_Unchecked;

        TabGrid.Children.Add(btn);
        _overlayButtons[key] = btn;
        _overlayOrder.Add(key);
        return btn;
    }

    /// <summary>显示指定隐藏页签按钮并选中。</summary>
    public void ShowOverlayTab(string key)
    {
        if (!_overlayButtons.TryGetValue(key, out var btn)) return;
        _suppressEvents = true;
        btn.Visibility = Visibility.Visible;
        btn.IsChecked = true;
        _suppressEvents = false;
    }

    /// <summary>隐藏所有隐藏页签按钮。</summary>
    public void HideOverlayTabs()
    {
        _suppressEvents = true;
        foreach (var btn in _overlayButtons.Values)
        {
            btn.IsChecked = false;
            btn.Visibility = Visibility.Collapsed;
        }
        _suppressEvents = false;

        // 恢复选中上一个普通页签
        if (_lastCheckedIndex >= 0 && _lastCheckedIndex < _tabButtons.Count)
        {
            _tabButtons[_lastCheckedIndex].IsChecked = true;
        }
    }

    /// <summary>更新指定页签按钮的显示文字（语言切换时由 MainWindow 调用）。</summary>
    public void UpdateTabLabel(string key, string label)
    {
        foreach (var btn in _tabButtons)
        {
            if (btn.Tag as string == key)
            {
                btn.Content = label;
                return;
            }
        }
        if (_overlayButtons.TryGetValue(key, out var overlay))
            overlay.Content = label;
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

    public event System.Action<string>? TabSelected;

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (sender is not RadioButton btn) return;
        var idx = _tabButtons.IndexOf(btn);
        if (idx < 0) return;
        _lastCheckedIndex = idx;
        TabSelected?.Invoke((string)btn.Tag);
    }

    private void OverlayTab_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (sender is not RadioButton btn) return;
        // 由 MainWindow 通过注册表处理导航
        TabSelected?.Invoke((string)btn.Tag);
    }

    private void OverlayTab_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (sender is RadioButton btn)
            btn.Visibility = Visibility.Collapsed;
    }
}
