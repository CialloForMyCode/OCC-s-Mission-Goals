using System.Windows;
using System.Windows.Controls;

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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // 打开「设置」页面
            if (Window.GetWindow(this) is MainWindow mw)
                mw.SwitchTab("settings");
        }
    }
}
