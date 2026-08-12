using System.Windows;
using System.Windows.Controls;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.ToolPages
{
    public partial class SortPage : Page
    {
        public SortPage()
        {
            InitializeComponent();

            // 从 config.ini 恢复上次的排序方式
            var savedTag = ConfigManager.Get("Sort", "SortMode", "0");
            if (int.TryParse(savedTag, out int index) && index >= 0 && index < SortComboBox.Items.Count)
            {
                SortComboBox.SelectedIndex = index;
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var tagString = item.Tag.ToString()!;
                var mode = (SortMode)int.Parse(tagString);

                // 保存到 config.ini
                ConfigManager.Set("Sort", "SortMode", tagString);

                var window = Window.GetWindow(this) as MainWindow;
                window?.OnSortModeChanged(mode);
            }
        }
    }
}
