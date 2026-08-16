using System;
using System.Windows;
using System.Windows.Controls;

namespace OCCMissionGoals.ToolPages
{
    public partial class MenuPage : Page
    {
        public MenuPage()
        {
            InitializeComponent();
            MenuItemNew.Click += NewProject_Click;
            MenuItemOpen.Click += OpenProject_Click;
            MenuItemSetting.Click += SettingProject_Click;
            MenuItemNewEntry.Click += NewEntry_Click;
            MenuItemNewData.Click += NewVersion_Click;
            MenuItemOpenData.Click += OpenVersion_Click;
            MenuItemHelp.Click += Help_Click;
        }

        // ======================== 项目 ========================

        private void NewProject_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowNewProjectDialog();

        private void OpenProject_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowOpenProjectDialog();

        private void SettingProject_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.OpenProjectSettings();

        // ======================== 数据文件 / 版本 ========================

        private void NewVersion_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowNewVersionDialog();

        private void OpenVersion_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowOpenVersionDialog();

        // ======================== 条目视图 ========================

        private void NewEntry_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowNewEntryDialog();

        // ======================== 帮助 ========================

        private void Help_Click(object sender, RoutedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.ShowHelpPage();
    }
}
