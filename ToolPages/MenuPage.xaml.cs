using System;
using System.Windows;
using System.Windows.Controls;
using OCCMissionGoals.Pages;
using OCCMissionGoals.Services;

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
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            mw.NewProjectDialog.Reset();
            mw.NewProjectDialog.Confirmed += OnNewProjectConfirmed;
            mw.NewProjectDialog.Cancelled += OnProjectDialogDismissed;

            mw.MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            mw.DialogOverlay.Visibility = Visibility.Visible;
            mw.NewProjectDialog.Visibility = Visibility.Visible;
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var dir = FolderPicker.Show(ProjectService.ProjectsDir);
            if (dir == null) return;

            try
            {
                var config = ProjectService.OpenProject(dir);
                if (config == null)
                {
                    mw.SetTipText("所选文件夹不是有效的项目。");
                    return;
                }
                mw.RefreshAllViews();
                mw.SetTipText($"已打开项目「{config.Name}」。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开项目失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingProject_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (ProjectService.CurrentProject == null)
            {
                mw.SetTipText("没有打开的项目。");
                return;
            }

            var p = ProjectService.CurrentProject;
            mw.NewProjectDialog.LoadConfig(p.Name, p.Description, p.CurrentVersion);
            mw.NewProjectDialog.Confirmed += OnSettingProjectConfirmed;
            mw.NewProjectDialog.Cancelled += OnProjectDialogDismissed;

            mw.MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            mw.DialogOverlay.Visibility = Visibility.Visible;
            mw.NewProjectDialog.Visibility = Visibility.Visible;
        }

        private void OnNewProjectConfirmed(object? sender, EventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            try
            {
                var (isValid, message) = mw.NewProjectDialog.Validate();
                if (!isValid)
                {
                    mw.SetTipText(message);
                    return;
                }

                var config = ProjectService.CreateProject(
                    mw.NewProjectDialog.ProjectName,
                    mw.NewProjectDialog.Description,
                    mw.NewProjectDialog.InitialVersion);

                mw.RefreshAllViews();
                DismissProjectDialog();
                mw.SetTipText($"已创建项目「{config.Name}」。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建项目失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSettingProjectConfirmed(object? sender, EventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            try
            {
                var (isValid, message) = mw.NewProjectDialog.Validate();
                if (!isValid)
                {
                    mw.SetTipText(message);
                    return;
                }

                if (ProjectService.CurrentProject == null) return;

                ProjectService.CurrentProject.Name = mw.NewProjectDialog.ProjectName;
                ProjectService.CurrentProject.Description = mw.NewProjectDialog.Description;
                ProjectService.UpdateProjectConfig(ProjectService.CurrentProject);
                mw.RefreshAllViews();
                DismissProjectDialog();
                mw.SetTipText("项目设置已保存。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存项目设置失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnProjectDialogDismissed(object? sender, EventArgs e)
        {
            DismissProjectDialog();
        }

        private void DismissProjectDialog()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.NewProjectDialog.Confirmed -= OnNewProjectConfirmed;
                mw.NewProjectDialog.Confirmed -= OnSettingProjectConfirmed;
                mw.NewProjectDialog.Cancelled -= OnProjectDialogDismissed;
                mw.NewProjectDialog.Visibility = Visibility.Collapsed;
                mw.DialogOverlay.Visibility = Visibility.Collapsed;
                mw.MainContentGrid.Effect = null;
            }
        }

        // ======================== 数据文件 / 版本 ========================

        private void NewVersion_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (ProjectService.CurrentProjectDir == null)
            {
                mw.SetTipText("请先打开一个项目。");
                return;
            }

            mw.VersionDialog.Reset();
            mw.VersionDialog.LoadVersions(ProjectService.CurrentProjectDir);
            mw.VersionDialog.Confirmed += OnVersionConfirmed;
            mw.VersionDialog.Cancelled += OnVersionDismissed;

            mw.MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            mw.DialogOverlay.Visibility = Visibility.Visible;
            mw.VersionDialog.Visibility = Visibility.Visible;
        }

        private void OpenVersion_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (ProjectService.CurrentProjectDir == null)
            {
                mw.SetTipText("请先打开一个项目。");
                return;
            }

            mw.VersionDialog.Reset();
            mw.VersionDialog.LoadVersions(ProjectService.CurrentProjectDir);
            // 切换到打开模式
            mw.VersionDialog.OpenModeBtn.IsChecked = true;
            mw.VersionDialog.Confirmed += OnVersionConfirmed;
            mw.VersionDialog.Cancelled += OnVersionDismissed;

            mw.MainContentGrid.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 8 };
            mw.DialogOverlay.Visibility = Visibility.Visible;
            mw.VersionDialog.Visibility = Visibility.Visible;
        }

        private void OnVersionConfirmed(object? sender, EventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            try
            {
                var versionName = mw.VersionDialog.VersionName;
                var selectedVersion = mw.VersionDialog.SelectedVersion;

                if (!string.IsNullOrEmpty(selectedVersion))
                {
                    // 打开已有版本
                    ProjectService.SwitchVersion(selectedVersion + ".json");
                    mw.RefreshAllViews();
                    mw.SetTipText($"已切换到版本 {selectedVersion}。");
                }
                else if (!string.IsNullOrEmpty(versionName))
                {
                    // 新建版本
                    ProjectService.CreateVersion(versionName + ".json");
                    ProjectService.SwitchVersion(versionName + ".json");
                    mw.RefreshAllViews();
                    mw.SetTipText($"已创建并切换到版本 {versionName}。");
                }

                DismissVersionDialog();
            }
            catch (Exception ex)
            {
                mw.SetTipText($"操作失败：{ex.Message}");
            }
        }

        private void OnVersionDismissed(object? sender, EventArgs e)
        {
            DismissVersionDialog();
        }

        private void DismissVersionDialog()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.VersionDialog.Confirmed -= OnVersionConfirmed;
                mw.VersionDialog.Cancelled -= OnVersionDismissed;
                mw.VersionDialog.Visibility = Visibility.Collapsed;
                mw.DialogOverlay.Visibility = Visibility.Collapsed;
                mw.MainContentGrid.Effect = null;
            }
        }

        // ======================== 条目视图 ========================

        private void NewEntry_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            mw.ShowNewEntryDialog();
        }

        // ======================== 帮助 ========================

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            mw.ShowHelpPage();
        }
    }
}
