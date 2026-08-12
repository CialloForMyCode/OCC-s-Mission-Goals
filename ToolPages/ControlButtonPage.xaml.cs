using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;
using static OCCMissionGoals.MainWindow;

namespace OCCMissionGoals.ToolPages
{
    /// <summary>
    /// ControlButtonPage.xaml 的交互逻辑
    /// </summary>
    public partial class ControlButtonPage : Page
    {
        public ControlButtonPage()
        {
            InitializeComponent();
            SyncThemeIcon();
        }

        /// <summary>根据当前主题模式同步图标。</summary>
        private void SyncThemeIcon()
        {
            ThemeIcon.Data = Geometry.Parse(ThemeManager.IsDark
                ? ColorPalette.LightIconData
                : ColorPalette.DarkIconData);
        }

        public void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            SyncThemeIcon();
            ConfigManager.Set("General", "theme", ThemeManager.IsDark ? "dark" : "light");
        }

        private void NewEntry_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.ShowNewEntryDialog();
        }

        private void EditEntry_Click(object sender, RoutedEventArgs e)
        {
            // 切换到未完成页面，方便用户找到要编辑的条目
            if (Window.GetWindow(this) is MainWindow mw)
                mw.SwitchTab(1);
        }
    }
}
